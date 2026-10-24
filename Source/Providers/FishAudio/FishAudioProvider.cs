using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider that delegates to FishAudioTTSClient (local Python helper server).
    /// </summary>
    public class FishAudioProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } =
            ProviderCapabilities.Key(localProcess: true);

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await FishAudioTTSClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            // Killing the helper process can take a moment; keep it off the caller's thread.
            return Task.Run(() => FishAudioTTSClient.ShutdownServer());
        }
    }
}