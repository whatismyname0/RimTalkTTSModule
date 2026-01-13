using System;
using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Service;
using Verse;

namespace RimTalk.TTS.Provider
{
    public class GeminiTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] audioData = await GeminiTTSClient.GenerateSpeechAsync(request, cancellationToken);
                return audioData;
            }
            catch (Exception ex)
            {
                Log.Error($"RimTalkTTS: GeminiTTS generation failed: {ex.Message}");
                return null;
            }
        }

        public void Shutdown()
        {
            // Gemini TTS 使用共享 HttpClient，无需特殊清理
        }

        public bool IsApiKeyValid(string apiKey)
        {
            // Gemini API key 通常以 "AIza" 开头，长度约为 39 个字符
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            return apiKey.StartsWith("AIza") && apiKey.Length >= 35;
        }
    }
}
