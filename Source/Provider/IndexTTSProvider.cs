using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// Provider wrapper for IndexTeam/IndexTTS-2 (SiliconFlow-backed)
    /// </summary>
    public class IndexTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Providers assume request is fully initialized by caller
            return await Service.SiliconFlowClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public void Shutdown()
        {
            // No persistent resources
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }
    }
}