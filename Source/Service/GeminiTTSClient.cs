using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using RimTalk.Util;

namespace RimTalk.TTS.Service
{
    public class GeminiTTSClient
    {
        private static readonly HttpClient client = new HttpClient();
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
        
        // Gemini TTS models
        private const string FlashModel = "gemini-2.5-flash-preview-tts";
        private const string ProModel = "gemini-2.5-pro-preview-tts";

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                string apiKey = request.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    Log.Error("RimTalkTTS: Gemini TTS API key is not set.");
                    return null;
                }

                // 选择模型（默认使用 Flash，更快更便宜）
                string model = FlashModel;
                string voiceName = request.Voice ?? "Kore"; // 默认语音

                // 构建请求 URL
                string url = $"{BaseUrl}{model}:generateContent?key={apiKey}";

                // 构建请求体
                var requestBody = BuildRequestBody(request.Input, voiceName);
                string jsonRequest = JsonUtil.SerializeToJson(requestBody);

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"RimTalkTTS: Gemini TTS API request failed. Status: {response.StatusCode}, Error: {errorContent}");
                    return null;
                }

                string responseText = await response.Content.ReadAsStringAsync();
                var responseData = JsonUtil.DeserializeFromJson<Dictionary<string, object>>(responseText);

                // 提取音频数据
                string audioData = ExtractAudioData(responseData);
                
                if (string.IsNullOrEmpty(audioData))
                {
                    Log.Error("RimTalkTTS: No audio data in Gemini TTS response.");
                    return null;
                }

                // Base64 解码
                byte[] audioBytes = Convert.FromBase64String(audioData);
                
                // Gemini返回的是PCM格式，需要转换为WAV
                byte[] wavData = ConvertPcmToWav(audioBytes);
                
                return wavData;
            }
            catch (Exception ex)
            {
                Log.Error($"RimTalkTTS: Error in Gemini TTS generation: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private static object BuildRequestBody(string text, string voiceName)
        {
            // 构建 Gemini TTS 请求体
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new
                        {
                            prebuiltVoiceConfig = new
                            {
                                voiceName = voiceName
                            }
                        }
                    }
                }
            };

            return requestBody;
        }

        // 从响应中提取音频数据
        private static string ExtractAudioData(Dictionary<string, object> response)
        {
            try
            {
                if (response == null || !response.ContainsKey("candidates")) return null;
                
                var candidates = response["candidates"] as List<object>;
                if (candidates == null || candidates.Count == 0) return null;
                
                var candidate = candidates[0] as Dictionary<string, object>;
                if (candidate == null || !candidate.ContainsKey("content")) return null;
                
                var content = candidate["content"] as Dictionary<string, object>;
                if (content == null || !content.ContainsKey("parts")) return null;
                
                var parts = content["parts"] as List<object>;
                if (parts == null || parts.Count == 0) return null;
                
                var part = parts[0] as Dictionary<string, object>;
                if (part == null || !part.ContainsKey("inlineData")) return null;
                
                var inlineData = part["inlineData"] as Dictionary<string, object>;
                if (inlineData == null || !inlineData.ContainsKey("data")) return null;
                
                return inlineData["data"]?.ToString();
            }
            catch (Exception ex)
            {
                Log.Error($"RimTalkTTS: Failed to extract audio data: {ex.Message}");
                return null;
            }
        }

        // 将 PCM 转换为 WAV 格式
        private static byte[] ConvertPcmToWav(byte[] pcmData)
        {
            // Gemini TTS 输出参数：24kHz, 16-bit, Mono
            int sampleRate = 24000;
            int channels = 1;
            int bitsPerSample = 16;

            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;

            using (var memoryStream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(memoryStream))
            {
                // RIFF header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcmData.Length); // ChunkSize
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt subchunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size (16 for PCM)
                writer.Write((short)1); // AudioFormat (1 for PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // data subchunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcmData.Length);
                writer.Write(pcmData);

                return memoryStream.ToArray();
            }
        }

        // 支持的语音选项（30种）
        public static readonly string[] AvailableVoices = new[]
        {
            "Zephyr", "Puck", "Charon",
            "Kore", "Fenrir", "Leda",
            "Orus", "Aoede", "Callirrhoe",
            "Autonoe", "Enceladus", "Iapetus",
            "Umbriel", "Algieba", "Despina",
            "Erinome", "Algenib", "Rasalgethi",
            "Laomedeia", "Achernar", "Alnilam",
            "Schedar", "Gacrux", "Pulcherrima",
            "Achird", "Zubenelgenubi", "Vindemiatrix",
            "Sadachbia", "Sadaltager", "Sulafat"
        };
    }
}
