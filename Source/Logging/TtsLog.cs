using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Centralised logging wrapper for RimTalk.TTS.
    /// Message / Warning are only emitted when the player has "Log verbose"
    /// enabled in RimWorld's developer settings so they never clutter a normal
    /// playthrough.  Error is always emitted so genuine failures are visible.
    ///
    /// Identical Warning/Error lines are throttled to one per 30 seconds so a failing supplier or
    /// audio clip cannot flood the log on every dialogue line. Suppressed repeats are reported on
    /// the next emission of the same line.
    /// </summary>
    internal static class TTSLog
    {
        private const double RepeatWindowSeconds = 30.0;
        private const int MaxTrackedMessages = 256;

        private sealed class RepeatState
        {
            public DateTime LastEmitUtc;
            public int Suppressed;
        }

        private static readonly object _throttleLock = new object();
        private static readonly Dictionary<string, RepeatState> _repeats = new Dictionary<string, RepeatState>();

        /// <summary>Emits an informational log entry. Visible only in verbose-log mode.</summary>
        public static void Message(string message)
        {
            if (Prefs.LogVerbose)
                Log.Message(message);
        }

        /// <summary>Emits a warning log entry. Visible only in verbose-log mode.</summary>
        public static void Warning(string message)
        {
            int suppressed;
            if (!ShouldEmit(message, out suppressed)) return;
            Log.Warning(AppendSuppressed(message, suppressed));
        }

        /// <summary>Emits an error log entry. Always visible.</summary>
        public static void Error(string message)
        {
            int suppressed;
            if (!ShouldEmit(message, out suppressed)) return;
            Log.Error(AppendSuppressed(message, suppressed));
        }

        private static string AppendSuppressed(string message, int suppressed)
        {
            return suppressed > 0
                ? message + $" (suppressed {suppressed} identical repeats)"
                : message;
        }

        /// <summary>True when this line may be written now; always true for its first occurrence.</summary>
        private static bool ShouldEmit(string message, out int suppressed)
        {
            suppressed = 0;
            if (string.IsNullOrEmpty(message)) return true;

            DateTime now = DateTime.UtcNow;
            lock (_throttleLock)
            {
                RepeatState state;
                if (!_repeats.TryGetValue(message, out state))
                {
                    _repeats[message] = new RepeatState { LastEmitUtc = now };
                    TrimIfNeeded();
                    return true;
                }

                if ((now - state.LastEmitUtc).TotalSeconds < RepeatWindowSeconds)
                {
                    state.Suppressed++;
                    return false;
                }

                suppressed = state.Suppressed;
                state.LastEmitUtc = now;
                state.Suppressed = 0;
                return true;
            }
        }

        private static void TrimIfNeeded()
        {
            // Bound memory for pathological unique-message churn; the first occurrence after a
            // reset is emitted again, so nothing important is permanently lost.
            if (_repeats.Count > MaxTrackedMessages)
                _repeats.Clear();
        }
    }
}
