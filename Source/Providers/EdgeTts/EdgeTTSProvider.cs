using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider wrapper for Edge-TTS (Microsoft Edge's free TTS service). No API key required.
    /// </summary>
    public class EdgeTTSProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.NoKey();

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await EdgeTTSClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            // Shared HttpClient / WebSocket, no resources to release
            return Task.CompletedTask;
        }
    }
}