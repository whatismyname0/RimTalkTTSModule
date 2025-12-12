using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Mod class for TTS module to handle settings
    /// </summary>
    public class TTSMod : Mod
    {
        public static System.Diagnostics.Stopwatch AppStopwatch = null;

        public TTSMod(ModContentPack content) : base(content)
        {
            // Settings are automatically loaded by Verse framework
            GetSettings<Data.TTSSettings>();
            AppStopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public override string SettingsCategory()
        {
            return "RimTalk TTS";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            var settings = GetSettings<Data.TTSSettings>();
            UI.SettingsUI.DrawTTSSettings(inRect, settings);
        }
    }
}