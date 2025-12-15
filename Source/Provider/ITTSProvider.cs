using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Provider
{
    public interface ITTSProvider
    {
        Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float temperature, float topP, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform provider-specific shutdown/cleanup (blocking)
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Check whether the provided API key is valid for this provider.
        /// Implementations should return true for providers that do not require an API key.
        /// </summary>
        bool IsApiKeyValid(string apiKey);
    }
}
