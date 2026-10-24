using System;
using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    public class GeminiTTSProvider : ITTSProvider
    {
        public ProviderCapabilities Capabilities { get; } =
            ProviderCapabilities.Key(key => key.StartsWith("AIza") && key.Length >= 35);

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                return await GeminiTTSClient.GenerateSpeechAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"RimTalkTTS: GeminiTTS generation failed: {ex.Message}");
                return null;
            }
        }

        public Task ShutdownAsync()
        {
            // Shared HttpClient, no resources to release
            return Task.CompletedTask;
        }
    }
}