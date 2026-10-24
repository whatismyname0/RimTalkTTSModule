using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.UI;
using RimTalk.Util;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Bridge
{
    /// <summary>Which display/consume step this RimTalk build exposes.</summary>
    internal enum DisplayHookKind
    {
        None,
        MarkTalkSpoken,
        CreateInteraction
    }

    /// <summary>How much a missing hook hurts. Controls the report severity.</summary>
    internal enum HookSeverity
    {
        /// <summary>Without it TTS cannot work at all.</summary>
        Fatal,
        /// <summary>Feature is degraded (warning, still usable).</summary>
        Degraded,
        /// <summary>Optional convenience only.</summary>
        Info
    }

    internal sealed class HookIssue
    {
        public readonly string Hook;
        public readonly HookSeverity Severity;
        public readonly string Effect;

        public HookIssue(string hook, HookSeverity severity, string effect)
        {
            Hook = hook;
            Severity = severity;
            Effect = effect;
        }

        public override string ToString()
        {
            return Effect == null ? Hook : Hook + " (" + Effect + ")";
        }
    }

    /// <summary>
    /// The single place that talks to RimTalk by reflection.
    ///
    /// Every member is resolved once at startup against the RimTalk assembly, then the capability
    /// table below is consulted by <see cref="Bootstrap.PatchRegistry"/>: a patch is registered only
    /// when its target exists, so a renamed/removed member can never abort the whole patch batch.
    ///
    /// Nothing outside this file may contain a RimTalk member name string. Business logic (Pipeline,
    /// UI, patches) receives typed callbacks or reads the resolved <see cref="MethodInfo"/>.
    /// </summary>
    internal static class RimTalkHooks
    {
        private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstancePublic = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags InstanceNonPublic = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly List<HookIssue> _issues = new List<HookIssue>();
        private static bool _resolved;
        private static string _description = "RimTalk hooks not resolved";

        // --- display / generation / cancel -------------------------------------------------
        public static DisplayHookKind DisplayHook { get; private set; }
        public static MethodInfo DisplayTarget { get; private set; }
        public static MethodInfo DrainIncomingTalkResponses { get; private set; }
        public static MethodInfo QueueIncomingResponse { get; private set; }
        public static MethodInfo AddIgnored { get; private set; }
        public static MethodInfo IsTalkIgnored { get; private set; }
        public static MethodInfo InitializePlayerPawn { get; private set; }

        // --- lifecycle (RimTalk mod class) --------------------------------------------------
        public static MethodInfo StartedNewGame { get; private set; }
        public static MethodInfo LoadedGame { get; private set; }

        // --- overlay control buttons (invasive: also forces the display pacing) ------------
        public static MethodInfo OverlayMapComponentOnGui { get; private set; }
        public static MethodInfo OverlayHandleInput { get; private set; }
        public static FieldInfo OverlayGearIconRect { get; private set; }
        public static MethodInfo CommonUtilHasPassed { get; private set; }
        public static MethodInfo DisplayTalk { get; private set; }
        public static bool OverlayButtonsAvailable { get; private set; }

        // --- optional RimTalk UI windows ----------------------------------------------------
        public static MethodInfo CharacterCardDoTopStack { get; private set; }
        public static MethodInfo PersonaEditorDoWindowContents { get; private set; }
        public static FieldInfo PersonaEditorPawnField { get; private set; }
        /// <summary>1.4.4+ tab state; null on builds without persona-editor tabs (dev).</summary>
        public static PropertyInfo PersonaEditorIsPersonalityTab { get; private set; }

        public static IReadOnlyList<HookIssue> Issues => _issues;

        public static string DisplayHookDescription
        {
            get
            {
                switch (DisplayHook)
                {
                    case DisplayHookKind.MarkTalkSpoken:
                        return "TalkService.MarkTalkSpoken(PawnState) [dev API]";
                    case DisplayHookKind.CreateInteraction:
                        return "TalkService.CreateInteraction(Pawn, TalkResponse) [1.4.4 API]";
                    default:
                        return "NONE - dialogue will display but cannot be spoken";
                }
            }
        }

        public static string Description => _description;

        /// <summary>Resolve every hook once. Never throws; failures become <see cref="Issues"/>.</summary>
        public static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _issues.Clear();

            ResolveDisplayHook();
            ResolveCore();
            ResolveOverlayButtons();
            ResolveOptionalUi();

            _description = "RimTalk hooks resolved for " + RimTalkVersion() +
                           " (display=" + DisplayHook + ")";
        }

        private static void ResolveDisplayHook()
        {
            MethodInfo markTalkSpoken = FindMethod(typeof(TalkService), "MarkTalkSpoken", StaticNonPublic, typeof(PawnState));
            MethodInfo createInteraction = FindMethod(typeof(TalkService), "CreateInteraction", StaticNonPublic,
                typeof(Pawn), typeof(TalkResponse));

            if (Miss("display"))
            {
                markTalkSpoken = null;
                createInteraction = null;
            }

            if (markTalkSpoken != null)
            {
                DisplayHook = DisplayHookKind.MarkTalkSpoken;
                DisplayTarget = markTalkSpoken;
            }
            else if (createInteraction != null)
            {
                DisplayHook = DisplayHookKind.CreateInteraction;
                DisplayTarget = createInteraction;
            }
            else
            {
                DisplayHook = DisplayHookKind.None;
                AddIssue("TalkService.MarkTalkSpoken / CreateInteraction", HookSeverity.Fatal,
                    "no display hook");
            }
        }

        private static void ResolveCore()
        {
            DrainIncomingTalkResponses = Miss("drain")
                ? null
                : FindMethod(typeof(PawnState), "DrainIncomingTalkResponses", InstancePublic);
            if (DrainIncomingTalkResponses == null)
            {
                AddIssue("PawnState.DrainIncomingTalkResponses", HookSeverity.Degraded,
                    "generation starts at display time instead of enqueue");
            }

            QueueIncomingResponse = Miss("queueincoming")
                ? null
                : FindMethod(typeof(PawnState), "QueueIncomingResponse", InstancePublic, typeof(TalkResponse));
            if (QueueIncomingResponse == null)
            {
                AddIssue("PawnState.QueueIncomingResponse", HookSeverity.Info,
                    "TTS starts at display time while paused");
            }

            AddIgnored = Miss("addignored")
                ? null
                : FindMethod(typeof(TalkHistory), "AddIgnored", StaticPublic, typeof(Guid));
            if (AddIgnored == null)
            {
                AddIssue("TalkHistory.AddIgnored", HookSeverity.Degraded,
                    "cancelled lines are not cleaned up immediately");
            }

            IsTalkIgnored = FindMethod(typeof(TalkHistory), "IsTalkIgnored", StaticPublic, typeof(Guid));

            InitializePlayerPawn = Miss("initplayerpawn")
                ? null
                : FindMethod(typeof(global::RimTalk.Data.Cache), "InitializePlayerPawn", StaticPublic);
            if (InitializePlayerPawn == null)
            {
                AddIssue("Cache.InitializePlayerPawn", HookSeverity.Info,
                    "player voice is not refreshed after cache init");
            }

            StartedNewGame = FindMethod(typeof(global::RimTalk.RimTalk), "StartedNewGame", InstancePublic);
            LoadedGame = FindMethod(typeof(global::RimTalk.RimTalk), "LoadedGame", InstancePublic);
            if (StartedNewGame == null || LoadedGame == null)
            {
                AddIssue("RimTalk.StartedNewGame / LoadedGame", HookSeverity.Degraded,
                    "stale block state may survive a game load");
            }
        }

        private static void ResolveOverlayButtons()
        {
            OverlayMapComponentOnGui = Miss("overlay")
                ? null
                : FindMethod(typeof(Overlay), "MapComponentOnGUI", InstancePublic);
            OverlayHandleInput = Miss("overlay")
                ? null
                : FindMethod(typeof(Overlay), "HandleInput", InstanceNonPublic, typeof(Rect).MakeByRefType());
            OverlayGearIconRect = Miss("overlay")
                ? null
                : FindField(typeof(Overlay), "_gearIconScreenRect", InstanceNonPublic);
            CommonUtilHasPassed = Miss("haspassed")
                ? null
                : FindMethod(typeof(CommonUtil), "HasPassed", StaticPublic, typeof(int), typeof(double));
            DisplayTalk = FindMethod(typeof(TalkService), "DisplayTalk", StaticPublic);

            var missing = new List<string>();
            if (OverlayMapComponentOnGui == null) missing.Add("Overlay.MapComponentOnGUI");
            if (OverlayHandleInput == null) missing.Add("Overlay.HandleInput");
            if (OverlayGearIconRect == null) missing.Add("Overlay._gearIconScreenRect");
            if (CommonUtilHasPassed == null) missing.Add("CommonUtil.HasPassed");
            if (DisplayTalk == null) missing.Add("TalkService.DisplayTalk");

            OverlayButtonsAvailable = missing.Count == 0;
            if (!OverlayButtonsAvailable)
            {
                AddIssue("overlay control buttons", HookSeverity.Degraded,
                    "buttons hidden; missing " + string.Join(", ", missing.ToArray()));
            }
        }

        private static void ResolveOptionalUi()
        {
            // RimWorld's DoTopStack is private static in 1.7 (it was patchable through Harmony's
            // attribute lookup, which searches non-public members too). Resolve it explicitly as
            // non-public, otherwise the bio-tab voice button silently disappears.
            CharacterCardDoTopStack = FindMethodAny(typeof(global::RimWorld.CharacterCardUtility), "DoTopStack",
                StaticPublic | BindingFlags.NonPublic);
            if (CharacterCardDoTopStack == null)
                AddIssue("CharacterCardUtility.DoTopStack", HookSeverity.Info, "bio-tab voice button disabled");

            Type personaEditor = AccessTools_TypeByName("RimTalk.UI.PersonaEditorWindow");
            if (personaEditor != null)
            {
                PersonaEditorDoWindowContents = FindMethodAny(personaEditor, "DoWindowContents", InstancePublic);
                PersonaEditorPawnField = FindField(personaEditor, "pawn", InstanceNonPublic | InstancePublic)
                                         ?? FindField(personaEditor, "_pawn", InstanceNonPublic | InstancePublic);
                // 1.4.4 added Personality/Memories tabs. Dev builds have no tabs, so a missing
                // property means "always visible" (the pre-tabs behaviour).
                PersonaEditorIsPersonalityTab = FindProperty(personaEditor, "IsPersonalityTabSelected", InstancePublic);
            }

            if (PersonaEditorDoWindowContents == null || PersonaEditorPawnField == null)
                AddIssue("PersonaEditorWindow.DoWindowContents/pawn", HookSeverity.Info, "persona editor voice button disabled");
            else if (PersonaEditorIsPersonalityTab == null)
                AddIssue("PersonaEditorWindow.IsPersonalityTabSelected", HookSeverity.Info,
                    "voice button is shown on every persona editor tab");
        }

        /// <summary>One aggregated report: fatal/degraded as warnings, the rest as verbose info.</summary>
        public static void LogReport()
        {
            TTSLog.Message("[RimTalk.TTS] " + _description +
                           $" (display={DisplayHook}, overlayButtons={(OverlayButtonsAvailable ? "yes" : "no")})");

            var fatal = _issues.Where(i => i.Severity == HookSeverity.Fatal).ToList();
            if (fatal.Count > 0)
                TTSLog.Warning("[RimTalk.TTS] Required RimTalk hooks missing: " +
                               string.Join("; ", fatal.Select(i => i.ToString()).ToArray()) +
                               " - TTS cannot play audio.");

            var degraded = _issues.Where(i => i.Severity == HookSeverity.Degraded).ToList();
            if (degraded.Count > 0)
                TTSLog.Warning("[RimTalk.TTS] RimTalk hooks degraded: " +
                               string.Join("; ", degraded.Select(i => i.ToString()).ToArray()));

            var info = _issues.Where(i => i.Severity == HookSeverity.Info).ToList();
            if (info.Count > 0)
                TTSLog.Message("[RimTalk.TTS] Optional hooks unavailable: " +
                               string.Join("; ", info.Select(i => i.ToString()).ToArray()));
        }

        private static void AddIssue(string hook, HookSeverity severity, string effect)
        {
            _issues.Add(new HookIssue(hook, severity, effect));
        }

        // ---------------------------------------------------------------------------------
        // resolution helpers (all swallow exceptions: a hook that cannot be inspected is absent)
        // ---------------------------------------------------------------------------------

        private static string RimTalkVersion()
        {
            try
            {
                var asm = typeof(TalkService).Assembly;
                return asm.GetName().Name + " " + asm.GetName().Version;
            }
            catch
            {
                return "RimTalk (version unknown)";
            }
        }

        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
        {
            if (type == null) return null;
            try
            {
                return parameters == null || parameters.Length == 0
                    ? type.GetMethod(name, flags)
                    : type.GetMethod(name, flags, null, parameters, null);
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Hook lookup failed: {type.Name}.{name} - {ex.Message}");
                return null;
            }
        }

        private static PropertyInfo FindProperty(Type type, string name, BindingFlags flags)
        {
            if (type == null) return null;
            try
            {
                return type.GetProperty(name, flags);
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Hook lookup failed: {type.Name}.{name} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find by name when the parameter list is irrelevant (or intentionally variable).
        /// Declared methods win over inherited virtuals so an override is never confused with its
        /// base declaration (e.g. Window.DoWindowContents).
        /// </summary>
        private static MethodInfo FindMethodAny(Type type, string name, BindingFlags flags)
        {
            if (type == null) return null;
            try
            {
                return type.GetMethods(flags | BindingFlags.DeclaredOnly).FirstOrDefault(m => m.Name == name)
                       ?? type.GetMethods(flags).FirstOrDefault(m => m.Name == name);
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Hook lookup failed: {type.Name}.{name} - {ex.Message}");
                return null;
            }
        }

        private static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            if (type == null) return null;
            try
            {
                return type.GetField(name, flags);
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Hook lookup failed: {type.Name}.{name} - {ex.Message}");
                return null;
            }
        }

        private static Type AccessTools_TypeByName(string fullName)
        {
            try
            {
                return AccessTools.TypeByName(fullName);
            }
            catch (Exception ex)
            {
                TTSLog.Message($"[RimTalk.TTS] Optional type {fullName} not found: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Test-only switch: <c>RIMTALK_TTS_DEBUG_MISS=drain,overlay</c> pretends those hooks are
        /// absent, so the degradation paths can be exercised without a mismatched RimTalk build.
        /// Unset by default and harmless in release.
        /// </summary>
        private static bool Miss(string key)
        {
            try
            {
                string raw = Environment.GetEnvironmentVariable("RIMTALK_TTS_DEBUG_MISS");
                if (string.IsNullOrWhiteSpace(raw)) return false;
                return raw.Split(',')
                          .Select(s => s.Trim())
                          .Any(s => string.Equals(s, key, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}
