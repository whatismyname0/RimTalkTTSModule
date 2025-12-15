using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// Provider wrapper for IndexTeam/IndexTTS-2 (SiliconFlow-backed)
    /// </summary>
    public class IndexTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float speed, float temperature, float topP, CancellationToken cancellationToken = default)
        {
            var resolvedModel = string.IsNullOrWhiteSpace(model) ? "IndexTeam/IndexTTS-2" : model;
            return await Service.SiliconFlowClient.GenerateSpeechAsync(text, apiKey, referenceId, resolvedModel, speed, temperature, topP, cancellationToken);
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