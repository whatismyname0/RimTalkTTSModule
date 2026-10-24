using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk.TTS.Bridge;
using RimTalk.TTS.Patches;
using RimWorld;
using Verse;

namespace RimTalk.TTS.Bootstrap
{
    /// <summary>
    /// Explicit patch registration.
    ///
    /// Unlike attribute-based PatchAll (which aborts the whole batch when one patch class fails to
    /// resolve its target), every patch is registered here by name, with its target taken from
    /// <see cref="RimTalkHooks"/>, inside its own try/catch. A missing or broken optional hook can
    /// therefore only disable its own feature.
    /// </summary>
    internal static class PatchRegistry
    {
        private static readonly List<string> _applied = new List<string>();
        private static readonly List<string> _skipped = new List<string>();
        private static readonly List<string> _failed = new List<string>();

        public static void ApplyAll(Harmony harmony)
        {
            // ---- core: display / generation / cancel -------------------------------------
            RegisterDisplayHook(harmony);

            Patch(harmony, "generation/drain",
                RimTalkHooks.DrainIncomingTalkResponses,
                postfix: Method(typeof(DrainResponsesPatch), nameof(DrainResponsesPatch.Postfix)));

            // Starts generation on the pause screen: TickManager.DoSingleTick never runs while paused,
            // so talk would stay queued until unpause without this independent main-thread drain pump.
            if (RimTalkHooks.QueueIncomingResponse != null && RimTalkHooks.DrainIncomingTalkResponses != null)
            {
                Patch(harmony, "generation/pause-queue", RimTalkHooks.QueueIncomingResponse,
                    postfix: Method(typeof(PauseDrainPatch), nameof(PauseDrainPatch.QueueIncomingPostfix)));
                Patch(harmony, "generation/pause-pump",
                    AccessTools.Method(typeof(TickManager), "TickManagerUpdate"),
                    prefix: Method(typeof(PauseDrainPatch), nameof(PauseDrainPatch.TickManagerUpdatePrefix)));
            }
            else
            {
                _skipped.Add("generation/pause-drain (hook missing)");
            }

            Patch(harmony, "cancel/sync",
                RimTalkHooks.AddIgnored,
                prefix: Method(typeof(IgnoredPatch), nameof(IgnoredPatch.Prefix)));

            Patch(harmony, "voice/player-pawn",
                RimTalkHooks.InitializePlayerPawn,
                postfix: Method(typeof(CacheInitializePatch), nameof(CacheInitializePatch.Postfix)));

            // ---- lifecycle ------------------------------------------------------------------
            Patch(harmony, "lifecycle/new-game", RimTalkHooks.StartedNewGame,
                postfix: Method(typeof(GameLifecyclePatches), nameof(GameLifecyclePatches.GameResetPostfix)));
            Patch(harmony, "lifecycle/load", RimTalkHooks.LoadedGame,
                postfix: Method(typeof(GameLifecyclePatches), nameof(GameLifecyclePatches.GameResetPostfix)));
            Patch(harmony, "lifecycle/pawn-discard", AccessTools.Method(typeof(Pawn), "Discard"),
                prefix: Method(typeof(GameLifecyclePatches), nameof(GameLifecyclePatches.PawnDiscardPrefix)));

            // ---- UI: toggle button + per-tick work -----------------------------------------
            Patch(harmony, "ui/toggle",
                AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls)),
                postfix: Method(typeof(ToggleButtonPatch), nameof(ToggleButtonPatch.Postfix)));
            Patch(harmony, "tick",
                AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)),
                postfix: Method(typeof(TickPatch), nameof(TickPatch.Postfix)));

            // ---- Overlay control buttons (core feature, invasive by design) ------------------
            if (RimTalkHooks.OverlayButtonsAvailable)
            {
                Patch(harmony, "overlay/gui", RimTalkHooks.OverlayMapComponentOnGui,
                    postfix: Method(typeof(OverlayButtonPatch), nameof(OverlayButtonPatch.MapComponentOnGuiPostfix)));
                Patch(harmony, "overlay/input", RimTalkHooks.OverlayHandleInput,
                    prefix: Method(typeof(OverlayButtonPatch), nameof(OverlayButtonPatch.HandleInputPrefix)));
                Patch(harmony, "overlay/pacing", RimTalkHooks.CommonUtilHasPassed,
                    prefix: Method(typeof(OverlayPacingPatch), nameof(OverlayPacingPatch.HasPassedPrefix)));
            }

            // ---- optional RimTalk UI windows -------------------------------------------------
            Patch(harmony, "ui/character-card", RimTalkHooks.CharacterCardDoTopStack,
                transpiler: Method(typeof(BioTabVoicePatch), nameof(BioTabVoicePatch.Transpiler)));
            Patch(harmony, "ui/persona-editor", RimTalkHooks.PersonaEditorDoWindowContents,
                postfix: Method(typeof(PersonaEditorPatch), nameof(PersonaEditorPatch.Postfix)));
        }

        private static void RegisterDisplayHook(Harmony harmony)
        {
            if (RimTalkHooks.DisplayHook == DisplayHookKind.MarkTalkSpoken)
            {
                Patch(harmony, "display/MarkTalkSpoken", RimTalkHooks.DisplayTarget,
                    prefix: Method(typeof(DisplayHooksPatch), nameof(DisplayHooksPatch.MarkTalkSpokenPrefix)));
            }
            else if (RimTalkHooks.DisplayHook == DisplayHookKind.CreateInteraction)
            {
                Patch(harmony, "display/CreateInteraction", RimTalkHooks.DisplayTarget,
                    prefix: Method(typeof(DisplayHooksPatch), nameof(DisplayHooksPatch.CreateInteractionPrefix)));
            }
            else
            {
                _skipped.Add("display (no display hook)");
            }
        }

        private static HarmonyMethod Method(Type patchClass, string methodName)
        {
            return new HarmonyMethod(patchClass, methodName);
        }

        private static void Patch(Harmony harmony, string name, MethodBase target,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
        {
            if (target == null)
            {
                _skipped.Add(name + " (target missing)");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: prefix, postfix: postfix, transpiler: transpiler);
                _applied.Add(name);
            }
            catch (Exception ex)
            {
                // Isolate: one bad patch must not stop the rest of the batch.
                _failed.Add(name + " (" + ex.GetType().Name + ": " + ex.Message + ")");
                TTSLog.Error($"[RimTalk.TTS] Patch failed: {name} - {ex}");
            }
        }

        /// <summary>One summary line plus verbose detail; never throws.</summary>
        public static void LogReport()
        {
            string summary = $"patches applied {_applied.Count}, skipped {_skipped.Count}, failed {_failed.Count}";

            if (_failed.Count > 0)
                TTSLog.Warning($"[RimTalk.TTS] {summary}; failed: {string.Join("; ", _failed.ToArray())}");
            else
                TTSLog.Message($"[RimTalk.TTS] {summary}");

            if (_applied.Count > 0)
                TTSLog.Message("[RimTalk.TTS] Patches applied: " + string.Join(", ", _applied.ToArray()));
            if (_skipped.Count > 0)
                TTSLog.Message("[RimTalk.TTS] Patches skipped: " + string.Join(", ", _skipped.ToArray()));
        }
    }
}
