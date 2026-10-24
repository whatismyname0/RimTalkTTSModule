using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider wrapper for Microsoft Azure Text-to-Speech API.
    /// </summary>
    public class AzureTTSProvider : ITTSProvider
    {
        private string _region = "eastus";

        public ProviderCapabilities Capabilities { get; } =
            ProviderCapabilities.Key(key => !string.IsNullOrWhiteSpace(key) && key.Length >= 32);

        public void SetRegion(string region)
        {
            _region = region ?? "eastus";
        }

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Voice field may contain "deploymentId:voiceName" for custom voices.
            return await AzureTTSClient.GenerateSpeechAsync(request, _region, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}