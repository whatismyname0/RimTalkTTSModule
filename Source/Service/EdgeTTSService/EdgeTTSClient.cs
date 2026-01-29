using System;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.TTS.Service.EdgeTTSService
{
    /// <summary>
    /// Client for Edge-TTS (Microsoft Edge's free TTS service)
    /// No API key required - uses the same voices as Azure TTS but through Edge browser endpoint
    /// Uses pure C# WebSocket implementation - no Python dependencies
    /// </summary>
    public static class EdgeTTSClient
    {
        /// <summary>
        /// Generate speech using Edge-TTS via WebSocket connection (free, no API key)
        /// </summary>
        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));

                string voiceName = request.Voice;
                if (string.IsNullOrWhiteSpace(voiceName))
                {
                    voiceName = "en-US-JennyNeural";
                }

                // Calculate rate parameter (edge-tts format: +50% or -50%)
                int ratePercent = (int)((request.Speed - 1.0f) * 100);
                string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

                // Calculate volume parameter
                int volumePercent = (int)((request.Volume - 1.0f) * 100);
                string volumeStr = volumePercent >= 0 ? $"+{volumePercent}%" : $"{volumePercent}%";

                // Use the new WebSocket client
                using (var client = new EdgeTTSWebSocketClient())
                {
                    byte[] audioData = await client.SynthesizeAsync(
                        text: request.Input,
                        voice: voiceName,
                        rate: rateStr,
                        volume: volumeStr
                    );

                    if (audioData == null || audioData.Length == 0)
                    {
                        Log.Warning("[RimTalk.TTS] EdgeTTSClient: No audio data received");
                        return null;
                    }

                    if (Prefs.DevMode)
                    {
                        Log.Message($"[RimTalk.TTS] EdgeTTSClient: Generated {audioData.Length} bytes of audio");
                    }

                    return audioData;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] EdgeTTSClient.GenerateSpeechAsync exception: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log.Error($"[RimTalk.TTS] EdgeTTSClient inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }
    }
}
