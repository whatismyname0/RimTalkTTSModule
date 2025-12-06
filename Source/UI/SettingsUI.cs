using System.Linq;
using UnityEngine;
using Verse;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// TTS settings UI renderer
    /// </summary>
    public static class SettingsUI
    {
        private static Vector2 scrollPosition = Vector2.zero;
        private static Vector2 mainScrollPosition = Vector2.zero;
        private static string processingPromptBuffer = "";
        private static bool processingPromptInitialized = false;

        public static void DrawTTSSettings(Rect inRect, TTSSettings settings)
        {
            // Calculate content height dynamically based on voice models count
            float baseHeight = 1200f;
            float voiceModelRowHeight = 36f; // Height per voice model row (30f + 6f gap)
            int voiceModelCount = settings.VoiceModels?.Count ?? 0;
            float contentHeight = baseHeight + (voiceModelCount * voiceModelRowHeight);
            
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);
            
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Enable TTS
            listing.CheckboxLabeled("RimTalk.Settings.TTS.Enable".Translate(), ref settings.EnableTTS, "RimTalk.Settings.TTS.EnableTooltip".Translate());

            if (!settings.EnableTTS)
            {
                listing.End();
                Widgets.EndScrollView();
                return;
            }

            listing.Gap();

            // Fish Audio API Configuration
            listing.Label("RimTalk.Settings.TTS.ApiKey".Translate());
            settings.FishAudioApiKey = listing.TextEntry(settings.FishAudioApiKey);

            listing.Gap();

            // TTS Model Selection
            listing.Label("RimTalk.Settings.TTS.ModelLabel".Translate(settings.TTSModel));
            if (listing.RadioButton("RimTalk.Settings.TTS.ModelHighQuality".Translate(), settings.TTSModel == "fishaudio-1"))
            {
                settings.TTSModel = "fishaudio-1";
            }
            if (listing.RadioButton("RimTalk.Settings.TTS.ModelFaster".Translate(), settings.TTSModel == "s1"))
            {
                settings.TTSModel = "s1";
            }

            listing.Gap();

            // Volume
            listing.Label("RimTalk.Settings.TTS.VolumeLabel".Translate(settings.TTSVolume.ToStringPercent()));
            settings.TTSVolume = listing.Slider(settings.TTSVolume, 0f, 1f);

            listing.Gap();

            // Temperature
            listing.Label("RimTalk.Settings.TTS.TemperatureLabel".Translate(settings.TTSTemperature.ToString("F2")));
            settings.TTSTemperature = listing.Slider(settings.TTSTemperature, 0.7f, 1.0f);

            // Top P
            listing.Label("RimTalk.Settings.TTS.TopPLabel".Translate(settings.TTSTopP.ToString("F2")));
            settings.TTSTopP = listing.Slider(settings.TTSTopP, 0.7f, 1.0f);

            listing.Gap();

            // LLM API Configuration Section
            DrawApiConfigSection(listing, settings);

            listing.Gap();

            // Translation Language
            listing.Label("RimTalk.Settings.TTS.TranslationLanguage".Translate());
            settings.TTSTranslationLanguage = listing.TextEntry(settings.TTSTranslationLanguage);

            listing.Gap();

            // Processing Prompt Section (similar to RimTalk main module)
            DrawProcessingPromptSection(listing, settings);

            listing.Gap();

            // Voice Models Section
            DrawVoiceModelsSection(listing, settings, viewRect.width);

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawProcessingPromptSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label("RimTalk.Settings.TTS.ProcessingPromptLabel".Translate());
            
            // Initialize buffer if needed - show default prompt if custom is empty
            if (!processingPromptInitialized)
            {
                processingPromptBuffer = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                    ? Data.TTSConstant.DefaultTTSProcessingPrompt
                    : settings.CustomTTSProcessingPrompt;
                processingPromptInitialized = true;
            }

            // Instructions
            Text.Font = GameFont.Tiny;
            GUI.color = Color.cyan;
            Rect tipRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(tipRect, "RimTalk.Settings.TTS.ProcessingPromptTip".Translate());
            GUI.color = Color.white;
            
            // Show current status (default or custom)
            Rect statusRect = listing.GetRect(Text.LineHeight);
            string statusText = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                ? "RimTalk.Settings.TTS.UsingDefaultPrompt".Translate()
                : "RimTalk.Settings.TTS.UsingCustomPrompt".Translate();
            GUI.color = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt) ? Color.yellow : Color.green;
            Widgets.Label(statusRect, statusText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Text area for prompt - display buffer which contains either custom or default
            float textAreaHeight = 120f;
            Rect textAreaRect = listing.GetRect(textAreaHeight);
            string displayPrompt = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                ? Data.TTSConstant.DefaultTTSProcessingPrompt
                : processingPromptBuffer;
            string newPrompt = Widgets.TextArea(textAreaRect, displayPrompt);

            // Only save if user actually modified the content
            if (newPrompt != displayPrompt)
            {
                processingPromptBuffer = newPrompt.Replace("\\n", "\n");
                // Mark as custom if it differs from default
                if (processingPromptBuffer != Data.TTSConstant.DefaultTTSProcessingPrompt)
                {
                    settings.CustomTTSProcessingPrompt = processingPromptBuffer;
                }
                else
                {
                    settings.CustomTTSProcessingPrompt = "";
                }
            }

            listing.Gap(6f);

            // Reset button
            Rect resetButtonRect = listing.GetRect(30f);
            if (Widgets.ButtonText(resetButtonRect, "RimTalk.Settings.TTS.ResetPrompt".Translate()))
            {
                settings.CustomTTSProcessingPrompt = "";
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt;
            }
        }

        private static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, float width)
        {
            listing.Label("RimTalk.Settings.TTS.VoiceModels".Translate());

            // Default model selector
            string currentDefaultName = "RimTalk.Settings.TTS.NotSet".Translate();
            if (!string.IsNullOrEmpty(settings.DefaultVoiceModelId))
            {
                if (settings.DefaultVoiceModelId == VoiceModel.NONE_MODEL_ID)
                {
                    currentDefaultName = "RimTalk.Settings.TTS.NoneModel".Translate();
                }
                else if (settings.VoiceModels != null)
                {
                    var m = settings.VoiceModels.FirstOrDefault(x => x.ModelId == settings.DefaultVoiceModelId);
                    if (m != null)
                        currentDefaultName = m.GetDisplayName();
                }
            }

            if (listing.ButtonText("RimTalk.Settings.TTS.DefaultModel".Translate(currentDefaultName)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.ClearDefault".Translate(), delegate
                {
                    settings.DefaultVoiceModelId = "";
                }));
                
                // Add NONE pseudo-model option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.DefaultVoiceModelId = VoiceModel.NONE_MODEL_ID;
                }));

                if (settings.VoiceModels != null)
                {
                    foreach (var vm in settings.VoiceModels)
                    {
                        var display = vm.GetDisplayName();
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.DefaultVoiceModelId = vm.ModelId;
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            // Header with add/remove buttons (similar to RimTalk API configs)
            Rect headerRect = listing.GetRect(24f);
            Rect addButtonRect = new Rect(headerRect.x + headerRect.width - 65f, headerRect.y, 30f, 24f);
            Rect removeButtonRect = new Rect(headerRect.x + headerRect.width - 30f, headerRect.y, 30f, 24f);
            headerRect.width -= 70f;

            Widgets.Label(headerRect, "RimTalk.Settings.TTS.ModelConfigurations".Translate());

            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                if (settings.VoiceModels == null)
                    settings.VoiceModels = new System.Collections.Generic.List<VoiceModel>();
                settings.VoiceModels.Add(new VoiceModel { ModelName = "", ModelId = "" });
            }

            GUI.enabled = settings.VoiceModels != null && settings.VoiceModels.Count > 0;
            if (Widgets.ButtonText(removeButtonRect, "−"))
            {
                if (settings.VoiceModels != null && settings.VoiceModels.Count > 0)
                {
                    settings.VoiceModels.RemoveAt(settings.VoiceModels.Count - 1);
                }
            }
            GUI.enabled = true;

            listing.Gap(6f);

            // Column descriptions
            listing.Label("RimTalk.Settings.TTS.ColumnDescription".Translate());
            listing.Gap(6f);

            // Draw table headers
            Rect tableHeaderRect = listing.GetRect(24f);
            float x = tableHeaderRect.x;
            float y = tableHeaderRect.y;
            float height = tableHeaderRect.height;

            x += 60f; // Space for reorder buttons

            float nameWidth = (width - 130f) * 0.4f;
            float idWidth = (width - 130f) * 0.4f;

            Rect nameHeaderRect = new Rect(x, y, nameWidth, height);
            Widgets.Label(nameHeaderRect, "RimTalk.Settings.TTS.ColumnModelName".Translate());
            x += nameWidth + 5f;

            Rect idHeaderRect = new Rect(x, y, idWidth, height);
            Widgets.Label(idHeaderRect, "RimTalk.Settings.TTS.ColumnModelID".Translate());

            listing.Gap(6f);

            // Draw each model config row
            if (settings.VoiceModels != null)
            {
                for (int i = 0; i < settings.VoiceModels.Count; i++)
                {
                    DrawModelConfigRow(listing, settings.VoiceModels[i], i, settings.VoiceModels, width);
                }
            }
        }

        private static void DrawModelConfigRow(Listing_Standard listing, VoiceModel model, int index, System.Collections.Generic.List<VoiceModel> models, float width)
        {
            Rect rowRect = listing.GetRect(30f);
            float x = rowRect.x;
            float y = rowRect.y;
            float height = rowRect.height;

            // Reorder buttons
            Rect upButtonRect = new Rect(x, y, 24f, height);
            if (Widgets.ButtonText(upButtonRect, "▲") && index > 0)
            {
                (models[index], models[index - 1]) = (models[index - 1], models[index]);
            }
            x += 30f;

            Rect downButtonRect = new Rect(x, y, 24f, height);
            if (Widgets.ButtonText(downButtonRect, "▼") && index < models.Count - 1)
            {
                (models[index], models[index + 1]) = (models[index + 1], models[index]);
            }
            x += 30f;

            float nameWidth = (width - 130f) * 0.4f;
            float idWidth = (width - 130f) * 0.4f;

            // Model Name field
            Rect nameRect = new Rect(x, y, nameWidth, height);
            model.ModelName = Widgets.TextField(nameRect, model.ModelName ?? "");
            x += nameWidth + 5f;

            // Model ID field
            Rect idRect = new Rect(x, y, idWidth, height);
            model.ModelId = Widgets.TextField(idRect, model.ModelId ?? "");
        }

        private static void DrawApiConfigSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label("RimTalk.Settings.TTS.LLMApiConfig".Translate());
            
            listing.Gap(6f);

            // Provider Selection
            listing.Label("RimTalk.Settings.TTS.ProviderLabel".Translate());
            if (listing.RadioButton("DeepSeek", settings.ApiProvider == TTSApiProvider.DeepSeek))
            {
                settings.ApiProvider = TTSApiProvider.DeepSeek;
            }
            if (listing.RadioButton("OpenAI", settings.ApiProvider == TTSApiProvider.OpenAI))
            {
                settings.ApiProvider = TTSApiProvider.OpenAI;
            }
            if (listing.RadioButton("RimTalk.Settings.TTS.CustomProvider".Translate(), settings.ApiProvider == TTSApiProvider.Custom))
            {
                settings.ApiProvider = TTSApiProvider.Custom;
            }

            listing.Gap(6f);

            // Model
            listing.Label("RimTalk.Settings.TTS.LLMModelLabel".Translate());
            settings.Model = listing.TextEntry(settings.Model ?? "");

            listing.Gap(6f);

            // API Key
            listing.Label("RimTalk.Settings.TTS.LLMApiKeyLabel".Translate());
            settings.ApiKey = listing.TextEntry(settings.ApiKey ?? "");

            listing.Gap(6f);

            // Custom Base URL (only for Custom provider)
            if (settings.ApiProvider == TTSApiProvider.Custom)
            {
                listing.Label("RimTalk.Settings.TTS.CustomBaseUrlLabel".Translate());
                settings.CustomBaseUrl = listing.TextEntry(settings.CustomBaseUrl ?? "");
            }
        }
    }
}
