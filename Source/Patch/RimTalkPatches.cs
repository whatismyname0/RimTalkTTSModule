using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;

namespace RimTalk.TTS.Patch
{
    /// <summary>
    /// Harmony patches to hook into main RimTalk events
    /// Simplified version that only patches CreateInteraction for TTS generation and playback
    /// </summary>
    public static class RimTalkPatches
    {
        // Core RimTalk types
        private static Type _talkServiceType;
        private static Type _talkResponseType;
        private static Type _talkHistoryType;
        private static Type _pawnStateType;
        
        // TalkResponse methods
        private static MethodInfo _getIdMethod;
        private static MethodInfo _getTextMethod;
        
        // TalkHistory methods
        private static MethodInfo _isTalkIgnoredMethod;
        
        /// <summary>
        /// Check if a dialogue is marked as ignored in main RimTalk
        /// </summary>
        public static bool IsTalkIgnored(Guid dialogueId)
        {
            try
            {
                if (_isTalkIgnoredMethod != null)
                {
                    return (bool)_isTalkIgnoredMethod.Invoke(null, new object[] { dialogueId });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] IsTalkIgnored error: {ex.Message}");
            }
            return false;
        }

        // Track which dialogues are blocked (waiting for TTS generation)
        public static readonly HashSet<Guid> blockedDialogues = new HashSet<Guid>();
        private static readonly object _blockLock = new object();
        
        // Map TalkResponses lists to their owning Pawns
        private static readonly ConditionalWeakTable<object, Pawn> _listToPawnMap = new ConditionalWeakTable<object, Pawn>();

        static RimTalkPatches()
        {
            try
            {
                Log.Message("[RimTalk.TTS] RimTalkPatches initialization started");
                
                // Find main RimTalk types via reflection
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "RimTalk")
                    {
                        Log.Message($"[RimTalk.TTS] Found RimTalk assembly: {assembly.FullName}");
                        
                        _talkServiceType = assembly.GetType("RimTalk.Service.TalkService");
                        _talkResponseType = assembly.GetType("RimTalk.Data.TalkResponse");
                        _talkHistoryType = assembly.GetType("RimTalk.Data.TalkHistory");
                        _pawnStateType = assembly.GetType("RimTalk.Data.PawnState");
                        
                        if (_talkResponseType != null)
                        {
                            _getIdMethod = _talkResponseType.GetProperty("Id")?.GetGetMethod();
                            _getTextMethod = _talkResponseType.GetProperty("Text")?.GetGetMethod();
                        }
                        
                        if (_talkHistoryType != null)
                        {
                            _isTalkIgnoredMethod = _talkHistoryType.GetMethod("IsTalkIgnored", BindingFlags.Public | BindingFlags.Static);
                        }
                        
                        Log.Message($"[RimTalk.TTS] Reflection initialized - TalkService:{_talkServiceType != null}, TalkResponse:{_talkResponseType != null}, PawnState:{_pawnStateType != null}, GetId:{_getIdMethod != null}, GetText:{_getTextMethod != null}");
                        break;
                    }
                }

                if (_talkServiceType == null)
                {
                    Log.Warning("[RimTalk.TTS] RimTalk assembly not found! Ensure RimTalk is loaded before RimTalk TTS.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] RimTalkPatches initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch: TalkService.CreateInteraction - Intercept when dialogue is displayed
        /// </summary>
        [HarmonyPatch]
        public static class CreateInteraction_Patch
        {
            static bool Prepare()
            {
                if (_talkServiceType == null || _talkResponseType == null)
                {
                    Log.Warning("[RimTalk.TTS] CreateInteraction_Patch: Required types not found, skipping patch");
                    return false;
                }
                
                var method = _talkServiceType.GetMethod("CreateInteraction", 
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn), _talkResponseType },
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
                return _talkServiceType?.GetMethod("CreateInteraction", 
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn), _talkResponseType },
                    null);
            }

            // Prefix: Check if dialogue is blocked (TTS still generating) or audio is playing
            static bool Prefix(Pawn pawn, object talk)
            {
                try
                {
                    if (pawn == null || talk == null || _getIdMethod == null)
                        return true;

                    var dialogueId = (Guid)_getIdMethod.Invoke(talk, null);
                    
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
                    Service.AudioPlaybackService.PlayAudio(dialogueId);
                    return true; // Allow original method to execute
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] CreateInteraction Prefix error: {ex.Message}");
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
                var method = _talkHistoryType?.GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
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
                var method = _talkHistoryType?.GetMethod("AddIgnored", BindingFlags.Public | BindingFlags.Static);
                Log.Message($"[RimTalk.TTS] AddIgnored_Patch.TargetMethod() returning: {method?.Name ?? "NULL"}");
                return method;
            }

            static void Prefix(Guid id)
            {
                try
                {
                    TTSModule.Instance.OnDialogueCancelled(id);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] AddIgnored error: {ex.Message}");
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
                if (_pawnStateType == null)
                {
                    Log.Warning("[RimTalk.TTS] PawnStateConstructor_Patch: PawnState type not found, skipping patch");
                    return false;
                }
                
                var ctor = _pawnStateType.GetConstructor(new[] { typeof(Pawn) });
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
                return _pawnStateType?.GetConstructor(new[] { typeof(Pawn) });
            }

