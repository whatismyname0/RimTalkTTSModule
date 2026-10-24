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
        private static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.VoiceModels".Translate());
            Text.Font = GameFont.Small;

            listing.Label("RimTalk.Settings.TTS.DefaultVoiceModel".Translate());

            // Show default model selector (now includes RULE_BASED as an option)
            DrawSimpleDefaultVoiceSelector(listing, settings, editor, voiceModels);

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
                DrawVoiceRulesList(listing, settings, editor, width, voiceModels);
            }

            // Player reference voice selection (always shown)
            DrawPlayerVoiceSelector(listing, settings, editor);

            // Voice model list (model configurations)
            DrawVoiceModelsList(listing, settings, editor, width, voiceModels);
        }

        private static bool showRulesList = false;
        private static int selectedRuleIndex = -1;
        private static int lastClickedRuleIndex = -1;
        private static float lastClickTime = 0f;
        private static readonly float DOUBLE_CLICK_TIME = 0.5f; // 500ms window for double click

        private static void DrawSimpleDefaultVoiceSelector(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor, System.Collections.Generic.List<VoiceModel> voiceModels)
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
                    editor.SetDefaultVoiceModel(supplierKey, null);
                }));

                // Add NONE pseudo-model option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.NoneModel".Translate(), delegate
                {
                    editor.SetDefaultVoiceModel(supplierKey, VoiceModel.NONE_MODEL_ID);
                }));

                // Add RULE_BASED option
                options.Add(new FloatMenuOption("RimTalk.Settings.TTS.RuleBased".Translate(), delegate
                {
                    editor.SetDefaultVoiceModel(supplierKey, VoiceModel.RULE_BASED_MODEL_ID);
                }));

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        var display = vm.GetDisplayName();
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            editor.SetDefaultVoiceModel(supplierKey, vm.ModelId);
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawVoiceRulesList(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
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
                            editor.SetVoiceRules(supplierKey, rules);
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
                    editor.SetVoiceRules(supplierKey, rules);
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
                    editor.SetVoiceRules(supplierKey, rules);
                }
            }
            
            Rect addButtonRect = new Rect(downButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                var newRule = new VoiceAssignmentRule();
                Find.WindowStack.Add(new VoiceRuleEditorWindow(newRule, settings, () =>
                {
                    rules.Add(newRule);
                    editor.SetVoiceRules(supplierKey, rules);
                    selectedRuleIndex = rules.Count - 1;
                }));
            }
            
            Rect deleteButtonRect = new Rect(addButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(deleteButtonRect, "×"))
            {
                if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count)
                {
                    rules.RemoveAt(selectedRuleIndex);
                    editor.SetVoiceRules(supplierKey, rules);
                    selectedRuleIndex = -1;
                }
            }

            listing.Gap();
        }

        private static void DrawPlayerVoiceSelector(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor)
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
                    editor.SetPlayerReferenceVoice(VoiceModel.NONE_MODEL_ID);
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
                            editor.SetPlayerReferenceVoice(id);
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();
        }

        private static void DrawVoiceModelsList(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
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
                    else if (string.IsNullOrWhiteSpace(uploadTextBuffer))
                    {
                        // "text" is a required field of the SiliconFlow upload API.
                        Messages.Message("RimTalk.TTS.UploadFailed.TextEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        // Kick off upload in background. Snapshot the buffers so edits made
                        // while the request is in flight cannot change what gets uploaded.
                        var apiKey = settings.GetSupplierApiKey(settings.GetCurrentSupplierKey());
                        var model = settings.GetSupplierModel(settings.GetCurrentSupplierKey());
                        string uploadPath = uploadPathBuffer;
                        string uploadName = uploadNameBuffer;
                        string uploadText = uploadTextBuffer;
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var uri = await SiliconFlowClient.UploadUserVoiceAsync(apiKey, model, uploadPath, uploadName, uploadText);
                            if (!string.IsNullOrWhiteSpace(uri))
                            {
                                // Defer the Refresh and message to run on the main thread
                                UiMessageQueue.EnqueueAction(() =>
                                {
                                    Refresh();
                                    Messages.Message("RimTalk.TTS.UploadComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
                                });
                            }
                            else
                            {
                                UiMessageQueue.EnqueueAction(() => Messages.Message("RimTalk.TTS.UploadFailed.ServerError".Translate(), MessageTypeDefOf.RejectInput, false));
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
                editor.SetVoiceModels(settings.GetCurrentSupplierKey(), voiceModels);
            }

            GUI.enabled = voiceModels != null && voiceModels.Count > 0;
            if (Widgets.ButtonText(removeButtonRect, "−"))
            {
                if (voiceModels != null && voiceModels.Count > 0)
                {
                    voiceModels.RemoveAt(voiceModels.Count - 1);
                    editor.SetVoiceModels(settings.GetCurrentSupplierKey(), voiceModels);
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
            var settings = TtsRuntime.Current?.Settings;
            if (settings == null) return;
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
                var sKey = supplierKey;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    var list = await SiliconFlowClient.ListUserVoicesAsync(apiKey);
                    if (list != null && list.Count > 0)
                    {
                        // Apply on the main thread: the same List<VoiceModel> instance is
                        // enumerated by the IMGUI code above, so mutating it from a
                        // background thread can throw or silently drop rows.
                        UiMessageQueue.EnqueueAction(() =>
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
                        });
                        // Notify user that sync completed (enqueue to show on main thread)
                        UiMessageQueue.EnqueueMessage("RimTalk.TTS.SyncComplete".Translate(), MessageTypeDefOf.TaskCompletion);
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
                string toDeleteId = model.ModelId ?? "";
                var settingsRef = TtsRuntime.Current?.Settings;

                // If it looks like a SiliconFlow user voice (speech:...), attempt server deletion
                if (settingsRef != null && !string.IsNullOrWhiteSpace(toDeleteId) && toDeleteId.StartsWith("speech:"))
                {
                    string supplierKey = settingsRef.GetCurrentSupplierKey();
                    string apiKey = settingsRef.GetSupplierApiKey(supplierKey);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            bool ok = await SiliconFlowClient.DeleteUserVoiceAsync(apiKey, toDeleteId);
                            if (ok)
                                UiMessageQueue.EnqueueMessage("RimTalk.TTS.DeleteComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                            else
                                UiMessageQueue.EnqueueMessage("RimTalk.TTS.DeleteFailed".Translate(), MessageTypeDefOf.RejectInput);
                        });
                    }
                }

                // If the default voice points at this entry, reset it so requests do not
                // keep referencing a voice that no longer exists.
                if (settingsRef != null && !string.IsNullOrWhiteSpace(toDeleteId))
                {
                    string supplierKey = settingsRef.GetCurrentSupplierKey();
                    if (settingsRef.GetSupplierDefaultVoiceModelId(supplierKey) == toDeleteId)
                    {
                        settingsRef.SetSupplierDefaultVoiceModelId(supplierKey, VoiceModel.NONE_MODEL_ID);
                    }
                }

                // Defer local removal: mutating the list while DrawVoiceModelsList is
                // iterating it would shift the remaining rows in the same frame.
                var toRemove = model;
                UiMessageQueue.EnqueueAction(() =>
                {
                    if (models != null) models.Remove(toRemove);
                });
            }
        }
    }
}
