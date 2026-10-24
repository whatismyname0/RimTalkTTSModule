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
        private static void DrawCustomProviderManagement(Listing_Standard listing, TTSSettings settings, TtsSettingsEditor editor)
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
                        editor.SelectCustomProvider(cp.Id);
                    }

                    // Edit button
                    Rect editRect = new Rect(selectRect.xMax + gap, rowRect.y, btnWidth, rowRect.height);
                    if (Widgets.ButtonText(editRect, "RimTalk.Settings.TTS.CustomProvider.Edit".Translate()))
                    {
                        var cpRef = cp;
                        Find.WindowStack.Add(new CustomProviderEditorWindow(cpRef, () =>
                        {
                            // Sync config changes to per-supplier dictionaries
                            editor.SyncCustomProviderConfig(cpRef);
                        }));
                    }

                    // Delete button
                    Rect deleteRect = new Rect(editRect.xMax + gap, rowRect.y, btnWidth, rowRect.height);
                    if (Widgets.ButtonText(deleteRect, "RimTalk.Settings.TTS.CustomProvider.Delete".Translate()))
                    {
                        editor.RemoveCustomProvider(cp.Id);
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
                        editor.AddCustomProvider(newConfig);
                    }
                }, isNew: true));
            }
        }
    }
}
