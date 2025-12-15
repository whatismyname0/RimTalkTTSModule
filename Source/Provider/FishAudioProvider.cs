using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service.FishAudioService;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// ITTSProvider implementation that delegates to FishAudioTTSClient
    /// </summary>
    public class FishAudioProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float temperature, float topP, CancellationToken cancellationToken = default)
        {
            return await FishAudioTTSClient.GenerateSpeechAsync(text, apiKey, referenceId, model, temperature, topP, cancellationToken);
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
