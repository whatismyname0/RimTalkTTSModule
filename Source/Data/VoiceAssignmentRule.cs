using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Requirement type for voice assignment rules
    /// </summary>
    public enum RequirementType
    {
        Gender,
        Xenotype,
        Race,
        Age
    }

    /// <summary>
    /// Base class for voice assignment requirements
    /// </summary>
    public class VoiceRuleRequirement : IExposable
    {
        public RequirementType Type;

        public VoiceRuleRequirement() { }

        public VoiceRuleRequirement(RequirementType type)
        {
            Type = type;
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref Type, "type", RequirementType.Gender);
        }

        /// <summary>
        /// Check if a pawn matches this requirement
        /// </summary>
        public virtual bool Matches(Pawn pawn)
        {
            return false;
        }

        /// <summary>
        /// Get display string for this requirement
        /// </summary>
        public virtual string GetDisplayString()
        {
            return Type.ToString();
        }
    }

    /// <summary>
    /// Gender requirement
    /// </summary>
    public class GenderRequirement : VoiceRuleRequirement
    {
        public Gender Gender = Gender.None;

        public GenderRequirement() : base(RequirementType.Gender) { }

        public GenderRequirement(Gender gender) : base(RequirementType.Gender)
        {
            Gender = gender;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Gender, "gender", Gender.None);
        }

        public override bool Matches(Pawn pawn)
        {
            return pawn?.gender == Gender;
        }

        public override string GetDisplayString()
        {
            string genderLabel = Gender.GetLabel();
            return "RimTalk.Settings.TTS.Rule.Gender".Translate(genderLabel);
        }
    }

    /// <summary>
    /// Xenotype requirement (Biotech DLC)
    /// </summary>
    public class XenotypeRequirement : VoiceRuleRequirement
    {
        public string XenotypeDefName = "";

        public XenotypeRequirement() : base(RequirementType.Xenotype) { }

        public XenotypeRequirement(string xenotypeDef) : base(RequirementType.Xenotype)
        {
            XenotypeDefName = xenotypeDef;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref XenotypeDefName, "xenotypeDefName", "");
        }

        public override bool Matches(Pawn pawn)
        {
            if (pawn?.genes == null) return false;
            
            return pawn.genes.Xenotype?.defName == XenotypeDefName;
        }

        public override string GetDisplayString()
        {
            string label = GetLabel();
            return "RimTalk.Settings.TTS.Rule.Xenotype".Translate(label);
        }

        /// <summary>
        /// Get the xenotype label without the "Xenotype:" prefix
        /// </summary>
        public string GetLabel()
        {
            var xenotypeDef = DefDatabase<RimWorld.XenotypeDef>.GetNamedSilentFail(XenotypeDefName);
            return xenotypeDef?.label ?? XenotypeDefName;
        }
    }

    /// <summary>
    /// Race requirement (Humanoid Alien Races mod)
    /// </summary>
    public class RaceRequirement : VoiceRuleRequirement
    {
        public string RaceDefName = "";

        public RaceRequirement() : base(RequirementType.Race) { }

        public RaceRequirement(string raceDef) : base(RequirementType.Race)
        {
            RaceDefName = raceDef;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref RaceDefName, "raceDefName", "");
        }

        public override bool Matches(Pawn pawn)
        {
            return pawn?.def?.defName == RaceDefName;
        }

        public override string GetDisplayString()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(RaceDefName);
            string label = def?.label ?? RaceDefName;
            return "RimTalk.Settings.TTS.Rule.Race".Translate(label);
        }

        /// <summary>
        /// Get the race label without the "Race:" prefix
        /// </summary>
        public string GetLabel()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(RaceDefName);
            return def?.label ?? RaceDefName;
        }
    }

    /// <summary>
    /// Age requirement
    /// </summary>
    public class AgeRequirement : VoiceRuleRequirement
    {
        public int MinAge = 0;
        public int MaxAge = 999999;

        public AgeRequirement() : base(RequirementType.Age) { }

        public AgeRequirement(int minAge, int maxAge) : base(RequirementType.Age)
        {
            MinAge = minAge;
            MaxAge = maxAge;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MinAge, "minAge", 0);
            Scribe_Values.Look(ref MaxAge, "maxAge", 999999);
        }

        public override bool Matches(Pawn pawn)
        {
            if (pawn == null) return false;
            int age = (int)pawn.ageTracker.AgeBiologicalYears;
            return age >= MinAge && age <= MaxAge;
        }

        public override string GetDisplayString()
        {
            return "RimTalk.Settings.TTS.Rule.Age".Translate(MinAge, MaxAge);
        }
    }

    /// <summary>
    /// Voice assignment rule - combines requirements and voice models
    /// </summary>
    public class VoiceAssignmentRule : IExposable
    {
        // List of requirements (all must match)
        public List<VoiceRuleRequirement> Requirements = new List<VoiceRuleRequirement>();
        
        // List of voice model IDs (randomly select one if pawn matches)
        public List<string> VoiceModelIds = new List<string>();

        public VoiceAssignmentRule() { }

        public void ExposeData()
        {
            // Custom serialization for polymorphic list
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var genderReqs = Requirements.OfType<GenderRequirement>().ToList();
                var xenotypeReqs = Requirements.OfType<XenotypeRequirement>().ToList();
                var raceReqs = Requirements.OfType<RaceRequirement>().ToList();
                var ageReqs = Requirements.OfType<AgeRequirement>().ToList();

                Scribe_Collections.Look(ref genderReqs, "genderRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref xenotypeReqs, "xenotypeRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref raceReqs, "raceRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref ageReqs, "ageRequirements", LookMode.Deep);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<GenderRequirement> genderReqs = null;
                List<XenotypeRequirement> xenotypeReqs = null;
                List<RaceRequirement> raceReqs = null;
                List<AgeRequirement> ageReqs = null;

                Scribe_Collections.Look(ref genderReqs, "genderRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref xenotypeReqs, "xenotypeRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref raceReqs, "raceRequirements", LookMode.Deep);
                Scribe_Collections.Look(ref ageReqs, "ageRequirements", LookMode.Deep);

                Requirements = new List<VoiceRuleRequirement>();
                if (genderReqs != null) Requirements.AddRange(genderReqs);
                if (xenotypeReqs != null) Requirements.AddRange(xenotypeReqs);
                if (raceReqs != null) Requirements.AddRange(raceReqs);
                if (ageReqs != null) Requirements.AddRange(ageReqs);
            }

            Scribe_Collections.Look(ref VoiceModelIds, "voiceModelIds", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (Requirements == null)
                    Requirements = new List<VoiceRuleRequirement>();
                if (VoiceModelIds == null)
                    VoiceModelIds = new List<string>();
            }
        }

        /// <summary>
        /// Check if a pawn matches all requirements
        /// Same type requirements use OR logic, different type results use AND logic
        /// </summary>
        public bool Matches(Pawn pawn)
        {
            if (pawn == null) return false;
            
            // If no requirements, match all pawns (universal rule)
            if (Requirements.Count == 0) return true;

            // Group requirements by type
            var genderReqs = Requirements.Where(r => r.Type == RequirementType.Gender).ToList();
            var xenotypeReqs = Requirements.Where(r => r.Type == RequirementType.Xenotype).ToList();
            var raceReqs = Requirements.Where(r => r.Type == RequirementType.Race).ToList();
            var ageReqs = Requirements.Where(r => r.Type == RequirementType.Age).ToList();

            // For each type: if has requirements, at least one must match (OR)
            // Between types: all type groups must pass (AND)
            
            bool genderMatch = genderReqs.Count == 0 || genderReqs.Any(req => req.Matches(pawn));
            bool xenotypeMatch = xenotypeReqs.Count == 0 || xenotypeReqs.Any(req => req.Matches(pawn));
            bool raceMatch = raceReqs.Count == 0 || raceReqs.Any(req => req.Matches(pawn));
            bool ageMatch = ageReqs.Count == 0 || ageReqs.Any(req => req.Matches(pawn));

            return genderMatch && xenotypeMatch && raceMatch && ageMatch;
        }

        /// <summary>
        /// Get a random voice model ID from the list
        /// </summary>
        public string GetRandomVoiceModelId()
        {
            if (VoiceModelIds.Count == 0) return VoiceModel.NONE_MODEL_ID;
            if (VoiceModelIds.Count == 1) return VoiceModelIds[0];
            return VoiceModelIds.RandomElement();
        }

        /// <summary>
        /// Get display string for this rule
        /// </summary>
        public string GetDisplayString()
        {
            // Group requirements by type
            var genderReqs = Requirements.Where(r => r.Type == RequirementType.Gender).ToList();
            var xenotypeReqs = Requirements.Where(r => r.Type == RequirementType.Xenotype).ToList();
            var raceReqs = Requirements.Where(r => r.Type == RequirementType.Race).ToList();
            var ageReqs = Requirements.Where(r => r.Type == RequirementType.Age).ToList();

            string reqStr;
            if (Requirements.Count == 0)
            {
                // No requirements
                if (VoiceModelIds.Count == 0)
                {
                    // No requirements and no voices = empty rule
                    return "RimTalk.Settings.TTS.Rule.EmptyRule".Translate();
                }
                else
                {
                    // No requirements but has voices = universal rule (matches all pawns)
                    reqStr = "RimTalk.Settings.TTS.Rule.UniversalRule".Translate();
                }
            }
            else
            {
                var groups = new List<string>();
                string orWord = "RimTalk.Settings.TTS.Rule.Or".Translate();

            // Gender group
            if (genderReqs.Count > 0)
            {
                var labels = genderReqs.Select(r => {
                    var genderReq = r as GenderRequirement;
                    return genderReq?.Gender.GetLabel() ?? "";
                }).Where(s => !string.IsNullOrEmpty(s));
                if (labels.Any())
                    groups.Add($"({string.Join(orWord, labels)})");
            }

            // Xenotype group
            if (xenotypeReqs.Count > 0)
            {
                var labels = xenotypeReqs.Select(r => {
                    var xenoReq = r as XenotypeRequirement;
                    return xenoReq?.GetLabel() ?? "";
                }).Where(s => !string.IsNullOrEmpty(s));
                if (labels.Any())
                    groups.Add($"({string.Join(orWord, labels)})");
            }

            // Race group
            if (raceReqs.Count > 0)
            {
                var labels = raceReqs.Select(r => {
                    var raceReq = r as RaceRequirement;
                    return raceReq?.GetLabel() ?? "";
                }).Where(s => !string.IsNullOrEmpty(s));
                if (labels.Any())
                    groups.Add($"({string.Join(orWord, labels)})");
            }

            // Age group
            if (ageReqs.Count > 0)
            {
                var labels = ageReqs.Select(r => {
                    var ageReq = r as AgeRequirement;
                    if (ageReq == null) return "";
                    return $"{ageReq.MinAge}-{ageReq.MaxAge}";
                }).Where(s => !string.IsNullOrEmpty(s));
                if (labels.Any())
                {
                    string ageLabel = "RimTalk.Settings.TTS.Rule.AgeLabel".Translate();
                    groups.Add($"({ageLabel}: {string.Join(orWord, labels)})");
                }
            }

                string andWord = "RimTalk.Settings.TTS.Rule.And".Translate();
                reqStr = string.Join(andWord, groups);
            }
            
            string voiceStr = VoiceModelIds.Count > 0 
                ? "RimTalk.Settings.TTS.Rule.VoiceCount".Translate(VoiceModelIds.Count) 
                : "RimTalk.Settings.TTS.Rule.NoVoices".Translate();
            return $"{reqStr} {voiceStr}";
        }

        /// <summary>
        /// Get shortened display string with length limit
        /// </summary>
        public string GetDisplayString(float maxWidth)
        {
            string fullStr = GetDisplayString();
            Text.Font = GameFont.Small;
            
            if (Text.CalcSize(fullStr).x <= maxWidth)
                return fullStr;
            
            // Truncate with ellipsis
            string ellipsis = "...";
            float ellipsisWidth = Text.CalcSize(ellipsis).x;
            float availableWidth = maxWidth - ellipsisWidth;
            
            string truncated = fullStr;
            while (Text.CalcSize(truncated).x > availableWidth && truncated.Length > 0)
            {
                truncated = truncated.Substring(0, truncated.Length - 1);
            }
            
            return truncated + ellipsis;
        }
    }
}
