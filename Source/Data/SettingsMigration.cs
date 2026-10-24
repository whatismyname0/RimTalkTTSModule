using System;
using System.Collections.Generic;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Read-only v1 -&gt; v2 settings import.
    ///
    /// Only v2 is ever written back (the one-release v1 dual-write was removed), but an existing
    /// player's v1 settings file must still be converted automatically on first load.
    /// </summary>
    internal static class SettingsMigration
    {
        public static void MigrateV1ToV2(LegacySettingsV1 legacy, TTSSettings target)
        {
            legacy ??= new LegacySettingsV1();

            target.Llm = new LlmConfig
            {
                ApiProvider = legacy.ApiProvider,
                ApiKey = legacy.ApiKey ?? "",
                Model = string.IsNullOrEmpty(legacy.Model) ? "deepseek-chat" : legacy.Model,
                CustomBaseUrl = legacy.CustomBaseUrl ?? "",
                RemoveBracketsInPreProcess = legacy.RemoveBracketsInPreProcess,
            };

            target.Ui = new UiConfig
            {
                EnableTTS = legacy.EnableTTS,
                ButtonDisplay = legacy.ButtonDisplay,
                ControlButtonDisplay = legacy.ControlButtonDisplay,
                IsOnButton = true, // never persisted in v1
                TranslationLanguage = legacy.TTSTranslationLanguage ?? "",
                TtsRequestTimeoutSeconds = legacy.TtsRequestTimeoutSeconds,
                PlayerReferenceVoiceModelId = legacy.PlayerReferenceVoiceModelId ?? VoiceModel.NONE_MODEL_ID,
                CustomTTSProcessingPrompt = legacy.CustomTTSProcessingPrompt ?? "",
            };

            var suppliers = new List<SupplierConfig>();
            foreach (TTSSettings.TTSSupplier kind in Enum.GetValues(typeof(TTSSettings.TTSSupplier)))
            {
                if (kind == TTSSettings.TTSSupplier.None) continue;
                suppliers.Add(CreateMigratedSupplier(kind, legacy));
            }

            // v1 dictionaries win over the legacy scalars when both are present.
            Overlay(legacy.SupplierApiKeys, suppliers, (c, v) => c.ApiKey = v ?? "");
            Overlay(legacy.SupplierModels, suppliers, (c, v) => c.Model = v ?? "");
            Overlay(legacy.SupplierGenerateCooldownMs, suppliers, (c, v) => c.CooldownMs = v);
            Overlay(legacy.SupplierVolume, suppliers, (c, v) => c.Volume = v);
            Overlay(legacy.SupplierTemperature, suppliers, (c, v) => c.Temperature = v);
            Overlay(legacy.SupplierTopP, suppliers, (c, v) => c.TopP = v);
            Overlay(legacy.SupplierSpeed, suppliers, (c, v) => c.Speed = v);
            Overlay(legacy.SupplierVoiceModels, suppliers, (c, v) => c.VoiceModels = CloneModels(v));
            Overlay(legacy.SupplierDefaultVoiceModelId, suppliers, (c, v) => c.DefaultVoiceModelId = v ?? VoiceModel.NONE_MODEL_ID);
            Overlay(legacy.SupplierRegion, suppliers, (c, v) => c.Region = v ?? "eastus");
            Overlay(legacy.SupplierAdvancedMode, suppliers, (c, v) => c.AdvancedMode = v);
            Overlay(legacy.SupplierVoiceRules, suppliers, (c, v) => c.Rules = v ?? new List<VoiceAssignmentRule>());

            target.Suppliers = suppliers;
            target.EnsureInitialized();
        }

        private static SupplierConfig CreateMigratedSupplier(TTSSettings.TTSSupplier kind, LegacySettingsV1 legacy)
        {
            var cfg = TTSSettings.CreateDefaultSupplier(kind);
            if (kind != TTSSettings.TTSSupplier.FishAudio) return cfg;

            // v1 kept the FishAudio values in global scalar fields (the dictionaries override them below).
            cfg.ApiKey = legacy.FishAudioApiKey ?? "";
            cfg.Model = string.IsNullOrEmpty(legacy.TTSModel) ? "s2-pro" : legacy.TTSModel;
            cfg.CooldownMs = legacy.GenerateCooldownMiliSeconds;
            cfg.Volume = legacy.TTSVolume;
            cfg.Temperature = legacy.TTSTemperature;
            cfg.TopP = legacy.TTSTopP;
            cfg.Speed = legacy.TTSSpeed;
            cfg.VoiceModels = CloneModels(legacy.VoiceModels);
            cfg.DefaultVoiceModelId = legacy.DefaultVoiceModelId ?? "";
            return cfg;
        }

        private static void Overlay<T>(
            Dictionary<string, T> source,
            List<SupplierConfig> suppliers,
            Action<SupplierConfig, T> apply)
        {
            if (source == null || suppliers == null) return;

            foreach (var pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;

                var cfg = suppliers.Find(c => c != null && c.Key == pair.Key);
                if (cfg == null)
                {
                    cfg = TTSSettings.CreateSupplierForKey(pair.Key);
                    suppliers.Add(cfg);
                }

                apply(cfg, pair.Value);
            }
        }

        private static List<VoiceModel> CloneModels(List<VoiceModel> models)
        {
            var clone = new List<VoiceModel>();
            if (models == null) return clone;

            foreach (var model in models)
            {
                if (model == null) continue;
                clone.Add(new VoiceModel(model.ModelId, model.ModelName));
            }
            return clone;
        }
    }
}
