using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;
using RimTalk.TTS.Patches;
using RimTalk.TTS.Config;
using RimTalk.TTS.Providers;

namespace RimTalk.TTS.UI
{
    public static partial class SettingsUI
    {
        private static void DrawProcessingPromptSection(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor)
        {
            listing.Label("RimTalk.Settings.TTS.ProcessingPromptLabel".Translate());
            
            // Initialize buffer if needed - show the effective prompt (custom or the
            // supplier-specific default) so the text area matches what is actually used.
            if (!processingPromptInitialized)
            {
                processingPromptBuffer = TTSConstant.GetTTSProcessingPrompt(settings);
                processingPromptInitialized = true;
            }

            // Instructions
            Text.Font = GameFont.Tiny;
            GUI.color = Color.cyan;
            Rect tipRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(tipRect, "RimTalk.Settings.TTS.ProcessingPromptTip".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Text area for prompt - display buffer which contains either custom or default
            float textAreaHeight = 120f;
            Rect textAreaRect = listing.GetRect(textAreaHeight);
            string displayPrompt = processingPromptBuffer;
            string newPrompt = Widgets.TextArea(textAreaRect, displayPrompt);

            // Only save if user actually modified the content
            if (newPrompt != displayPrompt)
            {
                processingPromptBuffer = newPrompt.Replace("\\n", "\n");
                editor.SetProcessingPrompt(processingPromptBuffer);
            }

            listing.Gap(6f);

            // Reset buttons - First row: FishAudio, CosyVoice, IndexTTS
            Rect resetButtonsRect1 = listing.GetRect(30f);
            float gap = 4f;
            float btnW = (resetButtonsRect1.width - gap * 2) / 3f;
            Rect fishRectOld = new Rect(resetButtonsRect1.x, resetButtonsRect1.y, btnW/2, resetButtonsRect1.height);
            Rect fishRectS2 = new Rect(resetButtonsRect1.x+btnW/2, resetButtonsRect1.y, btnW/2, resetButtonsRect1.height);
            Rect cosyRect = new Rect(resetButtonsRect1.x + btnW + gap, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
            Rect indexRect = new Rect(resetButtonsRect1.x + (btnW + gap) * 2f, resetButtonsRect1.y, btnW, resetButtonsRect1.height);

            if (Widgets.ButtonText(fishRectOld, "RimTalk.Settings.TTS.ResetPrompt.FishAudioOld".Translate()))
            {
                editor.ClearProcessingPromptOverride();
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt;
            }

            if (Widgets.ButtonText(fishRectS2, "RimTalk.Settings.TTS.ResetPrompt.FishAudioS2".Translate()))
            {
                editor.ClearProcessingPromptOverride();
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_FishAudioS2;
            }

            if (Widgets.ButtonText(cosyRect, "RimTalk.Settings.TTS.ResetPrompt.CosyVoice".Translate()))
            {
                editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_CosyVoice);
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
            }

            if (Widgets.ButtonText(indexRect, "RimTalk.Settings.TTS.ResetPrompt.IndexTTS".Translate()))
            {
                editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_IndexTTS);
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
            }

            // Reset buttons - Second row: AzureTTS, EdgeTTS, GeminiTTS
            listing.Gap(6f);
            Rect resetButtonsRect2 = listing.GetRect(30f);
            Rect azureRect = new Rect(resetButtonsRect2.x, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect edgeRect = new Rect(resetButtonsRect2.x + btnW + gap, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect geminiRect = new Rect(resetButtonsRect2.x + (btnW + gap) * 2f, resetButtonsRect2.y, btnW, resetButtonsRect2.height);

            if (Widgets.ButtonText(azureRect, "RimTalk.Settings.TTS.ResetPrompt.AzureTTS".Translate()))
            {
                editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_AzureTTS);
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
            }

            if (Widgets.ButtonText(edgeRect, "RimTalk.Settings.TTS.ResetPrompt.EdgeTTS".Translate()))
            {
                editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS);
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
            }

            if (Widgets.ButtonText(geminiRect, "RimTalk.Settings.TTS.ResetPrompt.GeminiTTS".Translate()))
            {
                editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS);
                processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
            }

            // Reset buttons - Third row: Custom
            if (settings.Supplier == TTSSettings.TTSSupplier.Custom)
            {
                listing.Gap(6f);
                Rect resetButtonsRect3 = listing.GetRect(30f);
                Rect customRect = new Rect(resetButtonsRect3.x, resetButtonsRect3.y, btnW, resetButtonsRect3.height);
                if (Widgets.ButtonText(customRect, "RimTalk.Settings.TTS.ResetPrompt.Custom".Translate()))
                {
                    editor.ResetProcessingPrompt(TTSConstant.DefaultTTSProcessingPrompt_Custom);
                    processingPromptBuffer = TTSConstant.DefaultTTSProcessingPrompt_Custom;
                }
            }
        }
    }
}
