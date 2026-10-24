using System;

namespace RimTalk.TTS.Bridge
{
    /// <summary>
    /// Thin, exception-safe access to RimTalk's ignore bookkeeping. Kept in the bridge layer so the
    /// pipeline does not touch host types directly for this one query.
    /// </summary>
    internal static class TalkHistoryBridge
    {
        public static bool IsIgnored(Guid dialogueId)
        {
            try
            {
                var target = RimTalkHooks.IsTalkIgnored;
                if (target == null) return false;
                return (bool)target.Invoke(null, new object[] { dialogueId });
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] IsTalkIgnored exception: {ex}");
                return false;
            }
        }
    }
}
