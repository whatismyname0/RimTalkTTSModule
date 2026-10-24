using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.TTS.Data;
using RimTalk.TTS.Patches;
using RimTalk.TTS.Config;
using RimTalk.TTS.Providers;

namespace RimTalk.TTS.UI
{
    public static partial class SettingsUI
    {
        private static string SupplierString(TTSSettings.TTSSupplier supplier)
        {
            if (supplier == TTSSettings.TTSSupplier.Custom)
            {
                var cfg = TtsRuntime.Current?.Settings?.GetCurrentCustomProvider();
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

        private static void DrawSupplierSection(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor, float contentWidth)
        {
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
                    editor.SelectSupplier(TTSSettings.TTSSupplier.FishAudio);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.CosyVoice".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.CosyVoice);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.IndexTTS".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.IndexTTS);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.AzureTTS".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.AzureTTS);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.EdgeTTS".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.EdgeTTS);
                }));
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.TTSSupplier.GeminiTTS".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.GeminiTTS);
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
                            editor.SelectCustomProvider(cpRef.Id);
                        }));
                    }
                }

                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.None".Translate(), delegate
                {
                    editor.SelectSupplier(TTSSettings.TTSSupplier.None);
                }));

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            // Custom provider management section (always shown)
            DrawCustomProviderManagement(listing, settings, editor);

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
                    // Custom providers decide for themselves whether a key is needed; the other
                    // cloud suppliers always require one.
                    bool apiKeyRequired = settings.Supplier != TTSSettings.TTSSupplier.Custom
                        || (settings.GetCurrentCustomProvider()?.RequiresApiKey ?? false);
                    string newApiKey = ValidatedTextEntry.Draw(
                        listing, "tts_supplier_api_key", currentApiKey, apiKeyRequired);
                    if (newApiKey != currentApiKey)
                    {
                        editor.SetSupplierApiKey(supplierKey, newApiKey);
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
                        editor.SetSupplierModel(supplierKey, "fishaudio-1");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelFaster".Translate(), currentModel == "s1"))
                    {
                        editor.SetSupplierModel(supplierKey, "s1");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS2Pro".Translate(), currentModel == "s2-pro"))
                    {
                        editor.SetSupplierModel(supplierKey, "s2-pro");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS21Pro".Translate(), currentModel == "s2.1-pro"))
                    {
                        editor.SetSupplierModel(supplierKey, "s2.1-pro");
                    }
                    if (listing.RadioButton("RimTalk.Settings.TTS.ModelS21ProFree".Translate(), currentModel == "s2.1-pro-free"))
                    {
                        editor.SetSupplierModel(supplierKey, "s2.1-pro-free");
                    }
                }

                // CosyVoice model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice)
                {
                    string currentModel = settings.GetSupplierModel(supplierKey);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.CosyVoice".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("FunAudioLLM/CosyVoice2-0.5B", currentModel == "FunAudioLLM/CosyVoice2-0.5B"))
                    {
                        editor.SetSupplierModel(supplierKey, "FunAudioLLM/CosyVoice2-0.5B");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelCosy = listing.TextEntry(currentModel ?? "");
                    if (customModelCosy != currentModel)
                    {
                        editor.SetSupplierModel(supplierKey, customModelCosy);
                    }
                }

                // IndexTTS model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
                {
                    string currentModel = settings.GetSupplierModel(supplierKey);
                    listing.Label("RimTalk.Settings.TTS.ModelLabel.IndexTTS".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("IndexTeam/IndexTTS-2", currentModel == "IndexTeam/IndexTTS-2"))
                    {
                        editor.SetSupplierModel(supplierKey, "IndexTeam/IndexTTS-2");
                    }
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelIndex = listing.TextEntry(currentModel ?? "");
                    if (customModelIndex != currentModel)
                    {
                        editor.SetSupplierModel(supplierKey, customModelIndex);
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
                            editor.SetSupplierModel(supplierKey, newModel);
                        }

                        // Edit provider config button
                        listing.Gap(6f);
                        if (listing.ButtonText("RimTalk.Settings.TTS.CustomProvider.EditConfig".Translate()))
                        {
                            Find.WindowStack.Add(new CustomProviderEditorWindow(customCfg, () =>
                            {
                                // Sync API key and model from config to supplier dictionaries
                                editor.SyncCustomProviderConfig(customCfg);
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
                                editor.SetSupplierRegion(settings.Supplier, region);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    
                    listing.Gap(6f);
                    listing.Label("RimTalk.Settings.TTS.CustomRegionLabel".Translate());
                    string customRegion = listing.TextEntry(currentRegion ?? "eastus");
                    if (customRegion != currentRegion)
                    {
                        editor.SetSupplierRegion(settings.Supplier, customRegion);
                    }
                }

                listing.Gap();
                
                int currentCooldown = settings.GetSupplierGenerateCooldown(supplierKey);
                listing.Label("RimTalk.Settings.TTS.GenerateCooldownMiliSecondsLabel".Translate(currentCooldown.ToString()));
                int newCooldown = (int)listing.Slider(currentCooldown, 0, 20000);
                if (newCooldown != currentCooldown)
                    editor.SetGenerateCooldown(supplierKey, newCooldown);

                listing.Gap();

                float currentVolume = settings.GetSupplierVolume(supplierKey);
                listing.Label("RimTalk.Settings.TTS.VolumeLabel".Translate(currentVolume.ToStringPercent()));
                float newVolume = listing.Slider(currentVolume, 0f, 1f);
                if (newVolume != currentVolume)
                    editor.SetVolume(supplierKey, newVolume);

                listing.Gap();

                float currentTemp = settings.GetSupplierTemperature(supplierKey);
                listing.Label("RimTalk.Settings.TTS.TemperatureLabel".Translate(currentTemp.ToString("F2")));
                float newTemp = listing.Slider(currentTemp, 0.7f, 1.0f);
                if (newTemp != currentTemp)
                    editor.SetTemperature(supplierKey, newTemp);

                // Top P
                float currentTopP = settings.GetSupplierTopP(supplierKey);
                listing.Label("RimTalk.Settings.TTS.TopPLabel".Translate(currentTopP.ToString("F2")));
                float newTopP = listing.Slider(currentTopP, 0.7f, 1.0f);
                if (newTopP != currentTopP)
                    editor.SetTopP(supplierKey, newTopP);

                listing.Gap();

                // Speed slider (0.25 - 4.0)
                float currentSpeed = settings.GetSupplierSpeed(supplierKey);
                listing.Label("RimTalk.Settings.TTS.SpeedLabel".Translate(currentSpeed.ToString("F2")));
                float newSpeed = listing.Slider(currentSpeed, 0.25f, 4.0f);
                if (newSpeed != currentSpeed)
                    editor.SetSpeed(supplierKey, newSpeed);

                listing.Gap();

                // Voice Models Section (per-supplier when a supplier is selected).
                System.Collections.Generic.List<VoiceModel> currentVoiceModels = settings.GetSupplierVoiceModels(supplierKey);
                DrawVoiceModelsSection(listing, settings, editor, contentWidth, currentVoiceModels);
            }
        }
    }
}
