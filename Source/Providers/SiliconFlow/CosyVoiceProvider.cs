using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider wrapper for FunAudioLLM/CosyVoice2-0.5B (SiliconFlow-backed).
    /// </summary>
    public class CosyVoiceProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.Key();

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await SiliconFlowClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}