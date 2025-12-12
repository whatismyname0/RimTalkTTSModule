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

        /// <summary>
        /// Initiate TTS generation for a dialogue. Runs asynchronously.
        /// </summary>
        public static void ProcessDialogue(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            // Early exit: shutting down
            if (_isShuttingDown)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Rejected - Shutting down");
                CleanupAndRelease(dialogueId);
                return;
            }
                
            // Validate API key
            if (string.IsNullOrEmpty(settings.FishAudioApiKey))
            {
                Log.Warning("[RimTalk.TTS] DEBUG: Rejected - Fish Audio API key not configured");
                CleanupAndRelease(dialogueId);
                return;
            }

            // Early exit: empty text
            if (string.IsNullOrEmpty(text))
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Rejected - Empty text");
                CleanupAndRelease(dialogueId);
                return;
            }

            // Early exit: pawn has "NONE" voice model (skip TTS entirely)
            string voiceModelId = GetVoiceModelId(pawn, settings);
            if (voiceModelId == VoiceModel.NONE_MODEL_ID)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Pawn '{pawn?.LabelShort}' has NONE voice model - setting null audio and releasing block");
                CleanupAndRelease(dialogueId);
                return;
            }

            // Check if dialogue was cancelled during generation
            if (RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                CleanupAndRelease(dialogueId);
                return;
            }

            // Check if TTS Module is still active
            if (!IsModuleActiveAndEnabled(settings))
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
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
                    processedText = await InputPreProcessService.PreProcessAsync(text, settings.TTSTranslationLanguage, settings);
                    if (string.IsNullOrWhiteSpace(processedText))
                    {
                        Log.Warning($"[RimTalk.TTS] DEBUG: Translation failed");
                        CleanupAndRelease(dialogueId);
                        return;
                    }
                }
                else
                {
                    Log.Warning($"[RimTalk.TTS] DEBUG: Translation language not configured");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                if (processedText == text)
                {
                    Log.Warning($"[RimTalk.TTS] DEBUG: Translation returned invalid result");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                // Check if dialogue was cancelled during generation
                if (RimTalkPatches.IsTalkIgnored(dialogueId))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                // Check if TTS Module is still active
                if (!IsModuleActiveAndEnabled(settings))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                lock (_waitingRequestLock)
                {
                    _waitingRequestCount++;
                }

                int nowMilisecond = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                int cooldownMilisecond = settings.GenerateCooldownMiliSeconds;
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
                if (!IsModuleActiveAndEnabled(settings))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was cancelled during generation (TTS module off)");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                // Check if dialogue was cancelled during generation
                if (RimTalkPatches.IsTalkIgnored(dialogueId))
                {
                    Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} was ignored during generation (discarding audio)");
                    CleanupAndRelease(dialogueId);
                }
                else if (audioData != null && audioData.Length > 0)
                {
                    if (!RimTalkPatches.IsBlocked(dialogueId))
                    {
                        Log.Message($"[RimTalk.TTS] DEBUG: Dialogue {dialogueId} is no longer blocked after generation (discarding audio)");
                        CleanupFailedDialogue(dialogueId);
                    }
                    else
                        AudioPlaybackService.SetAudioResult(dialogueId, audioData);
                    RimTalkPatches.ReleaseBlock(dialogueId);
                }
                else
                {
                    Log.Warning("[RimTalk.TTS] DEBUG: Failed - API returned no audio data");
                    CleanupAndRelease(dialogueId); // Release on failure
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[RimTalk.TTS] DEBUG: Cancelled - Dialogue {dialogueId} generation cancelled");
                CleanupAndRelease(dialogueId); // Release on cancellation
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] DEBUG: Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                CleanupAndRelease(dialogueId); // Release on error
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
            return TTSModule.Instance.IsActive && settings != null && settings.isOnButton;
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
                Log.Message("[RimTalk.TTS] ReloadMap: called with null map");
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
