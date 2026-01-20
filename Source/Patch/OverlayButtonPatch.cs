using UnityEngine;
using HarmonyLib;
using System.Reflection;
using Verse;
using System;
using RimWorld;
using RimTalk.TTS.Service;
using RimTalk.Service;
using RimTalk.Data;
using RimTalk.Util;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.Patch
{
    public static class OverlayButtonPatch
    {
        private static Rect resetButtonScreenRect = default;
        private static Rect generateButtonScreenRect = default;
        private static Rect ignoreButtonScreenRect = default;
        private static Rect displayButtonScreenRect = default;
        private static bool displayPassport = false;

        [HarmonyPatch]
        public static class Overlay_MapComponentOnGUI_Postfix
        {
            // Target the non-public instance method DrawSettingsDropdown on RimTalk.UI.Overlay
            static MethodBase TargetMethod()
            {
                return typeof(global::RimTalk.UI.Overlay).GetMethod("MapComponentOnGUI", BindingFlags.Public | BindingFlags.Instance);
            }

            static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (!TTSConfig.IsEnabled) return;

                    var overlayType = __instance.GetType();

                    // Prefer placing the Reset button to the left of the gear icon if available
                    var gearField = overlayType.GetField("_gearIconScreenRect", BindingFlags.NonPublic | BindingFlags.Instance);

                    var gearRect = (Rect)gearField.GetValue(__instance);
                    // Button sizing and positioning: place to the left of the gear icon with a small padding
                    float resetBtnWidth = 120f;
                    float generateBtnWidth = 150f;
                    float ignoreBtnWidth = 150f;
                    float displayBtnWidth = 150f;
                    float btnHeight = Mathf.Max(gearRect.height, 28f);
                    float padding = 6f;
                    resetButtonScreenRect.Set(gearRect.x - resetBtnWidth - padding, gearRect.y, resetBtnWidth, btnHeight);
                    generateButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - 2*padding, gearRect.y, generateBtnWidth, btnHeight);
                    ignoreButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - 2*padding, gearRect.y, ignoreBtnWidth, btnHeight);
                    displayButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - displayBtnWidth - 2*padding, gearRect.y, ignoreBtnWidth, btnHeight);

                    if (Widgets.ButtonText(resetButtonScreenRect, "RimTalk.TTS.Reset".Translate()))
                    {
                        ResetButtonFunc();
                    }
                    if (Widgets.ButtonText(generateButtonScreenRect, "RimTalk.TTS.Generate".Translate()))
                    {
                        generateButtonFunc();
                    }
                    if (Widgets.ButtonText(ignoreButtonScreenRect, "RimTalk.TTS.Ignore".Translate()))
                    {
                        ignoreButtonFunc();
                    }
                    if (Widgets.ButtonText(displayButtonScreenRect, "RimTalk.TTS.Display".Translate()))
                    {
                        displayButtonFunc();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk.TTS] Overlay_DrawSettingsDropdown_Postfix exception: {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class Overlay_HandleInput_Prefix
        {
            // Target the non-public instance method HandleInput on RimTalk.UI.Overlay
            static MethodBase TargetMethod()
            {
                return typeof(global::RimTalk.UI.Overlay).GetMethod("HandleInput", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            static bool Prefix(object __instance)
            {
                if (!TTSConfig.IsEnabled) return true;
                if (__instance == null) return true;
                
                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    if (resetButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        ResetButtonFunc();
                        return false; // Consume event
                    }
                    if (generateButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        generateButtonFunc();
                        return false; // Consume event
                    }
                    if (ignoreButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        ignoreButtonFunc();
                        return false; // Consume event
                    }
                    if (displayButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        displayButtonFunc();
                        return false; // Consume event
                    }
                }

                return true;
            }
        }

        private static void ResetButtonFunc()
        {
            Messages.Message("RimTalk.TTS.ResetComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            TTSService.StopAll(false);
        }

        private static void generateButtonFunc()
        {
            Messages.Message("RimTalk.TTS.GenerateComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            // Select a pawn based on the current iteration strategy
            Pawn selectedPawn = PawnSelector.SelectNextAvailablePawn();

            if (selectedPawn != null)
            {
                // 1. ALWAYS try to get from the general pool first.
                bool talkGenerated;
                // If the pawn is a free colonist not in danger and the pool has requests
                if (!selectedPawn.IsFreeNonSlaveColonist || selectedPawn.IsQuestLodger() || TalkRequestPool.IsEmpty || global::RimTalk.Util.PawnUtil.IsInDanger(selectedPawn,true)) talkGenerated=false;
                else
                {
                    var request = TalkRequestPool.GetRequestFromPool(selectedPawn);
                    talkGenerated = request != null && TalkService.GenerateTalk(request);
                }

                // 2. If the pawn has a specific talk request, try generating it
                if (!talkGenerated)
                {
                    var pawnState = global::RimTalk.Data.Cache.Get(selectedPawn);
                    if (pawnState.GetNextTalkRequest() != null)
                    {
                        talkGenerated = TalkService.GenerateTalk(pawnState.GetNextTalkRequest());
                        if(talkGenerated && pawnState.TalkRequests.Count > 0)
                            pawnState.TalkRequests.RemoveFirst();
                    }
                }

                // 3. Fallback: generate based on current context if nothing else worked
                if (!talkGenerated)
                {
                    TalkRequest talkRequest = new TalkRequest(null, selectedPawn);
                    TalkService.GenerateTalk(talkRequest);
                }
            }
        }

        private static void ignoreButtonFunc()
        {
            Messages.Message("RimTalk.TTS.IgnoreComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            
            foreach (var pawn in global::RimTalk.Data.Cache.GetAll())
            {
                pawn.IgnoreAllTalkResponses();
            }
        }
        
        private static void displayButtonFunc()
        {
            displayPassport = true;
            TalkService.DisplayTalk();
            displayPassport = false;
        }

        [HarmonyPatch]
        public static class CommonUtil_HasPassed_Prefix
        {
            static MethodBase TargetMethod()
            {
                return typeof(CommonUtil).GetMethod("HasPassed", BindingFlags.Public | BindingFlags.Static);
            }

            static bool Prefix(int pastTick, double seconds, ref bool __result)
            {
                if (displayPassport)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}