using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.TTS.Data;
using RimTalkPatches = RimTalk.TTS.Patch.RimTalkPatches;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Coordinates Text-to-Speech generation for dialogue.
    /// Each request has its own CancellationTokenSource for independent cancellation.
    /// </summary>
    public static class TTSService
    {
        private static int _lastGenerateTimeStampMilisecond = 0;
        private static int _waitingRequestCount = 0;
        private static readonly object _waitingRequestLock = new object();
        private static volatile bool _isShuttingDown = false;
        private static Provider.ITTSProvider _provider = new Provider.NoneProvider();

        private static readonly object _providerLock = new object();

        public static void SetProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings = null)
        {
            lock (_providerLock)
            {
                // Shutdown current provider
                ShutdownCurrentProvider();

                // Reset module runtime state when switching providers
                ResetRuntimeState();

                // Create new provider
                _provider = CreateProvider(supplier, settings);
                
                TTSLog.Message($"[RimTalk.TTS] TTS provider set to {supplier}");
            }
        }

        private static void ShutdownCurrentProvider()
        {
            try
            {
                _provider?.Shutdown();
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Error shutting down provider: {ex.Message}");
            }
        }

        private static void ResetRuntimeState()
        {
            try
            {
                StopAll(false);
                _lastGenerateTimeStampMilisecond = 0;
                lock (_waitingRequestLock)
                {
                    _waitingRequestCount = 0;
                }
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Error resetting runtime state: {ex.Message}");
            }
        }

        private static Provider.ITTSProvider CreateProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            switch (supplier)
            {
                case TTSSettings.TTSSupplier.FishAudio:
                    return new Provider.FishAudioProvider();
                case TTSSettings.TTSSupplier.CosyVoice:
                    return new Provider.CosyVoiceProvider();
                case TTSSettings.TTSSupplier.IndexTTS:
                    return new Provider.IndexTTSProvider();
                case TTSSettings.TTSSupplier.AzureTTS:
                    var azureProvider = new Provider.AzureTTSProvider();
                    if (settings != null)
                    {
                        string region = settings.GetSupplierRegion(supplier);
                        azureProvider.SetRegion(region);
                    }
                    return azureProvider;
                case TTSSettings.TTSSupplier.EdgeTTS:
                    return new Provider.EdgeTTSProvider();
                case TTSSettings.TTSSupplier.GeminiTTS:
                    return new Provider.GeminiTTSProvider();
                case TTSSettings.TTSSupplier.Custom:
                    if (settings != null)
                    {
                        var customConfig = settings.GetCurrentCustomProvider();
                        if (customConfig != null)
                        {
                            return new Provider.CustomTTSProvider(customConfig);
                        }
                        TTSLog.Warning("[RimTalk.TTS] No custom provider selected");
                    }
                    return new Provider.NoneProvider();
                case TTSSettings.TTSSupplier.None:
                default:
                    return new Provider.NoneProvider();
            }
        }

        /// <summary>
        /// Initiate TTS generation for a dialogue. Runs asynchronously.
        /// </summary>
        public static void ProcessDialogue(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            TTSLog.Message($"[RimTalk.TTS] Request: pawn={pawn?.LabelShort}, supplier={settings?.Supplier}, provider={_provider?.GetType().Name}, id={dialogueId.ToString().Substring(0, 8)}");

            // Perform early validation checks
            if (!ValidateDialogueRequest(text, pawn, dialogueId, settings, out string reason))
            {
                TTSLog.Warning($"[RimTalk.TTS] Rejected [{dialogueId.ToString().Substring(0, 8)}] - {reason}");
                CleanupAndRelease(dialogueId);
                return;
            }
            
            // Start async generation
            Task.Run(async () => 
            {
                await ProcessDialogueAsync(text, pawn, dialogueId, settings);
            });
        }

        /// <summary>
        /// Validate if a dialogue request should be processed
        /// </summary>
        private static bool ValidateDialogueRequest(string text, Pawn pawn, Guid dialogueId, TTSSettings settings, out string reason)
        {
            // Early exit: shutting down
            if (_isShuttingDown)
            {
                reason = "Shutting down";
                return false;
            }

            // Validate API key for the selected supplier. Each provider decides for itself
            // whether a key is required (EdgeTTS/None: never; Custom: per its RequiresApiKey
            // flag; cloud providers: always), so no supplier may be special-cased here.
            string apiKey = GetApiKeyForSupplier(settings.Supplier, settings);
            if (!_provider.IsApiKeyValid(apiKey))
            {
                reason = $"API key not configured or invalid for supplier {settings.Supplier}";
                return false;
            }

            // Early exit: empty text
            if (string.IsNullOrEmpty(text))
            {
                reason = "Empty text";
                return false;
            }

            // Early exit: pawn has "NONE" voice model (skip TTS entirely)
            string voiceModelId = GetVoiceModelId(pawn, settings);
            TTSLog.Message($"[RimTalk.TTS] Voice resolved: pawn={pawn?.LabelShort}, voice={voiceModelId}, supplierKey={settings.GetCurrentSupplierKey()}");
            if (voiceModelId == VoiceModel.NONE_MODEL_ID)
            {
                reason = $"Pawn '{pawn?.LabelShort}' has NONE voice model";
                return false;
            }

            // Check if dialogue was cancelled
            if (RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                reason = $"Dialogue {dialogueId} was ignored";
                return false;
            }

            // Check if TTS Module is active
            if (!IsModuleActiveAndEnabled(settings))
            {
                reason = "TTS module off";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Check if dialogue should continue processing (used during async operations)
        /// </summary>
        private static bool ShouldContinueProcessing(Guid dialogueId, TTSSettings settings, out string reason)
        {
            if (RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                reason = "Dialogue was ignored during generation";
                return false;
            }

            if (!IsModuleActiveAndEnabled(settings))
            {
                reason = "TTS module turned off during generation";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Async TTS generation pipeline
        /// </summary>
        private static async Task ProcessDialogueAsync(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            try
            {
                // Get voice model
                string voiceModelId = GetVoiceModelId(pawn, settings);
                TTSLog.Message($"[RimTalk.TTS] Pipeline start [{dialogueId.ToString().Substring(0, 8)}]: pawn={pawn?.LabelShort}, voice={voiceModelId}, textLen={text?.Length}");

                // Process and translate text (using pawn-specific language if set)
                string finalInputText = await ProcessTextAsync(text, pawn, dialogueId, settings);
                if (finalInputText == null)
                {
                    TTSLog.Warning($"[RimTalk.TTS] Preprocessing failed [{dialogueId.ToString().Substring(0, 8)}], aborting");
                    CleanupAndRelease(dialogueId);
                    return;
                }
                TTSLog.Message($"[RimTalk.TTS] Preprocessing done [{dialogueId.ToString().Substring(0, 8)}]: outputLen={finalInputText.Length}");

                string finalInstructText = null;

                // Check if should continue after preprocessing
                if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
                {
                    TTSLog.Message($"[RimTalk.TTS] {reason} (discarding audio)");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                // Apply cooldown
                await ApplyCooldownAsync(settings);
                
                // Generate speech
                byte[] audioData = await GenerateSpeechAsync(voiceModelId, finalInputText, finalInstructText, settings);

                // Final validation and playback setup
                HandleGenerationResult(dialogueId, audioData, settings);
            }
            catch (OperationCanceledException)
            {
                TTSLog.Message($"[RimTalk.TTS] Dialogue {dialogueId} generation cancelled");
                CleanupAndRelease(dialogueId);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                CleanupAndRelease(dialogueId);
            }
        }

        /// <summary>
        /// Process and translate text if needed
        /// </summary>
        private static async Task<string> ProcessTextAsync(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            // Skip preprocessing entirely if provider is set to Skip
            if (settings.ApiProvider == Data.TTSApiProvider.Skip)
            {
                TTSLog.Message($"[RimTalk.TTS] Preprocess skipped [{dialogueId.ToString().Substring(0, 8)}]: passing raw text to TTS");
                return text;
            }

            // Get effective language for this pawn (pawn-specific or global fallback)
            string language = Data.PawnVoiceManager.GetEffectiveLanguage(pawn, settings);
            TTSLog.Message($"[RimTalk.TTS] Preprocess [{dialogueId.ToString().Substring(0, 8)}]: language={language ?? "(null)"}, inputLen={text?.Length}");

            if (!string.IsNullOrWhiteSpace(language))
            {
                var preProcessResult = await InputPreProcessService.PreProcessAsync(text, language, settings);

                if (preProcessResult != null && !string.IsNullOrEmpty(preProcessResult.Text))
                {
                    return preProcessResult.Text;
                }
                else
                {
                    TTSLog.Warning($"[RimTalk.TTS] Translation/PreProcess returned empty result (preProcessResult={(preProcessResult == null ? "null" : "empty text")})");
                    return null;
                }
            }
            else
            {
                TTSLog.Warning($"[RimTalk.TTS] Translation language not configured");
                return null;
            }
        }

        /// <summary>
        /// Apply cooldown between requests
        /// </summary>
        private static async Task ApplyCooldownAsync(TTSSettings settings)
        {
            lock (_waitingRequestLock)
            {
                _waitingRequestCount++;
            }

            int nowMilisecond = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
            string supplierKey = settings.GetCurrentSupplierKey();
            int cooldownMilisecond = settings.GetSupplierGenerateCooldown(supplierKey);
            int cooldownEndMilisecond = _waitingRequestCount * cooldownMilisecond + _lastGenerateTimeStampMilisecond;

            if (nowMilisecond < cooldownEndMilisecond)
            {
                await Task.Delay(cooldownEndMilisecond - nowMilisecond);
            }

            lock (_waitingRequestLock)
            {
                _lastGenerateTimeStampMilisecond = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                _waitingRequestCount--;
            }
        }

        /// <summary>
        /// Generate speech using configured provider
        /// </summary>
        private static async Task<byte[]> GenerateSpeechAsync(string voiceModelId, string inputText, string instructText, TTSSettings settings)
        {
            // Use GetCurrentSupplierKey for dictionary lookups (handles Custom suppliers)
            string supplierKey = settings.GetCurrentSupplierKey();
            string apiKey = settings.GetSupplierApiKey(supplierKey);
            string model = settings.GetSupplierModel(supplierKey);
            string apiKeyDisplay = string.IsNullOrWhiteSpace(apiKey) ? "(empty)" : apiKey.Substring(0, System.Math.Min(6, apiKey.Length)) + "***";
            TTSLog.Message($"[RimTalk.TTS] Generating speech: supplierKey={supplierKey}, voice={voiceModelId}, model={model}, apiKey={apiKeyDisplay}, inputLen={inputText?.Length}, provider={_provider?.GetType().Name}");

            var ttsRequest = new Service.TTSRequest
            {
                ApiKey = apiKey,
                Model = model,
                Input = inputText,
                InstructText = instructText,
                Voice = voiceModelId,
                Speed = settings.GetSupplierSpeed(supplierKey),
                Volume = settings.GetSupplierVolume(supplierKey),
                Temperature = settings.GetSupplierTemperature(supplierKey),
                TopP = settings.GetSupplierTopP(supplierKey)
            };

            var result = await _provider.GenerateSpeechAsync(ttsRequest);
            TTSLog.Message($"[RimTalk.TTS] Speech result: {(result == null ? "null (failed)" : result.Length + " bytes")}");
            return result;
        }

        /// <summary>
        /// Handle the result of TTS generation
        /// </summary>
        private static void HandleGenerationResult(Guid dialogueId, byte[] audioData, TTSSettings settings)
        {
            bool stillBlocked = RimTalkPatches.IsBlocked(dialogueId);
            TTSLog.Diag($"audio ready [{dialogueId.ToString("N").Substring(0, 8)}]: {(audioData?.Length ?? 0)} bytes, blocked={stillBlocked}");

            // Check if should continue
            if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
            {
                TTSLog.Message($"[RimTalk.TTS] {reason} (discarding audio)");
                CleanupAndRelease(dialogueId);
                return;
            }

            if (audioData != null && audioData.Length > 0)
            {
                if (!RimTalkPatches.IsBlocked(dialogueId))
                {
                    TTSLog.Warning($"[RimTalk.TTS] Dialogue [{dialogueId.ToString().Substring(0, 8)}] unblocked before audio arrived — discarding {audioData.Length} bytes");
                    CleanupFailedDialogue(dialogueId);
                }
                else
                {
                    TTSLog.Message($"[RimTalk.TTS] Audio ready [{dialogueId.ToString().Substring(0, 8)}]: {audioData.Length} bytes, queued for playback");
                    AudioPlaybackService.SetAudioResult(dialogueId, audioData);
                }
                RimTalkPatches.ReleaseBlock(dialogueId);
            }
            else
            {
                TTSLog.Warning($"[RimTalk.TTS] API returned no audio data [{dialogueId.ToString().Substring(0, 8)}]");
                CleanupAndRelease(dialogueId);
            }
        }

        private static void CleanupFailedDialogue(Guid dialogueId)
        {
            if (dialogueId != Guid.Empty)
            {
                AudioPlaybackService.SetAudioResult(dialogueId, null);
            }
        }

        // Merge common cleanup + release pattern into one helper to simplify call sites
        private static void CleanupAndRelease(Guid dialogueId)
        {
            CleanupFailedDialogue(dialogueId);
            RimTalkPatches.ReleaseBlock(dialogueId);
        }

        private static bool IsModuleActiveAndEnabled(TTSSettings settings)
        {
            return TTSConfig.IsEnabled && settings != null && settings.isOnButton;
        }

        private static string GetVoiceModelId(Pawn pawn, TTSSettings settings)
        {
            string supplierKey = settings.GetCurrentSupplierKey();
            var voiceModels = settings.GetSupplierVoiceModels(supplierKey);
            string defaultVoice = settings.GetSupplierDefaultVoiceModelId(supplierKey);

            if (pawn != null)
            {
                string voiceModel = Data.PawnVoiceManager.GetVoiceModel(pawn);
                if (!string.IsNullOrEmpty(voiceModel) && voiceModels.Any(vm => vm.ModelId == voiceModel))
                {
                    return voiceModel;
                }
            }

            return defaultVoice;
        }

        private static string GetApiKeyForSupplier(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            if (settings == null) return string.Empty;

            // For custom suppliers, use the supplier key
            if (supplier == TTSSettings.TTSSupplier.Custom)
            {
                string key = settings.GetCurrentSupplierKey();
                return settings.GetSupplierApiKey(key);
            }

            // Prefer SupplierApiKeys dictionary if present
            return settings.GetSupplierApiKey(supplier);
        }

        public static void StopAll(bool permanentShutdown = false)
        {
            if (permanentShutdown)
            {
                _isShuttingDown = true;
                try
                {
                    _provider?.Shutdown();
                }
                catch { }
            }

            List<Guid> toCancel;
            lock (RimTalkPatches.blockedDialogues)
            {
                toCancel = RimTalkPatches.blockedDialogues.ToList();
            }

            TTSLog.Diag($"StopAll(permanentShutdown={permanentShutdown}): cancelling {toCancel.Count} blocked dialogue(s) - their audio will be reported as discarded");
            
            // Cancel all pending TTS generation tasks
            foreach (var id in toCancel)
            {
                CancelDialogue(id);
            }
            
            lock (RimTalkPatches.blockedDialogues)
            {
                RimTalkPatches.blockedDialogues.Clear();
            }
            
            AudioPlaybackService.StopAndClear();
        }

        public static void CancelDialogue(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return;

            bool wasBlocked = RimTalkPatches.IsBlocked(dialogueId);
            TTSLog.Diag($"cancel [{dialogueId.ToString("N").Substring(0, 8)}]: wasBlocked={wasBlocked} (block released, pending speech dropped)");

            if (wasBlocked)
            {
                CleanupAndRelease(dialogueId);
            }
            else
            {
                AudioPlaybackService.RemovePendingAudio(dialogueId);
            }
        }

        public static void ReloadMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            try
            {
                int pawnCount = 0;
                try
                {
                    pawnCount = map.mapPawns.AllPawns.Count;
                }
                catch (Exception exCount)
                {
                    TTSLog.Warning($"[RimTalk.TTS] ReloadMap: failed to get pawn count for map '{map}': {exCount}");
                }

                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    try
                    {
                        RimTalkPatches.AddPawnDialogueList(pawn);
                    }
                    catch (Exception exPawn)
                    {
                        try
                        {
                            var pawnId = pawn?.thingIDNumber.ToString() ?? "<null>";
                            var pawnName = pawn?.LabelShort ?? pawn?.Name?.ToString() ?? "<unnamed>";
                            TTSLog.Error($"[RimTalk.TTS] ReloadMap: AddPawnDialogueList failed for pawn '{pawnName}' (id={pawnId}): {exPawn}");
                        }
                        catch (Exception exInner)
                        {
                            // Best effort logging; avoid throwing from logger
                            TTSLog.Error($"[RimTalk.TTS] ReloadMap: failed to log pawn exception: {exInner}");
                        }
                    }
                }
}
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] ReloadMap: Unexpected error iterating pawns on map '{map?.ToString() ?? "<null>"}': {ex}");
            }
        }
    }
}
