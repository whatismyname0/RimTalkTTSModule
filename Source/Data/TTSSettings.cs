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
            IndexTTS,
            AzureTTS,
            EdgeTTS,
            GeminiTTS,
            Custom
        }

        // Default constants (use these instead of deprecated legacy fields)
        public const float DEFAULT_SUPPLIER_VOLUME = 0.8f;
        public const float DEFAULT_SUPPLIER_SPEED = 1.0f;
        public const int DEFAULT_GENERATE_COOLDOWN_MS = 5000;

        // Selected TTS supplier implementation
        private TTSSupplier _supplier = TTSSupplier.FishAudio;
        public TTSSupplier Supplier
        {
            get => _supplier;
            set
            {
                if (_supplier != value)
                {
                    _supplier = value;
                    // When supplier changes, the default voice model also changes
                    // Update cache for all pawns using default voice with new supplier's default
                    string newDefaultVoice = GetSupplierDefaultVoiceModelId(value);
                    PawnVoiceManager.OnDefaultVoiceChanged(newDefaultVoice);
                }
            }
        }

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
        
        public string TTSModel = "s2-pro"; // fishaudio-1 (v1.6) or s1 (default)//Deprecated
        public float TTSTemperature = 0.9f; // TTS generation temperature (0.7-1.0)//Deprecated
        public float TTSTopP = 0.9f; // TTS generation top_p (0.7-1.0)//Deprecated
        public float TTSSpeed = 1.0f; // TTS playback speed (0.25-4.0)//Deprecated

        public bool ButtonDisplay = true;

        public bool ControlButtonDisplay = true;

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
        // Per-supplier region (for Azure TTS)
        public System.Collections.Generic.Dictionary<string, string> SupplierRegion = new System.Collections.Generic.Dictionary<string, string>();

        // Advanced mode for default voice assignment
        public System.Collections.Generic.Dictionary<string, bool> SupplierAdvancedMode = new System.Collections.Generic.Dictionary<string, bool>();
        // Per-supplier voice assignment rules (advanced mode)
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceAssignmentRule>> SupplierVoiceRules = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceAssignmentRule>>();

        // Custom TTS providers list
        public System.Collections.Generic.List<CustomProviderConfig> CustomProviders = new System.Collections.Generic.List<CustomProviderConfig>();
        // Currently selected custom provider ID (used when Supplier == Custom)
        public string CurrentCustomProviderId = "";

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
            Scribe_Values.Look(ref TTSModel, "ttsModel", "s2-pro");
            Scribe_Values.Look(ref TTSTemperature, "ttsTemperature", 0.9f);
            Scribe_Values.Look(ref TTSTopP, "ttsTopP", 0.9f);
            Scribe_Values.Look(ref TTSVolume, "ttsVolume", DEFAULT_SUPPLIER_VOLUME);
            Scribe_Values.Look(ref TTSSpeed, "ttsSpeed", DEFAULT_SUPPLIER_SPEED);
            Scribe_Values.Look(ref GenerateCooldownMiliSeconds, "generateCooldownMiliSeconds", DEFAULT_GENERATE_COOLDOWN_MS);
            Scribe_Values.Look(ref ButtonDisplay, "buttonDisplay", true);
            Scribe_Values.Look(ref ControlButtonDisplay, "controlButtonDisplay", true);
            Scribe_Values.Look<TTSSupplier>(ref _supplier, "ttsSupplier", TTSSupplier.None);
            Scribe_Collections.Look(ref SupplierApiKeys, "supplierApiKeys", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierModels, "supplierModels", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierGenerateCooldownMs, "supplierGenerateCooldownMs", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVolume, "supplierVolume", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTemperature, "supplierTemperature", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierTopP, "supplierTopP", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVoiceModels, "supplierVoiceModels", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref SupplierSpeed, "supplierSpeed", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierDefaultVoiceModelId, "supplierDefaultVoiceModelId", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierRegion, "supplierRegion", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierAdvancedMode, "supplierAdvancedMode", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SupplierVoiceRules, "supplierVoiceRules", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref PlayerReferenceVoiceModelId, "playerReferenceVoiceModelId", VoiceModel.NONE_MODEL_ID);

            // LLM API configuration
            Scribe_Values.Look<TTSApiProvider>(ref ApiProvider, "apiProvider", TTSApiProvider.DeepSeek);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "deepseek-chat");
            Scribe_Values.Look(ref CustomBaseUrl, "customBaseUrl", "");
            Scribe_Values.Look(ref RemoveBracketsInPreProcess, "removeBracketsInPreProcess", false);

            // Custom providers
            Scribe_Collections.Look(ref CustomProviders, "customProviders", LookMode.Deep);
            Scribe_Values.Look(ref CurrentCustomProviderId, "currentCustomProviderId", "");
            if (CustomProviders == null) CustomProviders = new System.Collections.Generic.List<CustomProviderConfig>();

            LoadOldSettings();
        }

        private void LoadOldSettings()
        {
            // Initialize dictionaries if null (backwards compatibility)
            InitializeDictionaryIfNull(ref SupplierApiKeys, (s) => s == TTSSupplier.FishAudio ? (FishAudioApiKey ?? "") : "");
            InitializeDictionaryIfNull(ref SupplierModels, (s) => s == TTSSupplier.FishAudio ? (TTSModel ?? "s2-pro") : "");
            // SiliconFlow rejects requests with an empty "model". Keep usable defaults for
            // the CosyVoice/IndexTTS suppliers while preserving any custom model id.
            EnsureSupplierModelDefault(TTSSupplier.CosyVoice, "FunAudioLLM/CosyVoice2-0.5B");
            EnsureSupplierModelDefault(TTSSupplier.IndexTTS, "IndexTeam/IndexTTS-2");
            InitializeDictionaryIfNull(ref SupplierGenerateCooldownMs, (s) => s == TTSSupplier.FishAudio ? GenerateCooldownMiliSeconds : DEFAULT_GENERATE_COOLDOWN_MS);
            InitializeDictionaryIfNull(ref SupplierVolume, (s) => s == TTSSupplier.FishAudio ? TTSVolume : DEFAULT_SUPPLIER_VOLUME);
            InitializeDictionaryIfNull(ref SupplierTemperature, (s) => s == TTSSupplier.FishAudio ? TTSTemperature : 0.9f);
            InitializeDictionaryIfNull(ref SupplierSpeed, (s) => DEFAULT_SUPPLIER_SPEED);
            InitializeDictionaryIfNull(ref SupplierTopP, (s) => s == TTSSupplier.FishAudio ? TTSTopP : 0.9f);
            
            if (SupplierVoiceModels == null)
            {
                SupplierVoiceModels = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceModel>>();
                foreach (TTSSupplier supplier in System.Enum.GetValues(typeof(TTSSupplier)))
                {
                    if (supplier == TTSSupplier.None) continue;
                    SupplierVoiceModels[supplier.ToString()] = supplier == TTSSupplier.FishAudio 
                        ? (VoiceModels ?? new System.Collections.Generic.List<VoiceModel>())
                        : GetDefaultVoiceModels(supplier);
                }
            }

            InitializeDictionaryIfNull(ref SupplierDefaultVoiceModelId, (s) => s == TTSSupplier.FishAudio ? (DefaultVoiceModelId ?? "") : VoiceModel.NONE_MODEL_ID);
            
            if (SupplierRegion == null)
            {
                SupplierRegion = new System.Collections.Generic.Dictionary<string, string>();
                SupplierRegion[TTSSupplier.AzureTTS.ToString()] = "eastus";
            }

            if (SupplierAdvancedMode == null)
            {
                SupplierAdvancedMode = new System.Collections.Generic.Dictionary<string, bool>();
                foreach (TTSSupplier supplier in System.Enum.GetValues(typeof(TTSSupplier)))
                {
                    if (supplier == TTSSupplier.None) continue;
                    SupplierAdvancedMode[supplier.ToString()] = false;
                }
            }

            if (SupplierVoiceRules == null)
            {
                SupplierVoiceRules = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VoiceAssignmentRule>>();
                foreach (TTSSupplier supplier in System.Enum.GetValues(typeof(TTSSupplier)))
                {
                    if (supplier == TTSSupplier.None) continue;
                    SupplierVoiceRules[supplier.ToString()] = new System.Collections.Generic.List<VoiceAssignmentRule>();
                }
            }
        }

        /// <summary>
        /// Helper to initialize supplier dictionaries from legacy settings
        /// </summary>
        private void InitializeDictionaryIfNull<T>(ref System.Collections.Generic.Dictionary<string, T> dict, System.Func<TTSSupplier, T> getValue)
        {
            if (dict != null) return;
            
            dict = new System.Collections.Generic.Dictionary<string, T>();
            foreach (TTSSupplier supplier in System.Enum.GetValues(typeof(TTSSupplier)))
            {
                if (supplier == TTSSupplier.None) continue;
                dict[supplier.ToString()] = getValue(supplier);
            }
        }

        /// <summary>
        /// Ensure a supplier has a non-empty model id, falling back to the built-in model.
        /// </summary>
        private void EnsureSupplierModelDefault(TTSSupplier supplier, string defaultModel)
        {
            if (SupplierModels == null) return;
            string key = supplier.ToString();
            if (!SupplierModels.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                SupplierModels[key] = defaultModel;
            }
        }

        public string GetSupplierApiKey(TTSSupplier supplier)
        {
            return SupplierApiKeys.TryGetValue(supplier.ToString(), out var value) ? value : string.Empty;
        }

        public void SetSupplierApiKey(TTSSupplier supplier, string apiKey)
        {
            SupplierApiKeys[supplier.ToString()] = apiKey ?? string.Empty;
        }

        public string GetSupplierModel(TTSSupplier supplier)
        {
            return SupplierModels.TryGetValue(supplier.ToString(), out var value) ? value : string.Empty;
        }

        public void SetSupplierModel(TTSSupplier supplier, string model)
        {
            SupplierModels[supplier.ToString()] = model ?? string.Empty;
        }

        public System.Collections.Generic.List<VoiceModel> GetSupplierVoiceModels(TTSSupplier supplier)
        {
            return SupplierVoiceModels.TryGetValue(supplier.ToString(), out var value) ? value : new System.Collections.Generic.List<VoiceModel>();
        }

        public void SetSupplierVoiceModels(TTSSupplier supplier, System.Collections.Generic.List<VoiceModel> models)
        {
            SupplierVoiceModels[supplier.ToString()] = models ?? new System.Collections.Generic.List<VoiceModel>();
        }

        public string GetSupplierDefaultVoiceModelId(TTSSupplier supplier)
        {
            return SupplierDefaultVoiceModelId.TryGetValue(supplier.ToString(), out var value) ? value : VoiceModel.NONE_MODEL_ID;
        }

        public void SetSupplierDefaultVoiceModelId(TTSSupplier supplier, string modelId)
        {
            string oldValue = GetSupplierDefaultVoiceModelId(supplier);
            string newValue = string.IsNullOrEmpty(modelId) ? VoiceModel.NONE_MODEL_ID : modelId;
            
            SupplierDefaultVoiceModelId[supplier.ToString()] = newValue;
            
            // Notify voice manager if default voice changed, pass new value for intelligent cache update
            if (oldValue != newValue)
            {
                PawnVoiceManager.OnDefaultVoiceChanged(newValue);
            }
        }

        public int GetSupplierGenerateCooldown(TTSSupplier supplier)
        {
            return SupplierGenerateCooldownMs.TryGetValue(supplier.ToString(), out var value) ? value : DEFAULT_GENERATE_COOLDOWN_MS;
        }

        public void SetSupplierGenerateCooldown(TTSSupplier supplier, int ms)
        {
            SupplierGenerateCooldownMs[supplier.ToString()] = ms;
        }

        public float GetSupplierVolume(TTSSupplier supplier)
        {
            return SupplierVolume.TryGetValue(supplier.ToString(), out var value) ? value : DEFAULT_SUPPLIER_VOLUME;
        }

        public void SetSupplierVolume(TTSSupplier supplier, float vol)
        {
            SupplierVolume[supplier.ToString()] = vol;
        }

        public float GetSupplierTemperature(TTSSupplier supplier)
        {
            return SupplierTemperature.TryGetValue(supplier.ToString(), out var value) ? value : 0.9f;
        }

        public void SetSupplierTemperature(TTSSupplier supplier, float t)
        {
            SupplierTemperature[supplier.ToString()] = t;
        }

        public float GetSupplierTopP(TTSSupplier supplier)
        {
            return SupplierTopP.TryGetValue(supplier.ToString(), out var value) ? value : 0.9f;
        }

        public void SetSupplierTopP(TTSSupplier supplier, float p)
        {
            SupplierTopP[supplier.ToString()] = p;
        }

        /// <summary>
        /// Return a default preset voice model list for the given supplier.
        /// CosyVoice and IndexTTS have eight system presets (alex, benjamin, charles, david, anna, bella, claire, diana).
        /// AzureTTS has common neural voices.
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
                case TTSSupplier.AzureTTS:
                case TTSSupplier.EdgeTTS:
                    presets.Add(new VoiceModel("en-US-JennyNeural", "Jenny (US, Female)"));
                    presets.Add(new VoiceModel("en-US-GuyNeural", "Guy (US, Male)"));
                    presets.Add(new VoiceModel("en-US-AriaNeural", "Aria (US, Female)"));
                    presets.Add(new VoiceModel("en-US-DavisNeural", "Davis (US, Male)"));
                    presets.Add(new VoiceModel("en-GB-SoniaNeural", "Sonia (UK, Female)"));
                    presets.Add(new VoiceModel("en-GB-RyanNeural", "Ryan (UK, Male)"));
                    presets.Add(new VoiceModel("zh-CN-XiaoxiaoNeural", "Xiaoxiao (CN, Female)"));
                    presets.Add(new VoiceModel("zh-CN-YunxiNeural", "Yunxi (CN, Male)"));
                    break;
                case TTSSupplier.GeminiTTS:
                    // Gemini TTS: 8 common voices (selected from 30 available)
                    presets.Add(new VoiceModel("Kore", "Kore (Firm)"));
                    presets.Add(new VoiceModel("Puck", "Puck (Upbeat)"));
                    presets.Add(new VoiceModel("Aoede", "Aoede (Breezy)"));
                    presets.Add(new VoiceModel("Enceladus", "Enceladus (Breathy)"));
                    presets.Add(new VoiceModel("Charon", "Charon (Informative)"));
                    presets.Add(new VoiceModel("Fenrir", "Fenrir (Excitable)"));
                    presets.Add(new VoiceModel("Leda", "Leda (Youthful)"));
                    presets.Add(new VoiceModel("Callirrhoe", "Callirrhoe (Easy-going)"));
                    break;
                default:
                    // FishAudio: no presets
                    break;
            }

            return presets;
        }

        public float GetSupplierSpeed(TTSSupplier supplier)
        {
            return SupplierSpeed.TryGetValue(supplier.ToString(), out var value) ? value : DEFAULT_SUPPLIER_SPEED;
        }

        public void SetSupplierSpeed(TTSSupplier supplier, float s)
        {
            SupplierSpeed[supplier.ToString()] = s;
        }

        public string GetSupplierRegion(TTSSupplier supplier)
        {
            return SupplierRegion.TryGetValue(supplier.ToString(), out var value) ? value : "eastus";
        }

        public void SetSupplierRegion(TTSSupplier supplier, string region)
        {
            SupplierRegion[supplier.ToString()] = region ?? "eastus";
        }

        public bool GetSupplierAdvancedMode(TTSSupplier supplier)
        {
            return SupplierAdvancedMode.TryGetValue(supplier.ToString(), out var value) && value;
        }

        public void SetSupplierAdvancedMode(TTSSupplier supplier, bool enabled)
        {
            SupplierAdvancedMode[supplier.ToString()] = enabled;
        }

        public System.Collections.Generic.List<VoiceAssignmentRule> GetSupplierVoiceRules(TTSSupplier supplier)
        {
            return SupplierVoiceRules.TryGetValue(supplier.ToString(), out var value) ? value : new System.Collections.Generic.List<VoiceAssignmentRule>();
        }

        public void SetSupplierVoiceRules(TTSSupplier supplier, System.Collections.Generic.List<VoiceAssignmentRule> rules)
        {
            SupplierVoiceRules[supplier.ToString()] = rules ?? new System.Collections.Generic.List<VoiceAssignmentRule>();
            
            // Notify voice manager that rules changed
            PawnVoiceManager.OnRulesChanged();
        }

        // ===== Custom Provider Helpers =====

        /// <summary>
        /// Get the effective supplier key string for dictionary lookups.
        /// For built-in suppliers: enum name. For custom: "Custom_{Id}".
        /// </summary>
        public string GetCurrentSupplierKey()
        {
            if (Supplier == TTSSupplier.Custom)
            {
                var cfg = GetCurrentCustomProvider();
                return cfg?.GetSupplierKey() ?? "Custom";
            }
            return Supplier.ToString();
        }

        /// <summary>Get the currently selected custom provider config, or null.</summary>
        public CustomProviderConfig GetCurrentCustomProvider()
        {
            if (string.IsNullOrEmpty(CurrentCustomProviderId) || CustomProviders == null) return null;
            return CustomProviders.Find(c => c.Id == CurrentCustomProviderId);
        }

        /// <summary>Get a custom provider by its ID.</summary>
        public CustomProviderConfig GetCustomProviderById(string id)
        {
            if (string.IsNullOrEmpty(id) || CustomProviders == null) return null;
            return CustomProviders.Find(c => c.Id == id);
        }

        /// <summary>Add a new custom provider and return it.</summary>
        public CustomProviderConfig AddCustomProvider(string name, string baseUrl)
        {
            if (CustomProviders == null) CustomProviders = new System.Collections.Generic.List<CustomProviderConfig>();
            var config = new CustomProviderConfig(name, baseUrl);
            CustomProviders.Add(config);
            // Initialize per-supplier dictionaries for this custom provider
            InitializeCustomProviderDictionaries(config);
            return config;
        }

        /// <summary>Remove a custom provider by ID.</summary>
        public bool RemoveCustomProvider(string id)
        {
            if (CustomProviders == null) return false;
            string key = $"Custom_{id}";
            // Clean up per-supplier dictionaries
            SupplierApiKeys.Remove(key);
            SupplierModels.Remove(key);
            SupplierGenerateCooldownMs.Remove(key);
            SupplierVolume.Remove(key);
            SupplierTemperature.Remove(key);
            SupplierTopP.Remove(key);
            SupplierSpeed.Remove(key);
            SupplierVoiceModels.Remove(key);
            SupplierDefaultVoiceModelId.Remove(key);
            SupplierRegion.Remove(key);
            SupplierAdvancedMode.Remove(key);
            SupplierVoiceRules.Remove(key);
            // If it's the current custom provider, unselect
            if (CurrentCustomProviderId == id)
                CurrentCustomProviderId = "";
            return CustomProviders.RemoveAll(c => c.Id == id) > 0;
        }

        /// <summary>Initialize per-supplier dictionaries for a new custom provider.</summary>
        private void InitializeCustomProviderDictionaries(CustomProviderConfig config)
        {
            string key = config.GetSupplierKey();
            string defaultVoice = !string.IsNullOrWhiteSpace(config.DefaultVoice) ? config.DefaultVoice : "alloy";

            // Always sync API key from config so editor changes take effect immediately
            SupplierApiKeys[key] = config.ApiKey ?? "";
            // Always sync model from config
            SupplierModels[key] = config.Model ?? "tts-1";

            if (!SupplierGenerateCooldownMs.ContainsKey(key)) SupplierGenerateCooldownMs[key] = DEFAULT_GENERATE_COOLDOWN_MS;
            if (!SupplierVolume.ContainsKey(key)) SupplierVolume[key] = DEFAULT_SUPPLIER_VOLUME;
            if (!SupplierTemperature.ContainsKey(key)) SupplierTemperature[key] = 0.9f;
            if (!SupplierTopP.ContainsKey(key)) SupplierTopP[key] = 0.9f;
            if (!SupplierSpeed.ContainsKey(key)) SupplierSpeed[key] = DEFAULT_SUPPLIER_SPEED;
            if (!SupplierAdvancedMode.ContainsKey(key)) SupplierAdvancedMode[key] = false;
            if (!SupplierVoiceRules.ContainsKey(key)) SupplierVoiceRules[key] = new System.Collections.Generic.List<VoiceAssignmentRule>();

            // Initialize voice model list with the default voice so pawns can use it immediately
            if (!SupplierVoiceModels.ContainsKey(key) || SupplierVoiceModels[key] == null || SupplierVoiceModels[key].Count == 0)
            {
                var initialList = new System.Collections.Generic.List<VoiceModel>
                {
                    new VoiceModel(defaultVoice, defaultVoice)
                };
                SupplierVoiceModels[key] = initialList;
            }

            // Default voice model ID: use config.DefaultVoice instead of NONE so requests aren't blocked
            if (!SupplierDefaultVoiceModelId.ContainsKey(key) || SupplierDefaultVoiceModelId[key] == VoiceModel.NONE_MODEL_ID)
            {
                SupplierDefaultVoiceModelId[key] = defaultVoice;
            }
        }

        // ===== String-key overloads for custom supplier dictionary access =====

        public string GetSupplierApiKey(string key) => SupplierApiKeys.TryGetValue(key, out var v) ? v : string.Empty;
        public void SetSupplierApiKey(string key, string apiKey) => SupplierApiKeys[key] = apiKey ?? string.Empty;
        public string GetSupplierModel(string key) => SupplierModels.TryGetValue(key, out var v) ? v : string.Empty;
        public void SetSupplierModel(string key, string model) => SupplierModels[key] = model ?? string.Empty;
        public System.Collections.Generic.List<VoiceModel> GetSupplierVoiceModels(string key) => SupplierVoiceModels.TryGetValue(key, out var v) ? v : new System.Collections.Generic.List<VoiceModel>();
        public void SetSupplierVoiceModels(string key, System.Collections.Generic.List<VoiceModel> models) => SupplierVoiceModels[key] = models ?? new System.Collections.Generic.List<VoiceModel>();
        public string GetSupplierDefaultVoiceModelId(string key) => SupplierDefaultVoiceModelId.TryGetValue(key, out var v) ? v : VoiceModel.NONE_MODEL_ID;
        public void SetSupplierDefaultVoiceModelId(string key, string modelId)
        {
            string oldValue = GetSupplierDefaultVoiceModelId(key);
            string newValue = string.IsNullOrEmpty(modelId) ? VoiceModel.NONE_MODEL_ID : modelId;
            SupplierDefaultVoiceModelId[key] = newValue;
            if (oldValue != newValue) PawnVoiceManager.OnDefaultVoiceChanged(newValue);
        }
        public int GetSupplierGenerateCooldown(string key) => SupplierGenerateCooldownMs.TryGetValue(key, out var v) ? v : DEFAULT_GENERATE_COOLDOWN_MS;
        public void SetSupplierGenerateCooldown(string key, int ms) => SupplierGenerateCooldownMs[key] = ms;
        public float GetSupplierVolume(string key) => SupplierVolume.TryGetValue(key, out var v) ? v : DEFAULT_SUPPLIER_VOLUME;
        public void SetSupplierVolume(string key, float vol) => SupplierVolume[key] = vol;
        public float GetSupplierTemperature(string key) => SupplierTemperature.TryGetValue(key, out var v) ? v : 0.9f;
        public void SetSupplierTemperature(string key, float t) => SupplierTemperature[key] = t;
        public float GetSupplierTopP(string key) => SupplierTopP.TryGetValue(key, out var v) ? v : 0.9f;
        public void SetSupplierTopP(string key, float p) => SupplierTopP[key] = p;
        public float GetSupplierSpeed(string key) => SupplierSpeed.TryGetValue(key, out var v) ? v : DEFAULT_SUPPLIER_SPEED;
        public void SetSupplierSpeed(string key, float s) => SupplierSpeed[key] = s;
        public bool GetSupplierAdvancedMode(string key) => SupplierAdvancedMode.TryGetValue(key, out var v) && v;
        public void SetSupplierAdvancedMode(string key, bool enabled) => SupplierAdvancedMode[key] = enabled;
        public System.Collections.Generic.List<VoiceAssignmentRule> GetSupplierVoiceRules(string key) => SupplierVoiceRules.TryGetValue(key, out var v) ? v : new System.Collections.Generic.List<VoiceAssignmentRule>();
        public void SetSupplierVoiceRules(string key, System.Collections.Generic.List<VoiceAssignmentRule> rules)
        {
            SupplierVoiceRules[key] = rules ?? new System.Collections.Generic.List<VoiceAssignmentRule>();
            PawnVoiceManager.OnRulesChanged();
        }

        /// <summary>Ensure all custom providers have their dictionary entries initialized.</summary>
        public void EnsureCustomProviderDictionaries()
        {
            if (CustomProviders == null) return;
            foreach (var cfg in CustomProviders)
            {
                if (cfg == null) continue;
                InitializeCustomProviderDictionaries(cfg);
            }
        }
    }
}
