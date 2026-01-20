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
        // Dictionary: PawnId -> User selected voice (DEFAULT_MODEL_ID, RULE_BASED_MODEL_ID, or specific ID)
        private static Dictionary<int, string> _pawnVoiceMap = new Dictionary<int, string>();
        
        // Dictionary: PawnId -> Resolved/cached voice model ID (actual voice used for TTS)
        private static Dictionary<int, string> _pawnResolvedVoiceMap = new Dictionary<int, string>();

        /// <summary>
        /// Get voice model ID for a pawn (resolved to actual voice model for TTS)
        /// </summary>
        public static string GetVoiceModel(Pawn pawn)
        {
            if (pawn == null) 
                return GetDefaultVoiceModel(null);
            
            // Check if we have a cached resolved voice
            if (_pawnResolvedVoiceMap.TryGetValue(pawn.thingIDNumber, out string resolvedVoice) 
                && !string.IsNullOrEmpty(resolvedVoice))
            {
                // Validate the cached voice is still valid
                if (IsVoiceModelValid(resolvedVoice))
                    return resolvedVoice;
            }
            
            // Need to resolve the voice
            string userChoice = GetRawVoiceModel(pawn);
            string resolved = ResolveVoiceModel(pawn, userChoice);
            
            // Cache the resolved voice (only if it's from a rule)
            if (userChoice == VoiceModel.RULE_BASED_MODEL_ID 
                || (userChoice == VoiceModel.DEFAULT_MODEL_ID && IsDefaultRuleBased()))
            {
                _pawnResolvedVoiceMap[pawn.thingIDNumber] = resolved;
            }
            
            return resolved;
        }
        
        /// <summary>
        /// Resolve a voice model choice to actual voice ID
        /// </summary>
        private static string ResolveVoiceModel(Pawn pawn, string voiceChoice)
        {
            if (string.IsNullOrEmpty(voiceChoice) || voiceChoice == VoiceModel.DEFAULT_MODEL_ID)
            {
                return GetDefaultVoiceModel(pawn);
            }
            
            if (voiceChoice == VoiceModel.RULE_BASED_MODEL_ID)
            {
                return GetCustomDefaultVoiceModel(pawn);
            }
            
            // Direct voice ID - validate and return
            if (IsVoiceModelValid(voiceChoice))
                return voiceChoice;
            
            // Invalid voice, fallback to default
            return GetDefaultVoiceModel(pawn);
        }
        
        /// <summary>
        /// Check if current default voice is RULE_BASED
        /// </summary>
        private static bool IsDefaultRuleBased()
        {
            var settings = TTSConfig.Settings;
            if (settings == null) return false;
            
            var defaultModelId = settings.GetSupplierDefaultVoiceModelId(settings.Supplier);
            return defaultModelId == VoiceModel.RULE_BASED_MODEL_ID;
        }

        /// <summary>
        /// Get raw voice model ID for a pawn (without resolving DEFAULT or RULE_BASED tags)
        /// Used by UI to show current selection
        /// </summary>
        public static string GetRawVoiceModel(Pawn pawn)
        {
            if (pawn == null) return VoiceModel.DEFAULT_MODEL_ID;
            
            if (_pawnVoiceMap.TryGetValue(pawn.thingIDNumber, out string voiceId))
            {
                // If empty or null, return DEFAULT_MODEL_ID
                if (string.IsNullOrEmpty(voiceId))
                {
                    return VoiceModel.DEFAULT_MODEL_ID;
                }
                return voiceId;
            }
            
            return VoiceModel.DEFAULT_MODEL_ID; // No entry means "use default"
        }

        /// <summary>
        /// Check if a voice model ID is valid for the current supplier
        /// </summary>
        private static bool IsVoiceModelValid(string voiceModelId)
        {
            var settings = TTSConfig.Settings;
            if (settings == null) return false;

            var supplierModels = settings.GetSupplierVoiceModels(settings.Supplier);
            if (supplierModels == null) return false;

            // Use LINQ for efficient lookup instead of loop
            return supplierModels.Exists(m => m.ModelId == voiceModelId);
        }

        /// <summary>
        /// Get default voice model for current supplier
        /// </summary>
        private static string GetDefaultVoiceModel(Pawn pawn)
        {
            var settings = TTSConfig.Settings;
            if (settings == null) return VoiceModel.NONE_MODEL_ID;

            var defaultModelId = settings.GetSupplierDefaultVoiceModelId(settings.Supplier) ?? VoiceModel.NONE_MODEL_ID;
            
            // If default is RULE_BASED, resolve it
            if (defaultModelId == VoiceModel.RULE_BASED_MODEL_ID && pawn != null)
            {
                return GetCustomDefaultVoiceModel(pawn);
            }
            
            return defaultModelId;
        }

        /// <summary>
        /// Get default voice model based on custom rules (advanced mode)
        /// </summary>
        private static string GetCustomDefaultVoiceModel(Pawn pawn)
        {
            if (pawn == null) return VoiceModel.NONE_MODEL_ID;

            var settings = TTSConfig.Settings;
            if (settings == null) return VoiceModel.NONE_MODEL_ID;

            var rules = settings.GetSupplierVoiceRules(settings.Supplier);
            if (rules == null || rules.Count == 0)
            {
                // No rules defined, fallback to standard default
                return settings.GetSupplierDefaultVoiceModelId(settings.Supplier) ?? VoiceModel.NONE_MODEL_ID;
            }

            // Iterate through rules in order, return first match
            foreach (var rule in rules)
            {
                if (rule.Matches(pawn))
                {
                    return rule.GetRandomVoiceModelId();
                }
            }

            // No matching rule, fallback to standard default
            return settings.GetSupplierDefaultVoiceModelId(settings.Supplier) ?? VoiceModel.NONE_MODEL_ID;
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
                _pawnVoiceMap[pawn.thingIDNumber] = VoiceModel.DEFAULT_MODEL_ID;
            }
            else
            {
                _pawnVoiceMap[pawn.thingIDNumber] = voiceModelId;
            }
            
            // Clear cached resolved voice when user changes selection
            _pawnResolvedVoiceMap.Remove(pawn.thingIDNumber);
        }

        /// <summary>
        /// Remove voice model assignment for a pawn (called when pawn is destroyed)
        /// </summary>
        public static void RemovePawn(Pawn pawn)
        {
            if (pawn == null) return;
            
            _pawnVoiceMap.Remove(pawn.thingIDNumber);
            _pawnResolvedVoiceMap.Remove(pawn.thingIDNumber);
        }

        /// <summary>
        /// Clear all voice assignments (called when game resets)
        /// </summary>
        public static void Clear()
        {
            _pawnVoiceMap.Clear();
            _pawnResolvedVoiceMap.Clear();
        }
        
        /// <summary>
        /// Called when default voice model changes (including supplier switch)
        /// Intelligently updates cache for all pawns using default voice based on new default type:
        /// - RULE_BASED: Clear cache to trigger rule matching on next use
        /// - NONE: Clear cache (skip voice)
        /// - Specific voice ID: Pre-populate cache with that ID to avoid re-resolution
        /// </summary>
        /// <param name="newDefaultModelId">The new default voice model ID (can be null)</param>
        public static void OnDefaultVoiceChanged(string newDefaultModelId = null)
        {
            // Normalize the new default value
            if (string.IsNullOrEmpty(newDefaultModelId))
            {
                newDefaultModelId = VoiceModel.NONE_MODEL_ID;
            }
            
            // Collect all pawns that use default voice (explicitly or implicitly)
            var pawnsUsingDefault = new System.Collections.Generic.HashSet<int>();
            
            // Pawns explicitly using DEFAULT
            foreach (var kvp in _pawnVoiceMap)
            {
                if (kvp.Value == VoiceModel.DEFAULT_MODEL_ID)
                {
                    pawnsUsingDefault.Add(kvp.Key);
                }
            }
            
            // Pawns implicitly using DEFAULT (not in _pawnVoiceMap but have cached voice)
            foreach (var kvp in _pawnResolvedVoiceMap)
            {
                if (!_pawnVoiceMap.ContainsKey(kvp.Key))
                {
                    pawnsUsingDefault.Add(kvp.Key);
                }
            }
            
            // Update cache based on new default type
            foreach (var pawnId in pawnsUsingDefault)
            {
                if (newDefaultModelId == VoiceModel.RULE_BASED_MODEL_ID)
                {
                    // Rule-based: Clear cache, will be resolved via rules on next use
                    _pawnResolvedVoiceMap.Remove(pawnId);
                }
                else if (newDefaultModelId == VoiceModel.NONE_MODEL_ID)
                {
                    // None: Clear cache (skip voice)
                    _pawnResolvedVoiceMap.Remove(pawnId);
                }
                else if (IsVoiceModelValid(newDefaultModelId))
                {
                    _pawnResolvedVoiceMap[pawnId] = newDefaultModelId;
                }
                else
                {
                    // Invalid voice, clear cache
                    _pawnResolvedVoiceMap.Remove(pawnId);
                }
            }
        }
        
        /// <summary>
        /// Called when voice assignment rules change
        /// Clears resolved cache for all pawns using rule-based selection (directly or via default)
        /// This ensures pawns will re-resolve their voice based on updated rules
        /// </summary>
        public static void OnRulesChanged()
        {
            bool defaultIsRuleBased = IsDefaultRuleBased();
            
            foreach (var kvp in _pawnVoiceMap)
            {
                // Clear cache if using RULE_BASED directly
                if (kvp.Value == VoiceModel.RULE_BASED_MODEL_ID)
                {
                    _pawnResolvedVoiceMap.Remove(kvp.Key);
                }
                // Or using DEFAULT when default is RULE_BASED
                else if (kvp.Value == VoiceModel.DEFAULT_MODEL_ID && defaultIsRuleBased)
                {
                    _pawnResolvedVoiceMap.Remove(kvp.Key);
                }
            }
            
            // Also clear cache for pawns not explicitly in _pawnVoiceMap if default is rule-based
            if (defaultIsRuleBased)
            {
                var pawnsToRemove = new System.Collections.Generic.List<int>();
                foreach (var kvp in _pawnResolvedVoiceMap)
                {
                    if (!_pawnVoiceMap.ContainsKey(kvp.Key))
                    {
                        pawnsToRemove.Add(kvp.Key);
                    }
                }
                foreach (var pawnId in pawnsToRemove)
                {
                    _pawnResolvedVoiceMap.Remove(pawnId);
                }
            }
        }

        /// <summary>
        /// Expose data for save/load
        /// </summary>
        public static void ExposeData()
        {
            Scribe_Collections.Look(ref _pawnVoiceMap, "pawnVoiceMap", LookMode.Value, LookMode.Value);
            // Note: We don't save _pawnResolvedVoiceMap - it's a cache that will be rebuilt
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (_pawnVoiceMap == null)
                    _pawnVoiceMap = new Dictionary<int, string>();
                
                // Clear resolved cache on load - will be rebuilt on demand
                _pawnResolvedVoiceMap = new Dictionary<int, string>();
            }
        }
    }
}
