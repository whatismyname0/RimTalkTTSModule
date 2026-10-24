using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// TTS module settings - independent from main RimTalk settings.
    ///
    /// Schema v2 (2026-10): per-supplier values live in <see cref="Suppliers"/> (one
    /// <see cref="SupplierConfig"/> per key), LLM options in <see cref="Llm"/>, global options in
    /// <see cref="Ui"/>. The public members below are thin accessors so callers (snapshot, UI,
    /// pipeline) did not have to change.
    ///
    /// Only v2 is written. A pre-v2 settings file is imported automatically on load
    /// (<see cref="SettingsMigration"/>); the one-release v1 dual-write was removed.
    /// </summary>
    public class TTSSettings : ModSettings
    {
        public const float DEFAULT_SUPPLIER_VOLUME = 0.8f;
        public const float DEFAULT_SUPPLIER_SPEED = 1.0f;
        public const int DEFAULT_GENERATE_COOLDOWN_MS = 5000;
        public const int DEFAULT_REQUEST_TIMEOUT_SECONDS = 120;
        public const int MAX_REQUEST_TIMEOUT_SECONDS = 600;
        public const int CURRENT_SCHEMA = 2;

        // Missing schema key means a pre-v2 file; loading only warns (migration was removed).
        private int _schema = 1;

        public LlmConfig Llm = new LlmConfig();
        public UiConfig Ui = new UiConfig();
        public List<SupplierConfig> Suppliers = new List<SupplierConfig>();

        // Kept at the top level: both keys already existed in v1 with the same names.
        public List<CustomProviderConfig> CustomProviders = new List<CustomProviderConfig>();
        public string CurrentCustomProviderId = "";

        private TTSSupplier _supplier = TTSSupplier.FishAudio;

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

        public TTSSettings()
        {
            EnsureInitialized();
        }

        // ===== accessors (unchanged public surface) =====================================

        public TTSSupplier Supplier
        {
            get => _supplier;
            set
            {
                if (_supplier != value)
                {
                    _supplier = value;
                    // When supplier changes, the default voice model also changes.
                    string newDefaultVoice = GetSupplierDefaultVoiceModelId(value);
                    PawnVoiceManager.OnDefaultVoiceChanged(newDefaultVoice);
                }
            }
        }

        public bool EnableTTS { get => Ui.EnableTTS; set => Ui.EnableTTS = value; }
        public bool ButtonDisplay { get => Ui.ButtonDisplay; set => Ui.ButtonDisplay = value; }
        public bool ControlButtonDisplay { get => Ui.ControlButtonDisplay; set => Ui.ControlButtonDisplay = value; }
        public bool isOnButton { get => Ui.IsOnButton; set => Ui.IsOnButton = value; }
        public string TTSTranslationLanguage { get => Ui.TranslationLanguage; set => Ui.TranslationLanguage = value ?? ""; }
        public int TtsRequestTimeoutSeconds { get => Ui.TtsRequestTimeoutSeconds; set => Ui.TtsRequestTimeoutSeconds = value; }
        public string PlayerReferenceVoiceModelId { get => Ui.PlayerReferenceVoiceModelId; set => Ui.PlayerReferenceVoiceModelId = value ?? ""; }
        public string CustomTTSProcessingPrompt { get => Ui.CustomTTSProcessingPrompt; set => Ui.CustomTTSProcessingPrompt = value ?? ""; }

        public TTSApiProvider ApiProvider { get => Llm.ApiProvider; set => Llm.ApiProvider = value; }
        public string ApiKey { get => Llm.ApiKey; set => Llm.ApiKey = value ?? ""; }
        public string Model { get => Llm.Model; set => Llm.Model = value ?? ""; }
        public string CustomBaseUrl { get => Llm.CustomBaseUrl; set => Llm.CustomBaseUrl = value ?? ""; }
        public bool RemoveBracketsInPreProcess { get => Llm.RemoveBracketsInPreProcess; set => Llm.RemoveBracketsInPreProcess = value; }

        // ===== persistence ==============================================================

        public override void ExposeData()
        {
            base.ExposeData();

            bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
            bool saving = Scribe.mode == LoadSaveMode.Saving;
            if (!loading && !saving)
            {
                EnsureInitialized();
                return;
            }

            if (saving) _schema = CURRENT_SCHEMA;

            Scribe_Values.Look(ref _schema, "schema", 1);
            Scribe_Values.Look<TTSSupplier>(ref _supplier, "ttsSupplier", TTSSupplier.None);
            Scribe_Collections.Look(ref CustomProviders, "customProviders", LookMode.Deep);
            Scribe_Values.Look(ref CurrentCustomProviderId, "currentCustomProviderId", "");

            Scribe_Deep.Look(ref Llm, "llm");
            Scribe_Deep.Look(ref Ui, "ui");
            Scribe_Collections.Look(ref Suppliers, "suppliers", LookMode.Deep);

            if (loading)
            {
                try
                {
                    // Import-only: read the v1 key layout when the file predates schema v2, then
                    // persist v2 only (no v1 dual-write any more).
                    _legacy.ExposeData();
                    if (_schema < CURRENT_SCHEMA)
                    {
                        SettingsMigration.MigrateV1ToV2(_legacy, this);
                        _schema = CURRENT_SCHEMA;
                        TTSLog.Message($"[RimTalk.TTS] Settings migrated to schema v{CURRENT_SCHEMA} " +
                                       $"(suppliers={Suppliers.Count}, customProviders={CustomProviders.Count})");
                    }
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] Settings migration failed, using defaults: {ex}");
                    _schema = CURRENT_SCHEMA;
                }

                EnsureInitialized();
            }
            else
            {
                EnsureInitialized();
            }
        }

        private readonly LegacySettingsV1 _legacy = new LegacySettingsV1();

        /// <summary>Built-in supplier defaults (fresh entry, or after a removed/renamed custom key).</summary>
        internal static SupplierConfig CreateDefaultSupplier(TTSSupplier kind)
        {
            var cfg = new SupplierConfig
            {
                Key = kind.ToString(),
                Kind = kind,
                Region = "eastus",
            };

            switch (kind)
            {
                case TTSSupplier.FishAudio:
                    cfg.Model = "s2-pro";
                    break;
                case TTSSupplier.CosyVoice:
                    cfg.Model = "FunAudioLLM/CosyVoice2-0.5B";
                    break;
                case TTSSupplier.IndexTTS:
                    cfg.Model = "IndexTeam/IndexTTS-2";
                    break;
            }

            if (kind != TTSSupplier.FishAudio)
            {
                cfg.VoiceModels = GetDefaultVoiceModels(kind);
            }

            cfg.Normalize();
            return cfg;
        }

        /// <summary>Create the supplier entry for an arbitrary key (built-in name or "Custom_{id}").</summary>
        internal static SupplierConfig CreateSupplierForKey(string key)
        {
            TTSSupplier kind;
            if (!string.IsNullOrEmpty(key)
                && Enum.TryParse(key, out kind)
                && kind != TTSSupplier.None)
            {
                return CreateDefaultSupplier(kind);
            }

            return new SupplierConfig
            {
                Key = key ?? "",
                Kind = TTSSupplier.Custom,
                Region = "eastus",
            };
        }

        /// <summary>Create/normalize the v2 structure. Safe to call repeatedly.</summary>
        public void EnsureInitialized()
        {
            if (Llm == null) Llm = new LlmConfig();
            if (Ui == null) Ui = new UiConfig();
            if (Suppliers == null) Suppliers = new List<SupplierConfig>();
            if (CustomProviders == null) CustomProviders = new List<CustomProviderConfig>();

            foreach (TTSSupplier kind in Enum.GetValues(typeof(TTSSupplier)))
            {
                if (kind == TTSSupplier.None) continue;
                string key = kind.ToString();
                if (FindConfig(key) == null)
                {
                    Suppliers.Add(CreateDefaultSupplier(kind));
                }
            }

            foreach (var cfg in Suppliers)
            {
                cfg?.Normalize();
            }

            EnsureCustomProviderDictionaries();
        }

        private SupplierConfig FindConfig(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return Suppliers.Find(c => c != null && c.Key == key);
        }

        internal SupplierConfig GetOrCreateSupplierConfig(string key)
        {
            if (Suppliers == null) Suppliers = new List<SupplierConfig>();
            var cfg = FindConfig(key);
            if (cfg == null)
            {
                cfg = CreateSupplierForKey(key);
                Suppliers.Add(cfg);
            }
            return cfg;
        }

        // ===== per-supplier accessors (enum) =============================================

        public string GetSupplierApiKey(TTSSupplier supplier) => GetOrCreateSupplierConfig(supplier.ToString()).ApiKey ?? "";

        public void SetSupplierApiKey(TTSSupplier supplier, string apiKey) =>
            GetOrCreateSupplierConfig(supplier.ToString()).ApiKey = apiKey ?? "";

        public string GetSupplierModel(TTSSupplier supplier) => GetOrCreateSupplierConfig(supplier.ToString()).Model ?? "";

        public void SetSupplierModel(TTSSupplier supplier, string model) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Model = model ?? "";

        public List<VoiceModel> GetSupplierVoiceModels(TTSSupplier supplier)
        {
            var cfg = GetOrCreateSupplierConfig(supplier.ToString());
            if (cfg.VoiceModels == null) cfg.VoiceModels = new List<VoiceModel>();
            return cfg.VoiceModels;
        }

        public void SetSupplierVoiceModels(TTSSupplier supplier, List<VoiceModel> models) =>
            GetOrCreateSupplierConfig(supplier.ToString()).VoiceModels = models ?? new List<VoiceModel>();

        public string GetSupplierDefaultVoiceModelId(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).DefaultVoiceModelId ?? VoiceModel.NONE_MODEL_ID;

        public void SetSupplierDefaultVoiceModelId(TTSSupplier supplier, string modelId)
        {
            var cfg = GetOrCreateSupplierConfig(supplier.ToString());
            string oldValue = cfg.DefaultVoiceModelId ?? VoiceModel.NONE_MODEL_ID;
            string newValue = string.IsNullOrEmpty(modelId) ? VoiceModel.NONE_MODEL_ID : modelId;
            cfg.DefaultVoiceModelId = newValue;

            // Notify voice manager if default voice changed, pass new value for intelligent cache update
            if (oldValue != newValue)
            {
                PawnVoiceManager.OnDefaultVoiceChanged(newValue);
            }
        }

        public int GetSupplierGenerateCooldown(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).CooldownMs;

        public void SetSupplierGenerateCooldown(TTSSupplier supplier, int ms) =>
            GetOrCreateSupplierConfig(supplier.ToString()).CooldownMs = ms;

        public float GetSupplierVolume(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Volume;

        public void SetSupplierVolume(TTSSupplier supplier, float vol) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Volume = vol;

        public float GetSupplierTemperature(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Temperature;

        public void SetSupplierTemperature(TTSSupplier supplier, float t) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Temperature = t;

        public float GetSupplierTopP(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).TopP;

        public void SetSupplierTopP(TTSSupplier supplier, float p) =>
            GetOrCreateSupplierConfig(supplier.ToString()).TopP = p;

        public float GetSupplierSpeed(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Speed;

        public void SetSupplierSpeed(TTSSupplier supplier, float s) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Speed = s;

        public string GetSupplierRegion(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Region ?? "eastus";

        public void SetSupplierRegion(TTSSupplier supplier, string region) =>
            GetOrCreateSupplierConfig(supplier.ToString()).Region = region ?? "eastus";

        public bool GetSupplierAdvancedMode(TTSSupplier supplier) =>
            GetOrCreateSupplierConfig(supplier.ToString()).AdvancedMode;

        public void SetSupplierAdvancedMode(TTSSupplier supplier, bool enabled) =>
            GetOrCreateSupplierConfig(supplier.ToString()).AdvancedMode = enabled;

        public List<VoiceAssignmentRule> GetSupplierVoiceRules(TTSSupplier supplier)
        {
            var cfg = GetOrCreateSupplierConfig(supplier.ToString());
            if (cfg.Rules == null) cfg.Rules = new List<VoiceAssignmentRule>();
            return cfg.Rules;
        }

        public void SetSupplierVoiceRules(TTSSupplier supplier, List<VoiceAssignmentRule> rules)
        {
            GetOrCreateSupplierConfig(supplier.ToString()).Rules = rules ?? new List<VoiceAssignmentRule>();
            PawnVoiceManager.OnRulesChanged();
        }

        // ===== per-supplier accessors (string key) =======================================

        public string GetSupplierApiKey(string key) => GetOrCreateSupplierConfig(key).ApiKey ?? "";
        public void SetSupplierApiKey(string key, string apiKey) => GetOrCreateSupplierConfig(key).ApiKey = apiKey ?? "";
        public string GetSupplierModel(string key) => GetOrCreateSupplierConfig(key).Model ?? "";
        public void SetSupplierModel(string key, string model) => GetOrCreateSupplierConfig(key).Model = model ?? "";

        public List<VoiceModel> GetSupplierVoiceModels(string key)
        {
            var cfg = GetOrCreateSupplierConfig(key);
            if (cfg.VoiceModels == null) cfg.VoiceModels = new List<VoiceModel>();
            return cfg.VoiceModels;
        }

        public void SetSupplierVoiceModels(string key, List<VoiceModel> models) =>
            GetOrCreateSupplierConfig(key).VoiceModels = models ?? new List<VoiceModel>();

        public string GetSupplierDefaultVoiceModelId(string key) =>
            GetOrCreateSupplierConfig(key).DefaultVoiceModelId ?? VoiceModel.NONE_MODEL_ID;

        public void SetSupplierDefaultVoiceModelId(string key, string modelId)
        {
            var cfg = GetOrCreateSupplierConfig(key);
            string oldValue = cfg.DefaultVoiceModelId ?? VoiceModel.NONE_MODEL_ID;
            string newValue = string.IsNullOrEmpty(modelId) ? VoiceModel.NONE_MODEL_ID : modelId;
            cfg.DefaultVoiceModelId = newValue;
            if (oldValue != newValue) PawnVoiceManager.OnDefaultVoiceChanged(newValue);
        }

        public int GetSupplierGenerateCooldown(string key) => GetOrCreateSupplierConfig(key).CooldownMs;
        public void SetSupplierGenerateCooldown(string key, int ms) => GetOrCreateSupplierConfig(key).CooldownMs = ms;
        public float GetSupplierVolume(string key) => GetOrCreateSupplierConfig(key).Volume;
        public void SetSupplierVolume(string key, float vol) => GetOrCreateSupplierConfig(key).Volume = vol;
        public float GetSupplierTemperature(string key) => GetOrCreateSupplierConfig(key).Temperature;
        public void SetSupplierTemperature(string key, float t) => GetOrCreateSupplierConfig(key).Temperature = t;
        public float GetSupplierTopP(string key) => GetOrCreateSupplierConfig(key).TopP;
        public void SetSupplierTopP(string key, float p) => GetOrCreateSupplierConfig(key).TopP = p;
        public float GetSupplierSpeed(string key) => GetOrCreateSupplierConfig(key).Speed;
        public void SetSupplierSpeed(string key, float s) => GetOrCreateSupplierConfig(key).Speed = s;
        public bool GetSupplierAdvancedMode(string key) => GetOrCreateSupplierConfig(key).AdvancedMode;
        public void SetSupplierAdvancedMode(string key, bool enabled) => GetOrCreateSupplierConfig(key).AdvancedMode = enabled;

        public List<VoiceAssignmentRule> GetSupplierVoiceRules(string key)
        {
            var cfg = GetOrCreateSupplierConfig(key);
            if (cfg.Rules == null) cfg.Rules = new List<VoiceAssignmentRule>();
            return cfg.Rules;
        }

        public void SetSupplierVoiceRules(string key, List<VoiceAssignmentRule> rules)
        {
            GetOrCreateSupplierConfig(key).Rules = rules ?? new List<VoiceAssignmentRule>();
            PawnVoiceManager.OnRulesChanged();
        }

        // ===== misc helpers =============================================================

        /// <summary>
        /// Per-request TTS timeout in seconds, clamped to the supported range. 0 disables the
        /// timeout entirely (a request may then wait forever).
        /// </summary>
        public int GetTtsRequestTimeoutSeconds()
        {
            return Math.Max(0, Math.Min(MAX_REQUEST_TIMEOUT_SECONDS, TtsRequestTimeoutSeconds));
        }

        /// <summary>
        /// Return a default preset voice model list for the given supplier.
        /// CosyVoice and IndexTTS have eight system presets (alex, benjamin, charles, david, anna, bella, claire, diana).
        /// AzureTTS has common neural voices.
        /// FishAudio has no presets by default.
        /// </summary>
        public static List<VoiceModel> GetDefaultVoiceModels(TTSSupplier supplier)
        {
            var presets = new List<VoiceModel>();
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

        // ===== custom providers =========================================================

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
            if (CustomProviders == null) CustomProviders = new List<CustomProviderConfig>();
            var config = new CustomProviderConfig(name, baseUrl);
            CustomProviders.Add(config);
            InitializeCustomProviderDictionaries(config);
            return config;
        }

        /// <summary>Remove a custom provider by ID.</summary>
        public bool RemoveCustomProvider(string id)
        {
            if (CustomProviders == null) return false;
            string key = $"Custom_{id}";
            if (Suppliers != null)
            {
                Suppliers.RemoveAll(c => c != null && c.Key == key);
            }
            // If it's the current custom provider, unselect
            if (CurrentCustomProviderId == id)
                CurrentCustomProviderId = "";
            return CustomProviders.RemoveAll(c => c.Id == id) > 0;
        }

        /// <summary>Initialize the supplier entry of a custom provider.</summary>
        private void InitializeCustomProviderDictionaries(CustomProviderConfig config)
        {
            if (config == null) return;
            string key = config.GetSupplierKey();
            string defaultVoice = !string.IsNullOrWhiteSpace(config.DefaultVoice) ? config.DefaultVoice : "alloy";

            var cfg = GetOrCreateSupplierConfig(key);
            cfg.Kind = TTSSupplier.Custom;
            // Always sync API key/model from config so editor changes take effect immediately
            cfg.ApiKey = config.ApiKey ?? "";
            cfg.Model = config.Model ?? "tts-1";

            if (cfg.VoiceModels == null || cfg.VoiceModels.Count == 0)
            {
                cfg.VoiceModels = new List<VoiceModel> { new VoiceModel(defaultVoice, defaultVoice) };
            }
            // Default voice model ID: use config.DefaultVoice instead of NONE so requests aren't blocked
            if (string.IsNullOrEmpty(cfg.DefaultVoiceModelId) || cfg.DefaultVoiceModelId == VoiceModel.NONE_MODEL_ID)
            {
                cfg.DefaultVoiceModelId = defaultVoice;
            }
        }

        /// <summary>Ensure all custom providers have their supplier entries initialized.</summary>
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
