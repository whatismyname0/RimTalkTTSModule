using System;
using System.IO;
using NAudio.Wave;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Minimal SiliconFlow HTTP client for TTS generation.
    /// Sends POST /audio/speech and returns raw audio bytes.
    /// Note: This implements a conservative subset of the platform API (model, voice/reference, input).
    /// Advanced features (speed/gain/streaming/response format negotiation) are not implemented here.
    /// </summary>
    public static class SiliconFlowClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string DefaultBaseUrl = "https://api.siliconflow.cn/v1";

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

                string modelEsc = JsonEscape(request.Model);
                string inputEsc = JsonEscape(request.Input);
                string voiceValue = request.Voice ?? string.Empty; // empty indicates dynamic references per docs
                string voicePart = "\"" + JsonEscape(voiceValue) + "\"";
                // include speed and enforce wav response format
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"model\":\"").Append(modelEsc).Append("\",");
                sb.Append("\"input\":\"").Append(inputEsc).Append("\",");
                sb.Append("\"voice\":").Append(voicePart).Append(",");
                sb.Append("\"speed\":").Append(request.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"response_format\":\"wav\"");

                // optional references -> include as extra_body.references if provided
                if (request.References != null && request.References.Count > 0)
                {
                    sb.Append(",\"extra_body\":{\"references\":[");
                    for (int i = 0; i < request.References.Count; i++)
                    {
                        var r = request.References[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('{');
                        sb.Append("\"audio\":\"").Append(JsonEscape(r.Audio)).Append("\",");
                        sb.Append("\"text\":\"").Append(JsonEscape(r.Text)).Append("\"");
                        sb.Append('}');
                    }
                    sb.Append("]}");
                }

                sb.Append('}');
                string json = sb.ToString();
                req.Content = new StringContent(json);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    string respText = await resp.Content.ReadAsStringAsync();
                    Log.Warning($"[RimTalk.TTS] SiliconFlowClient: API returned {resp.StatusCode}: {respText}");
                    return null;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();

                // Check for valid WAV header (RIFF...WAVE)
                bool isWav = bytes != null && bytes.Length > 12 &&
                    bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                    bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E';

                if (isWav)
                {
                    return bytes;
                }

                // Not WAV - inspect Content-Type to decide how to handle
                var mediaType = resp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
                if (!string.IsNullOrEmpty(mediaType) && mediaType.StartsWith("audio/"))
                {
                    // Server returned audio in a non-wav format (e.g., mp3/opus).
                    // Log incoming sizes and a small preview to aid diagnosis.
                    try
                    {
                        int previewLen = Math.Min(bytes?.Length ?? 0, 128);
                        if (previewLen > 0)
                        {
                            string asciiPreview;
                            try { asciiPreview = System.Text.Encoding.ASCII.GetString(bytes, 0, previewLen); }
                            catch { asciiPreview = "<non-ascii-preview>"; }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk.TTS] SiliconFlowClient: Failed to generate response preview: {ex.GetType().Name}: {ex.Message}");
                    }

                    // If MP3, attempt to convert to WAV so AudioPlaybackService can consume it.
                    bool looksLikeMp3 = mediaType.Contains("mpeg") || mediaType.Contains("mp3") ||
                        (bytes.Length >= 3 && bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3') ||
                        (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0);

                    if (looksLikeMp3)
                    {
                        try
                        {
                            using var inMs = new MemoryStream(bytes);
                            using var mp3Reader = new Mp3FileReader(inMs);
                            using var outMs = new MemoryStream();
                            using (var waveWriter = new WaveFileWriter(outMs, mp3Reader.WaveFormat))
                            {
                                var buffer = new byte[16384];
                                int read;
                                while ((read = mp3Reader.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    waveWriter.Write(buffer, 0, read);
                                }
                                waveWriter.Flush();
                            }
                            var wavBytes = outMs.ToArray();
                            // Check WAV header of converted data
                            bool wavHeader = wavBytes != null && wavBytes.Length > 12 &&
                                wavBytes[0] == (byte)'R' && wavBytes[1] == (byte)'I' && wavBytes[2] == (byte)'F' && wavBytes[3] == (byte)'F' &&
                                wavBytes[8] == (byte)'W' && wavBytes[9] == (byte)'A' && wavBytes[10] == (byte)'V' && wavBytes[11] == (byte)'E';
                            return wavBytes;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[RimTalk.TTS] SiliconFlowClient: Failed to convert MP3 to WAV: {ex.GetType().Name}: {ex.Message}. Returning raw bytes.");
                            return bytes;
                        }
                    }

                    // Not MP3 or conversion failed - return raw bytes for other audio types
                    Log.Warning($"[RimTalk.TTS] SiliconFlowClient: Received non-WAV audio with Content-Type '{mediaType}'. Returning raw bytes.");
                    return bytes;
                }

                // If JSON or text returned, log the parsed content for debugging
                string textPreview = bytes != null ? System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 2048)) : "<null>";
                if (mediaType == "application/json" || mediaType.StartsWith("text/") || textPreview.StartsWith("{") || textPreview.StartsWith("["))
                {
                    Log.Error($"[RimTalk.TTS] SiliconFlowClient: Expected WAV but server returned '{mediaType}'. Response body: {textPreview}");
                    return null;
                }

                // Unknown non-audio response: log a binary preview and return null
                string binPreview = bytes != null ? System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 256)) : "<null>";
                Log.Error($"[RimTalk.TTS] SiliconFlowClient: Unexpected non-audio response (Content-Type='{mediaType}'). Preview: {binPreview}");
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] SiliconFlowClient exception: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
