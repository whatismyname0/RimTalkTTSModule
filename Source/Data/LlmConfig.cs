using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>LLM (text preprocessing/translation) options in the v2 settings schema.</summary>
    public class LlmConfig : IExposable
    {
        public TTSApiProvider ApiProvider = TTSApiProvider.DeepSeek;
        public string ApiKey = "";
        public string Model = "deepseek-chat";
        public string CustomBaseUrl = "";
        public bool RemoveBracketsInPreProcess;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ApiProvider, "apiProvider", TTSApiProvider.DeepSeek);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "deepseek-chat");
            Scribe_Values.Look(ref CustomBaseUrl, "customBaseUrl", "");
            Scribe_Values.Look(ref RemoveBracketsInPreProcess, "removeBracketsInPreProcess", false);
        }
    }
}
