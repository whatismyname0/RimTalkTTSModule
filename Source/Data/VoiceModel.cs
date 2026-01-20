using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Represents a TTS voice model configuration
    /// </summary>
    public class VoiceModel : IExposable
    {
        public const string NONE_MODEL_ID = "NONE";
        public const string RULE_BASED_MODEL_ID = "RULE_BASED";
        public const string DEFAULT_MODEL_ID = "DEFAULT";
        
        public string ModelId = "";
        public string ModelName = "";

        public VoiceModel()
        {
        }

        public VoiceModel(string modelId, string modelName)
        {
            ModelId = modelId;
            ModelName = modelName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ModelId, "modelId", "");
            Scribe_Values.Look(ref ModelName, "modelName", "");
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ModelId);
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(ModelName))
                return ModelName;
            return ModelId;
        }
    }
}
