using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// Provider wrapper for FunAudioLLM/CosyVoice2-0.5B (SiliconFlow-backed)
    /// </summary>
    public class CosyVoiceProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Providers assume request is fully initialized by caller
            return await Service.SiliconFlowClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public void Shutdown()
        {
            // No-op for HTTP client
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }
    }
}