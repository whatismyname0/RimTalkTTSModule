using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider wrapper for IndexTeam/IndexTTS-2 (SiliconFlow-backed).
    /// </summary>
    public class IndexTTSProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.Key();

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await SiliconFlowClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}