using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// TTS module settings - independent from main RimTalk settings
    /// </summary>
    public class TTSSettings : ModSettings
    {
        // TTS Configuration
        public bool EnableTTS = false;
        public string FishAudioApiKey = "";
        public float TTSVolume = 0.8f;
        public List<VoiceModel> VoiceModels = new();
        public string TTSTranslationLanguage = "";
        public string DefaultVoiceModelId = "";
        
        // LLM API Configuration (for text processing/translation)
        public TTSApiProvider ApiProvider = TTSApiProvider.DeepSeek;
        public string ApiKey = "";
        public string Model = "deepseek-chat";
        public string CustomBaseUrl = ""; // For custom provider
        
        // Custom TTS processing prompt (empty = use default from TTSConstant)
        public string CustomTTSProcessingPrompt = "";
        
        public string TTSModel = "s1"; // fishaudio-1 (v1.6) or s1 (default)
        public float TTSTemperature = 0.9f; // TTS generation temperature (0.7-1.0)
        public float TTSTopP = 0.9f; // TTS generation top_p (0.7-1.0)

        public bool ButtonDisplay = true;

        public bool isOnButton = true;
        
        // Generate cooldown (seconds) and queue behavior
        public int GenerateCooldownMiliSeconds = 5000;

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
            Scribe_Values.Look(ref ButtonDisplay, "buttonDisplay", true);
            Scribe_Values.Look(ref GenerateCooldownMiliSeconds, "generateCooldownMiliSeconds", 5000);
            
            // LLM API configuration
            Scribe_Values.Look<TTSApiProvider>(ref ApiProvider, "apiProvider", TTSApiProvider.DeepSeek);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "deepseek-chat");
            Scribe_Values.Look(ref CustomBaseUrl, "customBaseUrl", "");

            // Initialize collections if null after loading
            if (VoiceModels == null)
                VoiceModels = new List<VoiceModel>();
        }
    }
}
