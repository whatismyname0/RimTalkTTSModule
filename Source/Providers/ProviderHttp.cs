using System;
using System.Collections.Generic;
using System.Net.Http;
using RimTalk.Util;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Shared HTTP plumbing for the online TTS providers.
    ///
    /// All providers use cookie-less clients: on Mono/Win32 the default cookie container can take
    /// native paths that are unsafe off the main thread (the Fish Audio helper hit this first).
    /// Error responses are reduced to a single line with the API's own error message when the body
    /// contains one.
    /// </summary>
    internal static class ProviderHttp
    {
        private const int DefaultTimeoutSeconds = 100;
        private const int MaxErrorBodyLength = 300;

        public static HttpClient CreateClient(TimeSpan? timeout = null)
        {
            TimeSpan effective = timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds);

            try
            {
                var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = true };
                return new HttpClient(handler) { Timeout = effective };
            }
            catch (Exception ex)
            {
                // Some Mono/Unity combinations cannot construct the handler; the caller still gets a
                // usable client, just with the default cookie handling.
                TTSLog.Warning($"[RimTalk.TTS] ProviderHttp: cookie-less handler unavailable, using default HttpClient - {ex.Message}");
                return new HttpClient { Timeout = effective };
            }
        }

        /// <summary>One-line error description for logs: provider, HTTP status and API message if available.</summary>
        public static string DescribeFailure(HttpResponseMessage response, string body, string provider)
        {
            string status;
            if (response == null)
            {
                status = "no response";
            }
            else
            {
                status = ((int)response.StatusCode).ToString();
                if (!string.IsNullOrEmpty(response.ReasonPhrase))
                    status += " " + response.ReasonPhrase;
            }

            string detail = TryExtractApiError(body) ?? Truncate(body, MaxErrorBodyLength);
            return string.IsNullOrEmpty(detail)
                ? $"[RimTalk.TTS] {provider}: HTTP {status}"
                : $"[RimTalk.TTS] {provider}: HTTP {status}: {detail}";
        }

        private static string TryExtractApiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                var data = JsonUtil.DeserializeFromJson<Dictionary<string, object>>(body);
                if (data == null) return null;

                object value;
                if (data.TryGetValue("error", out value) && value != null)
                {
                    var nested = value as Dictionary<string, object>;
                    if (nested != null)
                    {
                        object message;
                        if (nested.TryGetValue("message", out message) && message != null)
                            return message.ToString();
                    }
                    return value.ToString();
                }

                foreach (string key in new[] { "message", "detail", "error_description" })
                {
                    if (data.TryGetValue(key, out value) && value != null)
                        return value.ToString();
                }
            }
            catch
            {
                // Body is not JSON (HTML gateway page, XML error, ...): the truncated body is enough.
            }

            return null;
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string singleLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= max ? singleLine : singleLine.Substring(0, max) + "...";
        }
    }
}
