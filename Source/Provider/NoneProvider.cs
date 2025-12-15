using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.TTS.Provider
{
    /// <summary>
    /// A no-op provider used when the user has not selected any supplier.
    /// GenerateSpeechAsync always returns null and emits a warning.
    /// </summary>
    public class NoneProvider : ITTSProvider
    {
        public Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float temperature, float topP, CancellationToken cancellationToken = default)
        {
            Log.Warning("[RimTalk.TTS] No TTS supplier selected - skipping TTS generation");
            return Task.FromResult<byte[]>(null);
        }

        public void Shutdown()
        {
            // No resources to clean up
        }

        public bool IsApiKeyValid(string apiKey)
        {
            // None provider does not require an API key
            return true;
        }
    }
}
