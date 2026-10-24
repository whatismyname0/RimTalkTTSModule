using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Per-supplier configuration stored in the v2 settings schema.
    ///
    /// <see cref="Key"/> is the dictionary key used by the rest of the code: a
    /// <see cref="TTSSettings.TTSSupplier"/> name for built-in suppliers, or "Custom_{id}" for
    /// custom providers. Full name is part of the persistence contract (frozen).
    /// </summary>
    public class SupplierConfig : IExposable
    {
        public string Key = "";
        public TTSSettings.TTSSupplier Kind;

        public string ApiKey = "";
        public string Model = "";
        public string Region = "eastus";

        public int CooldownMs = TTSSettings.DEFAULT_GENERATE_COOLDOWN_MS;
        public float Volume = TTSSettings.DEFAULT_SUPPLIER_VOLUME;
        public float Temperature = 0.9f;
        public float TopP = 0.9f;
        public float Speed = TTSSettings.DEFAULT_SUPPLIER_SPEED;

        public string DefaultVoiceModelId = VoiceModel.NONE_MODEL_ID;
        public List<VoiceModel> VoiceModels = new List<VoiceModel>();
        public bool AdvancedMode;
        public List<VoiceAssignmentRule> Rules = new List<VoiceAssignmentRule>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key", "");
            Scribe_Values.Look(ref Kind, "kind", TTSSettings.TTSSupplier.None);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref Model, "model", "");
            Scribe_Values.Look(ref Region, "region", "eastus");
            Scribe_Values.Look(ref CooldownMs, "cooldownMs", TTSSettings.DEFAULT_GENERATE_COOLDOWN_MS);
            Scribe_Values.Look(ref Volume, "volume", TTSSettings.DEFAULT_SUPPLIER_VOLUME);
            Scribe_Values.Look(ref Temperature, "temperature", 0.9f);
            Scribe_Values.Look(ref TopP, "topP", 0.9f);
            Scribe_Values.Look(ref Speed, "speed", TTSSettings.DEFAULT_SUPPLIER_SPEED);
            Scribe_Values.Look(ref DefaultVoiceModelId, "defaultVoiceModelId", VoiceModel.NONE_MODEL_ID);
            Scribe_Collections.Look(ref VoiceModels, "voiceModels", LookMode.Deep);
            Scribe_Values.Look(ref AdvancedMode, "advancedMode", false);
            Scribe_Collections.Look(ref Rules, "rules", LookMode.Deep);

            Normalize();
        }

        public void Normalize()
        {
            if (VoiceModels == null) VoiceModels = new List<VoiceModel>();
            if (Rules == null) Rules = new List<VoiceAssignmentRule>();
            if (ApiKey == null) ApiKey = "";
            if (Model == null) Model = "";
            if (Region == null) Region = "eastus";
            if (DefaultVoiceModelId == null) DefaultVoiceModelId = VoiceModel.NONE_MODEL_ID;
        }
    }
}
