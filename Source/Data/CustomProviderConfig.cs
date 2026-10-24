using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Configuration for a user-defined custom TTS provider (OpenAI-compatible).
    /// Stored as a list in TTSSettings and serialized via ExposeData.
    /// </summary>
    public class CustomProviderConfig : IExposable
    {
        /// <summary>Unique identifier for this custom provider (auto-generated GUID)</summary>
        public string Id = "";

        /// <summary>Display name shown in the supplier dropdown</summary>
        public string Name = "";

        /// <summary>Base URL for the TTS API (e.g. "https://api.openai.com/v1")</summary>
        public string BaseUrl = "";

        /// <summary>API endpoint path appended to BaseUrl (default: "/audio/speech")</summary>
        public string EndpointPath = "/audio/speech";

        /// <summary>API key for authentication</summary>
        public string ApiKey = "";

        /// <summary>Model identifier sent in the request (e.g. "tts-1", "tts-1-hd")</summary>
        public string Model = "tts-1";

        /// <summary>Default voice name (e.g. "alloy", "echo", "fable")</summary>
        public string DefaultVoice = "alloy";

        /// <summary>Response audio format (mp3, opus, aac, flac, wav, pcm)</summary>
        public string ResponseFormat = "mp3";

        /// <summary>Whether to send speed parameter</summary>
        public bool EnableSpeed = true;

        /// <summary>Whether to send temperature parameter (non-standard but common)</summary>
        public bool EnableTemperature = false;

        /// <summary>Whether to send top_p parameter (non-standard but common)</summary>
        public bool EnableTopP = false;

        /// <summary>Whether to send emotion/style parameter (non-standard)</summary>
        public bool EnableEmotion = false;

        /// <summary>Custom emotion field name in JSON body (e.g. "emotion", "style")</summary>
        public string EmotionFieldName = "emotion";

        /// <summary>Whether to send instruct_text parameter (for instruct-based TTS)</summary>
        public bool EnableInstructText = false;

        /// <summary>Custom instruct text field name (e.g. "instruct_text", "prompt")</summary>
        public string InstructTextFieldName = "instruct_text";

        /// <summary>Whether to use custom JSON request body template</summary>
        public bool UseCustomRequestBody = false;

        /// <summary>
        /// Custom JSON request body template. Placeholders:
        /// {model}, {input}, {voice}, {speed}, {response_format},
        /// {temperature}, {top_p}, {emotion}, {instruct_text}, {api_key}
        /// </summary>
        public string CustomRequestBodyTemplate = "";

        /// <summary>Custom HTTP header name for API key (default: "Authorization")</summary>
        public string AuthHeaderName = "Authorization";

        /// <summary>Custom HTTP header value prefix for API key (default: "Bearer ")</summary>
        public string AuthHeaderPrefix = "Bearer ";

        /// <summary>Whether the provider needs an API key</summary>
        public bool RequiresApiKey = true;

        /// <summary>Custom streaming mode</summary>
        public bool EnableStreaming = false;

        /// <summary>Connection timeout in seconds</summary>
        public int TimeoutSeconds = 30;

        public CustomProviderConfig()
        {
            Id = System.Guid.NewGuid().ToString("N");
        }

        public CustomProviderConfig(string name, string baseUrl) : this()
        {
            Name = name;
            BaseUrl = baseUrl;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", "");
            Scribe_Values.Look(ref Name, "name", "");
            Scribe_Values.Look(ref BaseUrl, "baseUrl", "");
            Scribe_Values.Look(ref EndpointPath, "endpointPath", "/audio/speech");
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "tts-1");
            Scribe_Values.Look(ref DefaultVoice, "defaultVoice", "alloy");
            Scribe_Values.Look(ref ResponseFormat, "responseFormat", "mp3");
            Scribe_Values.Look(ref EnableSpeed, "enableSpeed", true);
            Scribe_Values.Look(ref EnableTemperature, "enableTemperature", false);
            Scribe_Values.Look(ref EnableTopP, "enableTopP", false);
            Scribe_Values.Look(ref EnableEmotion, "enableEmotion", false);
            Scribe_Values.Look(ref EmotionFieldName, "emotionFieldName", "emotion");
            Scribe_Values.Look(ref EnableInstructText, "enableInstructText", false);
            Scribe_Values.Look(ref InstructTextFieldName, "instructTextFieldName", "instruct_text");
            Scribe_Values.Look(ref UseCustomRequestBody, "useCustomRequestBody", false);
            Scribe_Values.Look(ref CustomRequestBodyTemplate, "customRequestBodyTemplate", "");
            Scribe_Values.Look(ref AuthHeaderName, "authHeaderName", "Authorization");
            Scribe_Values.Look(ref AuthHeaderPrefix, "authHeaderPrefix", "Bearer ");
            Scribe_Values.Look(ref RequiresApiKey, "requiresApiKey", true);
            Scribe_Values.Look(ref EnableStreaming, "enableStreaming", false);
            Scribe_Values.Look(ref TimeoutSeconds, "timeoutSeconds", 30);
        }

        /// <summary>
        /// Get the supplier key used in per-supplier dictionaries.
        /// Format: "Custom_{Id}" to avoid collision with built-in supplier enum names.
        /// </summary>
        public string GetSupplierKey() => $"Custom_{Id}";

        /// <summary>
        /// Get the full URL for the TTS endpoint.
        /// </summary>
        public string GetFullUrl()
        {
            string baseUrl = (BaseUrl ?? "").TrimEnd('/');
            string endpoint = (EndpointPath ?? "/audio/speech");
            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;
            return baseUrl + endpoint;
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(BaseUrl);
        }

        public string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(Name) ? "(Unnamed Custom Provider)" : Name;
        }
    }
}
