using System;
using System.Collections.Generic;

namespace RimTalk.TTS.Pipeline
{
    internal enum SpeechState
    {
        /// <summary>Accepted, waiting for preprocessing/cooldown/provider.</summary>
        Requested,
        /// <summary>The provider request is in flight; the timeout deadline is armed.</summary>
        Generating,
        /// <summary>Audio is available and waits for the display hook.</summary>
        Ready,
        /// <summary>Audio is playing; the line is being displayed right now.</summary>
        Playing,
        /// <summary>Request failed (no audio); the line displays without speech.</summary>
        Failed,
        /// <summary>Cancelled or timed out; the line displays without speech.</summary>
        Cancelled,
    }

    /// <summary>
    /// Single source of truth for the lifecycle of one dialogue line's speech.
    ///
    /// Replaces the four previously independent collections (block set, requested set, audio
    /// dictionary, timeout registry): a line is "held" exactly while its state is
    /// Requested/Generating/Ready, and every cleanup path goes through this class.
    ///
    /// Thread-safety: methods are called from the main thread (display hook, tick, ignore) and from
    /// worker threads (pipeline transitions); the internal lock covers all state.
    /// </summary>
    internal sealed class DialogueSpeechRegistry
    {
        private sealed class Entry
        {
            public Guid Id;
            public SpeechState State;
            public byte[] Audio;
            public double DeadlineSeconds;
            public int TimeoutSeconds;
            public string LastReason;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<Guid, Entry> _entries = new Dictionary<Guid, Entry>();
        // Every id ever requested this session. Keeps the "request once" rule even after the entry
        // was consumed, so a failed line is never retried in a loop.
        private readonly HashSet<Guid> _known = new HashSet<Guid>();

        /// <summary>
        /// Record a request. False when this line was already requested (active or finished) or when
        /// the registry is reset - the caller must then not start another generation.
        /// </summary>
        public bool TryRequest(Guid id, string reason)
        {
            if (id == Guid.Empty) return false;

            lock (_lock)
            {
                if (_known.Contains(id)) return false;

                _known.Add(id);
                _entries[id] = new Entry
                {
                    Id = id,
                    State = SpeechState.Requested,
                    LastReason = reason,
                };
                return true;
            }
        }

        public void MarkGenerating(Guid id, int timeoutSeconds)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(id, out var entry) || entry.State != SpeechState.Requested)
                    return;

                entry.State = SpeechState.Generating;
                entry.TimeoutSeconds = timeoutSeconds;
                entry.DeadlineSeconds = timeoutSeconds > 0
                    ? TTSMod.AppStopwatch.Elapsed.TotalSeconds + timeoutSeconds
                    : 0;
            }
        }

        /// <summary>The provider returned; stop the countdown but keep the current state.</summary>
        public void EndGenerating(Guid id)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(id, out var entry) && entry.State == SpeechState.Generating)
                {
                    entry.DeadlineSeconds = 0;
                }
            }
        }

        /// <summary>Store audio. False when the request was already cancelled/timed out.</summary>
        public bool MarkReady(Guid id, byte[] audio)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(id, out var entry)) return false;
                if (entry.State == SpeechState.Cancelled || entry.State == SpeechState.Failed) return false;

                entry.State = SpeechState.Ready;
                entry.Audio = audio;
                entry.DeadlineSeconds = 0;
                return true;
            }
        }

        public void MarkFailed(Guid id, string reason)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(id, out var entry)) return;
                if (entry.State == SpeechState.Cancelled) return;

                entry.State = SpeechState.Failed;
                entry.Audio = null;
                entry.DeadlineSeconds = 0;
                entry.LastReason = reason;
            }
        }

        /// <summary>Cancel (ignore / timeout / reset): the line will display without speech.</summary>
        public bool Cancel(Guid id, string reason)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(id, out var entry)) return false;
                if (entry.State == SpeechState.Cancelled) return false;

                entry.State = SpeechState.Cancelled;
                entry.Audio = null;
                entry.DeadlineSeconds = 0;
                entry.LastReason = reason;
                return true;
            }
        }

        /// <summary>Ready -> Playing, handing the audio to the playback service exactly once.</summary>
        public bool TryTakeForPlayback(Guid id, out byte[] audio)
        {
            audio = null;
            lock (_lock)
            {
                if (!_entries.TryGetValue(id, out var entry) || entry.State != SpeechState.Ready)
                    return false;
                if (entry.Audio == null || entry.Audio.Length == 0)
                    return false;

                audio = entry.Audio;
                entry.Audio = null;
                entry.State = SpeechState.Playing;
                return true;
            }
        }

        /// <summary>Remove a finished line (after it was displayed): its id stays "known".</summary>
        public void MarkDisplayed(Guid id)
        {
            lock (_lock)
            {
                _entries.Remove(id);
            }
        }

        public SpeechState? GetState(Guid id)
        {
            lock (_lock)
            {
                return _entries.TryGetValue(id, out var entry) ? entry.State : (SpeechState?)null;
            }
        }

        public bool IsKnown(Guid id)
        {
            lock (_lock)
            {
                return _known.Contains(id);
            }
        }

        /// <summary>Move expired Generating entries to Cancelled and return them for logging.</summary>
        public List<(Guid Id, int TimeoutSeconds)> TakeExpired(double nowSeconds)
        {
            var expired = new List<(Guid, int)>();
            lock (_lock)
            {
                foreach (var entry in _entries.Values)
                {
                    if (entry.State != SpeechState.Generating) continue;
                    if (entry.DeadlineSeconds <= 0 || entry.DeadlineSeconds > nowSeconds) continue;

                    entry.State = SpeechState.Cancelled;
                    entry.Audio = null;
                    entry.DeadlineSeconds = 0;
                    entry.LastReason = "timeout";
                    expired.Add((entry.Id, entry.TimeoutSeconds));
                }
            }
            return expired;
        }

        /// <summary>Active line ids (held from display) - used by reset logging.</summary>
        public List<Guid> SnapshotActive()
        {
            var ids = new List<Guid>();
            lock (_lock)
            {
                foreach (var entry in _entries.Values)
                {
                    if (entry.State == SpeechState.Requested || entry.State == SpeechState.Generating ||
                        entry.State == SpeechState.Ready || entry.State == SpeechState.Playing)
                    {
                        ids.Add(entry.Id);
                    }
                }
            }
            return ids;
        }

        public int ActiveCount
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        /// <summary>Single cleanup entry: drop every entry (and allow lines to be requested again).</summary>
        public void ResetAll(string reason)
        {
            lock (_lock)
            {
                _entries.Clear();
                _known.Clear();
            }
            TTSLog.Message($"[RimTalk.TTS] Speech registry reset ({reason})");
        }
    }
}
