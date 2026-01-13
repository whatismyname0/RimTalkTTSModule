using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Client for Azure Text-to-Speech REST API
    /// https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech
    /// </summary>
    public static class AzureTTSClient
    {
        private static readonly HttpClient _http = new HttpClient();

        /// <summary>
        /// Generate speech using Azure TTS REST API
        /// </summary>
        /// <remarks>
        /// Voice format:
        /// - Standard voice: "en-US-JennyNeural" (voice name only)
        /// - Custom voice: "deploymentId:CustomVoiceName" (deployment ID + colon + voice name)
        /// </remarks>
        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, string region, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (string.IsNullOrWhiteSpace(request.ApiKey))
                    throw new ArgumentException("ApiKey required for AzureTTSClient");
                if (string.IsNullOrWhiteSpace(region))
                    region = "eastus";

                // Parse voice field for deployment ID (format: "deploymentId:voiceName" or just "voiceName")
                string deploymentId = null;
                string voiceName = request.Voice;
                
                if (!string.IsNullOrWhiteSpace(voiceName) && voiceName.Contains(":"))
                {
                    var parts = voiceName.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        deploymentId = parts[0].Trim();
                        voiceName = parts[1].Trim();
                    }
                }

                // Build endpoint URL - use custom voice endpoint if deploymentId is present
                string url;
                if (!string.IsNullOrWhiteSpace(deploymentId))
                {
                    // Custom Neural Voice endpoint
                    url = $"https://{region}.voice.speech.microsoft.com/cognitiveservices/v1?deploymentId={deploymentId}";
                }
                else
                {
                    // Standard voice endpoint
                    url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
                }

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                
                // Set required headers
                req.Headers.Add("Ocp-Apim-Subscription-Key", request.ApiKey);
                req.Headers.Add("User-Agent", "RimTalkTTS");
                
                // Set output format - use 24kHz 16-bit mono PCM in RIFF container (WAV)
                req.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

                // Build SSML body (pass parsed voiceName for custom voices)
                string ssml = BuildSSML(request, voiceName);
                req.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

                // Send request
                using var resp = await _http.SendAsync(req, cancellationToken);
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
                    Log.Error($"[RimTalk.TTS] AzureTTSClient: API returned {resp.StatusCode}: {errorText}");
                    return null;
                }

                // Return audio data
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] AzureTTSClient.GenerateSpeechAsync exception: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Build SSML for Azure TTS
        /// </summary>
        /// <param name="voiceNameOverride">Optional voice name to use instead of request.Voice (for custom voices)</param>
        private static string BuildSSML(TTSRequest request, string voiceNameOverride = null)
        {
            // Determine voice and language from Voice field
            // Voice should be in format like "en-US-JennyNeural" or just the short name
            // For custom voices, voiceNameOverride contains the parsed voice name
            string voiceName = voiceNameOverride ?? request.Voice;
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
            else
            {
                // Default voice
                voiceName = "en-US-JennyNeural";
            }

            // Create SSML document with mstts namespace
            var ns = XNamespace.Get("http://www.w3.org/2001/10/synthesis");
            var msttsNs = XNamespace.Get("http://www.w3.org/2001/mstts");
            
            var speak = new XElement(ns + "speak",
                new XAttribute("version", "1.0"),
                new XAttribute(XNamespace.Xml + "lang", language),
                new XAttribute(XNamespace.Xmlns + "mstts", msttsNs.NamespaceName));

            var voice = new XElement(ns + "voice",
                new XAttribute(XNamespace.Xml + "lang", language),
                new XAttribute("name", voiceName));

            // Process text content with SSML enhancements
            string processedText = ProcessTextWithSSMLTags(request.Input, ns);
            
            // Build prosody element with all supported attributes
            bool needsProsody = Math.Abs(request.Speed - 1.0f) > 0.01f || 
                               Math.Abs(request.Temperature - 0.5f) > 0.01f || 
                               Math.Abs(request.TopP - 0.5f) > 0.01f;
            
            object content;
            
            if (needsProsody)
            {
                var prosody = new XElement(ns + "prosody");
                
                // Rate (speed)
                if (Math.Abs(request.Speed - 1.0f) > 0.01f)
                {
                    int ratePercent = (int)((request.Speed - 1.0f) * 100);
                    string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";
                    prosody.Add(new XAttribute("rate", rateStr));
                }
                
                // Pitch (use Temperature as pitch modifier: 0.0-1.0 -> -50% to +50%)
                if (Math.Abs(request.Temperature - 0.5f) > 0.01f)
                {
                    int pitchPercent = (int)((request.Temperature - 0.5f) * 100);
                    string pitchStr = pitchPercent >= 0 ? $"+{pitchPercent}%" : $"{pitchPercent}%";
                    prosody.Add(new XAttribute("pitch", pitchStr));
                }
                
                prosody.Add(XElement.Parse(processedText));
                content = prosody;
            }
            else
            {
                // Parse the processed text as XML fragment
                try
                {
                    content = XElement.Parse(processedText);
                }
                catch
                {
                    // If parsing fails, use as plain text
                    content = processedText;
                }
            }

            // Check if emotion/style is specified in InstructText
            string style = ParseStyleFromInstructText(request.InstructText);
            
            if (!string.IsNullOrWhiteSpace(style))
            {
                // Wrap content in mstts:express-as tag
                var expressAs = new XElement(msttsNs + "express-as",
                    new XAttribute("style", style));
                expressAs.Add(content);
                voice.Add(expressAs);
            }
            else
            {
                // No style, add content directly
                voice.Add(content);
            }

            speak.Add(voice);

            return speak.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Process text with SSML tags support
        /// Converts special markers to SSML tags: [break], [emphasis], etc.
        /// </summary>
        private static string ProcessTextWithSSMLTags(string text, XNamespace ns)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "<p></p>";

            // Escape XML special characters first
            string processed = System.Security.SecurityElement.Escape(text);

            // Convert [break] tags to <break> SSML
            // [break] -> <break time="500ms"/>
            // [break:1s] -> <break time="1s"/>
            // [long-break] -> <break time="1s"/>
            processed = System.Text.RegularExpressions.Regex.Replace(
                processed,
                @"\[break(?::(\d+(?:ms|s)))?\]",
                match => {
                    string time = match.Groups[1].Success ? match.Groups[1].Value : "500ms";
                    return $"<break time=\"{time}\"/>";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            processed = System.Text.RegularExpressions.Regex.Replace(
                processed,
                @"\[long-break\]",
                "<break time=\"1s\"/>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Convert [emphasis:*] tags to <emphasis> SSML
            // [emphasis:strong]text[/emphasis] -> <emphasis level="strong">text</emphasis>
            // Levels: reduced, none, moderate, strong
            processed = System.Text.RegularExpressions.Regex.Replace(
                processed,
                @"\[emphasis:?(strong|moderate|reduced|none)?\](.*?)\[/emphasis\]",
                match => {
                    string level = match.Groups[1].Success ? match.Groups[1].Value : "moderate";
                    string content = match.Groups[2].Value;
                    return $"<emphasis level=\"{level}\">{content}</emphasis>";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Wrap in paragraph tag for proper XML structure
            return $"<p>{processed}</p>";
        }

        /// <summary>
        /// Parse speaking style from InstructText field
        /// Returns the style name if valid, otherwise empty string
        /// </summary>
        private static string ParseStyleFromInstructText(string instructText)
        {
            if (string.IsNullOrWhiteSpace(instructText))
                return string.Empty;

            // Clean and normalize the input
            string style = instructText.Trim().ToLowerInvariant();

            // List of valid Azure TTS speaking styles
            var validStyles = new System.Collections.Generic.HashSet<string>
            {
                "cheerful", "sad", "angry", "excited", "friendly", "terrified",
                "shouting", "unfriendly", "whispering", "hopeful", "calm",
                "fearful", "embarrassed", "serious", "depressed", "disgruntled",
                "assistant", "newscast", "customerservice", "chat",
                // Additional styles for some voices
                "gentle", "lyrical", "newscast-casual", "newscast-formal",
                "customer-service", "empathetic", "narration-professional",
                "narration-relaxed", "sports-commentary", "sports-commentary-excited",
                "advertisement-upbeat", "affectionate", "envious"
            };

            // Check if the style is valid
            if (validStyles.Contains(style))
                return style;

            // Try to extract style if it's in a format like "Style: cheerful" or similar
            if (style.Contains(":"))
            {
                var parts = style.Split(':');
                if (parts.Length >= 2)
                {
                    string extractedStyle = parts[1].Trim();
                    if (validStyles.Contains(extractedStyle))
                        return extractedStyle;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get list of available voices for a region
        /// </summary>
        public static async Task<System.Collections.Generic.List<VoiceInfo>> GetVoicesAsync(string apiKey, string region)
        {
            var result = new System.Collections.Generic.List<VoiceInfo>();
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("ApiKey required");
                if (string.IsNullOrWhiteSpace(region)) region = "eastus";

                var url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

                using var resp = await _http.SendAsync(req);
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
                    Log.Warning($"[RimTalk.TTS] AzureTTSClient.GetVoicesAsync: API returned {resp.StatusCode}: {errorText}");
                    return result;
                }

                var json = await resp.Content.ReadAsStringAsync();
                
                // Parse JSON manually (simple extraction)
                var voices = ParseVoiceList(json);
                return voices;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] AzureTTSClient.GetVoicesAsync exception: {ex.GetType().Name}: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Simple JSON parser for voice list
        /// </summary>
        private static System.Collections.Generic.List<VoiceInfo> ParseVoiceList(string json)
        {
            var result = new System.Collections.Generic.List<VoiceInfo>();
            try
            {
                // Extract ShortName and DisplayName using regex
                var shortNamePattern = @"""ShortName"":\s*""([^""]+)""";
                var displayNamePattern = @"""DisplayName"":\s*""([^""]+)""";
                var localePattern = @"""Locale"":\s*""([^""]+)""";

                var shortNames = System.Text.RegularExpressions.Regex.Matches(json, shortNamePattern);
                var displayNames = System.Text.RegularExpressions.Regex.Matches(json, displayNamePattern);
                var locales = System.Text.RegularExpressions.Regex.Matches(json, localePattern);

                int count = Math.Min(Math.Min(shortNames.Count, displayNames.Count), locales.Count);
                for (int i = 0; i < count; i++)
                {
                    var shortName = shortNames[i].Groups[1].Value;
                    var displayName = displayNames[i].Groups[1].Value;
                    var locale = locales[i].Groups[1].Value;

                    result.Add(new VoiceInfo
                    {
                        ShortName = shortName,
                        DisplayName = displayName,
                        Locale = locale
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk.TTS] AzureTTSClient.ParseVoiceList: Failed to parse JSON - {ex.Message}");
            }
            return result;
        }

        public class VoiceInfo
        {
            public string ShortName { get; set; }
            public string DisplayName { get; set; }
            public string Locale { get; set; }
        }
    }
}
