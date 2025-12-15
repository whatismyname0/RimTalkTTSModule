using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// Provider wrapper for FunAudioLLM/CosyVoice2-0.5B (SiliconFlow-backed)
    /// </summary>
    public class CosyVoiceProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float speed, float temperature, float topP, CancellationToken cancellationToken = default)
        {
            var resolvedModel = string.IsNullOrWhiteSpace(model) ? "FunAudioLLM/CosyVoice2-0.5B" : model;
            return await Service.SiliconFlowClient.GenerateSpeechAsync(text, apiKey, referenceId, resolvedModel, speed, temperature, topP, cancellationToken);
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