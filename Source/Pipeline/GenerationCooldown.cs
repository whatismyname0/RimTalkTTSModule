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

            lock (_lock)
            {
                _waitingRequests++;
            }

            int nowMs = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
            int cooldownEndMs = _waitingRequests * cooldownMs + _lastGenerateTimestampMs;

            if (nowMs < cooldownEndMs)
            {
                await Task.Delay(cooldownEndMs - nowMs, cancellationToken);
            }

            lock (_lock)
            {
                _lastGenerateTimestampMs = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                _waitingRequests--;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _lastGenerateTimestampMs = 0;
                _waitingRequests = 0;
            }
        }
    }
}
