using System;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// TTS settings UI renderer
    /// </summary>
    public static class SettingsUI
    {
        // Thread-safe queue for messages coming from background tasks
        private static System.Collections.Concurrent.ConcurrentQueue<(string text, MessageTypeDef type)> pendingMessages = new System.Collections.Concurrent.ConcurrentQueue<(string, MessageTypeDef)>();

        private static void EnqueueMessage(string text, MessageTypeDef type)
        {
            pendingMessages.Enqueue((text, type));
        }

        private static Vector2 scrollPosition = Vector2.zero;
        private static Vector2 mainScrollPosition = Vector2.zero;
        private static string processingPromptBuffer = "";
        private static bool processingPromptInitialized = false;
        // Buffers for upload UI
        private static string uploadPathBuffer = "";
        private static string uploadNameBuffer = "";
        private static string uploadTextBuffer = "";
        // Queue for actions that must run on the main thread (e.g. UI updates)
        private static System.Collections.Concurrent.ConcurrentQueue<System.Action> pendingActions = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        private static void EnqueueMainThreadAction(System.Action a)
        {
            if (a == null) return;
            pendingActions.Enqueue(a);
        }

        public static void DrawTTSSettings(Rect inRect, TTSSettings settings)
        {
            // First, run any actions enqueued by background tasks that must execute on the main thread
            while (pendingActions.TryDequeue(out var act))
            {
                act?.Invoke();
            }

            // Flush any messages enqueued by background tasks on the main thread
            while (pendingMessages.TryDequeue(out var _m))
            {
                Messages.Message(_m.text, _m.type, false);
            }

            // Calculate content height dynamically based on selected supplier's voice model count
            float baseHeight = 1280f; // base for other sections
            float voiceModelRowHeight = 40f; // Height per voice model row (30f + 6f gap + padding)
            var supplierVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            int voiceModelCount = supplierVoiceModels?.Count ?? 0;
            // If supplier supports SiliconFlow uploads, include upload UI height estimate
            float uploadSectionHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS) ? 280f : 0f;
            // Processing prompt area height (text area)
            float contentHeight = baseHeight + (voiceModelCount * voiceModelRowHeight) + uploadSectionHeight;
            bool isOn = settings.EnableTTS;
            
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);
            
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Enable TTS
            listing.CheckboxLabeled("RimTalk.Settings.TTS.Enable".Translate(), ref settings.EnableTTS, "RimTalk.Settings.TTS.EnableTooltip".Translate());

            // Handle TTS toggle
            if (isOn != settings.EnableTTS)
            {
                if (!settings.EnableTTS)
                {
                    // TTS turned OFF: stop all audio and clear state
                    AudioPlaybackService.StopAndClear();
                    Log.Message("[RimTalk.TTS] TTS disabled via settings");
                    listing.End();
                    Widgets.EndScrollView();
                    return;
                }
                else
                {
                    // TTS turned ON: reload map to register all pawns
                    if (Find.CurrentMap != null)
                    {
                        TTSService.ReloadMap(Find.CurrentMap);
                        Log.Message("[RimTalk.TTS] TTS enabled via settings, reloading map pawns");
                    }
                }
            }

            listing.Gap();

            listing.CheckboxLabeled("RimTalk.Settings.TTS.ButtonEnable".Translate(), ref settings.ButtonDisplay, "RimTalk.Settings.TTS.ButtonEnableTooltip".Translate());

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

            // Supplier selection (TTS backend)
            listing.Label("RimTalk.Settings.TTS.TTSSupplier".Translate());
            Rect supplierRect = listing.GetRect(Text.LineHeight);
            string supplierDisplay = SupplierString(settings.Supplier);

            if (Widgets.ButtonText(supplierRect, supplierDisplay))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.FishAudio".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.FishAudio;
                    TTSService.SetProvider(settings.Supplier);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.CosyVoice".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.CosyVoice;
                    TTSService.SetProvider(settings.Supplier);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.IndexTTS".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.IndexTTS;
                    TTSService.SetProvider(settings.Supplier);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.None".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.None;
                    TTSService.SetProvider(settings.Supplier);
                }));

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            // Per-supplier API key and model configuration
            if (settings.Supplier != TTSSettings.TTSSupplier.None)
            {
                listing.Label("RimTalk.Settings.TTS.ApiKey".Translate());
                string currentApiKey = settings.GetSupplierApiKey(settings.Supplier);
                string newApiKey = listing.TextEntry(currentApiKey ?? "");
                if (newApiKey != currentApiKey)
                {
                    settings.SetSupplierApiKey(settings.Supplier, newApiKey);
                }

                listing.Gap();

                // TTS Model Selection (example: FishAudio choices)
                if (settings.Supplier == TTSSettings.TTSSupplier.FishAudio)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel".Translate(currentModel));
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelHighQuality".Translate(), currentModel == "fishaudio-1"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "fishaudio-1");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelFaster".Translate(), currentModel == "s1"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "s1");
                    }
                }

                // CosyVoice model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.CozyVoice".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("FunAudioLLM/CosyVoice2-0.5B", currentModel == "FunAudioLLM/CosyVoice2-0.5B"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "FunAudioLLM/CosyVoice2-0.5B");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelCosy = listing.TextEntry(currentModel ?? "");
                    if (customModelCosy != currentModel)
                    {
                        settings.SetSupplierModel(settings.Supplier, customModelCosy);
                    }
                }

                // IndexTTS model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.IndexTTS".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("IndexTeam/IndexTTS-2", currentModel == "IndexTeam/IndexTTS-2"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "IndexTeam/IndexTTS-2");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelIndex = listing.TextEntry(currentModel ?? "");
                    if (customModelIndex != currentModel)
                    {
                        settings.SetSupplierModel(settings.Supplier, customModelIndex);
                    }
                }

                listing.Gap();
                
                int currentCooldown = settings.GetSupplierGenerateCooldown(settings.Supplier);
                listing.Label("RimTalk.Settings.TTS.GenerateCooldownMiliSecondsLabel".Translate(currentCooldown.ToString()));
                int newCooldown = (int)listing.Slider(currentCooldown, 0, 20000);
                if (newCooldown != currentCooldown)
                    settings.SetSupplierGenerateCooldown(settings.Supplier, newCooldown);

                listing.Gap();

                float currentVolume = settings.GetSupplierVolume(settings.Supplier);
                listing.Label("RimTalk.Settings.TTS.VolumeLabel".Translate(currentVolume.ToStringPercent()));
                float newVolume = listing.Slider(currentVolume, 0f, 1f);
                if (newVolume != currentVolume)
                    settings.SetSupplierVolume(settings.Supplier, newVolume);

                listing.Gap();

                float currentTemp = settings.GetSupplierTemperature(settings.Supplier);
                listing.Label("RimTalk.Settings.TTS.TemperatureLabel".Translate(currentTemp.ToString("F2")));
                float newTemp = listing.Slider(currentTemp, 0.7f, 1.0f);
                if (newTemp != currentTemp)
                    settings.SetSupplierTemperature(settings.Supplier, newTemp);

                // Top P
                float currentTopP = settings.GetSupplierTopP(settings.Supplier);
                listing.Label("RimTalk.Settings.TTS.TopPLabel".Translate(currentTopP.ToString("F2")));
                float newTopP = listing.Slider(currentTopP, 0.7f, 1.0f);
                if (newTopP != currentTopP)
                    settings.SetSupplierTopP(settings.Supplier, newTopP);

                listing.Gap();

                // Speed slider (0.25 - 4.0)
                float currentSpeed = settings.GetSupplierSpeed(settings.Supplier);
                listing.Label("RimTalk.Settings.TTS.SpeedLabel".Translate(currentSpeed.ToString("F2")));
                float newSpeed = listing.Slider(currentSpeed, 0.25f, 4.0f);
                if (newSpeed != currentSpeed)
                    settings.SetSupplierSpeed(settings.Supplier, newSpeed);

                listing.Gap();

                // Voice Models Section (per-supplier when a supplier is selected).
                System.Collections.Generic.List<VoiceModel> currentVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
                DrawVoiceModelsSection(listing, settings, viewRect.width, currentVoiceModels);
            }

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

        private static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            listing.Label("RimTalk.Settings.TTS.VoiceModels".Translate());

            // Default model selector (shows names from current voice model list)
            string defaultModelId = settings.GetSupplierDefaultVoiceModelId(settings.Supplier);

            string currentDefaultName = "RimTalk.Settings.TTS.NotSet".Translate();
            if (!string.IsNullOrEmpty(defaultModelId))
            {
                if (defaultModelId == VoiceModel.NONE_MODEL_ID)
                {
                    currentDefaultName = "RimTalk.Settings.TTS.NoneModel".Translate();
                }
                else if (voiceModels != null)
                {
                    var m = voiceModels.FirstOrDefault(x => x.ModelId == defaultModelId);
                    if (m != null)
                        currentDefaultName = m.GetDisplayName();
                }
            }

            if (listing.ButtonText("RimTalk.Settings.TTS.DefaultModel".Translate(currentDefaultName)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.ClearDefault".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(settings.Supplier, null);
                }));

                // Add NONE pseudo-model option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.NONE_MODEL_ID);
                }));

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        var display = vm.GetDisplayName();
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.SetSupplierDefaultVoiceModelId(settings.Supplier, vm.ModelId);
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

            listing.Gap(6f);

            // Upload user voice section (only shown when supplier supports SiliconFlow)
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                listing.Label("RimTalk.Settings.TTS.UploadUserVoiceLabel".Translate());
                listing.Label("RimTalk.Settings.TTS.UploadFilePath".Translate());
                uploadPathBuffer = listing.TextEntry(uploadPathBuffer ?? "");
                listing.Label("RimTalk.Settings.TTS.UploadName".Translate());
                uploadNameBuffer = listing.TextEntry(uploadNameBuffer ?? "");
                listing.Label("RimTalk.Settings.TTS.UploadTextPreview".Translate());
                uploadTextBuffer = listing.TextEntry(uploadTextBuffer ?? "");
                Rect uploadRect = listing.GetRect(30f);
                if (Widgets.ButtonText(uploadRect, "RimTalk.Settings.TTS.UploadButton".Translate()))
                {
                    // Validate local file
                    if (string.IsNullOrWhiteSpace(uploadPathBuffer) || !System.IO.File.Exists(uploadPathBuffer))
                    {
                        Messages.Message("RimTalk.TTS.UploadFailed.LocalFileNotFound".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else if (string.IsNullOrWhiteSpace(uploadNameBuffer))
                    {
                        Messages.Message("RimTalk.TTS.UploadFailed.NameEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        // Kick off upload in background
                        var apiKey = settings.GetSupplierApiKey(settings.Supplier);
                        var model = settings.GetSupplierModel(settings.Supplier);
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var uri = await Service.SiliconFlowClient.UploadUserVoiceAsync(apiKey, model, uploadPathBuffer, uploadNameBuffer, uploadTextBuffer);
                            if (!string.IsNullOrWhiteSpace(uri))
                            {
                                // Defer the Refresh and message to run on the main thread
                                EnqueueMainThreadAction(() =>
                                {
                                    Refresh();
                                    Messages.Message("RimTalk.TTS.UploadComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
                                });
                            }
                            else
                            {
                                EnqueueMainThreadAction(() => Messages.Message("RimTalk.TTS.UploadFailed.ServerError".Translate(), MessageTypeDefOf.RejectInput, false));
                            }
                        });
                    }
                }

                listing.Gap(6f);
            }

            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                if (voiceModels == null)
                    voiceModels = new System.Collections.Generic.List<VoiceModel>();
                voiceModels.Add(new VoiceModel { ModelName = "", ModelId = "" });
                settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
            }

            GUI.enabled = voiceModels != null && voiceModels.Count > 0;
            if (Widgets.ButtonText(removeButtonRect, "−"))
            {
                if (voiceModels != null && voiceModels.Count > 0)
                {
                    voiceModels.RemoveAt(voiceModels.Count - 1);
                    settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
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

            // Draw each model config row
            if (voiceModels != null)
            {
                for (int i = 0; i < voiceModels.Count; i++)
                {
                    DrawModelConfigRow(listing, voiceModels[i], i, voiceModels, width);
                }
            }

            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                listing.Gap(6f);
                // Single Reset Models button placed after the full list
                Rect resetAllRect = listing.GetRect(30f);
                if (Widgets.ButtonText(resetAllRect, "RimTalk.Settings.TTS.ResetModelsButton".Translate()))
                {
                    Refresh();
                }
            }
        }

        private static void Refresh()
        {
            var settings = TTSModule.Instance.GetSettings();
            var voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            var presets = TTSSettings.GetDefaultVoiceModels(settings.Supplier);
            if (presets != null && presets.Count > 0)
            {
                // Merge presets with existing user models: keep presets first, then
                // append any custom/empty entries that aren't already in presets.
                var merged = new System.Collections.Generic.List<VoiceModel>();
                foreach (var p in presets)
                {
                    if (p == null) continue;
                    merged.Add(new VoiceModel { ModelId = p.ModelId, ModelName = p.ModelName });
                }

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        if (vm == null) continue;
                        // preserve blank/custom entries (no ModelId) and any models not present in presets
                        if (string.IsNullOrWhiteSpace(vm.ModelId) || !merged.Any(x => x.ModelId == vm.ModelId))
                        {
                            merged.Add(new VoiceModel { ModelId = vm.ModelId, ModelName = vm.ModelName });
                        }
                    }
                }

                settings.SetSupplierVoiceModels(settings.Supplier, merged);
                voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            }

        // Also: when ResetModels is pressed above we attempt to sync user-uploaded voices from SiliconFlow.
        // The network call is done asynchronously and will merge any returned user voices into the settings.
        // (This runs when the user pressed ResetModels; the above code already applied system presets.)
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                var apiKey = settings.GetSupplierApiKey(settings.Supplier);
                var supplier = settings.Supplier;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    var list = await Service.SiliconFlowClient.ListUserVoicesAsync(apiKey);
                    if (list != null && list.Count > 0)
                    {
                        var current = settings.GetSupplierVoiceModels(supplier) ?? new System.Collections.Generic.List<Data.VoiceModel>();
                        bool changed = false;
                        foreach (var t in list)
                        {
                            if (!current.Exists(x => x.ModelId == t.Item1))
                            {
                                current.Add(new Data.VoiceModel(t.Item1, t.Item2));
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            settings.SetSupplierVoiceModels(supplier, current);
                        }
                        // Notify user that sync completed (enqueue to show on main thread)
                        EnqueueMessage("RimTalk.TTS.SyncComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                    }
                });
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

            // Delete button for this row
            Rect delRect = new Rect(idRect.xMax + 5f, y, 24f, height);
            if (Widgets.ButtonText(delRect, "X"))
            {
                // If looks like a SiliconFlow user voice (speech:...), attempt deletion
                string toDeleteId = model.ModelId ?? "";
                if (!string.IsNullOrWhiteSpace(toDeleteId) && toDeleteId.StartsWith("speech:"))
                {
                    var apiKey = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod mod ? (mod.GetSettings<TTSSettings>()?.GetSupplierApiKey(mod.GetSettings<TTSSettings>().Supplier) ?? "") : "";
                    var supplier = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod _m2 ? _m2.GetSettings<TTSSettings>().Supplier : TTSSettings.TTSSupplier.None;
                    // Use background task to delete
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        bool ok = await Service.SiliconFlowClient.DeleteUserVoiceAsync(apiKey, toDeleteId);
                        if (ok)
                            EnqueueMessage("RimTalk.TTS.DeleteComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                        else
                            EnqueueMessage("RimTalk.TTS.DeleteFailed".Translate(), MessageTypeDefOf.RejectInput);
                    });
                }

                // Remove locally regardless (server deletion attempted above)
                if (models != null && index >= 0 && index < models.Count)
                {
                    models.RemoveAt(index);
                }
            }
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

        private static string SupplierString(TTSSettings.TTSSupplier supplier)
        {
            return supplier switch
            {
                TTSSettings.TTSSupplier.FishAudio => "RimTalk.Settings.TTS.TTSSupplier.FishAudio".Translate(),
                TTSSettings.TTSSupplier.CosyVoice => "RimTalk.Settings.TTS.TTSSupplier.CosyVoice".Translate(),
                TTSSettings.TTSSupplier.IndexTTS => "RimTalk.Settings.TTS.TTSSupplier.IndexTTS".Translate(),
                TTSSettings.TTSSupplier.None => "RimTalk.Settings.TTS.None".Translate(),
                _ => supplier.ToString(),
            };
        }
    }
}
