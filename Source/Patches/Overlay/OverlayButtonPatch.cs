using System;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.TTS.Bridge;
using RimTalk.TTS.UI.Overlay;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Thin adapter for the four overlay control buttons. Layout/drawing live in
    /// <see cref="OverlayButtonBar"/>, the pacing bypass in <see cref="OverlayPacingPatch"/>;
    /// this class only maps a clicked button to the runtime command.
    /// </summary>
    internal static class OverlayButtonPatch
    {
        public static void MapComponentOnGuiPostfix(object __instance)
        {
            try
            {
                var runtime = TtsRuntime.Current;
                if (runtime == null || !runtime.ShowControlButtons) return;
                if (!OverlayBridge.TryGetGearRect(__instance, out Rect gearRect)) return;

                var clicked = OverlayButtonBar.NextTo(gearRect).Draw();
                if (clicked != OverlayButton.None)
                {
                    Execute(clicked);
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Overlay MapComponentOnGUI postfix exception: {ex}");
            }
        }

        public static bool HandleInputPrefix(object __instance)
        {
            var runtime = TtsRuntime.Current;
            if (runtime == null || !runtime.ShowControlButtons) return true;
            if (!OverlayBridge.TryGetGearRect(__instance, out Rect gearRect)) return true;

            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0) return true;

            var clicked = OverlayButtonBar.NextTo(gearRect).HitTest(currentEvent.mousePosition);
            if (clicked == OverlayButton.None) return true;

            currentEvent.Use();
            Execute(clicked);
            return false;
        }

        private static void Execute(OverlayButton button)
        {
            switch (button)
            {
                case OverlayButton.Reset:
                    ResetButtonFunc();
                    break;
                case OverlayButton.Generate:
                    GenerateButtonFunc();
                    break;
                case OverlayButton.Ignore:
                    IgnoreButtonFunc();
                    break;
                case OverlayButton.DisplayNext:
                    OverlayPacingPatch.DisplayNext();
                    break;
            }
        }

        private static void ResetButtonFunc()
        {
            Messages.Message("RimTalk.TTS.ResetComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            TtsRuntime.Current?.Pipeline?.ResetAll(false);
        }

        private static void GenerateButtonFunc()
        {
            Messages.Message("RimTalk.TTS.GenerateComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);

            Pawn selectedPawn = PawnSelector.SelectNextAvailablePawn();
            if (selectedPawn == null)
                return;

            // 1. ALWAYS try to get from the general pool first.
            bool talkGenerated;
            if (!selectedPawn.IsFreeNonSlaveColonist || selectedPawn.IsQuestLodger()
                || TalkRequestPool.IsEmpty || global::RimTalk.Util.PawnUtil.IsInDanger(selectedPawn, true))
            {
                talkGenerated = false;
            }
            else
            {
                var request = TalkRequestPool.GetRequestFromPool(selectedPawn);
                talkGenerated = request != null && TalkService.GenerateTalk(request);
            }

            // 2. If the pawn has a specific talk request, try generating it.
            if (!talkGenerated)
            {
                var pawnState = global::RimTalk.Data.Cache.Get(selectedPawn);
                if (pawnState.GetNextTalkRequest() != null)
                {
                    talkGenerated = TalkService.GenerateTalk(pawnState.GetNextTalkRequest());
                    if (talkGenerated && pawnState.TalkRequests.Count > 0)
                        pawnState.TalkRequests.RemoveFirst();
                }
            }

            // 3. Fallback: generate based on current context if nothing else worked.
            if (!talkGenerated)
            {
                TalkRequest talkRequest = new TalkRequest(null, selectedPawn);
                TalkService.GenerateTalk(talkRequest);
            }
        }

        private static void IgnoreButtonFunc()
        {
            Messages.Message("RimTalk.TTS.IgnoreComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);

            foreach (var pawn in global::RimTalk.Data.Cache.GetAll())
            {
                pawn.IgnoreAllTalkResponses();
            }
        }
    }
}