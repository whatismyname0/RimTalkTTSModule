using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Window for editing a single voice assignment rule
    /// Four-panel layout: top-left (selected requirements), bottom-left (available requirement types),
    /// top-right (selected voice models), bottom-right (unselected voice models)
    /// </summary>
    public class VoiceRuleEditorWindow : Window
    {
        private VoiceAssignmentRule rule;
        private TTSSettings settings;
        private System.Action onSave;

        private Vector2 selectedReqScrollPos;
        private Vector2 availableReqScrollPos;
        private Vector2 selectedVoiceScrollPos;
        private Vector2 availableVoiceScrollPos;

        // For age requirement editing
        private string minAgeBuffer = "0";
        private string maxAgeBuffer = "999999";

        // Collapse states for requirement categories
        private static bool genderCollapsed = false;
        private static bool xenotypeCollapsed = false;
        private static bool raceCollapsed = false;
        private static bool ageCollapsed = false;

        public VoiceRuleEditorWindow(VoiceAssignmentRule rule, TTSSettings settings, System.Action onSave = null)
        {
            this.rule = rule;
            this.settings = settings;
            this.onSave = onSave;
            
            this.doCloseX = true;
            this.forcePause = false;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(900f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "RimTalk.Settings.TTS.RuleEditor.Title".Translate());
            Text.Font = GameFont.Small;

            // Four-panel layout
            float panelWidth = (inRect.width - 20f) / 2f;
            float topHeight = (inRect.height - 100f) / 2f;
            float bottomHeight = (inRect.height - 100f) / 2f;
            float yOffset = 45f;

            // Top-left: Selected Requirements
            Rect topLeftRect = new Rect(0f, yOffset, panelWidth, topHeight);
            DrawSelectedRequirements(topLeftRect);

            // Top-right: Selected Voice Models
            Rect topRightRect = new Rect(panelWidth + 20f, yOffset, panelWidth, topHeight);
            DrawSelectedVoiceModels(topRightRect);

            // Bottom-left: Available Requirement Types
            Rect bottomLeftRect = new Rect(0f, yOffset + topHeight + 10f, panelWidth, bottomHeight);
            DrawAvailableRequirements(bottomLeftRect);

            // Bottom-right: Unselected Voice Models
            Rect bottomRightRect = new Rect(panelWidth + 20f, yOffset + topHeight + 10f, panelWidth, bottomHeight);
            DrawUnselectedVoiceModels(bottomRightRect);

            // Save/Cancel buttons at bottom
            Rect buttonRect = new Rect(0f, inRect.height - 35f, inRect.width, 35f);
            DrawButtons(buttonRect);
        }

        private void DrawSelectedRequirements(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.DrawBox(rect);

            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Widgets.Label(titleRect, "RimTalk.Settings.TTS.RuleEditor.SelectedRequirements".Translate());

            Rect scrollRect = new Rect(rect.x + 5f, rect.y + 30f, rect.width - 10f, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, rule.Requirements.Count * 30f);

            Widgets.BeginScrollView(scrollRect, ref selectedReqScrollPos, viewRect);
            
            float y = 0f;
            var toRemove = new List<VoiceRuleRequirement>();
            
            foreach (var req in rule.Requirements)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width - 40f, 26f);
                Widgets.Label(rowRect, req.GetDisplayString());

                Rect removeRect = new Rect(viewRect.width - 35f, y, 30f, 26f);
                if (Widgets.ButtonText(removeRect, "×"))
                {
                    toRemove.Add(req);
                }

                y += 30f;
            }

            foreach (var req in toRemove)
            {
                rule.Requirements.Remove(req);
            }

            Widgets.EndScrollView();
        }

        private void DrawAvailableRequirements(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.DrawBox(rect);

            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Widgets.Label(titleRect, "RimTalk.Settings.TTS.RuleEditor.AvailableRequirements".Translate());

            Rect scrollRect = new Rect(rect.x + 5f, rect.y + 30f, rect.width - 10f, rect.height - 35f);
            
            // Calculate dynamic content height based on what's expanded
            float contentHeight = 0f;
            
            // Gender section
            contentHeight += 25f; // Label always visible
            if (!genderCollapsed)
            {
                contentHeight += System.Enum.GetValues(typeof(Gender)).Length * 27f;
            }
            contentHeight += 5f; // Gap
            
            // Xenotype section (if Biotech active)
            if (ModsConfig.BiotechActive)
            {
                contentHeight += 25f; // Label always visible
                if (!xenotypeCollapsed)
                {
                    var xenotypeCount = DefDatabase<XenotypeDef>.AllDefs.Count();
                    contentHeight += xenotypeCount * 27f;
                }
                contentHeight += 5f; // Gap
            }
            
            // Race section (all humanlike races)
            var humanlikeRaceCount = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.race != null && 
                             def.race.Humanlike &&
                             def.race.intelligence == Intelligence.Humanlike &&
                             !def.IsCorpse &&
                             def.category == ThingCategory.Pawn)
                .Count();
            
            if (humanlikeRaceCount > 0)
            {
                contentHeight += 25f; // Label always visible
                if (!raceCollapsed)
                {
                    contentHeight += humanlikeRaceCount * 27f;
                }
                contentHeight += 5f; // Gap
            }
            
            // Age section
            contentHeight += 25f; // Label always visible
            if (!ageCollapsed)
            {
                contentHeight += 27f + 27f; // Input row + button
            }
            
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref availableReqScrollPos, viewRect);
            
            float y = 0f;
            
            // Gender
            Rect genderHeaderRect = new Rect(0f, y, viewRect.width, 25f);
            if (Widgets.ButtonInvisible(genderHeaderRect))
            {
                genderCollapsed = !genderCollapsed;
            }
            string genderArrow = genderCollapsed ? "▶ " : "▼ ";
            Widgets.Label(genderHeaderRect, genderArrow + "RimTalk.Settings.TTS.RuleEditor.Gender".Translate());
            y += 27f;
            
            if (!genderCollapsed)
            {
                foreach (Gender gender in System.Enum.GetValues(typeof(Gender)))
                {
                    Rect buttonRect = new Rect(10f, y, viewRect.width - 20f, 25f);
                    string genderLabel = gender.GetLabel();
                    if (Widgets.ButtonText(buttonRect, genderLabel))
                    {
                        // Check if already exists
                        bool alreadyExists = rule.Requirements.OfType<GenderRequirement>()
                            .Any(req => req.Gender == gender);
                        if (!alreadyExists)
                        {
                            rule.Requirements.Add(new GenderRequirement(gender));
                        }
                    }
                    y += 27f;
                }
            }

            y += 5f;

            // Xenotype (if Biotech DLC enabled)
            if (ModsConfig.BiotechActive)
            {
                Rect xenotypeHeaderRect = new Rect(0f, y, viewRect.width, 25f);
                if (Widgets.ButtonInvisible(xenotypeHeaderRect))
                {
                    xenotypeCollapsed = !xenotypeCollapsed;
                }
                string xenotypeArrow = xenotypeCollapsed ? "▶ " : "▼ ";
                Widgets.Label(xenotypeHeaderRect, xenotypeArrow + "RimTalk.Settings.TTS.RuleEditor.Xenotype".Translate());
                y += 27f;

                if (!xenotypeCollapsed)
                {
                    var xenotypes = DefDatabase<XenotypeDef>.AllDefs.OrderBy(x => x.label);
                    foreach (var xenotype in xenotypes)
                    {
                        Rect buttonRect = new Rect(10f, y, viewRect.width - 20f, 25f);
                        if (Widgets.ButtonText(buttonRect, xenotype.label))
                        {
                            // Check if already exists
                            bool alreadyExists = rule.Requirements.OfType<XenotypeRequirement>()
                                .Any(req => req.XenotypeDefName == xenotype.defName);
                            if (!alreadyExists)
                            {
                                rule.Requirements.Add(new XenotypeRequirement(xenotype.defName));
                            }
                        }
                        y += 27f;
                    }
                }

                y += 5f;
            }

            // Race - all humanlike races (including Human and HAR races)
            var humanlikeRacesList = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.race != null && 
                             def.race.Humanlike &&
                             def.race.intelligence == Intelligence.Humanlike &&
                             !def.IsCorpse &&
                             def.category == ThingCategory.Pawn)
                .GroupBy(def => def.defName)  // Remove duplicates by defName
                .Select(g => g.First())
                .GroupBy(def => def.label)  // Also remove duplicates by label
                .Select(g => g.First())
                .OrderBy(def => def == ThingDefOf.Human ? "" : def.label)  // Human first
                .ToList();
            
            if (humanlikeRacesList.Any())
            {
                Rect raceHeaderRect = new Rect(0f, y, viewRect.width, 25f);
                if (Widgets.ButtonInvisible(raceHeaderRect))
                {
                    raceCollapsed = !raceCollapsed;
                }
                string raceArrow = raceCollapsed ? "▶ " : "▼ ";
                Widgets.Label(raceHeaderRect, raceArrow + "RimTalk.Settings.TTS.RuleEditor.Race".Translate());
                y += 27f;

                if (!raceCollapsed)
                {
                    foreach (var race in humanlikeRacesList)
                    {
                        Rect buttonRect = new Rect(10f, y, viewRect.width - 20f, 25f);
                        if (Widgets.ButtonText(buttonRect, race.label))
                        {
                            // Check if already exists
                            bool alreadyExists = rule.Requirements.OfType<RaceRequirement>()
                                .Any(req => req.RaceDefName == race.defName);
                            if (!alreadyExists)
                            {
                                rule.Requirements.Add(new RaceRequirement(race.defName));
                            }
                        }
                        y += 27f;
                    }
                }

                y += 5f;
            }

            // Age
            Rect ageHeaderRect = new Rect(0f, y, viewRect.width, 25f);
            if (Widgets.ButtonInvisible(ageHeaderRect))
            {
                ageCollapsed = !ageCollapsed;
            }
            string ageArrow = ageCollapsed ? "▶ " : "▼ ";
            Widgets.Label(ageHeaderRect, ageArrow + "RimTalk.Settings.TTS.RuleEditor.Age".Translate());
            y += 27f;

            if (!ageCollapsed)
            {
                Rect ageRowRect = new Rect(10f, y, viewRect.width - 20f, 25f);
                float halfWidth = (ageRowRect.width - 60f) / 2f;
                
                Rect minLabelRect = new Rect(ageRowRect.x, ageRowRect.y, 40f, 25f);
                Widgets.Label(minLabelRect, "Min:");
                
                Rect minInputRect = new Rect(ageRowRect.x + 45f, ageRowRect.y, halfWidth - 45f, 25f);
                minAgeBuffer = Widgets.TextField(minInputRect, minAgeBuffer);
                
                Rect maxLabelRect = new Rect(ageRowRect.x + halfWidth + 10f, ageRowRect.y, 40f, 25f);
                Widgets.Label(maxLabelRect, "Max:");
                
                Rect maxInputRect = new Rect(ageRowRect.x + halfWidth + 55f, ageRowRect.y, halfWidth - 45f, 25f);
                maxAgeBuffer = Widgets.TextField(maxInputRect, maxAgeBuffer);
                
                y += 27f;
                
                Rect addAgeButtonRect = new Rect(10f, y, viewRect.width - 20f, 25f);
                if (Widgets.ButtonText(addAgeButtonRect, "RimTalk.Settings.TTS.RuleEditor.AddAge".Translate()))
                {
                    bool minValid = int.TryParse(minAgeBuffer, out int minAge);
                    bool maxValid = int.TryParse(maxAgeBuffer, out int maxAge);
                    
                    if (!minValid || !maxValid)
                    {
                        Messages.Message("RimTalk.Settings.TTS.RuleEditor.AgeInvalidNumberError".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        // Validate age range
                        if (minAge < 0 || minAge > 999999)
                        {
                            Messages.Message("RimTalk.Settings.TTS.RuleEditor.AgeRangeError".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                        else if (maxAge < 0 || maxAge > 999999)
                        {
                            Messages.Message("RimTalk.Settings.TTS.RuleEditor.AgeRangeError".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                        else if (maxAge < minAge)
                        {
                            Messages.Message("RimTalk.Settings.TTS.RuleEditor.MaxLessThanMinError".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                        else
                        {
                            // Check if already exists
                            bool alreadyExists = rule.Requirements.OfType<AgeRequirement>()
                                .Any(req => req.MinAge == minAge && req.MaxAge == maxAge);
                            if (!alreadyExists)
                            {
                                rule.Requirements.Add(new AgeRequirement(minAge, maxAge));
                                minAgeBuffer = "0";
                                maxAgeBuffer = "999999";
                            }
                        }
                    }
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawSelectedVoiceModels(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.DrawBox(rect);

            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Widgets.Label(titleRect, "RimTalk.Settings.TTS.RuleEditor.SelectedVoiceModels".Translate());

            Rect scrollRect = new Rect(rect.x + 5f, rect.y + 30f, rect.width - 10f, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, rule.VoiceModelIds.Count * 30f);

            Widgets.BeginScrollView(scrollRect, ref selectedVoiceScrollPos, viewRect);
            
            float y = 0f;
            var toRemove = new List<string>();
            var supplierModels = settings.GetSupplierVoiceModels(settings.Supplier);
            
            foreach (var modelId in rule.VoiceModelIds)
            {
                var model = supplierModels.FirstOrDefault(m => m.ModelId == modelId);
                string displayName = model?.ModelName ?? modelId;

                Rect rowRect = new Rect(0f, y, viewRect.width - 40f, 26f);
                Widgets.Label(rowRect, displayName);

                Rect removeRect = new Rect(viewRect.width - 35f, y, 30f, 26f);
                if (Widgets.ButtonText(removeRect, "×"))
                {
                    toRemove.Add(modelId);
                }

                y += 30f;
            }

            foreach (var modelId in toRemove)
            {
                rule.VoiceModelIds.Remove(modelId);
            }

            Widgets.EndScrollView();
        }

        private void DrawUnselectedVoiceModels(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.DrawBox(rect);

            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Widgets.Label(titleRect, "RimTalk.Settings.TTS.RuleEditor.UnselectedVoiceModels".Translate());

            Rect scrollRect = new Rect(rect.x + 5f, rect.y + 30f, rect.width - 10f, rect.height - 35f);
            
            var supplierModels = settings.GetSupplierVoiceModels(settings.Supplier);
            var unselectedModels = supplierModels.Where(m => !rule.VoiceModelIds.Contains(m.ModelId)).ToList();
            
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, unselectedModels.Count * 30f);

            Widgets.BeginScrollView(scrollRect, ref availableVoiceScrollPos, viewRect);
            
            float y = 0f;
            
            foreach (var model in unselectedModels)
            {
                Rect buttonRect = new Rect(0f, y, viewRect.width, 26f);
                if (Widgets.ButtonText(buttonRect, model.ModelName))
                {
                    rule.VoiceModelIds.Add(model.ModelId);
                }

                y += 30f;
            }

            Widgets.EndScrollView();
        }

        private void DrawButtons(Rect rect)
        {
            float buttonWidth = 100f;
            float gap = 10f;

            // Help button in bottom-left corner
            Rect helpRect = new Rect(rect.x, rect.y, 30f, 30f);
            if (Widgets.ButtonText(helpRect, "?"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "RimTalk.Settings.TTS.RuleEditor.Help".Translate(),
                    "RimTalk.TTS.OK".Translate()));
            }

            Rect saveRect = new Rect(rect.x + rect.width - (buttonWidth * 2 + gap), rect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(saveRect, "RimTalk.TTS.Save".Translate()))
            {
                onSave?.Invoke();
                Close();
            }

            Rect cancelRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(cancelRect, "RimTalk.TTS.Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
