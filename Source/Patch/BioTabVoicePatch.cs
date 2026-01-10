using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimTalk.TTS.Data;
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
        private static readonly Texture2D VoiceIcon = ContentFinder<Texture2D>.Get("UI/VoiceSettings");

        static BioTabVoicePatch()
        {
            // No runtime reflection needed; Hediff_Persona is available via assembly reference
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

                    string currentVoice = PawnVoiceManager.GetVoiceModel(pawn);
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
            
            // Use RimTalk's own eligibility check by verifying if the pawn has a PawnState
            // This ensures we only show voice UI for pawns that RimTalk considers eligible for talking
            try
            {
                var pawnState = global::RimTalk.Data.Cache.Get(pawn);
                return pawnState != null;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk.TTS] Failed to check pawn eligibility for {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
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
