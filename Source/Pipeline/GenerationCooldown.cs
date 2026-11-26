using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Pipeline
{
    /// <summary>
    /// Per-supplier request pacing, kept separate from the dialogue state machine so the schedule is
    /// testable and the pipeline stays readable.
    /// </summary>
    internal sealed class GenerationCooldown
    {
        private readonly object _lock = new object();
        private int _lastGenerateTimestampMs;
        private int _waitingRequests;
        // Bumped by Reset. A waiter that was counted before the reset must not release a slot of
        // the new (empty) schedule - that would drive the counter negative.
        private int _scheduleGeneration;

        /// <summary>Wait until this request's slot in the supplier's cooldown schedule.</summary>
        /// <param name="bypassQueue">
        /// Head-of-batch requests skip the queue: they are sent immediately, and the schedule is
        /// re-anchored to that send so the remaining lines are spaced after it.
        /// </param>
        public async Task WaitAsync(int cooldownMs, CancellationToken cancellationToken, bool bypassQueue = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bypassQueue)
            {
                lock (_lock)
                {
                    _lastGenerateTimestampMs = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                }
                return;
            }

            int generation;
            int cooldownEndMs;
            int nowMs = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;

            // Count this request and read the schedule in one lock: the slot it waits for and the
            // slot it releases afterwards have to come from the same snapshot.
            lock (_lock)
            {
                generation = _scheduleGeneration;
                _waitingRequests++;
                cooldownEndMs = _waitingRequests * cooldownMs + _lastGenerateTimestampMs;
            }

            bool requestSent = false;
            try
            {
                if (nowMs < cooldownEndMs)
                {
                    await Task.Delay(cooldownEndMs - nowMs, cancellationToken);
                }
                requestSent = true;
            }
            finally
            {
                // A cancelled wait (the line was ignored while queued) must still release its slot:
                // leaking the counter raises the cooldown floor of every later request by one
                // cooldown per leaked slot, for the rest of the session.
                lock (_lock)
                {
                    if (_scheduleGeneration == generation)
                    {
                        if (requestSent)
                        {
                            _lastGenerateTimestampMs = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                        }
                        _waitingRequests--;
                    }
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _lastGenerateTimestampMs = 0;
                _waitingRequests = 0;
                _scheduleGeneration++;
            }
        }
    }
}
