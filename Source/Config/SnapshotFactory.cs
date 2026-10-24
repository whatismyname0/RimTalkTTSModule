using System;
using System.Linq;
using RimTalk.Data;
using RimTalk.TTS.Data;
using RimTalk.Util;
using Verse;

namespace RimTalk.TTS.Config
{
    /// <summary>
    /// Builds a <see cref="SettingsSnapshot"/> from the live settings. Must run on the main thread:
    /// it reads pawn voice assignments and, for the "same as RimTalk" preprocessing provider,
    /// RimTalk's own active configuration.
    /// </summary>
    internal static class SnapshotFactory
    {
        private static readonly string[] FishAudioS2Models = { "s2-pro", "s2.1-pro", "s2.1-pro-free" };

        public static SettingsSnapshot Build(TTSSettings settings, Pawn pawn)
        {
            var s = settings ?? new TTSSettings();
            string supplierKey = s.GetCurrentSupplierKey();

            return new SettingsSnapshot
            {
                PawnLabel = pawn?.LabelShort,
                VoiceModelId = ResolveVoiceModelId(s, pawn, supplierKey),
                VoiceLanguage = PawnVoiceManager.GetEffectiveLanguage(pawn, s),
                Llm = BuildLlmOptions(s),
                Supplier = BuildSupplierOptions(s, supplierKey),
                RequestTimeoutSeconds = s.GetTtsRequestTimeoutSeconds(),
            };
        }

        private static string ResolveVoiceModelId(TTSSettings s, Pawn pawn, string supplierKey)
        {
            var voiceModels = s.GetSupplierVoiceModels(supplierKey);
            string defaultVoice = s.GetSupplierDefaultVoiceModelId(supplierKey);

            if (pawn != null)
            {
                string picked = PawnVoiceManager.GetVoiceModel(pawn);
                if (!string.IsNullOrEmpty(picked) && voiceModels.Any(vm => vm.ModelId == picked))
                    return picked;
            }

            return defaultVoice;
        }

        private static SupplierOptions BuildSupplierOptions(TTSSettings s, string supplierKey)
        {
            return new SupplierOptions
            {
                Kind = s.Supplier,
                SupplierKey = supplierKey,
                ApiKey = s.GetSupplierApiKey(supplierKey),
                Model = s.GetSupplierModel(supplierKey),
                CooldownMs = s.GetSupplierGenerateCooldown(supplierKey),
                Volume = s.GetSupplierVolume(supplierKey),
                Speed = s.GetSupplierSpeed(supplierKey),
                Temperature = s.GetSupplierTemperature(supplierKey),
                TopP = s.GetSupplierTopP(supplierKey),
            };
        }

        private static LlmRequestOptions BuildLlmOptions(TTSSettings s)
        {
            var options = new LlmRequestOptions
            {
                Skip = s.ApiProvider == TTSApiProvider.Skip,
                PromptTemplate = TTSConstant.GetTTSProcessingPrompt(s),
                RemoveBrackets = s.RemoveBracketsInPreProcess,
                ConvertBracketsToParentheses =
                    s.Supplier == TTSSettings.TTSSupplier.FishAudio &&
                    !FishAudioS2Models.Contains(s.GetSupplierModel(TTSSettings.TTSSupplier.FishAudio)),
            };

            if (options.Skip)
                return options;

            if (s.ApiProvider == TTSApiProvider.RimTalkSame)
            {
                var rimTalkConfig = global::RimTalk.Settings.Get()?.GetActiveConfig();
                if (rimTalkConfig == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] Preprocessing uses RimTalk's config, but RimTalk has no active API config");
                    return options;
                }

                options.BaseUrl = GetRimTalkBaseUrl(rimTalkConfig);
                options.ApiKey = rimTalkConfig.ApiKey;
                options.Model = rimTalkConfig.SelectedModel == "Custom"
                    ? rimTalkConfig.CustomModelName
                    : rimTalkConfig.SelectedModel;
                return options;
            }

            options.BaseUrl = GetBaseUrl(s);
            options.ApiKey = s.ApiKey;
            options.Model = s.Model;
            return options;
        }

        /// <summary>Base URL of the TTS module's own preprocessing provider.</summary>
        private static string GetBaseUrl(TTSSettings settings)
        {
            return settings.ApiProvider switch
            {
                TTSApiProvider.DeepSeek => "https://api.deepseek.com",
                TTSApiProvider.OpenAI => "https://api.openai.com",
                TTSApiProvider.Custom => settings.CustomBaseUrl,
                _ => "",
            };
        }

        /// <summary>Resolve the base URL from RimTalk's active AI config (Gemini uses its OpenAI-compatible endpoint).</summary>
        private static string GetRimTalkBaseUrl(ApiConfig config)
        {
            if (config == null) return "";

            switch (config.Provider)
            {
                case AIProvider.Google:
                    return "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                case AIProvider.Local:
                case AIProvider.Custom:
                    return config.BaseUrl ?? "";
                default:
                    var endpointUrl = config.Provider.GetEndpointUrl();
                    if (string.IsNullOrEmpty(endpointUrl)) return "";
                    int idx = endpointUrl.IndexOf("/v1/chat/completions", StringComparison.Ordinal);
                    if (idx > 0) return endpointUrl.Substring(0, idx);
                    idx = endpointUrl.IndexOf("/chat/completions", StringComparison.Ordinal);
                    if (idx > 0) return endpointUrl.Substring(0, idx);
                    return endpointUrl;
            }
        }
    }
}
