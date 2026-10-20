using HarmonyLib;
using System;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimTalk.Service;
using Verse;
using RimTalk.TTS.Service;
using RimTalk.Data;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.Patch
{
    /// <summary>
    /// Harmony patches to hook into main RimTalk events
    /// Simplified version that only patches CreateInteraction for TTS generation and playback
    /// </summary>
    public static class RimTalkPatches
    {
        // Note: RimTalk types are now referenced directly via assembly reference

        // ToggleButton
        private static bool _pendingToggle = false;
        private static bool _pendingToggleValue = false;
        private static readonly object _pendingToggleLock = new object();
        private static string _pendingToggleMessage = "";
        
        /// <summary>
        /// Check if a dialogue is marked as ignored in main RimTalk
        /// </summary>
        public static bool IsTalkIgnored(Guid dialogueId)
        {
            try
            {
                return TalkHistory.IsTalkIgnored(dialogueId);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] IsTalkIgnored exception: {ex}");
            }
            return false;
        }

        // Track which dialogues are blocked (waiting for TTS generation)
        public static readonly HashSet<Guid> blockedDialogues = new HashSet<Guid>();
        private static readonly object _blockLock = new object();
        
        // Map TalkResponses lists to their owning Pawns
        private static readonly ConditionalWeakTable<object, Pawn> _listToPawnMap = new ConditionalWeakTable<object, Pawn>();

        // No runtime reflection required; types are referenced directly

        /// <summary>
        /// Patch: TalkService.CreateInteraction - Intercept when dialogue is displayed
        /// </summary>
        [HarmonyPatch]
        public static class CreateInteraction_Patch
        {
            static bool Prepare()
            {
                var method = typeof(TalkService).GetMethod("CreateInteraction",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn), typeof(global::RimTalk.Data.TalkResponse) },
                    null);

                if (method == null)
                {
                    // Expected on RimTalk 1.2.0+, which replaced CreateInteraction with
                    // TalkService.MarkTalkSpoken (handled by MarkTalkSpoken_Patch below).
                    TTSLog.Message("[RimTalk.TTS] CreateInteraction_Patch: Method not found (RimTalk 1.2.0+), skipping patch");
                    return false;
                }

                TTSLog.Message("[RimTalk.TTS] Successfully found CreateInteraction method");
                return true;
            }

            static MethodBase TargetMethod()
            {
                return typeof(TalkService).GetMethod("CreateInteraction",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn), typeof(global::RimTalk.Data.TalkResponse) },
                    null);
            }

            // Prefix: Check if dialogue is blocked (TTS still generating) or audio is playing
            static bool Prefix(Pawn pawn, object talk)
            {
                
                try
                {
                    if (!TTSModuleIsActive())
                        return true;

                    if (pawn == null || talk == null)
                        return true;

                    // Cast to RimTalk.Data.TalkResponse and read properties directly
                    var talkResp = talk as TalkResponse;
                    if (talkResp == null)
                        return true;

                    var dialogueId = talkResp.Id;

                    // If this pawn has TTS disabled (NONE voice), skip audio entirely
                    if (PawnVoiceManager.GetVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID)
                        return true;

                    // Check if audio is currently playing - block new interactions
                    if (Service.AudioPlaybackService.IsCurrentlyPlaying())
                    {
                        return false; // Block the original method from executing
                    }
                    
                    // Check if this dialogue is blocked (TTS still generating)
                    if (IsBlocked(dialogueId))
                    {
                        return false; // Block the original method from executing
                    }

                    // Get volume from settings using correct supplier key (handles Custom suppliers)
                    var settings = TTSConfig.Settings;
                    float volume = settings != null
                        ? settings.GetSupplierVolume(settings.GetCurrentSupplierKey())
                        : 1.0f;

                    // Play audio (will wait for TTS generation and previous playback)
                    Service.AudioPlaybackService.PlayAudio(dialogueId, pawn, volume);
                    return true; // Allow original method to execute
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] CreateInteraction Prefix exception: {ex}");
                    return true; // On error, allow execution
                }
            }
        }

        /// <summary>
        /// Patch: TalkService.MarkTalkSpoken - Intercept when dialogue is actually shown.
        ///
        /// RimTalk 1.2.0 removed TalkService.CreateInteraction: spoken lines are no longer turned
        /// into a LogEntry there, and the line becomes visible (overlay log row, conversation
        /// pacing, export) the moment <c>MarkTalkSpoken</c> dequeues it from PawnState.TalkResponses.
        /// That makes this the point where a line has to be held back until its speech exists and
        /// the previous line has finished playing; without the hold the line is consumed instantly
        /// and the generated audio is never played because nothing calls PlayAudio any more.
        /// </summary>
        [HarmonyPatch]
        public static class MarkTalkSpoken_Patch
        {
            static bool Prepare()
            {
                var method = GetTarget();
                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] MarkTalkSpoken_Patch: Method not found, skipping patch");
                    return false;
                }

                TTSLog.Message("[RimTalk.TTS] MarkTalkSpoken_Patch: Method found, patch will be applied");
                TTSLog.Diag("patch applied on TalkService.MarkTalkSpoken (RimTalk 1.2.0 display path)");
                return true;
            }

            // ---- temporary diagnostics (see TTSLog.Diag) ----
            private static readonly HashSet<string> _diagSeen = new HashSet<string>();
            private static bool _diagModuleOffLogged;

            private static void DiagOnce(Guid id, string reason, string message)
            {
                string key = id.ToString("N") + "|" + reason;
                lock (_diagSeen)
                {
                    if (!_diagSeen.Add(key)) return;
                }
                TTSLog.Diag(message);
            }

            private static string ShortId(Guid id)
            {
                return id.ToString("N").Substring(0, 8);
            }

            static MethodBase TargetMethod()
            {
                return GetTarget();
            }

            private static MethodBase GetTarget()
            {
                return typeof(TalkService).GetMethod("MarkTalkSpoken",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(global::RimTalk.Data.PawnState) },
                    null);
            }

            // Prefix: while the line's audio is still generating, or while the previous line is
            // still playing, refuse to consume the talk. DisplayTalk runs again every 0.5s, so the
            // line simply waits at the head of its queue and is displayed once the audio is ready.
            static bool Prefix(global::RimTalk.Data.PawnState pawnState)
            {
                try
                {
                    if (!TTSModuleIsActive())
                    {
                        if (!_diagModuleOffLogged)
                        {
                            _diagModuleOffLogged = true;
                            TTSLog.Diag("pass: TTS module inactive (IsEnabled/isOnButton off) - no line will ever be held");
                        }
                        return true;
                    }

                    var pawn = pawnState?.Pawn;
                    if (pawn == null)
                        return true;

                    if (pawnState.TalkResponses.Count == 0)
                        return true;

                    var talk = pawnState.TalkResponses[0];
                    if (talk == null)
                        return true;

                    // Pawns without a voice never produce audio and must not be delayed.
                    if (PawnVoiceManager.GetVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID)
                    {
                        DiagOnce(talk.Id, "voice-none", $"pass [{ShortId(talk.Id)}] pawn={pawn.LabelShort}: voice=NONE");
                        return true;
                    }

                    // NOTE: the overlay "Display" button (OverlayButtonPatch.displayPassport) only
                    // shortens RimTalk's reply-interval pacing via CommonUtil.HasPassed - it must
                    // NOT make this line skip its speech. Cancelling the pending audio here (the
                    // old behaviour of this branch) dropped audio that was already generated, so a
                    // manually advanced line was shown without ever being spoken.

                    // Keep playback serialized: do not show a new line over audio still playing.
                    if (Service.AudioPlaybackService.IsCurrentlyPlaying())
                    {
                        DiagOnce(talk.Id, "playing", $"HOLD [{ShortId(talk.Id)}] pawn={pawn.LabelShort}: previous audio still playing");
                        return false;
                    }

                    // Speech for this line is still being generated - wait for it.
                    if (IsBlocked(talk.Id))
                    {
                        DiagOnce(talk.Id, "generating", $"HOLD [{ShortId(talk.Id)}] pawn={pawn.LabelShort}: speech still generating");
                        return false;
                    }

                    // Get volume from settings using correct supplier key (handles Custom suppliers)
                    var settings = TTSConfig.Settings;
                    float volume = settings != null
                        ? settings.GetSupplierVolume(settings.GetCurrentSupplierKey())
                        : 1.0f;

                    DiagOnce(talk.Id, "ready", $"PASS [{ShortId(talk.Id)}] pawn={pawn.LabelShort}: speech ready, starting playback (audioEntry={Service.AudioPlaybackService.HasAudioEntry(talk.Id)})");
                    Service.AudioPlaybackService.PlayAudio(talk.Id, pawn, volume);
                    return true;
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] MarkTalkSpoken Prefix exception: {ex}");
                    return true; // On error, allow execution
                }
            }
        }

        /// <summary>
        /// Patch: TalkHistory.AddIgnored - Sync cancelled dialogues
        /// </summary>
        [HarmonyPatch]
        public static class AddIgnored_Patch
        {
            static bool Prepare()
            {
                TTSLog.Message("[RimTalk.TTS] AddIgnored_Patch.Prepare() called");
                var method = typeof(global::RimTalk.Data.TalkHistory).GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] AddIgnored_Patch: Method not found, skipping patch");
                    return false;
                }
                TTSLog.Message("[RimTalk.TTS] AddIgnored_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                TTSLog.Message("[RimTalk.TTS] AddIgnored_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.Data.TalkHistory).GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
                TTSLog.Message($"[RimTalk.TTS] AddIgnored_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Prefix(Guid id)
            {
                if (!TTSModuleIsActive())
                    return;
                try
                {
                    TTSLog.Diag($"cancel requested by TalkHistory.AddIgnored [{id.ToString("N").Substring(0, 8)}]");
                    TTSModule.Instance.OnDialogueCancelled(id);
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] AddIgnored_Patch Prefix exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Patch: PawnState constructor - Register TalkResponses list in our map
        /// </summary>
        [HarmonyPatch]
        public static class PawnStateConstructor_Patch
        {
            static bool Prepare()
            {
                var ctor = typeof(global::RimTalk.Data.PawnState).GetConstructor(new[] { typeof(Pawn) });
                if (ctor == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] PawnStateConstructor_Patch: Constructor not found, skipping patch");
                    return false;
                }

                TTSLog.Message("[RimTalk.TTS] Successfully found PawnState constructor");
                return true;
            }

            static MethodBase TargetMethod()
            {
                return typeof(global::RimTalk.Data.PawnState).GetConstructor(new[] { typeof(Pawn) });
            }

            // Postfix: Register the TalkResponses list with its owning Pawn
            // Note: Always register, even if TTS is disabled, to support hot-switching
            static void Postfix(object __instance, Pawn pawn)
            {
                    try
                    {
                        // Register using the freshly constructed PawnState instance to avoid
                        // calling Cache.Get(pawn) which may construct PawnState and cause recursion.
                        AddPawnDialogueListForPawnState(__instance, pawn);
                    }
                    catch (Exception ex)
                    {
                        TTSLog.Error($"[RimTalk.TTS] PawnStateConstructor_Patch Postfix exception: {ex}");
                    }
            }
        }

        /// <summary>
        /// Patch: List<TalkResponse>.Add - Intercept when dialogue is added to queue
        /// </summary>
        [HarmonyPatch]
        public static class TalkResponsesAdd_Patch
        {
            static bool Prepare()
            {
                var listType = typeof(List<global::RimTalk.Data.TalkResponse>);
                var method = listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);

                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] TalkResponsesAdd_Patch: List<TalkResponse>.Add method not found, skipping patch");
                    return false;
                }

                TTSLog.Message("[RimTalk.TTS] Successfully found List<TalkResponse>.Add method");
                return true;
            }

            static MethodBase TargetMethod()
            {
                var listType = typeof(List<global::RimTalk.Data.TalkResponse>);
                return listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            }

            // Postfix: Check if this is a registered PawnState.TalkResponses list
            static void Postfix(object __instance)
            {
                try
                {
                    if (!TTSModuleIsActive())
                        return;

                    if (__instance == null)
                        return;

                    // Check if this list is registered in our map
                    if (!_listToPawnMap.TryGetValue(__instance, out Pawn pawn))
                    {
                        // Not a PawnState.TalkResponses list, ignore
                        return;
                    }

                    // Get the item that was just added
                    var list = __instance as List<global::RimTalk.Data.TalkResponse>;
                    if (list == null || list.Count == 0)
                        return;

                    var item = list[list.Count - 1];
                    if (item == null)
                        return;

                    var dialogueId = item.Id;
                    var text = item.Text;

                    string shortId = dialogueId.ToString("N").Substring(0, 8);
                    string voice = PawnVoiceManager.GetVoiceModel(pawn);

                    if (voice == VoiceModel.NONE_MODEL_ID)
                    {
                        TTSLog.Diag($"request [{shortId}] pawn={pawn.LabelShort}: voice=NONE -> no TTS, line will show without audio");
                        return;
                    }

                    // Immediately mark dialogue as "generating" to block display
                    RequestBlock(dialogueId);

                    TTSLog.Diag($"request [{shortId}] pawn={pawn.LabelShort} voice={voice}: blocked + TTS requested");

                    // Start TTS generation immediately when dialogue enters the queue
                    TTSModule.Instance.OnDialogueGenerated(text, pawn, dialogueId);
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] TalkResponsesAdd_Patch exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Patch: PawnState.IgnoreTalkResponse - Cancel TTS when dialogue is ignored
        /// </summary>
        [HarmonyPatch]
        public static class IgnoreTalkResponse_Patch
        {
            static bool Prepare()
            {
                var method = typeof(global::RimTalk.Data.PawnState).GetMethod("IgnoreTalkResponse", BindingFlags.Public | BindingFlags.Instance);
                
                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] IgnoreTalkResponse_Patch: Method not found, skipping patch");
                    return false;
                }
                
                TTSLog.Message("[RimTalk.TTS] Successfully found IgnoreTalkResponse method");
                return true;
            }

            static MethodBase TargetMethod()
            {
                return typeof(global::RimTalk.Data.PawnState).GetMethod("IgnoreTalkResponse", BindingFlags.Public | BindingFlags.Instance);
            }

            // Prefix: Capture the dialogue ID before it's removed
            static void Prefix(object __instance)
            {
                try
                {
                    if (!TTSModuleIsActive())
                        return;

                    if (__instance == null)
                        return;

                    // Get the list of TalkResponse objects from PawnState
                    var pawnState = __instance as global::RimTalk.Data.PawnState;
                    if (pawnState == null) return;

                    var talkResponsesList = pawnState.TalkResponses as System.Collections.IList;
                    if (talkResponsesList == null || talkResponsesList.Count == 0)
                        return;

                    // Get the first TalkResponse (the one about to be ignored)
                    var talkResponse = talkResponsesList[0] as global::RimTalk.Data.TalkResponse;
                    if (talkResponse == null)
                        return;

                    var dialogueId = talkResponse.Id;
                    // Cancel TTS generation or discard generated audio
                    TTSLog.Diag($"cancel requested by PawnState.IgnoreTalkResponse [{dialogueId.ToString("N").Substring(0, 8)}]");
                    TTSModule.Instance.OnDialogueCancelled(dialogueId);
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] IgnoreTalkResponse_Patch exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Lifecycle patches for RimTalk.RimTalk GameComponent
        /// </summary>
        [HarmonyPatch]
        public static class StartedNewGame_Patch
        {
            static bool Prepare()
            {
                TTSLog.Message("[RimTalk.TTS] StartedNewGame_Patch.Prepare() called");
                var type = typeof(global::RimTalk.RimTalk);
                if (type == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] StartedNewGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = typeof(global::RimTalk.RimTalk).GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] StartedNewGame_Patch: Method not found, skipping patch");
                    return false;
                }
                TTSLog.Message("[RimTalk.TTS] StartedNewGame_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                TTSLog.Message("[RimTalk.TTS] StartedNewGame_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.RimTalk).GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
                TTSLog.Message($"[RimTalk.TTS] StartedNewGame_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Postfix()
            {
                TTSModule.Instance.OnGameLoaded();
                TTSLog.Message("[RimTalk.TTS] New game started, TTS state cleared");
            }
        }

        [HarmonyPatch]
        public static class LoadedGame_Patch
        {
            static bool Prepare()
            {
                TTSLog.Message("[RimTalk.TTS] LoadedGame_Patch.Prepare() called");
                var type = typeof(global::RimTalk.RimTalk);
                if (type == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] LoadedGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = typeof(global::RimTalk.RimTalk).GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] LoadedGame_Patch: Method not found, skipping patch");
                    return false;
                }
                TTSLog.Message("[RimTalk.TTS] LoadedGame_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                TTSLog.Message("[RimTalk.TTS] LoadedGame_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.RimTalk).GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
                TTSLog.Message($"[RimTalk.TTS] LoadedGame_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Postfix()
            {
                TTSModule.Instance.OnGameLoaded();
                TTSLog.Message("[RimTalk.TTS] Game loaded, TTS state cleared");
            }
        }

        // ==============================================
        // GAME LIFECYCLE PATCHES
        // ==============================================
        // Note: Game shutdown cleanup handled by RimTalk GameComponent
        // StartedNewGame and LoadedGame patches above handle state resets

        /// <summary>
        /// Patch: Pawn.Discard - Clean up voice assignments when pawn is permanently removed
        /// Note: Destroy() is called when pawn leaves map (temporary), Discard() is permanent removal
        /// </summary>
        [HarmonyPatch(typeof(Pawn), "Discard")]
        public static class PawnDiscard_Patch
        {
            static void Prefix(Pawn __instance, bool silentlyRemoveReferences)
            {
                // if (!TTSModule.Instance.IsActive)
                //     return;
                try
                {
                    if (__instance != null)
                    {
                        Data.PawnVoiceManager.RemovePawn(__instance);
                    }
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] PawnDiscard_Patch exception: {ex}");
                }
            }
        }

        // ==============================================
        // DIALOGUE BLOCKING METHODS
        // ==============================================
        
        /// <summary>
        /// Request to block a dialogue from being displayed until TTS is ready
        /// </summary>
        public static void RequestBlock(Guid dialogueId)
        {
            lock (_blockLock)
            {
                blockedDialogues.Add(dialogueId);
            }
        }

        /// <summary>
        /// Release a dialogue block, allowing it to be displayed
        /// </summary>
        public static void ReleaseBlock(Guid dialogueId)
        {
            lock (_blockLock)
            {
                blockedDialogues.Remove(dialogueId);
            }
        }

        /// <summary>
        /// Check if a dialogue is currently blocked
        /// </summary>
        public static bool IsBlocked(Guid dialogueId)
        {
            lock (_blockLock)
            {
                return blockedDialogues.Contains(dialogueId);
            }
        }
    
        [StaticConstructorOnStartup]
        [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
        public static class TogglePatch
        {
            private static readonly Texture2D RimTalkToggleIcon = ContentFinder<Texture2D>.Get("UI/ToggleButton");

            public static void Postfix(WidgetRow row, bool worldView)
            {
                if (!TTSConfig.IsEnabled)
                    return;

                if (worldView || row is null)
                    return;

                var settings = TTSConfig.Settings;

                if (settings.ButtonDisplay != true)
                {
                    return;
                }

                bool onOff = settings.isOnButton;

                row.ToggleableIcon(ref onOff, RimTalkToggleIcon, "",
                    SoundDefOf.Mouseover_ButtonToggle);

                if (onOff != settings.isOnButton)
                {
                    settings.isOnButton = onOff;
                    lock (_pendingToggleLock)
                    {
                        _pendingToggle = true;
                        _pendingToggleValue = onOff;
                        _pendingToggleMessage = "RimTalk.TTS.OnOffUpdated".Translate(onOff ? "RimTalk.TTS.On".Translate() : "RimTalk.TTS.Off".Translate());
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
        public static class Update_PendingToggleExecutor
        {
            static void Postfix()
            {
                if (!TTSConfig.IsEnabled)
                    return;
                if (!_pendingToggle) return;
                bool onOff;
                string msg;
                lock (_pendingToggleLock)
                {
                    onOff = _pendingToggleValue;
                    msg = _pendingToggleMessage;
                    _pendingToggle = false;
                    _pendingToggleMessage = "";
                }

                try
                {
                    Messages.Message(msg, MessageTypeDefOf.TaskCompletion, false);
                    if (!onOff)
                    {
                        TTSService.StopAll(false);
                    }
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] PendingToggleExecutor exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Find and register a pawn's PawnState.TalkResponses list from the RimTalk WorldComponent
        /// Used when reloading a map to restore the pawn-to-list mapping
        /// </summary>
        public static void AddPawnDialogueList(Pawn pawn)
        {
            try
            {
                if (pawn == null)
                    return;

                var pawnId = pawn?.thingIDNumber.ToString() ?? "<null>";
                var pawnName = pawn?.LabelShort ?? pawn?.Name?.ToString() ?? "<unnamed>";

                // Get RimTalk WorldComponent from current game
                var worldComp = Current.Game?.World?.GetComponent<global::RimTalk.Data.RimTalkWorldComponent>();
                if (worldComp == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] AddPawnDialogueList: RimTalkWorldComponent not found in current game");
                    return;
                }
                
                PawnState pawnState = global::RimTalk.Data.Cache.Get(pawn);
                if (pawnState == null)
                {
                    return;
                }

                // Get TalkResponses list from PawnState
                var talkResponsesList = pawnState.TalkResponses;
                if (talkResponsesList == null)
                {
                    return;
                }

                // Register this list in our map (remove first if exists, then add)
                // This ensures the mapping is always up-to-date
                _listToPawnMap.Remove(talkResponsesList);
                _listToPawnMap.Add(talkResponsesList, pawn);

            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] AddPawnDialogueList (1 param) error for pawn '{pawn?.LabelShort}': {ex.Message}");
            }
        }

        /// <summary>
        /// Register a PawnState's TalkResponses list using the PawnState instance directly.
        /// This avoids calling into RimTalk's Cache.Get which can construct PawnState and
        /// trigger recursion when called from the PawnState constructor postfix.
        /// </summary>
        public static void AddPawnDialogueListForPawnState(object pawnStateInstance, Pawn pawn)
        {
            try
            {
                if (pawn == null || pawnStateInstance == null)
                    return;

                var pawnId = pawn?.thingIDNumber.ToString() ?? "<null>";
                var pawnName = pawn?.LabelShort ?? pawn?.Name?.ToString() ?? "<unnamed>";

                // Attempt to get TalkResponses from the PawnState instance via reflection
                object talkResponsesList = null;
                try
                {
                    var type = pawnStateInstance.GetType();
                    var prop = type.GetProperty("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        talkResponsesList = prop.GetValue(pawnStateInstance);
                    }
                    else
                    {
                        var field = type.GetField("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                        if (field != null)
                            talkResponsesList = field.GetValue(pawnStateInstance);
                    }
                }
                catch (Exception ex)
                {
                    TTSLog.Warning($"[RimTalk.TTS] AddPawnDialogueListForPawnState: failed to read TalkResponses via reflection for pawn '{pawnName}': {ex.Message}");
                }

                if (talkResponsesList == null)
                {
                    TTSLog.Message($"[RimTalk.TTS] AddPawnDialogueList: exit for pawn '{pawnName}' (id={pawnId}) - talkResponsesList null");
                    return;
                }

                _listToPawnMap.Remove(talkResponsesList);
                _listToPawnMap.Add(talkResponsesList, pawn);
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] AddPawnDialogueListForPawnState error for pawn '{pawn?.LabelShort}': {ex.Message}");
            }
        }

        public static bool TTSModuleIsActive()
        {
            return TTSConfig.IsEnabled
                && TTSConfig.Settings.isOnButton;
        }

        [HarmonyPatch]
        public static class Cache_InitializePlayerPawn_Patch
        {
            static bool Prepare()
            {
                var method = typeof(global::RimTalk.Data.Cache).GetMethod("InitializePlayerPawn", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    TTSLog.Message("[RimTalk.TTS] Cache.InitializePlayerPawn not found, skipping patch");
                    return false;
                }
                return true;
            }

            static MethodBase TargetMethod()
            {
                return typeof(global::RimTalk.Data.Cache).GetMethod("InitializePlayerPawn", BindingFlags.Public | BindingFlags.Static);
            }

            static void Postfix()
            {
                UpdatePlayerPawnVoice();
            }
        }

        public static void UpdatePlayerPawnVoice()
        {
            var pawn = global::RimTalk.Data.Cache.GetPlayer();
            var settings = TTSConfig.Settings;

            Data.PawnVoiceManager.SetVoiceModel(pawn, settings.PlayerReferenceVoiceModelId);
        }
    }
}