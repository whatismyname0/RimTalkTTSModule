using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Centralised logging wrapper for RimTalk.TTS.
    /// Message / Warning are only emitted when the player has "Log verbose"
    /// enabled in RimWorld's developer settings so they never clutter a normal
    /// playthrough.  Error is always emitted so genuine failures are visible.
    /// </summary>
    internal static class TTSLog
    {
        /// <summary>Emits an informational log entry. Visible only in verbose-log mode.</summary>
        public static void Message(string message)
        {
            if (Prefs.LogVerbose)
                Log.Message(message);
        }

        /// <summary>Emits a warning log entry. Visible only in verbose-log mode.</summary>
        public static void Warning(string message)
        {
            Log.Warning(message);
        }

        /// <summary>Emits an error log entry. Always visible.</summary>
        public static void Error(string message)
        {
            Log.Error(message);
        }

        /// <summary>
        /// Diagnostic trace used while tracking down the dialogue-blocking behaviour.
        /// Emitted at Warning level so it survives "Log verbose" being off, and prefixed
        /// so it can be filtered out of the player log.
        /// TODO: remove or re-gate once the blocking path is confirmed working.
        /// </summary>
        public static void Diag(string message)
        {
            Log.Warning("[RimTalk.TTS][diag] " + message);
        }
    }
}
