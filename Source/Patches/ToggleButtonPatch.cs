using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Global on/off toggle icon in the play settings bar. The click is queued and applied on the
    /// next tick by <see cref="TickPatch"/> so the state change never runs inside the widget draw.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ToggleButtonPatch
    {
        private static readonly Texture2D RimTalkToggleIcon = ContentFinder<Texture2D>.Get("UI/ToggleButton");

        public static void Postfix(WidgetRow row, bool worldView)
        {
            var runtime = TtsRuntime.Current;
            if (runtime == null || !runtime.IsEnabled)
                return;

            if (worldView || row is null)
                return;

            var settings = runtime.Settings;
            if (settings.ButtonDisplay != true)
                return;

            bool onOff = settings.isOnButton;

            row.ToggleableIcon(ref onOff, RimTalkToggleIcon, "",
                SoundDefOf.Mouseover_ButtonToggle);

            if (onOff != settings.isOnButton)
            {
                settings.isOnButton = onOff;
                RimTalkPatches.RequestToggle(onOff);
            }
        }
    }
}
