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
        private static void DrawApiConfigSection(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.LLMApiConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Gap(6f);

            // Provider Selection
            listing.Label("RimTalk.Settings.TTS.ProviderLabel".Translate());
            if (listing.RadioButton("RimTalk.Settings.TTS.ProviderSkip".Translate(), settings.ApiProvider == TTSApiProvider.Skip, tooltip: "RimTalk.Settings.TTS.ProviderSkipTooltip".Translate()))
            {
                editor.SetApiProvider(TTSApiProvider.Skip);
            }
            if (listing.RadioButton("RimTalk.Settings.TTS.ProviderRimTalkSame".Translate(), settings.ApiProvider == TTSApiProvider.RimTalkSame, tooltip: "RimTalk.Settings.TTS.ProviderRimTalkSameTooltip".Translate()))
            {
                editor.SetApiProvider(TTSApiProvider.RimTalkSame);
            }
            if (listing.RadioButton("DeepSeek", settings.ApiProvider == TTSApiProvider.DeepSeek))
            {
                editor.SetApiProvider(TTSApiProvider.DeepSeek);
            }
            if (listing.RadioButton("OpenAI", settings.ApiProvider == TTSApiProvider.OpenAI))
            {
                editor.SetApiProvider(TTSApiProvider.OpenAI);
            }
            if (listing.RadioButton("RimTalk.Settings.TTS.CustomProvider".Translate(), settings.ApiProvider == TTSApiProvider.Custom))
            {
                editor.SetApiProvider(TTSApiProvider.Custom);
            }

            // Skip and RimTalkSame don't need Model/ApiKey/BaseUrl fields
            if (settings.ApiProvider != TTSApiProvider.Skip && settings.ApiProvider != TTSApiProvider.RimTalkSame)
            {
                listing.Gap(6f);

                // Model
                listing.Label("RimTalk.Settings.TTS.LLMModelLabel".Translate());
                string llmModel = ValidatedTextEntry.Draw(listing, "llm_model", settings.Model, true);
                if (llmModel != settings.Model) editor.SetModel(llmModel);

                listing.Gap(6f);

                // API Key
                listing.Label("RimTalk.Settings.TTS.LLMApiKeyLabel".Translate());
                string llmApiKey = ValidatedTextEntry.Draw(listing, "llm_api_key", settings.ApiKey, true);
                if (llmApiKey != settings.ApiKey) editor.SetApiKey(llmApiKey);

                listing.Gap(6f);

                // Custom Base URL (only for Custom provider)
                if (settings.ApiProvider == TTSApiProvider.Custom)
                {
                    listing.Label("RimTalk.Settings.TTS.CustomBaseUrlLabel".Translate());
                    string customBaseUrl = ValidatedTextEntry.Draw(
                        listing, "llm_custom_base_url", settings.CustomBaseUrl, true);
                    if (customBaseUrl != settings.CustomBaseUrl) editor.SetCustomBaseUrl(customBaseUrl);
                }
            }

            listing.Gap(6f);

            // Remove brackets during preprocessing (not relevant for Skip)
            if (settings.ApiProvider != TTSApiProvider.Skip)
            {
                bool removeBrackets = settings.RemoveBracketsInPreProcess;
                listing.CheckboxLabeled("RimTalk.Settings.TTS.RemoveBracketsInPreProcess".Translate(), ref removeBrackets, "RimTalk.Settings.TTS.RemoveBracketsInPreProcessTooltip".Translate());
                if (removeBrackets != settings.RemoveBracketsInPreProcess) editor.SetRemoveBrackets(removeBrackets);
            }
        }
    }
}
