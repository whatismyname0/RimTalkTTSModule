using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// Provider wrapper for Microsoft Azure Text-to-Speech API
    /// </summary>
    public class AzureTTSProvider : ITTSProvider
    {
        private string _region = "eastus";

        public void SetRegion(string region)
        {
            _region = region ?? "eastus";
        }

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Call Azure TTS client with configured region
            // Voice field in request may contain "deploymentId:voiceName" for custom voices
            return await AzureTTSClient.GenerateSpeechAsync(request, _region, cancellationToken);
        }

        public void Shutdown()
        {
            // HTTP client doesn't need cleanup
        }

        public bool IsApiKeyValid(string apiKey)
        {
            // Azure uses subscription key format (32 hex characters typically)
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey.Length >= 32;
        }
    }
}
