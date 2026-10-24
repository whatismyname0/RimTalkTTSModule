using System.Collections.Generic;
using RimTalk.TTS.Data;
using RimTalk.TTS.Patches;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Single write gateway for the settings window.
    ///
    /// Rendering code only reads <see cref="TTSSettings"/> and calls these methods; direct field
    /// assignments live here, next to the side effects they require (provider swap, pipeline reset,
    /// player voice refresh, per-supplier dictionary bookkeeping).
    /// </summary>
    internal sealed class TtsSettingsEditor
    {
        private readonly TTSSettings _settings;

        public TtsSettingsEditor(TTSSettings settings)
        {
            _settings = settings;
        }

        public TTSSettings Settings => _settings;

        // ---- general ---------------------------------------------------------------------

        public void SetEnabled(bool value)
        {
            if (_settings.EnableTTS == value) return;

            _settings.EnableTTS = value;
            if (!value)
            {
                // TTS turned OFF: cancel pending speech and clear playback state.
                TtsRuntime.Current?.Pipeline?.ResetAll(false);
                TTSLog.Message("[RimTalk.TTS] TTS disabled via settings");
            }
            else
            {
                TTSLog.Message("[RimTalk.TTS] TTS enabled via settings");
            }
        }

        public void SetButtonDisplay(bool value) => _settings.ButtonDisplay = value;

        public void SetControlButtonDisplay(bool value) => _settings.ControlButtonDisplay = value;

        public void SetRequestTimeout(int seconds) => _settings.TtsRequestTimeoutSeconds = seconds;

        public void SetTranslationLanguage(string value) => _settings.TTSTranslationLanguage = value ?? "";

        // ---- LLM preprocessing -----------------------------------------------------------

        public void SetApiProvider(TTSApiProvider provider) => _settings.ApiProvider = provider;

        public void SetModel(string value) => _settings.Model = value;

        public void SetApiKey(string value) => _settings.ApiKey = value;

        public void SetCustomBaseUrl(string value) => _settings.CustomBaseUrl = value;

        public void SetRemoveBrackets(bool value) => _settings.RemoveBracketsInPreProcess = value;

        // ---- processing prompt -----------------------------------------------------------

        public void SetProcessingPrompt(string value) => _settings.CustomTTSProcessingPrompt = value ?? "";

        /// <summary>Fish Audio old/S2 semantics: an empty override means "use the supplier default".</summary>
        public void ClearProcessingPromptOverride() => _settings.CustomTTSProcessingPrompt = "";

        public void ResetProcessingPrompt(string defaultPrompt) => _settings.CustomTTSProcessingPrompt = defaultPrompt ?? "";

        // ---- supplier --------------------------------------------------------------------

        public void SelectSupplier(TTSSettings.TTSSupplier supplier)
        {
            _settings.Supplier = supplier;
            TtsRuntime.Current?.Pipeline?.SetProvider(supplier, _settings);
        }

        public void SelectCustomProvider(string providerId)
        {
            _settings.CurrentCustomProviderId = providerId;
            _settings.Supplier = TTSSettings.TTSSupplier.Custom;
            _settings.EnsureCustomProviderDictionaries();
            TtsRuntime.Current?.Pipeline?.SetProvider(_settings.Supplier, _settings);
        }

        public void SetSupplierApiKey(string supplierKey, string value) => _settings.SetSupplierApiKey(supplierKey, value);

        public void SetSupplierModel(string supplierKey, string value) => _settings.SetSupplierModel(supplierKey, value);

        public void SetSupplierRegion(TTSSettings.TTSSupplier supplier, string value)
        {
            _settings.SetSupplierRegion(supplier, value);
            // Azure picks its endpoint from the region, so rebuild the active provider.
            TtsRuntime.Current?.Pipeline?.SetProvider(_settings.Supplier, _settings);
        }

        public void SetGenerateCooldown(string supplierKey, int milliseconds) =>
            _settings.SetSupplierGenerateCooldown(supplierKey, milliseconds);

        public void SetVolume(string supplierKey, float value) => _settings.SetSupplierVolume(supplierKey, value);

        public void SetTemperature(string supplierKey, float value) => _settings.SetSupplierTemperature(supplierKey, value);

        public void SetTopP(string supplierKey, float value) => _settings.SetSupplierTopP(supplierKey, value);

        public void SetSpeed(string supplierKey, float value) => _settings.SetSupplierSpeed(supplierKey, value);

        public void SetDefaultVoiceModel(string supplierKey, string modelId) =>
            _settings.SetSupplierDefaultVoiceModelId(supplierKey, modelId);

        public void SetVoiceModels(string supplierKey, List<VoiceModel> models) =>
            _settings.SetSupplierVoiceModels(supplierKey, models);

        public void SetVoiceRules(string supplierKey, List<VoiceAssignmentRule> rules) =>
            _settings.SetSupplierVoiceRules(supplierKey, rules);

        public void SetPlayerReferenceVoice(string modelId)
        {
            _settings.PlayerReferenceVoiceModelId = modelId;
            RimTalkPatches.UpdatePlayerPawnVoice();
        }

        // ---- custom providers ------------------------------------------------------------

        public void AddCustomProvider(CustomProviderConfig config)
        {
            if (config == null) return;

            if (_settings.CustomProviders == null)
            {
                _settings.CustomProviders = new List<CustomProviderConfig>();
            }

            _settings.CustomProviders.Add(config);
            _settings.EnsureCustomProviderDictionaries();
        }

        public void RemoveCustomProvider(string providerId)
        {
            _settings.RemoveCustomProvider(providerId);

            // Removing the active provider leaves the supplier pointing at nothing: fall back to None.
            if (_settings.Supplier == TTSSettings.TTSSupplier.Custom && string.IsNullOrEmpty(_settings.CurrentCustomProviderId))
            {
                _settings.Supplier = TTSSettings.TTSSupplier.None;
                TtsRuntime.Current?.Pipeline?.SetProvider(_settings.Supplier, _settings);
            }
        }

        /// <summary>Push edited config fields back into the per-supplier dictionaries and rebuild if active.</summary>
        public void SyncCustomProviderConfig(CustomProviderConfig config)
        {
            if (config == null) return;

            string supplierKey = config.GetSupplierKey();
            _settings.SetSupplierApiKey(supplierKey, config.ApiKey);
            _settings.SetSupplierModel(supplierKey, config.Model);

            if (_settings.Supplier == TTSSettings.TTSSupplier.Custom && _settings.CurrentCustomProviderId == config.Id)
            {
                TtsRuntime.Current?.Pipeline?.SetProvider(_settings.Supplier, _settings);
            }
        }
    }
}
