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
    /// <summary>
    /// TTS settings UI renderer
    /// </summary>
    public static partial class SettingsUI
    {
        // Grow-only measured height of the settings page, so long pages never get clipped.
        private static float _contentHeight = 2100f;
        private static Vector2 scrollPosition = Vector2.zero;
        private static Vector2 mainScrollPosition = Vector2.zero;
        private static string processingPromptBuffer = "";
        private static bool processingPromptInitialized = false;
        // Buffers for upload UI
        private static string uploadPathBuffer = "";
        private static string uploadNameBuffer = "";
        private static string uploadTextBuffer = "";

        public static void DrawTTSSettings(Rect inRect, TTSSettings settings)
        {
            // Work produced by background tasks (uploads, voice sync) is queued on the main thread.
            UiMessageQueue.Drain();

            var editor = new TtsSettingsEditor(settings);

            // Calculate content height dynamically based on selected supplier's voice model count
            float baseHeight = 2100f; // baseline estimate; the measured height only grows from here
            float voiceModelRowHeight = 40f; // Height per voice model row (30f + 6f gap + padding)
            var supplierVoiceModels = settings.GetSupplierVoiceModels(settings.GetCurrentSupplierKey());
            int voiceModelCount = supplierVoiceModels?.Count ?? 0;
            // If supplier supports SiliconFlow uploads, include upload UI height estimate
            float uploadSectionHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS) ? 280f : 0f;
            // ResetModels button height (CosyVoice, IndexTTS, AzureTTS, EdgeTTS)
            float resetButtonHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS) ? 40f : 0f;
            // Processing prompt area height (text area)
            float contentHeight = Mathf.Max(_contentHeight, baseHeight + (voiceModelCount * voiceModelRowHeight) + uploadSectionHeight + resetButtonHeight);
            bool isOn = settings.EnableTTS;
            
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);
            
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Enable TTS
            bool enableTts = settings.EnableTTS;
            listing.CheckboxLabeled("RimTalk.Settings.TTS.Enable".Translate(), ref enableTts, "RimTalk.Settings.TTS.EnableTooltip".Translate());

            // Handle TTS toggle
            if (isOn != enableTts)
            {
                editor.SetEnabled(enableTts);
                if (!enableTts)
                {
                    // TTS turned OFF: the editor already cancelled pending speech.
                    listing.End();
                    Widgets.EndScrollView();
                    return;
                }
            }

            listing.Gap();

            bool showButton = settings.ButtonDisplay;
            listing.CheckboxLabeled("RimTalk.Settings.TTS.ButtonEnable".Translate(), ref showButton, "RimTalk.Settings.TTS.ButtonEnableTooltip".Translate());
            if (showButton != settings.ButtonDisplay) editor.SetButtonDisplay(showButton);

            listing.Gap();

            bool showControlButtons = settings.ControlButtonDisplay;
            listing.CheckboxLabeled("RimTalk.Settings.TTS.ControlButtonEnable".Translate(), ref showControlButtons, "RimTalk.Settings.TTS.ControlButtonEnableTooltip".Translate());
            if (showControlButtons != settings.ControlButtonDisplay) editor.SetControlButtonDisplay(showControlButtons);

            listing.Gap();

            // Per-request timeout: global (not per supplier) because it protects the whole TTS
            // pipeline from a supplier that never answers.
            int currentRequestTimeout = settings.GetTtsRequestTimeoutSeconds();
            listing.Label("RimTalk.Settings.TTS.RequestTimeoutLabel".Translate(currentRequestTimeout.ToString()));
            int newRequestTimeout = (int)listing.Slider(currentRequestTimeout, 0, TTSSettings.MAX_REQUEST_TIMEOUT_SECONDS);
            if (newRequestTimeout != currentRequestTimeout)
            {
                // Snap only the value the player just dragged, never one loaded from disk.
                newRequestTimeout = Mathf.RoundToInt(newRequestTimeout / 10f) * 10;
                editor.SetRequestTimeout(newRequestTimeout);
            }

            Text.Font = GameFont.Tiny;
            listing.Label("RimTalk.Settings.TTS.RequestTimeoutInfo".Translate());
            Text.Font = GameFont.Small;

            listing.Gap();

            // LLM API Configuration Section
            DrawApiConfigSection(listing, settings, editor);

            listing.Gap();

            // Translation Language
            listing.Label("RimTalk.Settings.TTS.TranslationLanguage".Translate());
            string translationLanguage = ValidatedTextEntry.Draw(
                listing, "tts_translation_language", settings.TTSTranslationLanguage,
                settings.ApiProvider != TTSApiProvider.Skip);
            if (translationLanguage != settings.TTSTranslationLanguage) editor.SetTranslationLanguage(translationLanguage);

            listing.Gap();

            // Processing Prompt Section (similar to RimTalk main module)
            DrawProcessingPromptSection(listing, settings, editor);

            listing.Gap();

            // Supplier selection + per-supplier configuration
            DrawSupplierSection(listing, settings, editor, viewRect.width);


            listing.End();
            Widgets.EndScrollView();
        }
    }
}
