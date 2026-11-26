using System;
using RimTalk.TTS.Data;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Draws the Voice Model button as the leftmost button of RimTalk's persona-editor action row
    /// (Save / Smart Gen / Roll Gen / Clear).
    ///
    /// The patch hooks <c>PersonaEditorWindow.DrawPersonalityTab</c> instead of the window's
    /// DoWindowContents: RimTalk calls that method only while the Personality tab is selected, so
    /// the button never shows up on the Memories tab. Registered only when
    /// <see cref="Bridge.RimTalkHooks.PersonaEditorDrawPersonalityTab"/> and the pawn field
    /// resolved; otherwise this is inert.
    /// </summary>
    internal static class PersonaEditorPatch
    {
        // RimTalk's action row geometry: 4 buttons, 90 x 28, 10 apart, centered in the tab.
        private const float RowButtonWidth = 90f;
        private const float RowButtonHeight = 28f;
        private const float RowButtonSpacing = 10f;
        private const float RowButtonCount = 4f;
        private const float RowButtonsTotalWidth =
            RowButtonWidth * RowButtonCount + RowButtonSpacing * (RowButtonCount - 1f);

        // Distance from the personality tab's content top to the action row's top. Both known
        // layouts chain the same elements but size the prompt text box differently; the offsets
        // are measured from RimTalk's own drawing code:
        //   1.4.4 tabbed : 8 + 4 + 160 + 2 + 18 +  8 + 22 + 4 + 22 + 14 = 262
        //   dev build    : 8 + 6 + 180 + 2 + 20 + 15 + 22 + 5 + 22 + 15 = 295
        private const float RowOffsetTabbed = 262f;
        private const float RowOffsetLegacy = 295f;

        private const float MinButtonWidth = 40f;

        public static void Postfix(object __instance, Rect __0)
        {
            try
            {
                if (!(TtsRuntime.Current?.IsEnabled ?? false))
                    return;
                if (__instance == null)
                    return;

                var pawnField = Bridge.RimTalkHooks.PersonaEditorPawnField;
                if (pawnField == null)
                    return;

                var pawn = pawnField.GetValue(__instance) as Pawn;
                if (pawn == null)
                    return;

                DrawVoiceModelButton(__0, pawn);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] PersonaEditorPatch.Postfix error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void DrawVoiceModelButton(Rect inRect, Pawn pawn)
        {
            float rowOffset = Bridge.RimTalkHooks.PersonaEditorLayout == Bridge.PersonaEditorLayoutKind.Tabbed
                ? RowOffsetTabbed
                : RowOffsetLegacy;

            // RimTalk's row sits below the instruction text and prompt box; the instruction height
            // depends on how the translation wraps, so measure it exactly the way RimTalk does.
            float instructHeight = MeasureInstructHeight(inRect.width);

            float rowStartX = inRect.center.x - RowButtonsTotalWidth / 2f;
            float rowY = inRect.y + instructHeight + rowOffset;

            // Stay inside the content rect even if the translation wraps differently from what
            // RimTalk's fixed layout was tuned for.
            rowY = Mathf.Clamp(rowY, inRect.y, Mathf.Max(inRect.y, inRect.yMax - RowButtonHeight));

            // Leftmost slot: immediately left of the row's first button. The tabbed 1.4.4 window is
            // narrower, so its centered row leaves less room than one full button; shrink instead of
            // drawing over the Save button or outside the window.
            float available = rowStartX - RowButtonSpacing - inRect.x;
            float width = Mathf.Min(RowButtonWidth, available);
            if (width < MinButtonWidth)
                return;

            Rect buttonRect = new Rect(rowStartX - RowButtonSpacing - width, rowY, width, RowButtonHeight);

            TooltipHandler.TipRegion(buttonRect, BuildTooltip(pawn));

            if (Widgets.ButtonText(buttonRect, "RimTalk.PersonaEditor.VoiceModel".Translate()))
            {
                // RimTalk 1.4.4's UIUtil.ButtonText plays this on click; match it so the new button
                // sounds like the row it sits in.
                SoundDefOf.Click.PlayOneShotOnCamera();
                Find.WindowStack.Add(new UI.VoiceSelectionWindow(pawn));
            }
        }

        private static float MeasureInstructHeight(float width)
        {
            // Measure with the same font/word-wrap state RimTalk used for its own CalcHeight: it
            // sets Small and never touches WordWrap inside DrawPersonalityTab.
            GameFont prevFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                return Text.CalcHeight("RimTalk.PersonaEditor.Instruct".Translate(), width);
            }
            finally
            {
                Text.Font = prevFont;
            }
        }

        private static string BuildTooltip(Pawn pawn)
        {
            string currentVoice = PawnVoiceManager.GetVoiceModel(pawn);
            string displayVoice = string.IsNullOrEmpty(currentVoice)
                ? "Default"
                : currentVoice == "NONE" ? "None" : currentVoice;

            string title = "RimTalk.TTS.VoiceModelTooltip".Translate().Colorize(ColoredText.TipSectionTitleColor);
            return title + "\n\n" + "RimTalk.TTS.CurrentVoice".Translate() + ": " + displayVoice;
        }
    }
}
