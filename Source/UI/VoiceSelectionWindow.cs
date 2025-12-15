using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Voice model selection window for individual pawns
    /// </summary>
    public class VoiceSelectionWindow : Window
    {
        private readonly Pawn _pawn;
        private string _selectedVoiceId;
        private Vector2 _scrollPos = Vector2.zero;
        private readonly TTSSettings _settings;
        private readonly List<VoiceModel> _voiceModels;

        static VoiceSelectionWindow()
        {
        }

        public VoiceSelectionWindow(Pawn pawn)
        {
            _pawn = pawn;
            
            // Load settings once
            var modInstance = LoadedModManager.GetMod(typeof(TTSMod)) as TTSMod;
            if (modInstance != null)
            {
                _settings = modInstance.GetSettings<TTSSettings>();
                _voiceModels = _settings != null ? (_settings.GetSupplierVoiceModels(_settings.Supplier) ?? new List<VoiceModel>()) : new List<VoiceModel>();
            }
            else
            {
                _settings = null;
                _voiceModels = new List<VoiceModel>();
            }
            
            _selectedVoiceId = GetCurrentVoiceModel();

            doCloseX = true;
            draggable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(500f, 450f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "RimTalk.TTS.VoiceSelection".Translate(_pawn.LabelShort));

            Text.Font = GameFont.Small;
            Rect instructRect = new Rect(inRect.x, titleRect.yMax + 5f, inRect.width, 30f);
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(instructRect, "RimTalk.TTS.VoiceSelectionDesc".Translate());
            GUI.color = Color.white;

            // Voice model list
            float listTop = instructRect.yMax + 10f;
            float listHeight = inRect.height - listTop - 50f;
            Rect listOutRect = new Rect(inRect.x, listTop, inRect.width, listHeight);

            // Calculate content height
            int itemCount = 2 + _voiceModels.Count; // "None" + "Default" + custom models
            float contentHeight = itemCount * 40f;
            Rect listViewRect = new Rect(0f, 0f, listOutRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listOutRect, ref _scrollPos, listViewRect);

            float y = 0f;

            // Option: None (disable TTS for this pawn)
            DrawVoiceOption(ref y, listViewRect.width, VoiceModel.NONE_MODEL_ID, 
                "RimTalk.TTS.VoiceNone".Translate(), 
                "RimTalk.TTS.VoiceNoneDesc".Translate());

            // Option: Default (use default voice model from settings)
            DrawVoiceOption(ref y, listViewRect.width, "", 
                "RimTalk.TTS.VoiceDefault".Translate(), 
                "RimTalk.TTS.VoiceDefaultDesc".Translate());

            // Custom voice models - with validation
            if (_voiceModels != null && _voiceModels.Count > 0)
            {
                foreach (var model in _voiceModels)
                {
                    if (model != null && !string.IsNullOrEmpty(model.ModelId))
                    {
                        string displayName = !string.IsNullOrEmpty(model.ModelName) ? model.ModelName : model.ModelId;
                        string description = $"ID: {model.ModelId}";
                        
                        DrawVoiceOption(ref y, listViewRect.width, model.ModelId, displayName, description);
                    }
                }
            }
            else
            {
                // Show a message if no custom models are configured
                Rect noModelsRect = new Rect(10f, y, listViewRect.width - 20f, 60f);
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(noModelsRect, "RimTalk.Settings.TTS.NoCustomModels".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 65f;
            }

            Widgets.EndScrollView();

            // Buttons
            float buttonY = listOutRect.yMax + 10f;
            float buttonWidth = 100f;
            float buttonHeight = 30f;
            float spacing = 10f;

            Rect saveButton = new Rect(inRect.center.x - buttonWidth - spacing / 2f, buttonY, buttonWidth, buttonHeight);
            Rect cancelButton = new Rect(inRect.center.x + spacing / 2f, buttonY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(saveButton, "RimTalk.TTS.Save".Translate()))
            {
                SaveVoiceModel(_selectedVoiceId);
                Messages.Message("RimTalk.TTS.VoiceUpdated".Translate(_pawn.LabelShort), 
                    MessageTypeDefOf.TaskCompletion, false);
                Close();
            }

            if (Widgets.ButtonText(cancelButton, "RimTalk.TTS.Cancel".Translate()))
            {
                Close();
            }
        }

        private void DrawVoiceOption(ref float y, float width, string voiceId, string label, string description)
        {
            Rect optionRect = new Rect(0f, y, width, 35f);
            
            bool isSelected = _selectedVoiceId == voiceId;
            
            if (isSelected)
            {
                Widgets.DrawBoxSolid(optionRect, new Color(0.3f, 0.5f, 0.3f, 0.5f));
            }
            else
            {
                Widgets.DrawBoxSolid(optionRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            }
            
            Widgets.DrawHighlightIfMouseover(optionRect);

            // Radio button
            Rect radioRect = new Rect(optionRect.x + 5f, optionRect.y + 7f, 20f, 20f);
            bool wasSelected = isSelected;
            Widgets.Checkbox(radioRect.position, ref isSelected, 20f, false, true);
            
            if (isSelected && !wasSelected)
            {
                _selectedVoiceId = voiceId;
            }

            // Label
            Rect labelRect = new Rect(radioRect.xMax + 10f, optionRect.y + 2f, width - 40f, 18f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);

            // Description
            Rect descRect = new Rect(labelRect.x, labelRect.yMax, labelRect.width, 15f);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(descRect, description);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Make entire row clickable
            if (Widgets.ButtonInvisible(optionRect))
            {
                _selectedVoiceId = voiceId;
            }

            y += 40f;
        }

        private string GetCurrentVoiceModel()
        {
            try
            {
                // Get voice model from PawnVoiceManager
                string voiceId = Data.PawnVoiceManager.GetVoiceModel(_pawn);
                return voiceId ?? ""; // Return empty string for default, not null
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Failed to get current voice model: {ex.Message}");
            }
            return "";
        }

        private void SaveVoiceModel(string voiceId)
        {
            try
            {
                // Save voice model directly to PawnVoiceManager
                Data.PawnVoiceManager.SetVoiceModel(_pawn, voiceId);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Failed to save voice model: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
