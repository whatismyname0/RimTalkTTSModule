using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RimTalk.TTS.Config;
using RimTalk.Util;

namespace RimTalk.TTS.Llm
{
    //给 TTSService 返回结构化数据的辅助类
    public class PreProcessResult
    {
        public string Text;
        public string Emotion;
    }

    [Serializable]
    public class PreProcessResultJson
    {
        public string text;
        public string emotion;
    }

    /// <summary>
    /// Simple OpenAI-compatible LLM client for TTS text preprocessing: plain text in/out, connection
    /// details supplied by the caller's <see cref="LlmRequestOptions"/> snapshot.
    /// </summary>
    public static class InputPreProcessClient
    {
        // Preprocessing runs on a worker thread (the pipeline starts with Task.Run), where Unity's
        // networking API must not be used. HttpClient is pure managed code, and its timeout also
        // bounds an LLM call that would otherwise hang forever.
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        /// <summary>Send a simple text query and get text response (no role/conversation context).</summary>
        public static async Task<(PreProcessResult response, bool success)> QueryAsync(string prompt, string text, LlmRequestOptions options)
        {
            if (options == null)
            {
                TTSLog.Warning("[RimTalk.TTS] SimpleLLMClient: options are null");
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                TTSLog.Warning("[RimTalk.TTS] SimpleLLMClient: API key not configured");
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(options.Model))
            {
                TTSLog.Warning("[RimTalk.TTS] SimpleLLMClient: Model not configured");
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                TTSLog.Warning("[RimTalk.TTS] Empty prompt provided to SimpleLLMClient");
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                TTSLog.Warning("[RimTalk.TTS] SimpleLLMClient: Base URL is empty");
                return (null, false);
            }

            try
            {
                if (options.RemoveBrackets)
                    text = RemoveBrackets(text);

                string jsonRequest = BuildRequest(prompt, text, options.Model);

                TTSLog.Message($"[RimTalk.TTS] Sending LLM request to {options.BaseUrl}: {prompt}");

                var (responseJson, success) = await SendHttpRequestAsync(jsonRequest, options.BaseUrl, options.ApiKey);

                if (!success)
                {
                    TTSLog.Warning("[RimTalk.TTS] LLM HTTP request failed");
                    return (null, false);
                }

                if (string.IsNullOrEmpty(responseJson))
                {
                    TTSLog.Warning("[RimTalk.TTS] LLM returned empty response");
                    return (null, false);
                }

                PreProcessResult result = ExtractContentFromResponse(responseJson);
                if (result == null)
                {
                    TTSLog.Warning("[RimTalk.TTS] Failed to extract content from LLM response");
                    return (null, false);
                }

                return (result, true);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] SimpleLLMClient.QueryAsync error: {ex.Message}\n{ex.StackTrace}");
                return (null, false);
            }
        }

        private static string BuildRequest(string prompt, string text, string model)
        {
            var escapedPrompt = prompt
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            var escapedText = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            return $@"{{
  ""model"": ""{model}"",
  ""messages"": [
    {{
      ""role"": ""system"",
      ""content"": ""{escapedPrompt}""
    }},
    {{
      ""role"": ""user"",
      ""content"": ""{escapedText}""
    }}
  ],
  ""temperature"": 0.65,
  ""top_p"": 0.9,
  ""response_format"": {{
    ""type"": ""json_object""
  }}
}}";
        }

        private static async Task<(string response, bool success)> SendHttpRequestAsync(string jsonContent, string baseUrl, string apiKey)
        {
            // OpenAI-compatible endpoint format
            baseUrl = baseUrl?.Trim().TrimEnd('/');
            string endpoint;
            if (baseUrl.Contains("/chat/completions"))
            {
                endpoint = baseUrl;
            }
            else if (baseUrl.EndsWith("/v1"))
            {
                endpoint = baseUrl + "/chat/completions";
            }
            else
            {
                endpoint = baseUrl + "/v1/chat/completions";
            }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // OpenAI-compatible: Bearer token
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
                    }

                    using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                    {
                        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            TTSLog.Error($"[RimTalk.TTS] HTTP request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                            TTSLog.Error($"[RimTalk.TTS] Response: {responseText}");
                            return (null, false);
                        }

                        TTSLog.Message($"[RimTalk.TTS] HTTP response received: {responseText}");
                        return (responseText, true);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // HttpClient reports its own timeout as a cancellation.
                TTSLog.Error($"[RimTalk.TTS] HTTP request timed out after {Http.Timeout.TotalSeconds:F0}s");
                return (null, false);
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] HTTP request error: {ex.Message}\n{ex.StackTrace}");
                return ("", false);
            }
        }

        private static PreProcessResult ExtractContentFromResponse(string jsonResponse)
        {
            try
            {
                // Simple JSON parsing to extract content field
                // Looking for: "choices":[{"message":{"content":"..."}}]
                int contentIndex = jsonResponse.IndexOf("\"content\"");
                if (contentIndex == -1) return null;

                int startQuote = jsonResponse.IndexOf("\"", contentIndex + 9);
                if (startQuote == -1) return null;

                startQuote++; // Move past the opening quote

                // Find the closing quote, handling escaped quotes
                int endQuote = startQuote;
                while (endQuote < jsonResponse.Length)
                {
                    if (jsonResponse[endQuote] == '\"' && jsonResponse[endQuote - 1] != '\\')
                    {
                        break;
                    }
                    endQuote++;
                }

                if (endQuote >= jsonResponse.Length) return null;

                string content = jsonResponse.Substring(startQuote, endQuote - startQuote);

                // Unescape JSON string
                content = content
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                var parsed = JsonUtil.DeserializeFromJson<PreProcessResultJson>(content);
                return new PreProcessResult { Text = parsed.text, Emotion = parsed.emotion };
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Failed to parse JSON response: {ex.Message}");
                return null;
            }
        }

        /// <summary>Remove content within various bracket types and replace with ellipsis.</summary>
        private static string RemoveBrackets(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = Regex.Replace(text, @"\([^()]*\)", "...");  // (content)
            text = Regex.Replace(text, @"\uff08[^\uff08\uff09]*\uff09", "...");  // （content）full-width
            text = Regex.Replace(text, @"\[[^\[\]]*\]", "...");  // [content]
            text = Regex.Replace(text, @"\u3010[^\u3010\u3011]*\u3011", "...");  // 【content】full-width
            text = Regex.Replace(text, @"\*[^*]*\*", "...");  // *content*
            text = Regex.Replace(text, @"<[^<>]*>", "...");  // <content>
            text = Regex.Replace(text, @"/[^/]*/", "...");  // /content/
            text = Regex.Replace(text, @"\\[^\\]*\\", "...");  // \content\
            text = Regex.Replace(text, @"#[^#]*#", "...");  // #content#

            return text;
        }
    }
}
