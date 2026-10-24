using System;
using RimTalk.TTS.Bridge;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// The invasive part of the overlay "show next" button: while it runs, RimTalk's reply pacing
    /// check reports "enough time has passed" so the queued line is displayed immediately.
    /// </summary>
    internal static class OverlayPacingPatch
    {
        private static bool _displayPassport;

        public static bool HasPassedPrefix(int pastTick, double seconds, ref bool __result)
        {
            if (!_displayPassport) return true;

            __result = true;
            return false;
        }

        /// <summary>Display the next queued line now, bypassing RimTalk's reply interval.</summary>
        public static void DisplayNext()
        {
            _displayPassport = true;
            try
            {
                var displayTalk = RimTalkHooks.DisplayTalk;
                if (displayTalk == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] Overlay display skipped: TalkService.DisplayTalk hook is unavailable");
                    return;
                }

                displayTalk.Invoke(null, null);
            }
            catch (Exception ex)
            {
                // Without the finally the pacing bypass would stay on for the rest of the session.
                TTSLog.Error($"[RimTalk.TTS] Overlay Display: TalkService.DisplayTalk() threw: {ex}");
            }
            finally
            {
                _displayPassport = false;
            }
        }
    }
}
