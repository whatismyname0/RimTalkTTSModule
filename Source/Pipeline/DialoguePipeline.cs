using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.TTS.Audio;
using RimTalk.TTS.Bridge;
using RimTalk.TTS.Config;
using RimTalk.TTS.Data;
using RimTalk.TTS.Llm;
using RimTalk.TTS.Providers;
using Verse;

namespace RimTalk.TTS.Pipeline
{
    /// <summary>
    /// The TTS orchestration: turns a dialogue line into audio and drives the display decision
    /// through <see cref="DialogueSpeechRegistry"/>.
    ///
    /// Provider ownership and serialized swapping live in <see cref="ProviderHost"/>; every request
    /// carries a cancellation token that is cancelled when the line is ignored, times out, or the
    /// provider is replaced.
    /// </summary>
    public sealed class DialoguePipeline
    {
        private readonly DialogueSpeechRegistry _registry = new DialogueSpeechRegistry();
        private readonly GenerationCooldown _cooldown = new GenerationCooldown();
        private readonly ProviderHost _providers = new ProviderHost();
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _requestTokens =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();

        /// <summary>Active provider + capabilities.</summary>
        public ProviderHost Providers => _providers;

        public void SetProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings = null)
        {
            // Swap first (in-flight requests are cancelled between the swap and the old shutdown),
            // then reset playback/queue state.
            _providers.Apply(supplier, settings, cancelInFlight: CancelAllRequests);
            ResetAll(permanentShutdown: false);
        }

        // =================================================================================
        // request entry points (main thread)
        // =================================================================================

        /// <summary>
        /// Start speech generation for a line exactly once. Returns true when a request was started,
        /// in which case the line must be held (see <see cref="ShouldDisplay"/>).
        /// </summary>
        /// <param name="priority">
        /// Head-of-batch lines skip the supplier cooldown queue so the first line of a conversation
        /// can start speaking without waiting behind the rest of the batch.
        /// </param>
        public bool Request(string text, Guid dialogueId, Pawn pawn, bool priority = false)
        {
            var runtime = TtsRuntime.Current;
            if (runtime == null || !runtime.IsPlaybackOn) return false;
            if (string.IsNullOrEmpty(text) || pawn == null) return false;

            // Pawns without a voice never produce audio and must not be delayed.
            if (PawnVoiceManager.GetVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID) return false;

            if (!_registry.TryRequest(dialogueId, "enqueue")) return false;

            // Priority belongs to the first line of the whole conversation only: grant it when this
            // request is the only entry (ActiveCount == 1 means we just added it) and nothing is
            // playing. In a two-pawn exchange both heads arrive in the same drain sweep, so only the
            // very first of them skips the cooldown queue.
            bool grantPriority = priority
                                 && _registry.ActiveCount <= 1
                                 && !AudioPlaybackService.IsCurrentlyPlaying();

            var cts = new CancellationTokenSource();
            _requestTokens[dialogueId] = cts;

            try
            {
                var snapshot = SnapshotFactory.Build(runtime.Settings, pawn);
                if (!ValidateDialogueRequest(text, dialogueId, snapshot, out string reason))
                {
                    TTSLog.Warning($"[RimTalk.TTS] Rejected [{ShortId(dialogueId)}] - {reason}");
                    _registry.MarkFailed(dialogueId, reason);
                    RemoveToken(dialogueId);
                    return false;
                }

                TTSLog.Message($"[RimTalk.TTS] Request{(grantPriority ? " (priority)" : "")}: pawn={snapshot.PawnLabel}, " +
                               $"supplier={snapshot.Supplier?.Kind}, provider={_providers.Current?.GetType().Name}, " +
                               $"id={ShortId(dialogueId)}");
                _ = Task.Run(() => RunAsync(text, dialogueId, snapshot, grantPriority, cts));
                return true;
            }
            catch (Exception ex)
            {
                // Never leave an entry stuck in Requested: it would hold the line forever.
                TTSLog.Error($"[RimTalk.TTS] Request failed [{ShortId(dialogueId)}]: {ex}");
                _registry.MarkFailed(dialogueId, ex.GetType().Name);
                RemoveToken(dialogueId);
                return false;
            }
        }

