using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Global (non per-supplier) options in the v2 settings schema.
    ///
    /// <see cref="IsOnButton"/> stays session-only on purpose: the pre-v2 layout never scribed it,
    /// so persisting it now would change behaviour.
    /// </summary>
    public class UiConfig : IExposable
    {
        public bool EnableTTS;
        public bool ButtonDisplay = true;
        public bool ControlButtonDisplay = true;
        public bool IsOnButton = true;

        public string TranslationLanguage = "";
        public int TtsRequestTimeoutSeconds = TTSSettings.DEFAULT_REQUEST_TIMEOUT_SECONDS;
        public string PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;
        public string CustomTTSProcessingPrompt = "";

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableTTS, "enableTTS", false);
            Scribe_Values.Look(ref ButtonDisplay, "buttonDisplay", true);
            Scribe_Values.Look(ref ControlButtonDisplay, "controlButtonDisplay", true);
            Scribe_Values.Look(ref TranslationLanguage, "ttsTranslationLanguage", "");
            Scribe_Values.Look(ref TtsRequestTimeoutSeconds, "ttsRequestTimeoutSeconds", TTSSettings.DEFAULT_REQUEST_TIMEOUT_SECONDS);
            Scribe_Values.Look(ref PlayerReferenceVoiceModelId, "playerReferenceVoiceModelId", VoiceModel.NONE_MODEL_ID);
            Scribe_Values.Look(ref CustomTTSProcessingPrompt, "customTTSProcessingPrompt", "");
        }
    }
}
