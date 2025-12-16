using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;

namespace RimTalk.TTS.Provider
{
    public interface ITTSProvider
    {
        Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default);

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
