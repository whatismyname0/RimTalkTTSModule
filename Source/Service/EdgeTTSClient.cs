using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Client for Edge-TTS (Microsoft Edge's free TTS service)
    /// No API key required - uses the same voices as Azure TTS but through Edge browser endpoint
    /// </summary>
    public static class EdgeTTSClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string EDGE_TTS_ENDPOINT = "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

        /// <summary>
        /// Generate speech using Edge-TTS (free, no API key)
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

                // Build SSML
                string ssml = BuildSSML(request, voiceName);

                using var req = new HttpRequestMessage(HttpMethod.Post, EDGE_TTS_ENDPOINT);
                
                // Set required headers for Edge-TTS
                req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
                req.Headers.Add("Origin", "https://www.bing.com");
                req.Headers.Add("Referer", "https://www.bing.com/");
                req.Headers.Add("Sec-Fetch-Dest", "empty");
                req.Headers.Add("Sec-Fetch-Mode", "cors");
                req.Headers.Add("Sec-Fetch-Site", "same-site");

                // Set content
                req.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
                req.Content.Headers.ContentType.Parameters.Clear();
                req.Content.Headers.ContentType.CharSet = "UTF-8";

                // Send request
                using var resp = await _http.SendAsync(req, cancellationToken);
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
                    Log.Error($"[RimTalk.TTS] EdgeTTSClient: API returned {resp.StatusCode}: {errorText}");
                    return null;
                }

                // Return audio data
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] EdgeTTSClient.GenerateSpeechAsync exception: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Build SSML for Edge-TTS
        /// </summary>
        private static string BuildSSML(TTSRequest request, string voiceName)
        {
            string language = "en-US";

            if (!string.IsNullOrWhiteSpace(voiceName))
            {
                // Extract language from voice name (e.g., "en-US-JennyNeural" -> "en-US")
                if (voiceName.Contains("-"))
                {
                    var parts = voiceName.Split('-');
                    if (parts.Length >= 2)
                    {
                        language = $"{parts[0]}-{parts[1]}";
                    }
                }
            }

            // Create SSML document
            var ns = XNamespace.Get("http://www.w3.org/2001/10/synthesis");
            
            var speak = new XElement(ns + "speak",
                new XAttribute("version", "1.0"),
                new XAttribute(XNamespace.Xml + "lang", language));

            var voice = new XElement(ns + "voice",
                new XAttribute("name", voiceName));

            // Apply speed if specified
            if (Math.Abs(request.Speed - 1.0f) > 0.01f)
            {
                var prosody = new XElement(ns + "prosody");
                int ratePercent = (int)((request.Speed - 1.0f) * 100);
                string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";
                prosody.Add(new XAttribute("rate", rateStr));
                prosody.Add(request.Input);
                voice.Add(prosody);
            }
            else
            {
                voice.Add(request.Input);
            }

            speak.Add(voice);

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                speak
            );

            return doc.ToString(SaveOptions.DisableFormatting);
        }
    }
}