            // Postfix: Register the TalkResponses list with its owning Pawn
            static void Postfix(object __instance, Pawn pawn)
            {
                try
                {
                    if (__instance == null || pawn == null)
                        return;

                    var talkResponsesField = _pawnStateType.GetField("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                    if (talkResponsesField == null)
                        return;

                    var talkResponsesList = talkResponsesField.GetValue(__instance);
                    if (talkResponsesList == null)
                        return;

                    // Register this list in our map
                    _listToPawnMap.Add(talkResponsesList, pawn);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk.TTS] PawnStateConstructor_Patch error: {ex.Message}");
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
                if (_talkResponseType == null)
                {
                    Log.Warning("[RimTalk.TTS] TalkResponsesAdd_Patch: TalkResponse type not found, skipping patch");
                    return false;
                }
                
                var listType = typeof(List<>).MakeGenericType(_talkResponseType);
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
                if (_talkResponseType == null) return null;
                var listType = typeof(List<>).MakeGenericType(_talkResponseType);
                return listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            }

            // Postfix: Check if this is a registered PawnState.TalkResponses list
            static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null || _getIdMethod == null || _getTextMethod == null)
                        return;

                    // Check if this list is registered in our map
                    if (!_listToPawnMap.TryGetValue(__instance, out Pawn pawn))
                    {
                        // Not a PawnState.TalkResponses list, ignore
                        return;
                    }

                    // Get the item that was just added
                    var list = __instance as System.Collections.IList;
                    if (list == null || list.Count == 0)
                        return;

                    object item = list[list.Count - 1];
                    if (item == null)
                        return;

                    var dialogueId = (Guid)_getIdMethod.Invoke(item, null);
                    var text = (string)_getTextMethod.Invoke(item, null);
                    
                    // Immediately mark dialogue as "generating" to block display
                    RequestBlock(dialogueId);
                    
                    // Start TTS generation immediately when dialogue enters the queue
                    TTSModule.Instance.OnDialogueGenerated(text, pawn, dialogueId);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] TalkResponsesAdd_Patch error: {ex.Message}");
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
                if (_pawnStateType == null || _talkResponseType == null)
                {
                    Log.Warning("[RimTalk.TTS] IgnoreTalkResponse_Patch: Required types not found, skipping patch");
                    return false;
                }
                
                var method = _pawnStateType.GetMethod("IgnoreTalkResponse", BindingFlags.Public | BindingFlags.Instance);
                
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
                return _pawnStateType?.GetMethod("IgnoreTalkResponse", BindingFlags.Public | BindingFlags.Instance);
            }

            // Prefix: Capture the dialogue ID before it's removed
            static void Prefix(object __instance)
            {
                try
                {
                    if (__instance == null || _getIdMethod == null)
                        return;

                    // Get TalkResponses field from PawnState instance
                    var talkResponsesField = _pawnStateType.GetField("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                    if (talkResponsesField == null)
                    {
                        Log.Warning("[RimTalk.TTS] IgnoreTalkResponse_Patch: TalkResponses field not found");
                        return;
                    }

                    // Get the list of TalkResponse objects
                    var talkResponsesList = talkResponsesField.GetValue(__instance) as System.Collections.IList;
                    if (talkResponsesList == null || talkResponsesList.Count == 0)
                        return;

                    // Get the first TalkResponse (the one about to be ignored)
                    var talkResponse = talkResponsesList[0];
                    if (talkResponse == null)
                        return;

                    var dialogueId = (Guid)_getIdMethod.Invoke(talkResponse, null);
                    // Cancel TTS generation or discard generated audio
                    TTSModule.Instance.OnDialogueCancelled(dialogueId);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] IgnoreTalkResponse_Patch error: {ex.Message}\n{ex.StackTrace}");
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
                var type = _talkServiceType?.Assembly.GetType("RimTalk.RimTalk");
                if (type == null)
                {
                    Log.Warning("[RimTalk.TTS] StartedNewGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = type.GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
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
                var method = _talkServiceType?.Assembly.GetType("RimTalk.RimTalk")
                    ?.GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
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
                var type = _talkServiceType?.Assembly.GetType("RimTalk.RimTalk");
                if (type == null)
                {
                    Log.Warning("[RimTalk.TTS] LoadedGame_Patch: RimTalk.RimTalk type not found, skipping patch");
                    return false;
                }
                var method = type.GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
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
                var method = _talkServiceType?.Assembly.GetType("RimTalk.RimTalk")
                    ?.GetMethod("LoadedGame", BindingFlags.Public | BindingFlags.Instance);
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
                try
                {
                    if (__instance != null)
                    {
                        Data.PawnVoiceManager.RemovePawn(__instance);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] PawnDiscard_Patch error: {ex.Message}");
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
    }
}