        /// <summary>
        /// Display decision for one line: true = display now, false = hold and retry in 0.5s.
        /// Also starts playback when the audio is ready, and requests generation at display time when
        /// the enqueue hook was missed.
        /// </summary>
        public bool ShouldDisplay(Pawn pawn, TalkResponse talk, string hook)
        {
            try
            {
                var runtime = TtsRuntime.Current;
                if (runtime == null || !runtime.IsPlaybackOn) return true;
                if (pawn == null || talk == null) return true;

                // Pawns without a voice never produce audio and must not be delayed.
                if (PawnVoiceManager.GetVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID) return true;

                switch (_registry.GetState(talk.Id))
                {
                    case SpeechState.Requested:
                    case SpeechState.Generating:
                        // Still generating: hold the line at the head of the queue.
                        return false;

                    case SpeechState.Ready:
                        // Keep playback serialized: do not show a new line over audio still playing.
                        if (AudioPlaybackService.IsCurrentlyPlaying()) return false;
                        if (!_registry.TryTakeForPlayback(talk.Id, out byte[] audio)) return true;

                        PlayAudio(audio, talk.Id, runtime.Settings);
                        _registry.MarkDisplayed(talk.Id);
                        return true;

                    case SpeechState.Playing:
                        // Already handed to the player; never hold a line that is being displayed.
                        return true;

                    case SpeechState.Failed:
                    case SpeechState.Cancelled:
                        // Generation failed or was cancelled: display without audio.
                        _registry.MarkDisplayed(talk.Id);
                        return true;

                    case null:
                        if (_registry.IsKnown(talk.Id))
                        {
                            // Finished earlier in this session (failure/cancel after display): no audio.
                            return true;
                        }

                        // Enqueue hook was missed (or TTS was enabled mid-queue): request now and hold.
                        if (Request(talk.Text, talk.Id, pawn)) return false;

                        // Request was rejected (no supplier/key/ignored): display without audio and
                        // drop the failure entry so rejected lines do not accumulate.
                        _registry.MarkDisplayed(talk.Id);
                        return true;

                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] ShouldDisplay ({hook}) exception: {ex}");
                return true; // on error never block the game's dialogue
            }
        }

        private static void PlayAudio(byte[] audio, Guid dialogueId, TTSSettings settings)
        {
            float volume = settings != null
                ? settings.GetSupplierVolume(settings.GetCurrentSupplierKey())
                : 1.0f;
            _ = AudioPlaybackService.PlayAsync(audio, dialogueId, volume);
        }

        /// <summary>RimTalk ignored this line: abandon pending speech (a started clip keeps playing).</summary>
        public void Cancel(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return;

            CancellationTokenSource cts;
            if (_requestTokens.TryRemove(dialogueId, out cts))
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }

