using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Manages pawn-specific voice model assignments
    /// Stores mappings independently since main RimTalk's Hediff_Persona doesn't have VoiceModelId field
    /// </summary>
    public static class PawnVoiceManager
    {
        // Dictionary: PawnId -> VoiceModelId
        private static Dictionary<int, string> _pawnVoiceMap = new Dictionary<int, string>();

        /// <summary>
        /// Get voice model ID for a pawn
        /// Returns null if no custom voice is assigned
        /// </summary>
        public static string GetVoiceModel(Pawn pawn)
        {
            if (pawn == null) return null;
            _pawnVoiceMap.TryGetValue(pawn.thingIDNumber, out string voiceId);
            var settings = TTSModule.Instance.GetSettings();
            var supplierModels = settings?.GetSupplierVoiceModels(settings.Supplier);

            if (voiceId == null)
                return settings.GetSupplierDefaultVoiceModelId(TTSModule.Instance.Settings.Supplier);

            foreach (var m in supplierModels)
                if (m.ModelId == voiceId)
                    return voiceId;

            return settings.GetSupplierDefaultVoiceModelId(TTSModule.Instance.Settings.Supplier);
        }

        /// <summary>
        /// Set voice model ID for a pawn
        /// Pass null or empty string to remove custom voice assignment
        /// </summary>
        public static void SetVoiceModel(Pawn pawn, string voiceModelId)
        {
            if (pawn == null) return;
            
            if (string.IsNullOrEmpty(voiceModelId))
            {
                _pawnVoiceMap[pawn.thingIDNumber] = VoiceModel.NONE_MODEL_ID;
            }
            else
            {
                _pawnVoiceMap[pawn.thingIDNumber] = voiceModelId;
            }
        }

        /// <summary>
        /// Remove voice model assignment for a pawn (called when pawn is destroyed)
        /// </summary>
        public static void RemovePawn(Pawn pawn)
        {
            if (pawn == null) return;
            
            _pawnVoiceMap.Remove(pawn.thingIDNumber);
        }

        /// <summary>
        /// Clear all voice assignments (called when game resets)
        /// </summary>
        public static void Clear()
        {
            _pawnVoiceMap.Clear();
        }

        /// <summary>
        /// Expose data for save/load
        /// </summary>
        public static void ExposeData()
        {
            Scribe_Collections.Look(ref _pawnVoiceMap, "pawnVoiceMap", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && _pawnVoiceMap == null)
            {
                _pawnVoiceMap = new Dictionary<int, string>();
            }
        }
    }
}
