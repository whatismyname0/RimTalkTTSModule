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
using RimTalk.TTS.Util;
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
                ErrorUtil.LogException("IsTalkIgnored", ex);
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
                    Log.Warning("[RimTalk.TTS] CreateInteraction_Patch: Method not found, skipping patch");
                    return false;
                }

                Log.Message("[RimTalk.TTS] Successfully found CreateInteraction method");
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

                    // Play audio (will wait for TTS generation and previous playback)
                    Service.AudioPlaybackService.PlayAudio(dialogueId, pawn);
                    return true; // Allow original method to execute
                }
                catch (Exception ex)
                {
                    ErrorUtil.LogException("CreateInteraction Prefix", ex);
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
                Log.Message("[RimTalk.TTS] AddIgnored_Patch.Prepare() called");
                var method = typeof(global::RimTalk.Data.TalkHistory).GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Log.Warning("[RimTalk.TTS] AddIgnored_Patch: Method not found, skipping patch");
                    return false;
                }
                Log.Message("[RimTalk.TTS] AddIgnored_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                Log.Message("[RimTalk.TTS] AddIgnored_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.Data.TalkHistory).GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
                Log.Message($"[RimTalk.TTS] AddIgnored_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Prefix(Guid id)
            {
                if (!TTSModuleIsActive())
                    return;
                try
                {
                    TTSModule.Instance.OnDialogueCancelled(id);
                }
                catch (Exception ex)
                {
                    ErrorUtil.LogException("AddIgnored_Patch Prefix", ex);
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
                    Log.Warning("[RimTalk.TTS] PawnStateConstructor_Patch: Constructor not found, skipping patch");
                    return false;
                }

                Log.Message("[RimTalk.TTS] Successfully found PawnState constructor");
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
                        ErrorUtil.LogException("PawnStateConstructor_Patch Postfix", ex);
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
                    Log.Warning("[RimTalk.TTS] TalkResponsesAdd_Patch: List<TalkResponse>.Add method not found, skipping patch");
                    return false;
                }

                Log.Message("[RimTalk.TTS] Successfully found List<TalkResponse>.Add method");
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

                    if (PawnVoiceManager.GetVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID)
                        return;

                    // Immediately mark dialogue as "generating" to block display
                    RequestBlock(dialogueId);

                    // Start TTS generation immediately when dialogue enters the queue
                    TTSModule.Instance.OnDialogueGenerated(text, pawn, dialogueId);
                }
                catch (Exception ex)
                {
                    ErrorUtil.LogException("TalkResponsesAdd_Patch", ex);
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
                    Log.Warning("[RimTalk.TTS] IgnoreTalkResponse_Patch: Method not found, skipping patch");
                    return false;
                }
                
                Log.Message("[RimTalk.TTS] Successfully found IgnoreTalkResponse method");
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
                    TTSModule.Instance.OnDialogueCancelled(dialogueId);
                }
                catch (Exception ex)
                {
                    ErrorUtil.LogException("IgnoreTalkResponse_Patch", ex);
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
                Log.Message("[RimTalk.TTS] StartedNewGame_Patch.Prepare() called");
                var type = typeof(global::RimTalk.RimTalk);
                if (type == null)
                {
                    Log.Warning("[RimTalk.TTS] StartedNewGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = typeof(global::RimTalk.RimTalk).GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    Log.Warning("[RimTalk.TTS] StartedNewGame_Patch: Method not found, skipping patch");
                    return false;
                }
                Log.Message("[RimTalk.TTS] StartedNewGame_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                Log.Message("[RimTalk.TTS] StartedNewGame_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.RimTalk).GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
                Log.Message($"[RimTalk.TTS] StartedNewGame_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Postfix()
            {
                TTSModule.Instance.OnGameLoaded();
                Log.Message("[RimTalk.TTS] New game started, TTS state cleared");
            }
        }

        [HarmonyPatch]
        public static class LoadedGame_Patch
        {
            static bool Prepare()
            {
                Log.Message("[RimTalk.TTS] LoadedGame_Patch.Prepare() called");
                var type = typeof(global::RimTalk.RimTalk);
                if (type == null)
                {
                    Log.Warning("[RimTalk.TTS] LoadedGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = typeof(global::RimTalk.RimTalk).GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    Log.Warning("[RimTalk.TTS] LoadedGame_Patch: Method not found, skipping patch");
                    return false;
                }
                Log.Message("[RimTalk.TTS] LoadedGame_Patch: Method found, patch will be applied");
                return true;
            }

            static MethodBase TargetMethod()
            {
                Log.Message("[RimTalk.TTS] LoadedGame_Patch.TargetMethod() called");
                var method = typeof(global::RimTalk.RimTalk).GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
                Log.Message($"[RimTalk.TTS] LoadedGame_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Postfix()
            {
                TTSModule.Instance.OnGameLoaded();
                Log.Message("[RimTalk.TTS] Game loaded, TTS state cleared");
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
                    ErrorUtil.LogException("PawnDiscard_Patch", ex);
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
                if (!TTSModule.Instance.IsActive)
                    return;

                if (worldView || row is null)
                    return;

                var settings = TTSModule.Instance.GetSettings();

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
                if (!TTSModule.Instance.IsActive)
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
                    ErrorUtil.LogException("PendingToggleExecutor", ex);
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
                    Log.Warning("[RimTalk.TTS] AddPawnDialogueList: RimTalkWorldComponent not found in current game");
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
                Log.Warning($"[RimTalk.TTS] AddPawnDialogueList (1 param) error for pawn '{pawn?.LabelShort}': {ex.Message}");
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
                    Log.Warning($"[RimTalk.TTS] AddPawnDialogueListForPawnState: failed to read TalkResponses via reflection for pawn '{pawnName}': {ex.Message}");
                }

                if (talkResponsesList == null)
                {
                    Log.Message($"[RimTalk.TTS] AddPawnDialogueList: exit for pawn '{pawnName}' (id={pawnId}) - talkResponsesList null");
                    return;
                }

                _listToPawnMap.Remove(talkResponsesList);
                _listToPawnMap.Add(talkResponsesList, pawn);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk.TTS] AddPawnDialogueListForPawnState error for pawn '{pawn?.LabelShort}': {ex.Message}");
            }
        }

        public static bool TTSModuleIsActive()
        {
            return TTSModule.Instance.IsActive
                && TTSModule.Instance.Settings.isOnButton;
        }

        [HarmonyPatch]
        public static class Cache_InitializePlayerPawn_Patch
        {
            static bool Prepare()
            {
                var method = typeof(global::RimTalk.Data.Cache).GetMethod("InitializePlayerPawn", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Log.Message("[RimTalk.TTS] Cache.InitializePlayerPawn not found, skipping patch");
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
            var settings = TTSModule.Instance.GetSettings();

            Data.PawnVoiceManager.SetVoiceModel(pawn, settings.PlayerReferenceVoiceModelId);
        }
    }
}