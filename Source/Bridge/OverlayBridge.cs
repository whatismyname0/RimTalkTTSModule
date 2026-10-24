using System;
using UnityEngine;

namespace RimTalk.TTS.Bridge
{
    /// <summary>
    /// Reads RimTalk's overlay geometry through the field resolved by <see cref="RimTalkHooks"/>.
    /// Kept in the bridge so no patch or UI code contains the host member name.
    /// </summary>
    internal static class OverlayBridge
    {
        public static bool TryGetGearRect(object overlayInstance, out Rect rect)
        {
            rect = default;
            if (overlayInstance == null) return false;

            var field = RimTalkHooks.OverlayGearIconRect;
            if (field == null) return false;

            try
            {
                if (field.GetValue(overlayInstance) is Rect value)
                {
                    rect = value;
                    return true;
                }
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Could not read overlay gear rect: {ex.Message}");
            }

            return false;
        }
    }
}
