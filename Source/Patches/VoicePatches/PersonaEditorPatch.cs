using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Adds a Voice Model button to RimTalk's Persona Editor window. Registered only when
    /// <see cref="Bridge.RimTalkHooks.PersonaEditorDoWindowContents"/> and the pawn field resolved;
    /// otherwise this is inert.
    /// </summary>
    internal static class PersonaEditorPatch
    {
        public static void Postfix(object __instance, Rect inRect)
        {
            try
            {
                if (!(TtsRuntime.Current?.IsEnabled ?? false))
                    return;
                if (__instance == null)
                    return;

                // 1.4.4 persona editor has Personality/Memories tabs; the voice button belongs to
                // the Personality tab only. Builds without tabs (dev) have no property and keep the
                // previous always-visible behaviour.
                var personalityTab = Bridge.RimTalkHooks.PersonaEditorIsPersonalityTab;
                if (personalityTab != null)
                {
                    object selected = personalityTab.GetValue(__instance, null);
                    if (selected is bool onPersonalityTab && !onPersonalityTab)
                        return;
                }

                // Get the pawn from the PersonaEditorWindow instance (field name differs between
                // RimTalk builds; both were resolved by RimTalkHooks).
                var pawnField = Bridge.RimTalkHooks.PersonaEditorPawnField;
                if (pawnField == null)
                    return;

                var pawn = pawnField.GetValue(__instance) as Pawn;

                DrawVoiceModelButton(inRect, pawn);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] PersonaEditorPatch.Postfix error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void DrawVoiceModelButton(Rect inRect, Pawn pawn)
        {
            // Position the button at the bottom of the window, above OK/Cancel buttons.
            float buttonWidth = 120f;
            float buttonHeight = 20f;
            // Larger margin = further up from the window bottom (raised 15px to clear the buttons).
            float bottomMargin = 105f;

            Rect buttonRect = new Rect(
                inRect.x + 360f,
                inRect.yMax - buttonHeight - bottomMargin,
                buttonWidth,
                buttonHeight
            );

            if (Widgets.ButtonText(buttonRect, "RimTalk.PersonaEditor.VoiceModel".Translate()))
            {
                Find.WindowStack.Add(new UI.VoiceSelectionWindow(pawn));
            }
        }
    }
}
