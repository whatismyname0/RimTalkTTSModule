using System;
using Verse;

namespace RimTalk.TTS.Util
{
    public static class ErrorUtil
    {
        public static void LogException(string context, Exception ex)
        {
            try
            {
                Log.Error($"[RimTalk.TTS] {context} exception: {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}");
            }
            catch
            {
                // swallow
            }
        }
    }
}
