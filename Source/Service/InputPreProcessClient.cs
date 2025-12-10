using System;
using System.Text;
using System.Threading.Tasks;
using RimTalk.TTS.Data;
using UnityEngine.Networking;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Simple LLM client for TTS text processing - no role concept, just plain text in/out
    /// </summary>
    public static class InputPreProcessClient
    {
        /// <summary>
        /// Get base URL for the configured provider
        /// </summary>
        private static string GetBaseUrl(TTSSettings settings)
        {
            return settings.ApiProvider switch
            {
                TTSApiProvider.DeepSeek => "https://api.deepseek.com",
                TTSApiProvider.OpenAI => "https://api.openai.com",
                TTSApiProvider.Custom => settings.CustomBaseUrl,
                _ => ""
            };
        }

        /// <summary>
        /// Send a simple text query and get text response (no role/conversation context)
        /// </summary>
        public static async Task<(string response, bool success)> QueryAsync(string prompt, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimTalk.TTS] SimpleLLMClient: settings is null");
                return ("", false);
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                Log.Warning("[RimTalk.TTS] SimpleLLMClient: API key not configured");
                return ("", false);
            }

            if (string.IsNullOrWhiteSpace(settings.Model))
            {
                Log.Warning("[RimTalk.TTS] SimpleLLMClient: Model not configured");
                return ("", false);
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Log.Warning("[RimTalk.TTS] Empty prompt provided to SimpleLLMClient");
                return ("", false);
            }

            string baseUrl = GetBaseUrl(settings);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Log.Warning("[RimTalk.TTS] SimpleLLMClient: Base URL is empty");
                return ("", false);
            }

            try
            {
                // Build simple OpenAI-compatible request with single user message
                string jsonRequest = BuildRequest(prompt, settings.Model);
                
                Log.Message($"[RimTalk.TTS] Sending LLM request to {settings.ApiProvider}: {prompt}");

                // Send HTTP request
                var (responseJson, success) = await SendHttpRequestAsync(jsonRequest, baseUrl, settings.ApiKey);
                
                if (!success)
                {
                    Log.Warning("[RimTalk.TTS] LLM HTTP request failed");
                    return ("", false);
                }
                
                if (string.IsNullOrEmpty(responseJson))
                {
                    Log.Warning("[RimTalk.TTS] LLM returned empty response");
                    return ("", false);
                }

                // Extract content from response
                string content = ExtractContentFromResponse(responseJson);
                
                if (string.IsNullOrEmpty(content))
                {
                    Log.Warning("[RimTalk.TTS] Failed to extract content from LLM response");
                    return ("", false);
                }

                return (content, true);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] SimpleLLMClient.QueryAsync error: {ex.Message}\n{ex.StackTrace}");
                return ("", false);
            }
        }

        private static string BuildRequest(string prompt, string model)
        {
            // Manually build minimal JSON to avoid role concept entirely
            var escapedPrompt = prompt
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            return $@"{{
  ""model"": ""{model}"",
  ""messages"": [
    {{
      ""role"": ""user"",
      ""content"": ""{escapedPrompt}""
    }}
  ]
}}";
        }

        private static async Task<(string response, bool success)> SendHttpRequestAsync(string jsonContent, string baseUrl, string apiKey)
        {
            // OpenAI-compatible endpoint format
            baseUrl = baseUrl?.Trim().TrimEnd('/');
            string endpoint;
            if (baseUrl.Contains("/v1/chat/completions"))
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
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonContent);

                using var webRequest = new UnityWebRequest(endpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                // OpenAI-compatible: Bearer token
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                var asyncOperation = webRequest.SendWebRequest();

                // Wait for completion
                while (!asyncOperation.isDone)
                {
                    if (Current.Game == null) return ("", false);
                    await Task.Delay(50);
                }

                // Check for errors
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Log.Error($"[RimTalk.TTS] HTTP request failed: {webRequest.responseCode} {webRequest.error}");
                    Log.Error($"[RimTalk.TTS] Response: {webRequest.downloadHandler?.text}");
                    return ("", false);
                }

                string responseText = webRequest.downloadHandler.text;
                Log.Message($"[RimTalk.TTS] HTTP response received: {responseText}");
                return (responseText, true);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] HTTP request error: {ex.Message}\n{ex.StackTrace}");
                return ("", false);
            }
        }

        private static string ExtractContentFromResponse(string jsonResponse)
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

                return content;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Failed to parse JSON response: {ex.Message}");
                return null;
            }
        }
    }
}
