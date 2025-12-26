using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// TTS module settings - independent from main RimTalk settings
    /// </summary>
    public class TTSSettings : ModSettings
    {
        // Player reference voice model id (null/empty = use supplier default, VoiceModel.NONE_MODEL_ID = none)
        public string PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;

        public enum TTSSupplier
        {
            None,
            FishAudio,
            CosyVoice,
            IndexTTS
        }

        // Default constants (use these instead of deprecated legacy fields)
        public const float DEFAULT_SUPPLIER_VOLUME = 0.8f;
        public const float DEFAULT_SUPPLIER_SPEED = 1.0f;
        public const int DEFAULT_GENERATE_COOLDOWN_MS = 5000;

        // Selected TTS supplier implementation
        public TTSSupplier Supplier = TTSSupplier.FishAudio;

        // TTS Configuration
        public bool EnableTTS = false;
        public string FishAudioApiKey = "";//Deprecated
        public float TTSVolume = 0.8f;//Deprecated
        public List<VoiceModel> VoiceModels = new();//Deprecated
        public string TTSTranslationLanguage = "";
        public string DefaultVoiceModelId = "";//Deprecated
        
        // LLM API Configuration (for text processing/translation)
        public TTSApiProvider ApiProvider = TTSApiProvider.DeepSeek;
        public string ApiKey = "";
        public string Model = "deepseek-chat";
        public string CustomBaseUrl = ""; // For custom provider
        
        // Custom TTS processing prompt (empty = use default from TTSConstant)
        public string CustomTTSProcessingPrompt = "";
        
        // Remove bracketed content during preprocessing
        public bool RemoveBracketsInPreProcess = false;
        
        public string TTSModel = "s1"; // fishaudio-1 (v1.6) or s1 (default)//Deprecated
        public float TTSTemperature = 0.9f; // TTS generation temperature (0.7-1.0)//Deprecated
        public float TTSTopP = 0.9f; // TTS generation top_p (0.7-1.0)//Deprecated
        public float TTSSpeed = 1.0f; // TTS playback speed (0.25-4.0)//Deprecated

        public bool ButtonDisplay = true;

        public bool isOnButton = true;
        
        // Generate cooldown (seconds) and queue behavior
        public int GenerateCooldownMiliSeconds = 5000;//Deprecated

        // Per-supplier API keys (string key is supplier enum name)
        public System.Collections.Generic.Dictionary<string, string> SupplierApiKeys = new System.Collections.Generic.Dictionary<string, string>();
        // Per-supplier models (string key is supplier enum name)
        public System.Collections.Generic.Dictionary<string, string> SupplierModels = new System.Collections.Generic.Dictionary<string, string>();

        // Per-supplier basic config values
        public System.Collections.Generic.Dictionary<string, int> SupplierGenerateCooldownMs = new System.Collections.Generic.Dictionary<string, int>();
        public System.Collections.Generic.Dictionary<string, float> SupplierVolume = new System.Collections.Generic.Dictionary<string, float>();
        public System.Collections.Generic.Dictionary<string, float> SupplierTemperature = new System.Collections.Generic.Dictionary<string, float>();
        public System.Collections.Generic.Dictionary<string, float> SupplierTopP = new System.Collections.Generic.Dictionary<string, float>();
        public System.Collections.Generic.Dictionary<string, float> SupplierSpeed = new System.Collections.Generic.Dictionary<string, float>();
        // Per-supplier voice model lists
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceModel>> SupplierVoiceModels = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceModel>>();
        // Per-supplier default voice model id
        public System.Collections.Generic.Dictionary<string, string> SupplierDefaultVoiceModelId = new System.Collections.Generic.Dictionary<string, string>();

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref EnableTTS, "enableTTS", false);
            Scribe_Values.Look(ref FishAudioApiKey, "fishAudioApiKey", "");
            Scribe_Values.Look(ref TTSVolume, "ttsVolume", 0.8f);
            Scribe_Collections.Look(ref VoiceModels, "voiceModels", LookMode.Deep);
            Scribe_Values.Look(ref TTSTranslationLanguage, "ttsTranslationLanguage", "");
            Scribe_Values.Look(ref DefaultVoiceModelId, "defaultVoiceModelId", "");
            Scribe_Values.Look(ref CustomTTSProcessingPrompt, "customTTSProcessingPrompt", "");
            Scribe_Values.Look(ref TTSModel, "ttsModel", "s1");
            Scribe_Values.Look(ref TTSTemperature, "ttsTemperature", 0.9f);
            Scribe_Values.Look(ref TTSTopP, "ttsTopP", 0.9f);
            Scribe_Values.Look(ref TTSVolume, "ttsVolume", DEFAULT_SUPPLIER_VOLUME);
            Scribe_Values.Look(ref TTSSpeed, "ttsSpeed", DEFAULT_SUPPLIER_SPEED);
            Scribe_Values.Look(ref GenerateCooldownMiliSeconds, "generateCooldownMiliSeconds", DEFAULT_GENERATE_COOLDOWN_MS);
            Scribe_Values.Look(ref ButtonDisplay, "buttonDisplay", true);
            Scribe_Values.Look<TTSSupplier>(ref Supplier, "ttsSupplier", TTSSupplier.None);
            Scribe_Collections.Look(ref SupplierApiKeys, "supplierApiKeys", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierModels, "supplierModels", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierGenerateCooldownMs, "supplierGenerateCooldownMs", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVolume, "supplierVolume", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTemperature, "supplierTemperature", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTopP, "supplierTopP", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVoiceModels, "supplierVoiceModels", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref SupplierSpeed, "supplierSpeed", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierDefaultVoiceModelId, "supplierDefaultVoiceModelId", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref PlayerReferenceVoiceModelId, "playerReferenceVoiceModelId", VoiceModel.NONE_MODEL_ID);

            // LLM API configuration
            Scribe_Values.Look<TTSApiProvider>(ref ApiProvider, "apiProvider", TTSApiProvider.DeepSeek);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "deepseek-chat");
            Scribe_Values.Look(ref CustomBaseUrl, "customBaseUrl", "");
            Scribe_Values.Look(ref RemoveBracketsInPreProcess, "removeBracketsInPreProcess", false);

            LoadOldSettings();
        }

        private void LoadOldSettings()
        {
            // Backwards compatibility: if old FishAudioApiKey exists and no entry in SupplierApiKeys, populate it
            if (SupplierApiKeys == null)
            {
                SupplierApiKeys = new System.Collections.Generic.Dictionary<string, string>();
                SupplierApiKeys[TTSSupplier.FishAudio.ToString()] = FishAudioApiKey ?? "";
            }
            // Backwards compatibility: ensure legacy TTSModel is used for FishAudio if no per-supplier model exists
            if (SupplierModels == null)
            {
                SupplierModels = new System.Collections.Generic.Dictionary<string, string>();
                SupplierModels[TTSSupplier.FishAudio.ToString()] = TTSModel ?? "s1";
            }
            // Backwards compatibility: populate per-supplier basic values from legacy fields if missing
            if (SupplierGenerateCooldownMs == null)
            {
                SupplierGenerateCooldownMs = new System.Collections.Generic.Dictionary<string, int>();
                SupplierGenerateCooldownMs[TTSSupplier.FishAudio.ToString()] = GenerateCooldownMiliSeconds;
                SupplierGenerateCooldownMs[TTSSupplier.CosyVoice.ToString()] = DEFAULT_GENERATE_COOLDOWN_MS;
                SupplierGenerateCooldownMs[TTSSupplier.IndexTTS.ToString()] = DEFAULT_GENERATE_COOLDOWN_MS;
            }

            if (SupplierVolume == null)
            {
                SupplierVolume = new System.Collections.Generic.Dictionary<string, float>();
                SupplierVolume[TTSSupplier.FishAudio.ToString()] = TTSVolume;
                SupplierVolume[TTSSupplier.CosyVoice.ToString()] = DEFAULT_SUPPLIER_VOLUME;
                SupplierVolume[TTSSupplier.IndexTTS.ToString()] = DEFAULT_SUPPLIER_VOLUME;
            }

            if (SupplierTemperature == null)
            {
                SupplierTemperature = new System.Collections.Generic.Dictionary<string, float>();
                SupplierTemperature[TTSSupplier.FishAudio.ToString()] = TTSTemperature;
            }

            if (SupplierSpeed == null)
            {
                SupplierSpeed = new System.Collections.Generic.Dictionary<string, float>();
                SupplierSpeed[TTSSupplier.FishAudio.ToString()] = DEFAULT_SUPPLIER_SPEED;
                SupplierSpeed[TTSSupplier.CosyVoice.ToString()] = DEFAULT_SUPPLIER_SPEED;
                SupplierSpeed[TTSSupplier.IndexTTS.ToString()] = DEFAULT_SUPPLIER_SPEED;
            }

            if (SupplierTopP == null)
            {
                SupplierTopP = new System.Collections.Generic.Dictionary<string, float>();
                SupplierTopP[TTSSupplier.FishAudio.ToString()] = TTSTopP;
            }

            if (SupplierVoiceModels == null)
            {
                SupplierVoiceModels = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceModel>>();
                SupplierVoiceModels[TTSSupplier.FishAudio.ToString()] = VoiceModels ?? new System.Collections.Generic.List<VoiceModel>();
                SupplierVoiceModels[TTSSupplier.CosyVoice.ToString()] = GetDefaultVoiceModels(TTSSupplier.CosyVoice);
                SupplierVoiceModels[TTSSupplier.IndexTTS.ToString()] = GetDefaultVoiceModels(TTSSupplier.IndexTTS);
            }

            if (SupplierDefaultVoiceModelId == null)
            {
                SupplierDefaultVoiceModelId = new System.Collections.Generic.Dictionary<string, string>();
                SupplierDefaultVoiceModelId[TTSSupplier.FishAudio.ToString()] = DefaultVoiceModelId ?? "";
                SupplierDefaultVoiceModelId[TTSSupplier.CosyVoice.ToString()] = VoiceModel.NONE_MODEL_ID;
                SupplierDefaultVoiceModelId[TTSSupplier.IndexTTS.ToString()] = VoiceModel.NONE_MODEL_ID;
            }
        }

        public string GetSupplierApiKey(TTSSupplier supplier)
        {
            return SupplierApiKeys.TryGetValue(supplier.ToString());
        }

        public void SetSupplierApiKey(TTSSupplier supplier, string apiKey)
        {
            SupplierApiKeys[supplier.ToString()] = string.IsNullOrEmpty(apiKey) ? null : apiKey;
        }

        public string GetSupplierModel(TTSSupplier supplier)
        {
            return SupplierModels.TryGetValue(supplier.ToString());
        }

        public void SetSupplierModel(TTSSupplier supplier, string model)
        {
            SupplierModels[supplier.ToString()] = model ?? null;
        }

        public System.Collections.Generic.List<VoiceModel> GetSupplierVoiceModels(TTSSupplier supplier)
        {
            return SupplierVoiceModels.TryGetValue(supplier.ToString());
        }

        public void SetSupplierVoiceModels(TTSSupplier supplier, System.Collections.Generic.List<VoiceModel> models)
        {
            SupplierVoiceModels[supplier.ToString()] = models ?? null;
        }

        public string GetSupplierDefaultVoiceModelId(TTSSupplier supplier)
        {
            return SupplierDefaultVoiceModelId.TryGetValue(supplier.ToString());
        }

        public void SetSupplierDefaultVoiceModelId(TTSSupplier supplier, string modelId)
        {
            SupplierDefaultVoiceModelId[supplier.ToString()] = string.IsNullOrEmpty(modelId) ? null : modelId;
        }

        public int GetSupplierGenerateCooldown(TTSSupplier supplier)
        {
            return SupplierGenerateCooldownMs.TryGetValue(supplier.ToString());
        }

        public void SetSupplierGenerateCooldown(TTSSupplier supplier, int ms)
        {
            SupplierGenerateCooldownMs[supplier.ToString()] = ms;
        }

        public float GetSupplierVolume(TTSSupplier supplier)
        {
            return SupplierVolume.TryGetValue(supplier.ToString());
        }

        public void SetSupplierVolume(TTSSupplier supplier, float vol)
        {
            SupplierVolume[supplier.ToString()] = vol;
        }

        public float GetSupplierTemperature(TTSSupplier supplier)
        {
            return SupplierTemperature.TryGetValue(supplier.ToString());
        }

        public void SetSupplierTemperature(TTSSupplier supplier, float t)
        {
            SupplierTemperature[supplier.ToString()] = t;
        }

        public float GetSupplierTopP(TTSSupplier supplier)
        {
            return SupplierTopP.TryGetValue(supplier.ToString());
        }

        public void SetSupplierTopP(TTSSupplier supplier, float p)
        {
            SupplierTopP[supplier.ToString()] = p;
        }

        /// <summary>
        /// Return a default preset voice model list for the given supplier.
        /// CosyVoice and IndexTTS have eight system presets (alex, benjamin, charles, david, anna, bella, claire, diana).
        /// FishAudio has no presets by default.
        /// </summary>
        public static System.Collections.Generic.List<VoiceModel> GetDefaultVoiceModels(TTSSupplier supplier)
        {
            var presets = new System.Collections.Generic.List<VoiceModel>();
            string[] names = new[] { "alex", "benjamin", "charles", "david", "anna", "bella", "claire", "diana" };

            switch (supplier)
            {
                case TTSSupplier.CosyVoice:
                    foreach (var n in names)
                        presets.Add(new VoiceModel($"FunAudioLLM/CosyVoice2-0.5B:{n}", n));
                    break;
                case TTSSupplier.IndexTTS:
                    foreach (var n in names)
                        presets.Add(new VoiceModel($"IndexTeam/IndexTTS-2:{n}", n));
                    break;
                default:
                    // FishAudio: no presets
                    break;
            }

            return presets;
        }

        public float GetSupplierSpeed(TTSSupplier supplier)
        {
            return SupplierSpeed.TryGetValue(supplier.ToString());
        }

        public void SetSupplierSpeed(TTSSupplier supplier, float s)
        {
            SupplierSpeed[supplier.ToString()] = s;
            if (supplier == TTSSupplier.FishAudio)
                TTSSpeed = s;
        }
    }
}
