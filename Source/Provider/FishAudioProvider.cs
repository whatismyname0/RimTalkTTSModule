using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;
using RimTalk.TTS.Service.FishAudioService;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// ITTSProvider implementation that delegates to FishAudioTTSClient
    /// </summary>
    public class FishAudioProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Delegate to existing FishAudio client which accepts parameter list
            return await FishAudioTTSClient.GenerateSpeechAsync(
                request,
                cancellationToken);
        }

        public void Shutdown()
        {
            // Delegate to existing shutdown helper
            FishAudioTTSClient.ShutdownServer();
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }
    }
}
