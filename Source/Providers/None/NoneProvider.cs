using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// A no-op provider used when the user has not selected any supplier.
    /// GenerateSpeechAsync always returns null and emits a warning.
    /// </summary>
    public class NoneProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.NoKey();

        public Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            TTSLog.Warning("[RimTalk.TTS] No TTS supplier selected - skipping TTS generation");
            return Task.FromResult<byte[]>(null);
        }

        public Task ShutdownAsync()
        {
            // No resources to clean up
            return Task.CompletedTask;
        }
    }
}