using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Data;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// HTTP client for custom OpenAI-compatible TTS providers.
    /// Supports standard OpenAI TTS format and custom request body templates.
    /// </summary>
    public static class CustomTTSClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>Extra attempts made after the first one, for HTTP 503 (busy).</summary>
        private const int BUSY_MAX_RETRIES = 4;

        /// <summary>Backoff before retry 1..BUSY_MAX_RETRIES: 1s, 2s, 4s, 8s.</summary>
        private static readonly int[] BUSY_RETRY_DELAYS_MS = { 1000, 2000, 4000, 8000 };

        /// <summary>
        /// Generate speech audio from a custom TTS provider.
        /// </summary>
        public static async Task<byte[]> GenerateSpeechAsync(
            TTSRequest request,
            CustomProviderConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                TTSLog.Error("[RimTalk.TTS] CustomTTSClient: config is null");
                return null;
            }

            if (string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                TTSLog.Error("[RimTalk.TTS] CustomTTSClient: BaseUrl is empty");
                return null;
            }

            try
            {
                string url = config.GetFullUrl();
                string jsonBody = BuildRequestBody(request, config);

                TTSLog.Message($"[RimTalk.TTS] CustomTTSClient: POST {url} (voice={request.Voice}, model={request.Model})");

                int timeoutSeconds = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30;

                for (int attempt = 0; ; attempt++)
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    // Set authentication header
                    if (config.RequiresApiKey && !string.IsNullOrWhiteSpace(request.ApiKey))
                    {
                        string headerName = string.IsNullOrWhiteSpace(config.AuthHeaderName) ? "Authorization" : config.AuthHeaderName;
                        string headerPrefix = config.AuthHeaderPrefix ?? "Bearer ";
                        httpRequest.Headers.TryAddWithoutValidation(headerName, headerPrefix + request.ApiKey);
                    }

                    // Set timeout (applies per attempt, so retries do not eat the whole budget)
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();

                        // 503 = "busy", a transient condition: retry with exponential backoff.
                        // 400/404/409/500 are terminal - retrying them unchanged cannot help.
                        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable &&
                            attempt < BUSY_MAX_RETRIES)
                        {
                            int delayMs = BUSY_RETRY_DELAYS_MS[attempt];
                            TTSLog.Warning($"[RimTalk.TTS] CustomTTSClient: HTTP 503 (server busy), retry {attempt + 1}/{BUSY_MAX_RETRIES} in {delayMs} ms - {errorBody}");
                            await Task.Delay(delayMs, cancellationToken);
                            continue;
                        }

                        TTSLog.Error($"[RimTalk.TTS] CustomTTSClient: HTTP {(int)response.StatusCode} {response.ReasonPhrase} - {errorBody}");
                        return null;
                    }

                    byte[] audioData = await response.Content.ReadAsByteArrayAsync();

                    if (audioData == null || audioData.Length == 0)
                    {
                        TTSLog.Warning("[RimTalk.TTS] CustomTTSClient: Empty response from server");
                        return null;
                    }

                    if (attempt > 0)
                    {
                        TTSLog.Message($"[RimTalk.TTS] CustomTTSClient: succeeded on retry {attempt}");
                    }

                    TTSLog.Message($"[RimTalk.TTS] CustomTTSClient: Received {audioData.Length} bytes");
                    return audioData;
                }
            }
            catch (OperationCanceledException)
            {
                TTSLog.Warning("[RimTalk.TTS] CustomTTSClient: Request cancelled or timed out");
                throw;
            }
            catch (Exception ex)
            {
                // Walk the full exception chain to reveal root cause (e.g. SSL, DNS, connection refused)
                var sb = new System.Text.StringBuilder();
                sb.Append($"{ex.GetType().Name}: {ex.Message}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.Append($" --> {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                TTSLog.Error($"[RimTalk.TTS] CustomTTSClient: {sb}");
                return null;
            }
        }

        /// <summary>
        /// Build the JSON request body, either from template or standard OpenAI format.
        /// </summary>
        private static string BuildRequestBody(TTSRequest request, CustomProviderConfig config)
        {
            if (config.UseCustomRequestBody && !string.IsNullOrWhiteSpace(config.CustomRequestBodyTemplate))
            {
                return BuildFromTemplate(request, config);
            }

            return BuildStandardBody(request, config);
        }

        /// <summary>
        /// Build standard OpenAI-compatible request body with optional extra fields.
        /// </summary>
        private static string BuildStandardBody(TTSRequest request, CustomProviderConfig config)
        {
            var sb = new StringBuilder();
            sb.Append('{');

            // Required fields
            sb.Append($"\"model\":{JsonEscape(request.Model ?? config.Model ?? "tts-1")}");
            sb.Append($",\"input\":{JsonEscape(request.Input ?? "")}");
            sb.Append($",\"voice\":{JsonEscape(request.Voice ?? config.DefaultVoice ?? "alloy")}");

            // Response format
            if (!string.IsNullOrWhiteSpace(config.ResponseFormat))
            {
                sb.Append($",\"response_format\":{JsonEscape(config.ResponseFormat)}");
            }

            // Optional: speed
            if (config.EnableSpeed)
            {
                sb.Append($",\"speed\":{request.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            // Optional: temperature
            if (config.EnableTemperature)
            {
                sb.Append($",\"temperature\":{request.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            // Optional: top_p
            if (config.EnableTopP)
            {
                sb.Append($",\"top_p\":{request.TopP.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            // Optional: emotion/style
            if (config.EnableEmotion && !string.IsNullOrWhiteSpace(request.InstructText))
            {
                string fieldName = string.IsNullOrWhiteSpace(config.EmotionFieldName) ? "emotion" : config.EmotionFieldName;
                sb.Append($",{JsonEscape(fieldName, true)}:{JsonEscape(request.InstructText)}");
            }

            // Optional: instruct_text
            if (config.EnableInstructText && !string.IsNullOrWhiteSpace(request.InstructText))
            {
                string fieldName = string.IsNullOrWhiteSpace(config.InstructTextFieldName) ? "instruct_text" : config.InstructTextFieldName;
                sb.Append($",{JsonEscape(fieldName, true)}:{JsonEscape(request.InstructText)}");
            }

            // Optional: streaming
            if (config.EnableStreaming)
            {
                sb.Append(",\"stream\":true");
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Build request body from user's custom template with placeholder substitution.
        /// </summary>
        private static string BuildFromTemplate(TTSRequest request, CustomProviderConfig config)
        {
            string template = config.CustomRequestBodyTemplate;

            template = template.Replace("{model}", EscapeForTemplate(request.Model ?? config.Model ?? "tts-1"));
            template = template.Replace("{input}", EscapeForTemplate(request.Input ?? ""));
            template = template.Replace("{voice}", EscapeForTemplate(request.Voice ?? config.DefaultVoice ?? "alloy"));
            template = template.Replace("{speed}", request.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            template = template.Replace("{response_format}", EscapeForTemplate(config.ResponseFormat ?? "mp3"));
            template = template.Replace("{temperature}", request.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
            template = template.Replace("{top_p}", request.TopP.ToString(System.Globalization.CultureInfo.InvariantCulture));
            template = template.Replace("{emotion}", EscapeForTemplate(request.InstructText ?? ""));
            template = template.Replace("{instruct_text}", EscapeForTemplate(request.InstructText ?? ""));
            template = template.Replace("{api_key}", EscapeForTemplate(request.ApiKey ?? ""));

            // Anything still looking like {placeholder} was never substituted: the name is
            // not one we know (typically a typo such as {voices}). It goes out verbatim, and
            // the outcome is quiet - an unknown field is ignored, so the server answers 200
            // and speaks with whatever default voice it picked. Warn, but send as-is.
            var leftovers = System.Text.RegularExpressions.Regex.Matches(template, @"\{[A-Za-z_][A-Za-z0-9_]*\}");
            if (leftovers.Count > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (System.Text.RegularExpressions.Match m in leftovers)
                {
                    if (!names.Contains(m.Value)) names.Add(m.Value);
                }
                TTSLog.Warning($"[RimTalk.TTS] CustomTTSClient: unrecognized placeholder(s) in template, sent as-is: {string.Join(", ", names.ToArray())}");
            }

            return template;
        }

        /// <summary>
        /// Escape a string for JSON value (with surrounding quotes).
        /// </summary>
        private static string JsonEscape(string value, bool keyOnly = false)
        {
            if (value == null) return "null";
            
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Escape a string for use inside a template (content only, no surrounding quotes).
        /// </summary>
        private static string EscapeForTemplate(string value)
        {
            if (value == null) return "";
            var sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
