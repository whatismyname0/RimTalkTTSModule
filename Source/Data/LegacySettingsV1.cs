using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// The pre-schema-v2 settings key layout, read once when an old settings file is loaded.
    ///
    /// This is import-only: <see cref="SettingsMigration"/> converts it into the v2 model, and the
    /// v2 model is the only thing ever written back. No v1 keys are produced on save.
    /// Keys that kept their place in v2 (<c>ttsSupplier</c>, <c>customProviders</c>,
    /// <c>currentCustomProviderId</c>) are not repeated here.
    /// </summary>
    public sealed class LegacySettingsV1
    {
        public bool EnableTTS;
        public string FishAudioApiKey = "";
        public float TTSVolume = 0.8f;
        public List<VoiceModel> VoiceModels = new List<VoiceModel>();
        public string TTSTranslationLanguage = "";
        public string DefaultVoiceModelId = "";
        public string CustomTTSProcessingPrompt = "";
        public string TTSModel = "s2-pro";
        public float TTSTemperature = 0.9f;
        public float TTSTopP = 0.9f;
        public float TTSSpeed = 1.0f;
        public int GenerateCooldownMiliSeconds = 5000;
        public int TtsRequestTimeoutSeconds = 120;
        public bool ButtonDisplay = true;
        public bool ControlButtonDisplay = true;

        public Dictionary<string, string> SupplierApiKeys = new Dictionary<string, string>();
        public Dictionary<string, string> SupplierModels = new Dictionary<string, string>();
        public Dictionary<string, int> SupplierGenerateCooldownMs = new Dictionary<string, int>();
        public Dictionary<string, float> SupplierVolume = new Dictionary<string, float>();
        public Dictionary<string, float> SupplierTemperature = new Dictionary<string, float>();
        public Dictionary<string, float> SupplierTopP = new Dictionary<string, float>();
        public Dictionary<string, float> SupplierSpeed = new Dictionary<string, float>();
        public Dictionary<string, List<VoiceModel>> SupplierVoiceModels = new Dictionary<string, List<VoiceModel>>();
        public Dictionary<string, string> SupplierDefaultVoiceModelId = new Dictionary<string, string>();
        public Dictionary<string, string> SupplierRegion = new Dictionary<string, string>();
        public Dictionary<string, bool> SupplierAdvancedMode = new Dictionary<string, bool>();
        public Dictionary<string, List<VoiceAssignmentRule>> SupplierVoiceRules = new Dictionary<string, List<VoiceAssignmentRule>>();

        public string PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;
        public TTSApiProvider ApiProvider = TTSApiProvider.DeepSeek;
        public string ApiKey = "";
        public string Model = "deepseek-chat";
        public string CustomBaseUrl = "";
        public bool RemoveBracketsInPreProcess;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableTTS, "enableTTS", false);
            Scribe_Values.Look(ref FishAudioApiKey, "fishAudioApiKey", "");
            Scribe_Values.Look(ref TTSVolume, "ttsVolume", 0.8f);
            Scribe_Collections.Look(ref VoiceModels, "voiceModels", LookMode.Deep);
            Scribe_Values.Look(ref TTSTranslationLanguage, "ttsTranslationLanguage", "");
            Scribe_Values.Look(ref DefaultVoiceModelId, "defaultVoiceModelId", "");
            Scribe_Values.Look(ref CustomTTSProcessingPrompt, "customTTSProcessingPrompt", "");
            Scribe_Values.Look(ref TTSModel, "ttsModel", "s2-pro");
            Scribe_Values.Look(ref TTSTemperature, "ttsTemperature", 0.9f);
            Scribe_Values.Look(ref TTSTopP, "ttsTopP", 0.9f);
            Scribe_Values.Look(ref TTSSpeed, "ttsSpeed", 1.0f);
            Scribe_Values.Look(ref GenerateCooldownMiliSeconds, "generateCooldownMiliSeconds", 5000);
            Scribe_Values.Look(ref TtsRequestTimeoutSeconds, "ttsRequestTimeoutSeconds", 120);
            Scribe_Values.Look(ref ButtonDisplay, "buttonDisplay", true);
            Scribe_Values.Look(ref ControlButtonDisplay, "controlButtonDisplay", true);

            Scribe_Collections.Look(ref SupplierApiKeys, "supplierApiKeys", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierModels, "supplierModels", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierGenerateCooldownMs, "supplierGenerateCooldownMs", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVolume, "supplierVolume", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTemperature, "supplierTemperature", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTopP, "supplierTopP", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierSpeed, "supplierSpeed", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVoiceModels, "supplierVoiceModels", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref SupplierDefaultVoiceModelId, "supplierDefaultVoiceModelId", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierRegion, "supplierRegion", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierAdvancedMode, "supplierAdvancedMode", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVoiceRules, "supplierVoiceRules", LookMode.Value, LookMode.Deep);

            Scribe_Values.Look(ref PlayerReferenceVoiceModelId, "playerReferenceVoiceModelId", VoiceModel.NONE_MODEL_ID);
            Scribe_Values.Look(ref ApiProvider, "apiProvider", TTSApiProvider.DeepSeek);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "deepseek-chat");
            Scribe_Values.Look(ref CustomBaseUrl, "customBaseUrl", "");
            Scribe_Values.Look(ref RemoveBracketsInPreProcess, "removeBracketsInPreProcess", false);

            if (VoiceModels == null) VoiceModels = new List<VoiceModel>();
            if (SupplierApiKeys == null) SupplierApiKeys = new Dictionary<string, string>();
            if (SupplierModels == null) SupplierModels = new Dictionary<string, string>();
            if (SupplierGenerateCooldownMs == null) SupplierGenerateCooldownMs = new Dictionary<string, int>();
            if (SupplierVolume == null) SupplierVolume = new Dictionary<string, float>();
            if (SupplierTemperature == null) SupplierTemperature = new Dictionary<string, float>();
            if (SupplierTopP == null) SupplierTopP = new Dictionary<string, float>();
            if (SupplierSpeed == null) SupplierSpeed = new Dictionary<string, float>();
            if (SupplierVoiceModels == null) SupplierVoiceModels = new Dictionary<string, List<VoiceModel>>();
            if (SupplierDefaultVoiceModelId == null) SupplierDefaultVoiceModelId = new Dictionary<string, string>();
            if (SupplierRegion == null) SupplierRegion = new Dictionary<string, string>();
            if (SupplierAdvancedMode == null) SupplierAdvancedMode = new Dictionary<string, bool>();
            if (SupplierVoiceRules == null) SupplierVoiceRules = new Dictionary<string, List<VoiceAssignmentRule>>();
        }
    }
}
