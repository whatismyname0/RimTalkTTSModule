using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;
using RimTalk.TTS.Service;
using RimTalk.TTS.Patch;

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
            float baseHeight = 2000f; // base for other sections
            float voiceModelRowHeight = 40f; // Height per voice model row (30f + 6f gap + padding)
            var supplierVoiceModels = settings.GetSupplierVoiceModels(settings.GetCurrentSupplierKey());
            int voiceModelCount = supplierVoiceModels?.Count ?? 0;
            // If supplier supports SiliconFlow uploads, include upload UI height estimate
            float uploadSectionHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS) ? 280f : 0f;
            // ResetModels button height (CosyVoice, IndexTTS, AzureTTS, EdgeTTS)
            float resetButtonHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS) ? 40f : 0f;
            // Processing prompt area height (text area)
            float contentHeight = baseHeight + (voiceModelCount * voiceModelRowHeight) + uploadSectionHeight + resetButtonHeight;
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
                    TTSLog.Message("[RimTalk.TTS] TTS disabled via settings");
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
                        TTSLog.Message("[RimTalk.TTS] TTS enabled via settings, reloading map pawns");
                    }
                }
            }

            listing.Gap();

            listing.CheckboxLabeled("RimTalk.Settings.TTS.ButtonEnable".Translate(), ref settings.ButtonDisplay, "RimTalk.Settings.TTS.ButtonEnableTooltip".Translate());

            listing.Gap();

            listing.CheckboxLabeled("RimTalk.Settings.TTS.ControlButtonEnable".Translate(), ref settings.ControlButtonDisplay, "RimTalk.Settings.TTS.ControlButtonEnableTooltip".Translate());

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
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.TTSConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Label("RimTalk.Settings.TTS.TTSSupplier".Translate());
            Rect supplierRect = listing.GetRect(Text.LineHeight);
            string supplierDisplay = SupplierString(settings.Supplier);

            if (Widgets.ButtonText(supplierRect, supplierDisplay))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.FishAudio".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.FishAudio;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.CosyVoice".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.CosyVoice;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.IndexTTS".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.IndexTTS;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.AzureTTS".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.AzureTTS;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.EdgeTTS".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.EdgeTTS;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.GeminiTTS".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.GeminiTTS;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));

                // Custom providers
                if (settings.CustomProviders != null && settings.CustomProviders.Count > 0)
                {
                    foreach (var cp in settings.CustomProviders)
                    {
                        if (cp == null || !cp.IsValid()) continue;
                        var cpRef = cp;
                        string cpName = cpRef.GetDisplayName();
                        options.Add(new FloatMenuOption($"[Custom] {cpName}", delegate
                        {
                            settings.CurrentCustomProviderId = cpRef.Id;
                            settings.Supplier = TTSSettings.TTSSupplier.Custom;
                            settings.EnsureCustomProviderDictionaries();
                            TTSService.SetProvider(settings.Supplier, settings);
                        }));
                    }
                }

                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.None".Translate(), delegate
                {
                    settings.Supplier = TTSSettings.TTSSupplier.None;
                    TTSService.SetProvider(settings.Supplier, settings);
                }));

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            // Custom provider management section (always shown)
            DrawCustomProviderManagement(listing, settings);

            listing.Gap();

            // Per-supplier API key and model configuration
            if (settings.Supplier != TTSSettings.TTSSupplier.None)
            {
                // Get the supplier key for dictionary access
                string supplierKey = settings.GetCurrentSupplierKey();

                // EdgeTTS doesn't need API key - skip it
                // Custom providers have API key in their config, but also allow override
                if (settings.Supplier != TTSSettings.TTSSupplier.EdgeTTS)
                {
                    listing.Label("RimTalk.Settings.TTS.ApiKey".Translate());
                    string currentApiKey = settings.GetSupplierApiKey(supplierKey);
                    string newApiKey = listing.TextEntry(currentApiKey ?? "");
                    if (newApiKey != currentApiKey)
                    {
                        settings.SetSupplierApiKey(supplierKey, newApiKey);
                    }

                    listing.Gap();
                }

                // TTS Model Selection (example: FishAudio choices)
                if (settings.Supplier == TTSSettings.TTSSupplier.FishAudio)
                {
                    string currentModel = settings.GetSupplierModel(supplierKey);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel".Translate(currentModel));
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelHighQuality".Translate(), currentModel == "fishaudio-1"))
                    {
                        settings.SetSupplierModel(supplierKey, "fishaudio-1");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelFaster".Translate(), currentModel == "s1"))
                    {
                        settings.SetSupplierModel(supplierKey, "s1");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS2Pro".Translate(), currentModel == "s2-pro"))
                    {
                        settings.SetSupplierModel(supplierKey, "s2-pro");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS21Pro".Translate(), currentModel == "s2.1-pro"))
                    {
                        settings.SetSupplierModel(supplierKey, "s2.1-pro");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS21ProFree".Translate(), currentModel == "s2.1-pro-free"))
                    {
                        settings.SetSupplierModel(supplierKey, "s2.1-pro-free");
                    }
                }

                // CosyVoice model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice)
                {
                    string currentModel = settings.GetSupplierModel(supplierKey);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.CosyVoice".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("FunAudioLLM/CosyVoice2-0.5B", currentModel == "FunAudioLLM/CosyVoice2-0.5B"))
                    {
                        settings.SetSupplierModel(supplierKey, "FunAudioLLM/CosyVoice2-0.5B");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelCosy = listing.TextEntry(currentModel ?? "");
                    if (customModelCosy != currentModel)
                    {
                        settings.SetSupplierModel(supplierKey, customModelCosy);
                    }
                }

                // IndexTTS model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
                {
                    string currentModel = settings.GetSupplierModel(supplierKey);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.IndexTTS".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("IndexTeam/IndexTTS-2", currentModel == "IndexTeam/IndexTTS-2"))
                    {
                        settings.SetSupplierModel(supplierKey, "IndexTeam/IndexTTS-2");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelIndex = listing.TextEntry(currentModel ?? "");
                    if (customModelIndex != currentModel)
                    {
                        settings.SetSupplierModel(supplierKey, customModelIndex);
                    }
                }

                // Custom provider: show model input field
                if (settings.Supplier == TTSSettings.TTSSupplier.Custom)
                {
                    var customCfg = settings.GetCurrentCustomProvider();
                    if (customCfg != null)
                    {
                        listing.Label("RimTalk.Settings.TTS.CustomProvider.CurrentModel".Translate(settings.GetSupplierModel(supplierKey) ?? customCfg.Model ?? "(not set)"));
                        listing.Gap(6f);
                        listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                        string currentModel = settings.GetSupplierModel(supplierKey);
                        string newModel = listing.TextEntry(currentModel ?? "");
                        if (newModel != currentModel)
                        {
                            settings.SetSupplierModel(supplierKey, newModel);
                        }

                        // Edit provider config button
                        listing.Gap(6f);
                        if (listing.ButtonText("RimTalk.Settings.TTS.CustomProvider.EditConfig".Translate()))
                        {
                            Find.WindowStack.Add(new CustomProviderEditorWindow(customCfg, () =>
                            {
                                // Sync API key and model from config to supplier dictionaries
                                settings.SetSupplierApiKey(supplierKey, customCfg.ApiKey);
                                settings.SetSupplierModel(supplierKey, customCfg.Model);
                                TTSService.SetProvider(settings.Supplier, settings);
                            }));
                        }
                    }
                }

                // AzureTTS region configuration
                if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS)
                {
                    string currentRegion = settings.GetSupplierRegion(settings.Supplier);
                    listing.Label("RimTalk.Settings.TTS.AzureRegionLabel".Translate(currentRegion ?? "eastus"));
                    listing.Gap(6f);
                    
                    // Common Azure regions for TTS
                    var regionOptions = new[] { "eastus", "westus", "westus2", "eastus2", "westeurope", "northeurope", 
                                               "southeastasia", "eastasia", "australiaeast", "japaneast", "canadacentral" };
                    
                    Rect regionRect = listing.GetRect(30f);
                    string regionDisplay = currentRegion ?? "eastus";
                    if (Widgets.ButtonText(regionRect, regionDisplay))
                    {
                        var options = new System.Collections.Generic.List<FloatMenuOption>();
                        foreach (var region in regionOptions)
                        {
                            options.Add(new FloatMenuOption(region, delegate
                            {
                                settings.SetSupplierRegion(settings.Supplier, region);
                                // Update provider with new region
                                TTSService.SetProvider(settings.Supplier, settings);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomRegionLabel".Translate());
                    string customRegion = listing.TextEntry(currentRegion ?? "eastus");
                    if (customRegion != currentRegion)
                    {
                        settings.SetSupplierRegion(settings.Supplier, customRegion);
                        // Update provider with new region
                        TTSService.SetProvider(settings.Supplier, settings);
                    }
                }

                listing.Gap();
                
                int currentCooldown = settings.GetSupplierGenerateCooldown(supplierKey);
                listing.Label("RimTalk.Settings.TTS.GenerateCooldownMiliSecondsLabel".Translate(currentCooldown.ToString()));
                int newCooldown = (int)listing.Slider(currentCooldown, 0, 20000);
                if (newCooldown != currentCooldown)
                    settings.SetSupplierGenerateCooldown(supplierKey, newCooldown);

                listing.Gap();

                float currentVolume = settings.GetSupplierVolume(supplierKey);
                listing.Label("RimTalk.Settings.TTS.VolumeLabel".Translate(currentVolume.ToStringPercent()));
                float newVolume = listing.Slider(currentVolume, 0f, 1f);
                if (newVolume != currentVolume)
                    settings.SetSupplierVolume(supplierKey, newVolume);

                listing.Gap();

                float currentTemp = settings.GetSupplierTemperature(supplierKey);
                listing.Label("RimTalk.Settings.TTS.TemperatureLabel".Translate(currentTemp.ToString("F2")));
                float newTemp = listing.Slider(currentTemp, 0.7f, 1.0f);
                if (newTemp != currentTemp)
                    settings.SetSupplierTemperature(supplierKey, newTemp);

                // Top P
                float currentTopP = settings.GetSupplierTopP(supplierKey);
                listing.Label("RimTalk.Settings.TTS.TopPLabel".Translate(currentTopP.ToString("F2")));
                float newTopP = listing.Slider(currentTopP, 0.7f, 1.0f);
                if (newTopP != currentTopP)
                    settings.SetSupplierTopP(supplierKey, newTopP);

                listing.Gap();

                // Speed slider (0.25 - 4.0)
                float currentSpeed = settings.GetSupplierSpeed(supplierKey);
                listing.Label("RimTalk.Settings.TTS.SpeedLabel".Translate(currentSpeed.ToString("F2")));
                float newSpeed = listing.Slider(currentSpeed, 0.25f, 4.0f);
                if (newSpeed != currentSpeed)
                    settings.SetSupplierSpeed(supplierKey, newSpeed);

                listing.Gap();

                // Voice Models Section (per-supplier when a supplier is selected).
                System.Collections.Generic.List<VoiceModel> currentVoiceModels = settings.GetSupplierVoiceModels(supplierKey);
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
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Text area for prompt - display buffer which contains either custom or default
            float textAreaHeight = 120f;
            Rect textAreaRect = listing.GetRect(textAreaHeight);
            string displayPrompt = processingPromptBuffer;
            string newPrompt = Widgets.TextArea(textAreaRect, displayPrompt);

            // Only save if user actually modified the content
            if (newPrompt != displayPrompt)
            {
                processingPromptBuffer = newPrompt.Replace("\\n", "\n");
                settings.CustomTTSProcessingPrompt = processingPromptBuffer;
            }

            listing.Gap(6f);

            // Reset buttons - First row: FishAudio, CosyVoice, IndexTTS
            Rect resetButtonsRect1 = listing.GetRect(30f);
            float gap = 4f;
            float btnW = (resetButtonsRect1.width - gap * 2) / 3f;
            Rect fishRectOld = new Rect(resetButtonsRect1.x, resetButtonsRect1.y, btnW/2, resetButtonsRect1.height);
            Rect fishRectS2 = new Rect(resetButtonsRect1.x+btnW/2, resetButtonsRect1.y, btnW/2, resetButtonsRect1.height);
            Rect cosyRect = new Rect(resetButtonsRect1.x + btnW + gap, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
            Rect indexRect = new Rect(resetButtonsRect1.x + (btnW + gap) * 2f, resetButtonsRect1.y, btnW, resetButtonsRect1.height);

            if (Widgets.ButtonText(fishRectOld, "RimTalk.Settings.TTS.ResetPrompt.FishAudioOld".Translate()))
            {
                settings.CustomTTSProcessingPrompt = "";
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt;
            }

            if (Widgets.ButtonText(fishRectS2, "RimTalk.Settings.TTS.ResetPrompt.FishAudioS2".Translate()))
            {
                settings.CustomTTSProcessingPrompt = "";
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_FishAudioS2;
            }

            if (Widgets.ButtonText(cosyRect, "RimTalk.Settings.TTS.ResetPrompt.CosyVoice".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
            }

            if (Widgets.ButtonText(indexRect, "RimTalk.Settings.TTS.ResetPrompt.IndexTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
            }

            // Reset buttons - Second row: AzureTTS, EdgeTTS, GeminiTTS
            listing.Gap(6f);
            Rect resetButtonsRect2 = listing.GetRect(30f);
            Rect azureRect = new Rect(resetButtonsRect2.x, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect edgeRect = new Rect(resetButtonsRect2.x + btnW + gap, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect geminiRect = new Rect(resetButtonsRect2.x + (btnW + gap) * 2f, resetButtonsRect2.y, btnW, resetButtonsRect2.height);

            if (Widgets.ButtonText(azureRect, "RimTalk.Settings.TTS.ResetPrompt.AzureTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
            }

            if (Widgets.ButtonText(edgeRect, "RimTalk.Settings.TTS.ResetPrompt.EdgeTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
            }

            if (Widgets.ButtonText(geminiRect, "RimTalk.Settings.TTS.ResetPrompt.GeminiTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
            }

            // Reset buttons - Third row: Custom
            if (settings.Supplier == TTSSettings.TTSSupplier.Custom)
            {
                listing.Gap(6f);
                Rect resetButtonsRect3 = listing.GetRect(30f);
                Rect customRect = new Rect(resetButtonsRect3.x, resetButtonsRect3.y, btnW, resetButtonsRect3.height);
                if (Widgets.ButtonText(customRect, "RimTalk.Settings.TTS.ResetPrompt.Custom".Translate()))
                {
                    settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_Custom;
                    processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_Custom;
                }
            }
        }

        private static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.VoiceModels".Translate());
            Text.Font = GameFont.Small;

            listing.Label("RimTalk.Settings.TTS.DefaultVoiceModel".Translate());

            // Show default model selector (now includes RULE_BASED as an option)
            DrawSimpleDefaultVoiceSelector(listing, settings, voiceModels);

            listing.Gap(6f);

            // Rules button and list
            if (listing.ButtonText("RimTalk.Settings.TTS.Rules".Translate()))
            {
                // Toggle rules visibility (using a static variable)
                showRulesList = !showRulesList;
            }

            if (showRulesList)
            {
                listing.Gap(6f);
                DrawVoiceRulesList(listing, settings, width, voiceModels);
            }

            // Player reference voice selection (always shown)
            DrawPlayerVoiceSelector(listing, settings);

            // Voice model list (model configurations)
            DrawVoiceModelsList(listing, settings, width, voiceModels);
        }

        private static bool showRulesList = false;
        private static int selectedRuleIndex = -1;
        private static int lastClickedRuleIndex = -1;
        private static float lastClickTime = 0f;
        private static readonly float DOUBLE_CLICK_TIME = 0.5f; // 500ms window for double click

        private static void DrawSimpleDefaultVoiceSelector(Listing_Standard listing, TTSSettings settings, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            // Default model selector (shows names from current voice model list)
            string supplierKey = settings.GetCurrentSupplierKey();
            string defaultModelId = settings.GetSupplierDefaultVoiceModelId(supplierKey);

            string currentDefaultName = "RimTalk.Settings.TTS.NotSet".Translate();
            if (!string.IsNullOrEmpty(defaultModelId))
            {
                if (defaultModelId == VoiceModel.NONE_MODEL_ID)
                {
                    currentDefaultName = "RimTalk.Settings.TTS.NoneModel".Translate();
                }
                else if (defaultModelId == VoiceModel.RULE_BASED_MODEL_ID)
                {
                    currentDefaultName = "RimTalk.Settings.TTS.RuleBased".Translate();
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
                    settings.SetSupplierDefaultVoiceModelId(supplierKey, null);
                }));

                // Add NONE pseudo-model option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(supplierKey, VoiceModel.NONE_MODEL_ID);
                }));

                // Add RULE_BASED option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.RuleBased".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(supplierKey, VoiceModel.RULE_BASED_MODEL_ID);
                }));

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        var display = vm.GetDisplayName();
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.SetSupplierDefaultVoiceModelId(supplierKey, vm.ModelId);
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawVoiceRulesList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            string supplierKey = settings.GetCurrentSupplierKey();
            var rules = settings.GetSupplierVoiceRules(supplierKey);
            
            // Rules list title
            listing.Label("RimTalk.Settings.TTS.AdvancedMode.RulesList".Translate());
            
            // Container box for rules
            float ruleListHeight = Mathf.Max(200f, rules.Count * 35f + 10f);
            Rect ruleListOuterRect = listing.GetRect(ruleListHeight);
            
            Widgets.DrawBoxSolid(ruleListOuterRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            Widgets.DrawBox(ruleListOuterRect);
            
            Rect ruleListInnerRect = ruleListOuterRect.ContractedBy(5f);
            Rect ruleListViewRect = new Rect(0f, 0f, ruleListInnerRect.width - 20f, rules.Count * 35f);
            
            Vector2 ruleScrollPos = Vector2.zero;
            Widgets.BeginScrollView(ruleListInnerRect, ref ruleScrollPos, ruleListViewRect);
            
            float y = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                Rect ruleRect = new Rect(0f, y, ruleListViewRect.width, 30f);
                
                // Highlight selected rule
                if (i == selectedRuleIndex)
                {
                    Widgets.DrawHighlight(ruleRect);
                }
                
                // Rule display text (truncated if needed)
                string displayText = rule.GetDisplayString(ruleRect.width - 10f);
                
                // Double click detection: check if same item clicked within time window
                if (Widgets.ButtonInvisible(ruleRect))
                {
                    float currentTime = Time.realtimeSinceStartup;
                    bool isDoubleClick = (i == lastClickedRuleIndex) && 
                                        (currentTime - lastClickTime < DOUBLE_CLICK_TIME);
                    
                    if (isDoubleClick)
                    {
                        // Double click - open editor
                        Find.WindowStack.Add(new VoiceRuleEditorWindow(rule, settings, () =>
                        {
                            settings.SetSupplierVoiceRules(supplierKey, rules);
                        }));
                        lastClickedRuleIndex = -1; // Reset to prevent triple-click
                    }
                    else
                    {
                        // Single click - select and record click
                        selectedRuleIndex = i;
                        lastClickedRuleIndex = i;
                        lastClickTime = currentTime;
                    }
                }
                
                Rect labelRect = new Rect(ruleRect.x + 5f, ruleRect.y, ruleRect.width - 10f, ruleRect.height);
                Widgets.Label(labelRect, displayText);
                
                y += 35f;
            }
            
            Widgets.EndScrollView();
            
            listing.Gap(6f);
            
            // Control buttons: ↑ ↓ + ×
            Rect buttonRowRect = listing.GetRect(30f);
            float buttonWidth = 40f;
            float buttonGap = 5f;
            
            Rect upButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(upButtonRect, "↑"))
            {
                if (selectedRuleIndex > 0)
                {
                    var temp = rules[selectedRuleIndex];
                    rules[selectedRuleIndex] = rules[selectedRuleIndex - 1];
                    rules[selectedRuleIndex - 1] = temp;
                    selectedRuleIndex--;
                    settings.SetSupplierVoiceRules(supplierKey, rules);
                }
            }
            
            Rect downButtonRect = new Rect(upButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(downButtonRect, "↓"))
            {
                if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count - 1)
                {
                    var temp = rules[selectedRuleIndex];
                    rules[selectedRuleIndex] = rules[selectedRuleIndex + 1];
                    rules[selectedRuleIndex + 1] = temp;
                    selectedRuleIndex++;
                    settings.SetSupplierVoiceRules(supplierKey, rules);
                }
            }
            
            Rect addButtonRect = new Rect(downButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                var newRule = new VoiceAssignmentRule();
                Find.WindowStack.Add(new VoiceRuleEditorWindow(newRule, settings, () =>
                {
                    rules.Add(newRule);
                    settings.SetSupplierVoiceRules(supplierKey, rules);
                    selectedRuleIndex = rules.Count - 1;
                }));
            }
            
            Rect deleteButtonRect = new Rect(addButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(deleteButtonRect, "×"))
            {
                if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count)
                {
                    rules.RemoveAt(selectedRuleIndex);
                    settings.SetSupplierVoiceRules(supplierKey, rules);
                    selectedRuleIndex = -1;
                }
            }

            listing.Gap();
        }

        private static void DrawPlayerVoiceSelector(Listing_Standard listing, TTSSettings settings)
        {
            // Player reference voice selection (single-line dropdown using supplier voice models)
            listing.Gap(6f);
            listing.Label("RimTalk.Settings.TTS.PlayerVoiceModel".Translate());
            Rect playerRect = listing.GetRect(Text.LineHeight);

            string currentPlayerSelectionName;
            var playerModelId = settings.PlayerReferenceVoiceModelId;
            if (playerModelId == VoiceModel.NONE_MODEL_ID)
            {
                currentPlayerSelectionName = "RimTalk.Settings.TTS.NoneModel".Translate();
            }
            else
            {
                var vm = settings.GetSupplierVoiceModels(settings.GetCurrentSupplierKey())?.FirstOrDefault(x => x.ModelId == playerModelId);
                currentPlayerSelectionName = vm?.GetDisplayName() ?? playerModelId;
            }

            if (Widgets.ButtonText(playerRect, currentPlayerSelectionName))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();

                // None
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;
                    RimTalkPatches.UpdatePlayerPawnVoice();
                }));

                var list = settings.GetSupplierVoiceModels(settings.GetCurrentSupplierKey());
                if (list != null)
                {
                    foreach (var vm in list)
                    {
                        var display = vm.GetDisplayName();
                        var id = vm.ModelId ?? "";
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.PlayerReferenceVoiceModelId = id;
                            RimTalkPatches.UpdatePlayerPawnVoice();
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();
        }

        private static void DrawVoiceModelsList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
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
                        var apiKey = settings.GetSupplierApiKey(settings.GetCurrentSupplierKey());
                        var model = settings.GetSupplierModel(settings.GetCurrentSupplierKey());
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
                settings.SetSupplierVoiceModels(settings.GetCurrentSupplierKey(), voiceModels);
            }

            GUI.enabled = voiceModels != null && voiceModels.Count > 0;
            if (Widgets.ButtonText(removeButtonRect, "−"))
            {
                if (voiceModels != null && voiceModels.Count > 0)
                {
                    voiceModels.RemoveAt(voiceModels.Count - 1);
                    settings.SetSupplierVoiceModels(settings.GetCurrentSupplierKey(), voiceModels);
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

            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS || settings.Supplier == TTSSettings.TTSSupplier.GeminiTTS)
            {
                listing.Gap(6f);
                // Single Reset Models button placed after the full list
                Rect resetAllRect = listing.GetRect(30f);
                if (Widgets.ButtonText(resetAllRect, "RimTalk.Settings.TTS.ResetModelsButton".Translate()))
                {
                    Refresh();
                }
            }

            // Voice library button for AzureTTS/EdgeTTS
            if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS)
            {
                listing.Gap(6f);
                Rect voiceLibraryRect = listing.GetRect(30f);
                string buttonLabel = settings.Supplier == TTSSettings.TTSSupplier.AzureTTS 
                    ? "RimTalk.Settings.TTS.AzureVoiceLibrary".Translate() 
                    : "RimTalk.Settings.TTS.EdgeVoiceLibrary".Translate();
                if (Widgets.ButtonText(voiceLibraryRect, buttonLabel))
                {
                    Find.WindowStack.Add(new VoiceLibraryWindow(settings.Supplier));
                }
            }
        }

        private static void Refresh()
        {
            var settings = TTSConfig.Settings;
            string supplierKey = settings.GetCurrentSupplierKey();
            var voiceModels = settings.GetSupplierVoiceModels(supplierKey);
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

                settings.SetSupplierVoiceModels(supplierKey, merged);
                voiceModels = settings.GetSupplierVoiceModels(supplierKey);
            }

        // Also: when ResetModels is pressed above we attempt to sync user-uploaded voices from SiliconFlow.
        // The network call is done asynchronously and will merge any returned user voices into the settings.
        // (This runs when the user pressed ResetModels; the above code already applied system presets.)
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                var apiKey = settings.GetSupplierApiKey(supplierKey);
                var supplier = settings.Supplier;
                var sKey = supplierKey;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    var list = await Service.SiliconFlowClient.ListUserVoicesAsync(apiKey);
                    if (list != null && list.Count > 0)
                    {
                        var current = settings.GetSupplierVoiceModels(sKey) ?? new System.Collections.Generic.List<Data.VoiceModel>();
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
                            settings.SetSupplierVoiceModels(sKey, current);
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
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.LLMApiConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Gap(6f);

            // Provider Selection
            listing.Label("RimTalk.Settings.TTS.ProviderLabel".Translate());
            if (listing.RadioButton("RimTalk.Settings.TTS.ProviderSkip".Translate(), settings.ApiProvider == TTSApiProvider.Skip, tooltip: "RimTalk.Settings.TTS.ProviderSkipTooltip".Translate()))
            {
                settings.ApiProvider = TTSApiProvider.Skip;
            }
            if (listing.RadioButton("RimTalk.Settings.TTS.ProviderRimTalkSame".Translate(), settings.ApiProvider == TTSApiProvider.RimTalkSame, tooltip: "RimTalk.Settings.TTS.ProviderRimTalkSameTooltip".Translate()))
            {
                settings.ApiProvider = TTSApiProvider.RimTalkSame;
            }
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

            // Skip and RimTalkSame don't need Model/ApiKey/BaseUrl fields
            if (settings.ApiProvider != TTSApiProvider.Skip && settings.ApiProvider != TTSApiProvider.RimTalkSame)
            {
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

            listing.Gap(6f);

            // Remove brackets during preprocessing (not relevant for Skip)
            if (settings.ApiProvider != TTSApiProvider.Skip)
            {
                listing.CheckboxLabeled("RimTalk.Settings.TTS.RemoveBracketsInPreProcess".Translate(), ref settings.RemoveBracketsInPreProcess, "RimTalk.Settings.TTS.RemoveBracketsInPreProcessTooltip".Translate());
            }
        }

        private static string SupplierString(TTSSettings.TTSSupplier supplier)
        {
            if (supplier == TTSSettings.TTSSupplier.Custom)
            {
                var cfg = TTSConfig.Settings?.GetCurrentCustomProvider();
                if (cfg != null) return $"[Custom] {cfg.GetDisplayName()}";
                return "RimTalk.Settings.TTS.CustomProvider.Unnamed".Translate();
            }
            return supplier switch
            {
                TTSSettings.TTSSupplier.FishAudio => "RimTalk.Settings.TTS.TTSSupplier.FishAudio".Translate(),
                TTSSettings.TTSSupplier.CosyVoice => "RimTalk.Settings.TTS.TTSSupplier.CosyVoice".Translate(),
                TTSSettings.TTSSupplier.IndexTTS => "RimTalk.Settings.TTS.TTSSupplier.IndexTTS".Translate(),
                TTSSettings.TTSSupplier.AzureTTS => "RimTalk.Settings.TTS.TTSSupplier.AzureTTS".Translate(),
                TTSSettings.TTSSupplier.EdgeTTS => "RimTalk.Settings.TTS.TTSSupplier.EdgeTTS".Translate(),
                TTSSettings.TTSSupplier.GeminiTTS => "RimTalk.Settings.TTS.TTSSupplier.GeminiTTS".Translate(),
                TTSSettings.TTSSupplier.None => "RimTalk.Settings.TTS.None".Translate(),
                _ => supplier.ToString(),
            };
        }

        private static void DrawCustomProviderManagement(Listing_Standard listing, TTSSettings settings)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.Section".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            GUI.color = Color.cyan;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.Description".Translate());
            GUI.color = Color.white;
            listing.Gap(6f);

            // List existing custom providers
            if (settings.CustomProviders != null && settings.CustomProviders.Count > 0)
            {
                for (int i = 0; i < settings.CustomProviders.Count; i++)
                {
                    var cp = settings.CustomProviders[i];
                    if (cp == null) continue;

                    Rect rowRect = listing.GetRect(30f);
                    float btnWidth = 60f;
                    float gap = 4f;

                    // Name label (highlight if currently active)
                    bool isActive = settings.Supplier == TTSSettings.TTSSupplier.Custom && settings.CurrentCustomProviderId == cp.Id;
                    Rect nameRect = new Rect(rowRect.x, rowRect.y, rowRect.width - (btnWidth + gap) * 3, rowRect.height);
                    if (isActive)
                    {
                        GUI.color = Color.green;
                        Widgets.Label(nameRect, $"▶ {cp.GetDisplayName()} ({cp.BaseUrl})");
                        GUI.color = Color.white;
                    }
                    else
                    {
                        Widgets.Label(nameRect, $"  {cp.GetDisplayName()} ({cp.BaseUrl})");
                    }

                    // Select button
                    Rect selectRect = new Rect(rowRect.xMax - (btnWidth + gap) * 3 + gap, rowRect.y, btnWidth, rowRect.height);
                    if (Widgets.ButtonText(selectRect, "RimTalk.Settings.TTS.CustomProvider.Select".Translate()))
                    {
                        settings.CurrentCustomProviderId = cp.Id;
                        settings.Supplier = TTSSettings.TTSSupplier.Custom;
                        settings.EnsureCustomProviderDictionaries();
                        TTSService.SetProvider(settings.Supplier, settings);
                    }

                    // Edit button
                    Rect editRect = new Rect(selectRect.xMax + gap, rowRect.y, btnWidth, rowRect.height);
                    if (Widgets.ButtonText(editRect, "RimTalk.Settings.TTS.CustomProvider.Edit".Translate()))
                    {
                        var cpRef = cp;
                        Find.WindowStack.Add(new CustomProviderEditorWindow(cpRef, () =>
                        {
                            // Sync config changes to per-supplier dictionaries
                            string key = cpRef.GetSupplierKey();
                            settings.SetSupplierApiKey(key, cpRef.ApiKey);
                            settings.SetSupplierModel(key, cpRef.Model);
                            if (settings.Supplier == TTSSettings.TTSSupplier.Custom && settings.CurrentCustomProviderId == cpRef.Id)
                            {
                                TTSService.SetProvider(settings.Supplier, settings);
                            }
                        }));
                    }

                    // Delete button
                    Rect deleteRect = new Rect(editRect.xMax + gap, rowRect.y, btnWidth, rowRect.height);
                    if (Widgets.ButtonText(deleteRect, "RimTalk.Settings.TTS.CustomProvider.Delete".Translate()))
                    {
                        string idToDelete = cp.Id;
                        settings.RemoveCustomProvider(idToDelete);
                        if (settings.Supplier == TTSSettings.TTSSupplier.Custom && string.IsNullOrEmpty(settings.CurrentCustomProviderId))
                        {
                            settings.Supplier = TTSSettings.TTSSupplier.None;
                            TTSService.SetProvider(settings.Supplier, settings);
                        }
                        break; // list modified, break to avoid iteration errors
                    }

                    listing.Gap(2f);
                }
            }
            else
            {
                listing.Label("RimTalk.Settings.TTS.CustomProvider.NoProviders".Translate());
            }

            listing.Gap(6f);

            // Add new custom provider button
            if (listing.ButtonText("RimTalk.Settings.TTS.CustomProvider.AddNew".Translate()))
            {
                var newConfig = new Data.CustomProviderConfig();
                Find.WindowStack.Add(new CustomProviderEditorWindow(newConfig, () =>
                {
                    if (newConfig.IsValid())
                    {
                        if (settings.CustomProviders == null)
                            settings.CustomProviders = new System.Collections.Generic.List<Data.CustomProviderConfig>();
                        settings.CustomProviders.Add(newConfig);
                        settings.EnsureCustomProviderDictionaries();
                    }
                }, isNew: true));
            }
        }
    }
}