            _registry.Cancel(dialogueId, "cancelled");
        }

        /// <summary>Main-thread tick work: abandon requests whose deadline passed.</summary>
        public void SweepTimeouts()
        {
            foreach (var (id, timeoutSeconds) in _registry.TakeExpired(TTSMod.AppStopwatch.Elapsed.TotalSeconds))
            {
                CancellationTokenSource cts;
                if (_requestTokens.TryRemove(id, out cts))
                {
                    try { cts.Cancel(); } catch { }
                    cts.Dispose();
                }

                TTSLog.Warning($"[RimTalk.TTS] TTS request [{ShortId(id)}] timed out after {timeoutSeconds}s, " +
                               $"discarding audio (line will be displayed without speech)");
            }
        }

        /// <summary>Single cleanup entry: game load, provider switch, toggle off, shutdown.</summary>
        public void ResetAll(bool permanentShutdown)
        {
            if (permanentShutdown)
            {
                _providers.ShutdownCurrent();
            }

            CancelAllRequests();

            int active = _registry.SnapshotActive().Count;
            if (active > 0)
            {
                TTSLog.Message($"[RimTalk.TTS] Resetting TTS state ({active} pending line(s))");
            }

            _registry.ResetAll(permanentShutdown ? "shutdown" : "reset");
            _cooldown.Reset();
            AudioPlaybackService.StopAndClear();
        }

        private void CancelAllRequests()
        {
            foreach (var key in new System.Collections.Generic.List<Guid>(_requestTokens.Keys))
            {
                CancellationTokenSource cts;
                if (!_requestTokens.TryRemove(key, out cts)) continue;
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }
        }

        private void RemoveToken(Guid dialogueId)
        {
            CancellationTokenSource cts;
            if (_requestTokens.TryRemove(dialogueId, out cts))
            {
                cts.Dispose();
            }
        }

        // =================================================================================
        // background pipeline
        // =================================================================================

        private async Task RunAsync(string text, Guid dialogueId, SettingsSnapshot snapshot, bool priority, CancellationTokenSource cts)
        {
            var token = cts.Token;
            try
            {
                TTSLog.Message($"[RimTalk.TTS] Pipeline start [{ShortId(dialogueId)}]: pawn={snapshot.PawnLabel}, " +
                               $"voice={snapshot.VoiceModelId}, textLen={text?.Length}");

                string finalInputText = await ProcessTextAsync(text, dialogueId, snapshot, token);
                if (finalInputText == null)
                {
                    TTSLog.Warning($"[RimTalk.TTS] Preprocessing failed [{ShortId(dialogueId)}], aborting");
                    _registry.MarkFailed(dialogueId, "preprocessing failed");
                    return;
                }
                TTSLog.Message($"[RimTalk.TTS] Preprocessing done [{ShortId(dialogueId)}]: outputLen={finalInputText.Length}");

                if (!ShouldContinueProcessing(dialogueId, out string reason))
                {
                    TTSLog.Message($"[RimTalk.TTS] {reason} (discarding audio)");
                    _registry.MarkFailed(dialogueId, reason);
                    return;
                }

                await _cooldown.WaitAsync(snapshot.Supplier.CooldownMs, token, bypassQueue: priority);

                _registry.MarkGenerating(dialogueId, snapshot.RequestTimeoutSeconds);
                byte[] audioData;
                try
                {
                    audioData = await GenerateSpeechAsync(dialogueId, finalInputText, snapshot, token);
                }
                finally
                {
                    // The provider returned: stop the countdown even when the request timed out.
                    _registry.EndGenerating(dialogueId);
                }

                if (audioData == null || audioData.Length == 0)
                {
                    TTSLog.Warning($"[RimTalk.TTS] API returned no audio data [{ShortId(dialogueId)}]");
                    _registry.MarkFailed(dialogueId, "no audio");
                    return;
                }

                if (!ShouldContinueProcessing(dialogueId, out reason))
                {
                    TTSLog.Message($"[RimTalk.TTS] {reason} (discarding audio)");
                    _registry.MarkFailed(dialogueId, reason);
                    return;
                }

                if (_registry.MarkReady(dialogueId, audioData))
                {
                    TTSLog.Message($"[RimTalk.TTS] Audio ready [{ShortId(dialogueId)}]: {audioData.Length} bytes, queued for playback");
                }
                else
                {
                    TTSLog.Message($"[RimTalk.TTS] Audio for [{ShortId(dialogueId)}] arrived after timeout/cancel - " +
                                   $"discarded ({audioData.Length} bytes)");
                }
            }
            catch (OperationCanceledException)
            {
                TTSLog.Message($"[RimTalk.TTS] Dialogue {dialogueId} generation cancelled");
                _registry.Cancel(dialogueId, "cancelled");
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _registry.MarkFailed(dialogueId, ex.GetType().Name);
            }
            finally
            {
                RemoveToken(dialogueId);
            }
        }

        private async Task<string> ProcessTextAsync(string text, Guid dialogueId, SettingsSnapshot snapshot, CancellationToken token)
        {
            if (snapshot.Llm.Skip)
            {
                TTSLog.Message($"[RimTalk.TTS] Preprocess skipped [{ShortId(dialogueId)}]: passing raw text to TTS");
                return text;
            }

            string language = snapshot.VoiceLanguage;
            TTSLog.Message($"[RimTalk.TTS] Preprocess [{ShortId(dialogueId)}]: language={language ?? "(null)"}, inputLen={text?.Length}");

            if (string.IsNullOrWhiteSpace(language))
            {
                TTSLog.Warning("[RimTalk.TTS] Translation language not configured");
                return null;
            }

            var preProcessResult = await InputPreProcessService.PreProcessAsync(text, language, snapshot.Llm);
            token.ThrowIfCancellationRequested();

            if (preProcessResult == null || string.IsNullOrEmpty(preProcessResult.Text))
            {
                TTSLog.Warning($"[RimTalk.TTS] Translation/PreProcess returned empty result " +
                               $"(preProcessResult={(preProcessResult == null ? "null" : "empty text")})");
                return null;
            }

            return preProcessResult.Text;
        }

        private async Task<byte[]> GenerateSpeechAsync(Guid dialogueId, string inputText, SettingsSnapshot snapshot,
            CancellationToken token)
        {
            var supplier = snapshot.Supplier;

            string apiKeyDisplay = string.IsNullOrWhiteSpace(supplier.ApiKey)
                ? "(empty)"
                : supplier.ApiKey.Substring(0, Math.Min(6, supplier.ApiKey.Length)) + "***";
            TTSLog.Message($"[RimTalk.TTS] Generating speech: supplierKey={supplier.SupplierKey}, " +
                           $"voice={snapshot.VoiceModelId}, model={supplier.Model}, apiKey={apiKeyDisplay}, " +
                           $"inputLen={inputText?.Length}, provider={_providers.Current?.GetType().Name}");

            var ttsRequest = new TTSRequest
            {
                ApiKey = supplier.ApiKey,
                Model = supplier.Model,
                Input = inputText,
                InstructText = null,
                Voice = snapshot.VoiceModelId,
                Speed = supplier.Speed,
                Volume = supplier.Volume,
                Temperature = supplier.Temperature,
                TopP = supplier.TopP,
            };

            var result = await _providers.GenerateAsync(ttsRequest, token);
            TTSLog.Message($"[RimTalk.TTS] Speech result: {(result == null ? "null (failed)" : result.Length + " bytes")}");
            return result;
        }

        private bool ValidateDialogueRequest(string text, Guid dialogueId, SettingsSnapshot snapshot, out string reason)
        {
            if (snapshot?.Supplier == null)
            {
                reason = "Missing settings snapshot";
                return false;
            }

            // A disabled supplier would otherwise run text preprocessing (a paid LLM call) and only
            // fail later at speech generation.
            if (snapshot.Supplier.Kind == TTSSettings.TTSSupplier.None)
            {
                reason = "No TTS supplier selected";
                return false;
            }

            var capabilities = _providers.Capabilities;
            if (capabilities.RequiresApiKey && !capabilities.IsApiKeyAcceptable(snapshot.Supplier.ApiKey))
            {
                reason = $"API key not configured or invalid for supplier {snapshot.Supplier.Kind}";
                return false;
            }

            if (string.IsNullOrEmpty(text))
            {
                reason = "Empty text";
                return false;
            }

            if (TalkHistoryBridge.IsIgnored(dialogueId))
            {
                reason = $"Dialogue {dialogueId} was ignored";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool ShouldContinueProcessing(Guid dialogueId, out string reason)
        {
            if (TalkHistoryBridge.IsIgnored(dialogueId))
            {
                reason = "Dialogue was ignored during generation";
                return false;
            }

            var runtime = TtsRuntime.Current;
            if (runtime == null || !runtime.IsPlaybackOn)
            {
                reason = "TTS module turned off during generation";
                return false;
            }

            reason = null;
            return true;
        }

        private static string ShortId(Guid dialogueId)
        {
            return dialogueId.ToString().Substring(0, 8);
        }
    }
}
