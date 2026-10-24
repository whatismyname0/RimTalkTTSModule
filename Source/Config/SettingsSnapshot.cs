using RimTalk.TTS.Data;

namespace RimTalk.TTS.Config
{
    /// <summary>
    /// Immutable view of everything a single dialogue line needs from the configuration.
    ///
    /// Captured on the main thread (voice assignment, RimTalk's own active config and the mod
    /// settings may only be read there) and then handed to the background pipeline, which no longer
    /// touches <see cref="TTSSettings"/> or game state.
    /// </summary>
    public sealed class SettingsSnapshot
    {
        public string PawnLabel;
        public string VoiceModelId;
        public string VoiceLanguage;
        public LlmRequestOptions Llm;
        public SupplierOptions Supplier;
        public int RequestTimeoutSeconds;
    }

    /// <summary>Resolved LLM preprocessing endpoint (already flattened from the provider selection).</summary>
    public sealed class LlmRequestOptions
    {
        /// <summary>Preprocessing disabled (provider = Skip): pass the raw text to TTS.</summary>
        public bool Skip;

        public string PromptTemplate;
        public string BaseUrl;
        public string ApiKey;
        public string Model;
        public bool RemoveBrackets;

        /// <summary>Old Fish Audio models read "[]" as SSML; brackets are rewritten to "()".</summary>
        public bool ConvertBracketsToParentheses;
    }

    /// <summary>Resolved TTS supplier values used to build one synthesis request.</summary>
    public sealed class SupplierOptions
    {
        public TTSSettings.TTSSupplier Kind;
        public string SupplierKey;
        public string ApiKey;
        public string Model;
        public int CooldownMs;
        public float Volume;
        public float Speed;
        public float Temperature;
        public float TopP;
    }
}
