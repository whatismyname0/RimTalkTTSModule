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
                
                Log.Message($"[RimTalk.TTS] TTS provider set to {supplier}");
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
                Log.Warning($"[RimTalk.TTS] Error shutting down provider: {ex.Message}");
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
                Log.Warning($"[RimTalk.TTS] Error resetting runtime state: {ex.Message}");
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
            // Perform early validation checks
            if (!ValidateDialogueRequest(text, pawn, dialogueId, settings, out string reason))
            {
                Log.Message($"[RimTalk.TTS] Rejected - {reason}");
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

            // Validate API key for selected supplier
            // EdgeTTS doesn't need API key - skip validation
            if (settings.Supplier != TTSSettings.TTSSupplier.EdgeTTS)
            {
                string apiKey = GetApiKeyForSupplier(settings.Supplier, settings);
                if (!_provider.IsApiKeyValid(apiKey))
                {
                    reason = $"API key not configured or invalid for supplier {settings.Supplier}";
                    return false;
                }
            }

            // Early exit: empty text
            if (string.IsNullOrEmpty(text))
            {
                reason = "Empty text";
                return false;
            }

            // Early exit: pawn has "NONE" voice model (skip TTS entirely)
            string voiceModelId = GetVoiceModelId(pawn, settings);
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

                // Process and translate text
                string finalInputText = await ProcessTextAsync(text, dialogueId, settings);
                if (finalInputText == null)
                {
                    CleanupAndRelease(dialogueId);
                    return;
                }

                string finalInstructText = null;

                // Check if should continue after preprocessing
                if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
                {
                    Log.Message($"[RimTalk.TTS] {reason} (discarding audio)");
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
                Log.Message($"[RimTalk.TTS] Dialogue {dialogueId} generation cancelled");
                CleanupAndRelease(dialogueId);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                CleanupAndRelease(dialogueId);
            }
        }

        /// <summary>
        /// Process and translate text if needed
        /// </summary>
        private static async Task<string> ProcessTextAsync(string text, Guid dialogueId, TTSSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.TTSTranslationLanguage))
            {
                var preProcessResult = await InputPreProcessService.PreProcessAsync(text, settings.TTSTranslationLanguage, settings);
                
                if (preProcessResult != null && !string.IsNullOrEmpty(preProcessResult.Text))
                {
                    return preProcessResult.Text;
                }
                else
                {
                    Log.Warning($"[RimTalk.TTS] Translation/PreProcess returned empty result");
                    return null;
                }
            }
            else
            {
                Log.Warning($"[RimTalk.TTS] Translation language not configured");
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
            int cooldownMilisecond = settings.GetSupplierGenerateCooldown(settings.Supplier);
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
            var ttsRequest = new Service.TTSRequest
            {
                ApiKey = settings.GetSupplierApiKey(settings.Supplier),
                Model = settings.GetSupplierModel(settings.Supplier),
                Input = inputText,
                InstructText = instructText,
                Voice = voiceModelId,
                Speed = settings.GetSupplierSpeed(settings.Supplier),
                Volume = settings.GetSupplierVolume(settings.Supplier),
                Temperature = settings.GetSupplierTemperature(settings.Supplier),
                TopP = settings.GetSupplierTopP(settings.Supplier)
            };

            return await _provider.GenerateSpeechAsync(ttsRequest);
        }

        /// <summary>
        /// Handle the result of TTS generation
        /// </summary>
        private static void HandleGenerationResult(Guid dialogueId, byte[] audioData, TTSSettings settings)
        {
            // Check if should continue
            if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
            {
                Log.Message($"[RimTalk.TTS] {reason} (discarding audio)");
                CleanupAndRelease(dialogueId);
                return;
            }

            if (audioData != null && audioData.Length > 0)
            {
                if (!RimTalkPatches.IsBlocked(dialogueId))
                {
                    Log.Message($"[RimTalk.TTS] Dialogue {dialogueId} is no longer blocked after generation (discarding audio)");
                    CleanupFailedDialogue(dialogueId);
                }
                else
                {
                    AudioPlaybackService.SetAudioResult(dialogueId, audioData);
                }
                RimTalkPatches.ReleaseBlock(dialogueId);
            }
            else
            {
                Log.Warning("[RimTalk.TTS] Failed - API returned no audio data");
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
            // Get pawn-specific voice model directly from PawnVoiceManager
            if (pawn != null)
            {
                string voiceModel = Data.PawnVoiceManager.GetVoiceModel(pawn);
                if (!string.IsNullOrEmpty(voiceModel) && settings.GetSupplierVoiceModels(settings.Supplier).Any(vm => vm.ModelId == voiceModel))
                {
                    return voiceModel;
                }
            }

            // Fallback to default voice model
            return settings.GetSupplierDefaultVoiceModelId(settings.Supplier);
        }

        private static string GetApiKeyForSupplier(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            if (settings == null) return string.Empty;

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
            
            if (RimTalkPatches.IsBlocked(dialogueId))
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
                    Log.Warning($"[RimTalk.TTS] ReloadMap: failed to get pawn count for map '{map}': {exCount}");
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
                            Log.Error($"[RimTalk.TTS] ReloadMap: AddPawnDialogueList failed for pawn '{pawnName}' (id={pawnId}): {exPawn}");
                        }
                        catch (Exception exInner)
                        {
                            // Best effort logging; avoid throwing from logger
                            Log.Error($"[RimTalk.TTS] ReloadMap: failed to log pawn exception: {exInner}");
                        }
                    }
                }
}
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] ReloadMap: Unexpected error iterating pawns on map '{map?.ToString() ?? "<null>"}': {ex}");
            }
        }
    }
}
