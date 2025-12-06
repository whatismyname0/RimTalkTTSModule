using System;
using System.Threading.Tasks;
using RimTalk.TTS.Data;
using RimTalk.TTS.Patch;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Coordinates Text-to-Speech generation for dialogue.
    /// Each request has its own CancellationTokenSource for independent cancellation.
    /// </summary>
    public static class TTSService
    {
        
        private static volatile bool _isShuttingDown = false;

        /// <summary>
        /// Initiate TTS generation for a dialogue. Runs asynchronously.
        /// </summary>
        public static void ProcessDialogue(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            // Early exit: shutting down
            if (_isShuttingDown)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Rejected - Shutting down");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }
                
            // Validate API key
            if (string.IsNullOrEmpty(settings.FishAudioApiKey))
            {
                Log.Warning("[RimTalk.TTS] DEBUG: Rejected - Fish Audio API key not configured");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }

            // Early exit: empty text
            if (string.IsNullOrEmpty(text))
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Rejected - Empty text");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }

            // Early exit: pawn has "NONE" voice model (skip TTS entirely)
            string voiceModelId = GetVoiceModelId(pawn, settings);
            if (voiceModelId == VoiceModel.NONE_MODEL_ID)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Pawn '{pawn?.LabelShort}' has NONE voice model - setting null audio and releasing block");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }

            // Check if dialogue was cancelled during generation
            if (Patch.RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }

            // Check if TTS Module is still active
            if (!TTSModule.Instance.IsActive||!settings.isTemporarilyOff)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                return;
            }
            
            // Start async generation
            Task.Run(async () => 
            {
                await ProcessDialogueAsync(text, pawn, dialogueId, settings);
            });
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

                // Process text
                string processedText = null;

                // Translate if configured
                if (!string.IsNullOrWhiteSpace(settings.TTSTranslationLanguage))
                {
                    try
                    {
                        processedText = await TranslationService.TranslateAsync(text, settings.TTSTranslationLanguage, settings);
                        // If translation returns empty or null, use original text
                        if (string.IsNullOrWhiteSpace(processedText))
                        {
                            Log.Warning($"[RimTalk.TTS] DEBUG: Translation returned empty result");
                        }
                    }
                    catch (Exception translateEx)
                    {
                        Log.Error($"[RimTalk.TTS] DEBUG: Translation failed with exception: {translateEx.Message}");
                    }
                }
                else
                {
                    Log.Warning($"[RimTalk.TTS] DEBUG: Translation language not configured");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                    return;
                }

                if (processedText == text)
                {
                    Log.Warning($"[RimTalk.TTS] DEBUG: Translation returned invalid result");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                    return;
                }

                // Check if dialogue was cancelled during generation
                if (Patch.RimTalkPatches.IsTalkIgnored(dialogueId))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                    return;
                }

                // Check if TTS Module is still active
                if (!TTSModule.Instance.IsActive||!settings.isTemporarilyOff)
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                    return;
                }
                
                // Generate speech via Fish Audio API
                byte[] audioData = await FishAudioTTSClient.GenerateSpeechAsync(
                    processedText,
                    settings.FishAudioApiKey,
                    voiceModelId,
                    settings.TTSModel,
                    settings.TTSTemperature,
                    settings.TTSTopP
                );

                // Check if TTS Module is still active
                if (!TTSModule.Instance.IsActive||!settings.isTemporarilyOff)
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                    return;
                }

                // Check if dialogue was cancelled during generation
                if (Patch.RimTalkPatches.IsTalkIgnored(dialogueId))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                }
                else if (audioData != null && audioData.Length > 0)
                {
                    if (!RimTalkPatches.IsBlocked(dialogueId))
                    {
                        Log.Warning($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} is no longer blocked after generation (discarding audio)");
                        CleanupFailedDialogue(dialogueId);
                    }
                    else
                        AudioPlaybackService.SetAudioResult(dialogueId, audioData);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                }
                else
                {
                    Log.Warning("[RimTalk.TTS] DEBUG: Failed - API returned no audio data");
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId); // Release on failure
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Cancelled - Dialogue {dialogueId} generation cancelled");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId); // Release on cancellation
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] DEBUG: Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                CleanupFailedDialogue(dialogueId);
                Patch.RimTalkPatches.ReleaseBlock(dialogueId); // Release on error
            }
        }

        private static void CleanupFailedDialogue(Guid dialogueId)
        {
            if (dialogueId != Guid.Empty)
            {
                AudioPlaybackService.SetAudioResult(dialogueId, null);
            }
        }

        private static string GetVoiceModelId(Pawn pawn, TTSSettings settings)
        {
            // Get pawn-specific voice model directly from PawnVoiceManager
            if (pawn != null)
            {
                string voiceModel = Data.PawnVoiceManager.GetVoiceModel(pawn);
                if (!string.IsNullOrEmpty(voiceModel))
                {
                    return voiceModel;
                }
            }

            // Fallback to default voice model
            if (!string.IsNullOrEmpty(settings.DefaultVoiceModelId))
            {
                return settings.DefaultVoiceModelId;
            }

            return "";
        }

        public static void StopAll(bool permanentShutdown = false)
        {
            if (permanentShutdown)
            {
                _isShuttingDown = true;
            }
            
            // Cancel all pending TTS generation tasks
            foreach (var kvp in RimTalkPatches.blockedDialogues)
            {
                CancelDialogue(kvp);
            }
            RimTalkPatches.blockedDialogues.Clear();
            
            AudioPlaybackService.StopAndClear();
        }

        public static void CancelDialogue(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return;
            
            if (RimTalkPatches.IsBlocked(dialogueId))
            {
                try
                {
                    CleanupFailedDialogue(dialogueId);
                    Patch.RimTalkPatches.ReleaseBlock(dialogueId);
                }
                catch { /* Ignore cancellation errors */ }
            }
            else
            {
                AudioPlaybackService.RemovePendingAudio(dialogueId);
            }
        }
    }
}
