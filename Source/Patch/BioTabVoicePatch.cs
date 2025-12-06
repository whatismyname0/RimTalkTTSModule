using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Patch
{
    /// <summary>
    /// Adds Voice Model selection button to character bio tab
    /// This creates a separate button next to the RimTalk Persona button
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BioTabVoicePatch
    {
        private static readonly Texture2D VoiceIcon = ContentFinder<Texture2D>.Get("UI/VoiceSettings", reportFailure: false) 
            ?? Texture2D.whiteTexture;

        private static Type _hediffPersonaType;
        private static MethodInfo _getOrAddNewMethod;
        private static FieldInfo _voiceModelIdField;

        static BioTabVoicePatch()
        {
            try
            {
                // Find RimTalk types via reflection
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "RimTalk")
                    {
                        _hediffPersonaType = assembly.GetType("RimTalk.Data.Hediff_Persona");
                        if (_hediffPersonaType != null)
                        {
                            _getOrAddNewMethod = _hediffPersonaType.GetMethod("GetOrAddNew", BindingFlags.Public | BindingFlags.Static);
                            _voiceModelIdField = _hediffPersonaType.GetField("VoiceModelId", BindingFlags.Public | BindingFlags.Instance);
                        }
                        break;
                    }
                }

                if (_hediffPersonaType == null)
                {
                    Log.Warning("[RimTalk.TTS] Could not find RimTalk.Data.Hediff_Persona for voice UI");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] BioTabVoicePatch initialization failed: {ex.Message}");
            }
        }

        private static void AddVoiceElement(Pawn pawn)
        {
            // Only show for colonists, prisoners, or pawns with vocal link
            if (!ShouldShowVoiceUI(pawn))
                return;

            var tmpStackElements =
                (List<GenUI.AnonymousStackElement>)AccessTools.Field(typeof(CharacterCardUtility), "tmpStackElements")
                    .GetValue(null);
            if (tmpStackElements == null) return;

            string voiceLabelText = "RimTalk.TTS.VoiceModel".Translate();
            float textWidth = Text.CalcSize(voiceLabelText).x;
            float totalLabelWidth = 22f + 5f + textWidth + 5f; // Icon + padding + text + padding

            tmpStackElements.Add(new GenUI.AnonymousStackElement
            {
                width = totalLabelWidth,
                drawer = rect =>
                {
                    Widgets.DrawOptionBackground(rect, false);
                    Widgets.DrawHighlightIfMouseover(rect);

                    string currentVoice = GetVoiceModelForPawn(pawn);
                    string displayVoice = string.IsNullOrEmpty(currentVoice) ? "Default" : 
                                         currentVoice == "NONE" ? "None" : currentVoice;
                    
                    string tooltipText = $"{"RimTalk.TTS.VoiceModelTooltip".Translate().Colorize(ColoredText.TipSectionTitleColor)}\n\n" +
                                       $"{"RimTalk.TTS.CurrentVoice".Translate()}: {displayVoice}";
                    TooltipHandler.TipRegion(rect, tooltipText);

                    Rect iconRect = new Rect(rect.x + 2f, rect.y + 1f, 20f, 20f);
                    GUI.DrawTexture(iconRect, VoiceIcon);

                    Rect labelRect = new Rect(iconRect.xMax + 5f, rect.y, textWidth, rect.height);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(labelRect, voiceLabelText);
                    Text.Anchor = TextAnchor.UpperLeft;

                    if (Widgets.ButtonInvisible(rect))
                    {
                        Find.WindowStack.Add(new UI.VoiceSelectionWindow(pawn));
                    }
                }
            });
        }

        private static bool ShouldShowVoiceUI(Pawn pawn)
        {
            if (pawn == null) return false;
            
            // Check if pawn is colonist, prisoner, or has vocal link
            if (pawn.IsColonist || pawn.IsPrisonerOfColony)
                return true;

            // Check for vocal link via reflection
            try
            {
                if (_hediffPersonaType != null)
                {
                    var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_VocalLink");
                    if (hediffDef != null && pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) != null)
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static string GetVoiceModelForPawn(Pawn pawn)
        {
            try
            {
                if (_getOrAddNewMethod == null || _voiceModelIdField == null)
                    return "";

                var hediff = _getOrAddNewMethod.Invoke(null, new object[] { pawn });
                if (hediff != null)
                {
                    return (string)_voiceModelIdField.GetValue(hediff) ?? "";
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Failed to get voice model: {ex.Message}");
            }
            return "";
        }

        /// <summary>
        /// Transpiler to inject voice element after persona element
        /// Hooks into the same location as RimTalk's persona patch
        /// </summary>
        [HarmonyPatch(typeof(CharacterCardUtility), "DoTopStack")]
        public static class DoTopStack_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo anchorMethod = AccessTools.Method(
                    typeof(QuestUtility),
                    nameof(QuestUtility.AppendInspectStringsFromQuestParts),
                    new Type[]
                    {
                        typeof(Action<string, Quest>),
                        typeof(ISelectable),
                        typeof(int).MakeByRefType()
                    }
                );

                foreach (var instruction in instructions)
                {
                    yield return instruction;

                    if (instruction.Calls(anchorMethod))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'pawn'
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BioTabVoicePatch), nameof(AddVoiceElement)));
                    }
                }
            }
        }
    }
}
