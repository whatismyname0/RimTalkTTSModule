using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// A TTS backend. Implementations only map a request to audio bytes; request pacing, timeouts,
    /// cancellation and lifecycle live in the pipeline / <see cref="ProviderHost"/>.
    /// </summary>
    public interface ITTSProvider
    {
        /// <summary>What this provider needs (API key, supported parameters).</summary>
        ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Generate speech. The token is cancelled when the request is ignored, times out or the
        /// provider is swapped out; providers should stop work as soon as possible.
        /// </summary>
        Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default);

        /// <summary>Release provider resources (helper process, sockets). Fire-and-forget safe.</summary>
        Task ShutdownAsync();
    }
}
