using System;
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

        public static async Task<byte[]> GenerateSpeechAsync(string text, string apiKey, string referenceId, string model, float speed, float temperature, float topP, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new ArgumentException("apiKey required for SiliconFlowClient");

                var url = DefaultBaseUrl + "/audio/speech";

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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

                string modelEsc = JsonEscape(model ?? "");
                string inputEsc = JsonEscape(text ?? "");
                string voicePart = string.IsNullOrWhiteSpace(referenceId) ? "null" : "\"" + JsonEscape(referenceId) + "\"";
                // include speed and enforce wav response format
                string json = "{\"model\":\"" + modelEsc + "\",\"input\":\"" + inputEsc + "\",\"voice\":" + voicePart + ",\"speed\":" + speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"response_format\":\"wav\"}";
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
                return bytes?.Length > 0 ? bytes : null;
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
