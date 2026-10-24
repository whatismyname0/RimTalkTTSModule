using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Minimal SiliconFlow HTTP client for TTS generation.
    /// Sends POST /audio/speech and returns raw audio bytes.
    /// Note: This implements a conservative subset of the platform API (model, voice/reference, input).
    /// Advanced features (speed/gain/streaming/response format negotiation) are not implemented here.
    /// </summary>
    public static class SiliconFlowClient
    {
        private static readonly HttpClient _http = ProviderHttp.CreateClient();
        private const string DefaultBaseUrl = "https://api.siliconflow.cn/v1";

        /// <summary>Model used for uploading reference audio (SiliconFlow only accepts CosyVoice2 for uploads).</summary>
        public const string CosyVoiceModelId = "FunAudioLLM/CosyVoice2-0.5B";

        /// <summary>
        /// Upload a user voice file (multipart/form-data) to SiliconFlow uploads endpoint.
        /// Returns the returned URI (e.g. speech:...) on success or null on failure.
        /// </summary>
        public static async Task<string> UploadUserVoiceAsync(string apiKey, string model, string filePath, string customName, string textPreview = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("ApiKey required for SiliconFlowClient");
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) throw new ArgumentException("file not found");

            // SiliconFlow reference-audio uploads are backed by CosyVoice2; other model ids
            // (e.g. IndexTeam/IndexTTS-2) are rejected by the upload endpoint even though the
            // returned speech:... uri works for IndexTTS-2 generation.
            string uploadModel = string.IsNullOrWhiteSpace(model) ||
                model.StartsWith("IndexTeam/", StringComparison.OrdinalIgnoreCase)
                    ? CosyVoiceModelId
                    : model;

            var url = DefaultBaseUrl + "/uploads/audio/voice";

            using var content = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            content.Add(new StringContent(uploadModel), "model");

            // Sanitize or generate customName to meet SiliconFlow requirements: letters, digits, _ and - only, max 64 chars
            string SanitizeName(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var cleaned = System.Text.RegularExpressions.Regex.Replace(s, "[^A-Za-z0-9_-]", "_");
                if (cleaned.Length > 64) cleaned = cleaned.Substring(0, 64);
                // Trim leading/trailing underscores/hyphens
                cleaned = cleaned.Trim('_', '-');
                return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
            }

            string finalName = SanitizeName(customName);
            if (string.IsNullOrWhiteSpace(finalName))
            {
                // Try to derive from file name
                try
                {
                    var baseName = Path.GetFileNameWithoutExtension(filePath) ?? "user_voice";
                    finalName = SanitizeName(baseName) ?? "user_voice";
                }
                catch { finalName = "user_voice"; }
            }

            if (!string.IsNullOrWhiteSpace(finalName))
                content.Add(new StringContent(finalName), "customName");
            // "text" (the reference clip transcript) is a required field, so always send it.
            content.Add(new StringContent(textPreview ?? string.Empty), "text");

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = content;

            using var resp = await _http.SendAsync(req);
            var respText = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
            if (!resp.IsSuccessStatusCode)
            {
                TTSLog.Warning(ProviderHttp.DescribeFailure(resp, respText, "SiliconFlowClient.UploadUserVoiceAsync"));
                return null;
            }

            // Try to extract uri from response JSON
            var uri = ExtractFirstJsonString(respText, "uri");
            return uri;
        }

        /// <summary>
        /// List user uploaded voices. Returns an array of (uri, name) tuples.
        /// </summary>
        public static async Task<System.Collections.Generic.List<System.Tuple<string,string>>> ListUserVoicesAsync(string apiKey)
        {
            var result = new System.Collections.Generic.List<System.Tuple<string,string>>();
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("ApiKey required for SiliconFlowClient");
                var url = DefaultBaseUrl + "/audio/voice/list";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var resp = await _http.SendAsync(req);
                var respBytes = resp.Content != null ? await resp.Content.ReadAsByteArrayAsync() : Array.Empty<byte>();
                var respText = respBytes.Length > 0 ? Encoding.UTF8.GetString(respBytes) : string.Empty;
                if (!resp.IsSuccessStatusCode)
                {
                    TTSLog.Warning(ProviderHttp.DescribeFailure(resp, respText, "SiliconFlowClient.ListUserVoicesAsync"));
                    return result;
                }

                // Extract all uri and optional name fields from JSON
                var uris = ExtractAllJsonStrings(respText, "uri");
                var names = ExtractAllJsonStrings(respText, "customName");
                for (int i = 0; i < uris.Count; i++)
                {
                    string name = i < names.Count ? names[i] : uris[i];
                    result.Add(System.Tuple.Create(uris[i], name));
                }
                return result;
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] SiliconFlowClient.ListUserVoicesAsync exception: {ex.GetType().Name}: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Delete a user-uploaded voice by uri. Returns true on success.
        /// </summary>
        public static async Task<bool> DeleteUserVoiceAsync(string apiKey, string uri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("ApiKey required for SiliconFlowClient");
                if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentException("uri required");
                var url = DefaultBaseUrl + "/audio/voice/deletions";
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                req.Content = new StringContent("{\"uri\":\"" + uri.Replace("\"","\\\"") + "\"}");
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using var resp = await _http.SendAsync(req);
                var respBytes = resp.Content != null ? await resp.Content.ReadAsByteArrayAsync() : Array.Empty<byte>();
                var respText = respBytes.Length > 0 ? Encoding.UTF8.GetString(respBytes) : string.Empty;
                if (!resp.IsSuccessStatusCode)
                {
                    TTSLog.Warning(ProviderHttp.DescribeFailure(resp, respText, "SiliconFlowClient.DeleteUserVoiceAsync"));
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] SiliconFlowClient.DeleteUserVoiceAsync exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        // Helper: extract first JSON string field value by key using regex
        private static string ExtractFirstJsonString(string json, string key)
        {
            try
            {
                var pattern = "\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*:\\s*\"(.*?)\"";
                var m = System.Text.RegularExpressions.Regex.Match(json ?? string.Empty, pattern);
                if (m.Success && m.Groups.Count > 1) return m.Groups[1].Value;
            }
            catch { }
            return null;
        }

        private static System.Collections.Generic.List<string> ExtractAllJsonStrings(string json, string key)
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                var pattern = "\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*:\\s*\"(.*?)\"";
                var matches = System.Text.RegularExpressions.Regex.Matches(json ?? string.Empty, pattern);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (m.Success && m.Groups.Count > 1) list.Add(m.Groups[1].Value);
                }
            }
            catch { }
            return list;
        }

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (string.IsNullOrWhiteSpace(request.ApiKey))
                    throw new ArgumentException("ApiKey required for SiliconFlowClient");

                var url = DefaultBaseUrl + "/audio/speech";

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Build a minimal request body following documented fields (manual JSON to avoid System.Text.Json dependency)
                string JsonEscape(string s)
                {
                    if (s == null) return null;
                    var sb = new StringBuilder();
                    foreach (var ch in s)
                    {
                        switch (ch)
                        {
                            case '"': sb.Append("\\\""); break;
                            case '\\': sb.Append("\\\\"); break;
                            case '\b': sb.Append("\\b"); break;
                            case '\f': sb.Append("\\f"); break;
                            case '\n': sb.Append("\\n"); break;
                            case '\r': sb.Append("\\r"); break;
                            case '\t': sb.Append("\\t"); break;
                            default:
                                if (char.IsControl(ch))
                                    sb.AppendFormat("\\u{0:X4}", (int)ch);
                                else
                                    sb.Append(ch);
                                break;
                        }
                    }
                    return sb.ToString();
                }

                // Build the documented request body. The SiliconFlow REST API has no
                // "instruct_text" field; instruction/emotion control for CosyVoice2 is
                // expressed inside "input" (natural language followed by <|endofprompt|>).
                // "voice" and "references" are mutually exclusive.
                float speed = request.Speed;
                if (float.IsNaN(speed) || speed < 0.25f) speed = 0.25f;
                if (speed > 4.0f) speed = 4.0f;

                var sb = new StringBuilder();
                sb.Append('{');
                sb.Append("\"model\":\"").Append(JsonEscape(request.Model)).Append("\",");
                sb.Append("\"input\":\"").Append(JsonEscape(request.Input)).Append("\",");

                if (request.References != null && request.References.Count > 0)
                {
                    sb.Append("\"references\":[");
                    for (int i = 0; i < request.References.Count; i++)
                    {
                        var r = request.References[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('{');
                        sb.Append("\"audio\":\"").Append(JsonEscape(r.Audio)).Append("\",");
                        sb.Append("\"text\":\"").Append(JsonEscape(r.Text)).Append("\"");
                        sb.Append('}');
                    }
                    sb.Append("],");
                }
                else
                {
                    sb.Append("\"voice\":\"").Append(JsonEscape(request.Voice ?? string.Empty)).Append("\",");
                }

                sb.Append("\"speed\":").Append(speed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                // WAV is the cheapest format for AudioPlaybackService to consume (no decoder needed).
                sb.Append("\"response_format\":\"wav\"");
                sb.Append('}');
                string json = sb.ToString();
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                req.Content.Headers.ContentType.CharSet = "utf-8";

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    var errBytes = resp.Content != null ? await resp.Content.ReadAsByteArrayAsync() : Array.Empty<byte>();
                    string respText = errBytes.Length > 0 ? Encoding.UTF8.GetString(errBytes) : string.Empty;
                    TTSLog.Warning(ProviderHttp.DescribeFailure(resp, respText, "SiliconFlowClient"));
                    return null;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes == null || bytes.Length == 0)
                {
                    TTSLog.Warning("[RimTalk.TTS] SiliconFlowClient: API returned an empty audio body");
                    return null;
                }

                var mediaType = resp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;

                // Preferred path: the server honoured response_format=wav.
                if (IsWav(bytes))
                {
                    return bytes;
                }

                // Some deployments/models ignore response_format and return MP3 (or another
                // audio container). Do NOT convert here: NAudio is a vanilla-RimWorld
                // (Managed folder) assembly that the .NET 10 mod loader does not resolve,
                // so any reference to Mp3FileReader crashes the whole method at JIT time.
                // AudioPlaybackService already knows how to play MP3 (via a temp file and
                // UnityWebRequestMultimedia), so just pass the raw bytes through.
                bool looksLikeMp3 = LooksLikeMp3(bytes) ||
                    mediaType.Contains("mpeg") || mediaType.Contains("mp3");
                if (looksLikeMp3 || mediaType.StartsWith("audio/"))
                {
                    TTSLog.Warning($"[RimTalk.TTS] SiliconFlowClient: server returned non-WAV audio (Content-Type='{mediaType}', {bytes.Length} bytes); passing raw bytes to playback.");
                    return bytes;
                }

                // If JSON or text returned, log the parsed content for debugging
                string textPreview = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
                if (mediaType == "application/json" || mediaType.StartsWith("text/") || textPreview.StartsWith("{") || textPreview.StartsWith("["))
                {
                    TTSLog.Error($"[RimTalk.TTS] SiliconFlowClient: Expected audio but server returned '{mediaType}'. Response body: {textPreview}");
                    return null;
                }

                // Unknown binary payload - hand it over and let the playback layer report
                // an unsupported format with a clear log instead of guessing here.
                TTSLog.Warning($"[RimTalk.TTS] SiliconFlowClient: Unexpected non-audio response (Content-Type='{mediaType}', {bytes.Length} bytes); returning raw bytes.");
                return bytes;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] SiliconFlowClient exception: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>True if the byte array starts with a RIFF/WAVE header.</summary>
        private static bool IsWav(byte[] bytes)
        {
            return bytes != null && bytes.Length > 12 &&
                bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E';
        }

        /// <summary>True if the byte array looks like an MP3 stream (ID3 tag or MPEG frame sync).</summary>
        private static bool LooksLikeMp3(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2) return false;
            if (bytes.Length >= 3 && bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3') return true;
            return bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0;
        }
    }
}
