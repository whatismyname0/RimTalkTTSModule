using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.TTS.Service.EdgeTTSService
{
    /// <summary>
    /// Edge TTS WebSocket 客户端
    /// 使用 .NET 内置的 ClientWebSocket，无需额外依赖
    ///
    /// 端点与鉴权方式迁移自 edge-tts（https://github.com/rany2/edge-tts）
    /// 与 issue #490：2026 年 10 月起旧的 v1 Sec-MS-GEC token 会被服务端以 403 拒绝，
    /// 必须改用新的 v2 token（"&lt;16位 CS64&gt;-&lt;64位 SHA-256&gt;"），
    /// 同时 Edge 的朗读服务正在从 speech.platform.bing.com 迁移到
    /// api.msedgeservices.com。实测结论（2026-10）：
    ///
    ///   旧主机 + v1 token            -> 403
    ///   旧主机 + v2 token            -> 101
    ///   新主机 + v2 token + Ocp key  -> 101
    ///   新主机 + v2 token + TrustedClientToken -> 403
    ///
    /// 所以这里以新主机 + v2 token 为主，旧主机 + v2 token 作为回退。
    /// 新主机与旧主机的客户端密钥参数名不同（Ocp-Apim-Subscription-Key /
    /// TrustedClientToken），必须分别设置。
    /// </summary>
    public class EdgeTTSWebSocketClient : IDisposable
    {
        // TrustedClientToken - 来自 Edge 浏览器（两种端点共用同一个密钥值）
        private const string TRUSTED_CLIENT_TOKEN = EdgeTTSDRM.TRUSTED_CLIENT_TOKEN;

        // Chrome 版本信息 (来自 edge-tts 7.2.7 constants.py)
        private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
        private static readonly string CHROMIUM_MAJOR_VERSION = CHROMIUM_FULL_VERSION.Split('.')[0];

        /// <summary>
        /// v2 token 的版本串：新的 Sec-MS-GEC 格式对应前缀 2。
        /// </summary>
        private static readonly string SEC_MS_GEC_VERSION = $"2-{CHROMIUM_FULL_VERSION}";

        /// <summary>
        /// 一次朗读请求的端点候选：新主机优先，旧主机回退。
        /// </summary>
        private sealed class EndpointCandidate
        {
            public string Label;
            public string WssUrl;
            public string Origin;
            public bool UseV2Token;
        }

        private static readonly EndpointCandidate[] ENDPOINTS =
        {
            new EndpointCandidate
            {
                Label = "api.msedgeservices.com",
                WssUrl = $"wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1"
                         + $"?Ocp-Apim-Subscription-Key={TRUSTED_CLIENT_TOKEN}",
                // Edge 155 的朗读页面来源
                Origin = "msedge",
                UseV2Token = true,
            },
            new EndpointCandidate
            {
                Label = "speech.platform.bing.com",
                WssUrl = $"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1"
                         + $"?TrustedClientToken={TRUSTED_CLIENT_TOKEN}",
                // 旧主机的朗读请求来自 Edge 内置的 Read Aloud 扩展
                Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold",
                UseV2Token = true,
            },
        };

        /// <summary>
        /// 时钟偏移校正（秒）。迁移到 v2 token 之后 403 基本不再是时钟问题，
        /// 这里只在遇到 403 时作为兜底推后一个窗口，见 NudgeClockSkewOnForbidden。
        /// </summary>
        private static double clockSkewSeconds = 0.0;

        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private bool isDisposed = false;

        // 音频格式
        private const string OUTPUT_FORMAT = "audio-24khz-48kbitrate-mono-mp3";

        // 每个端点的尝试次数
        private const int ATTEMPTS_PER_ENDPOINT = 2;

        /// <summary>
        /// 生成 Sec-MS-GEC token。
        /// 使用当前时间戳（含时钟偏移校正）与客户端密钥，具体算法见 EdgeTTSDRM。
        /// </summary>
        private static string GenerateSecMsGec(bool useV2)
        {
            double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + clockSkewSeconds;
            return useV2 ? EdgeTTSDRM.GenerateSecMsGecV2(now) : EdgeTTSDRM.GenerateSecMsGecV1(now);
        }

        /// <summary>
        /// 生成随机 MUID (来自 edge-tts 7.2.7)
        /// </summary>
        private static string GenerateMuid()
        {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        /// <summary>
        /// 生成连接 ID (来自 edge-tts 7.2.7)
        /// </summary>
        private static string GenerateConnectId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 构建 WebSocket URL (来自 edge-tts 7.2.7)
        /// </summary>
        private static string BuildWebSocketUrl(EndpointCandidate endpoint)
        {
            string connectId = GenerateConnectId();
            string secMsGec = GenerateSecMsGec(endpoint.UseV2Token);
            return $"{endpoint.WssUrl}&ConnectionId={connectId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SEC_MS_GEC_VERSION}";
        }

        /// <summary>
        /// 生成 Edge TTS 音频
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <param name="voice">语音名称，如 "zh-CN-XiaoxiaoNeural"</param>
        /// <param name="rate">语速，如 "+0%", "+50%", "-25%"</param>
        /// <param name="volume">音量，如 "+0%"</param>
        /// <returns>MP3 音频数据</returns>
        public async Task<byte[]> SynthesizeAsync(string text, string voice = "zh-CN-XiaoxiaoNeural", 
            string rate = "+0%", string volume = "+0%")
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            
            // 依次尝试每个端点候选；每个候选内部再重试，用于扛过 429/暂时性失败。
            foreach (EndpointCandidate endpoint in ENDPOINTS)
            {
                for (int attempt = 1; attempt <= ATTEMPTS_PER_ENDPOINT; attempt++)
                {
                    string wssUrl = BuildWebSocketUrl(endpoint);
                    try
                    {
                        byte[] result = await TrySynthesizeAsync(wssUrl, endpoint, text, voice, rate, volume);
                        if (result != null && result.Length > 0)
                        {
                            return result;
                        }

                        // 连接成功但没有音频：多半是参数问题，换端点也没用，但保留一次重试。
                        TTSLog.Warning($"[EdgeTTS] {endpoint.Label} returned no audio");
                    }
                    catch (Exception ex)
                    {
                        TTSLog.Warning($"[EdgeTTS] {endpoint.Label} attempt {attempt}/{ATTEMPTS_PER_ENDPOINT} failed: {ex.Message}");

                        if (ex.Message.Contains("403"))
                        {
                            NudgeClockSkewOnForbidden(wssUrl);
                        }
                    }

                    // 同一端点的下一次尝试之前稍等，避免连着撞限流。
                    if (attempt < ATTEMPTS_PER_ENDPOINT)
                    {
                        await Task.Delay(500 * attempt);
                    }
                }
            }

            TTSLog.Error("[EdgeTTS] All endpoints and attempts failed");
            return null;
        }

        /// <summary>
        /// 403 的兜底处理。
        ///
        /// 历史上 403 表示本机时钟与服务端的 5 分钟窗口对不上：请求里带的是本机算出来的
        /// token，窗口错了就必然被拒。改成 v2 token 之后 403 基本不再由时钟引起，但如果
        /// 本机时间恰好落后于服务端一个窗口，把偏移推后一个窗口仍然是对的兜底，而且它
        /// 不会像旧实现那样无限累加。
        /// </summary>
        private static void NudgeClockSkewOnForbidden(string wssUrl)
        {
            lock (EndpointLock)
            {
                if (lastForbiddenUrl == wssUrl)
                {
                    // 同一个 token 窗口内已经调整过一次，再推只会更偏。
                    return;
                }

                lastForbiddenUrl = wssUrl;
                clockSkewSeconds += 300.0;
            }

            TTSLog.Warning($"[EdgeTTS] Got 403, nudging clock skew to {clockSkewSeconds:F0}s for the next attempt");
        }

        private static readonly object EndpointLock = new object();
        private static string lastForbiddenUrl;
        
        private async Task<byte[]> TrySynthesizeAsync(string wssUrl, EndpointCandidate endpoint, string text, string voice, string rate, string volume)
        {
            try
            {
                webSocket = new ClientWebSocket();
                cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60秒超时
                
                // 设置请求头 - 完全模拟 Edge 浏览器 (来自 edge-tts 7.2.7 constants.py)
                webSocket.Options.SetRequestHeader("User-Agent", 
                    $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{CHROMIUM_MAJOR_VERSION}.0.0.0 Safari/537.36 Edg/{CHROMIUM_MAJOR_VERSION}.0.0.0");
                webSocket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                webSocket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                webSocket.Options.SetRequestHeader("Pragma", "no-cache");
                webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
                webSocket.Options.SetRequestHeader("Origin", endpoint.Origin);
                
                // 添加 MUID Cookie (来自 edge-tts 7.2.7)
                webSocket.Options.SetRequestHeader("Cookie", $"muid={GenerateMuid()};");
                
                if (Prefs.DevMode)
                {
                    TTSLog.Message($"[EdgeTTS] Connecting to {endpoint.Label}: {wssUrl.Substring(0, Math.Min(100, wssUrl.Length))}...");
                }
                
                // 连接
                await webSocket.ConnectAsync(new Uri(wssUrl), cts.Token);
                
                if (webSocket.State != WebSocketState.Open)
                {
                    throw new Exception($"WebSocket connection failed, state: {webSocket.State}");
                }
                
                if (Prefs.DevMode)
                {
                    TTSLog.Message($"[EdgeTTS] WebSocket connected ({endpoint.Label})");
                }
                
                // 发送配置消息
                await SendConfigMessageAsync();
                
                // 发送 SSML 消息
                string ssml = BuildSSML(text, voice, rate, volume);
                await SendSSMLMessageAsync(ssml);
                
                // 接收音频数据
                byte[] audioData = await ReceiveAudioAsync();
                
                return audioData;
            }
            finally
            {
                CloseWebSocket();
            }
        }
        
        /// <summary>
        /// 生成 JavaScript 风格的日期字符串 (来自 edge-tts 7.2.7)
        /// </summary>
        private static string DateToString()
        {
            return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0000 (Coordinated Universal Time)";
        }
        
        /// <summary>
        /// 发送配置消息 (来自 edge-tts 7.2.7)
        /// </summary>
        private async Task SendConfigMessageAsync()
        {
            string timestamp = DateToString();
            
            // JSON 结构 (来自 edge-tts 7.2.7 communicate.py)
            string configMessage = 
                $"X-Timestamp:{timestamp}\r\n" +
                "Content-Type:application/json; charset=utf-8\r\n" +
                "Path:speech.config\r\n\r\n" +
                "{\"context\":{\"synthesis\":{\"audio\":{" +
                "\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"}," +
                $"\"outputFormat\":\"{OUTPUT_FORMAT}\"" +
                "}}}}\r\n";
            
            byte[] buffer = Encoding.UTF8.GetBytes(configMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
            
            if (Prefs.DevMode)
            {
                TTSLog.Message("[EdgeTTS] Config message sent");
            }
        }
        
        /// <summary>
        /// 发送 SSML 消息 (来自 edge-tts 7.2.7)
        /// </summary>
        private async Task SendSSMLMessageAsync(string ssml)
        {
            string timestamp = DateToString();
            string requestId = GenerateConnectId();
            
            // 注意：X-Timestamp 后面有个 Z，这是 Microsoft Edge 的 bug (来自 edge-tts 7.2.7 注释)
            string ssmlMessage = 
                $"X-RequestId:{requestId}\r\n" +
                "Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{timestamp}Z\r\n" +
                "Path:ssml\r\n\r\n" +
                ssml;
            
            byte[] buffer = Encoding.UTF8.GetBytes(ssmlMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
            
            if (Prefs.DevMode)
            {
                TTSLog.Message($"[EdgeTTS] SSML message sent, RequestId: {requestId}");
            }
        }
        
        /// <summary>
        /// 接收音频数据 (来自 edge-tts 7.2.7)
        /// </summary>
        private async Task<byte[]> ReceiveAudioAsync()
        {
            var audioChunks = new List<byte[]>();
            byte[] buffer = new byte[16384]; // 增大缓冲区
            bool audioReceived = false;
            
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (Prefs.DevMode)
                        {
                            TTSLog.Message($"[EdgeTTS] WebSocket closed: {result.CloseStatus} - {result.CloseStatusDescription}");
                        }
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // 二进制消息包含音频数据
                        // 格式: 2字节头长度 + 头部 + 音频数据
                        if (result.Count > 2)
                        {
                            int headerLength = (buffer[0] << 8) | buffer[1];
                            if (result.Count > headerLength + 2)
                            {
                                // 解析头部检查 Path
                                string header = Encoding.UTF8.GetString(buffer, 2, headerLength);
                                if (header.Contains("Path:audio"))
                                {
                                    // Content-Type 可能是 audio/mpeg；服务在流结束时会发送
                                    // 没有 Content-Type 的空消息，所以这里只认真正的音频。
                                    if (header.Contains("Content-Type:audio/mpeg"))
                                    {
                                        int audioStart = headerLength + 2;
                                        int audioLength = result.Count - audioStart;
                                        if (audioLength > 0)
                                        {
                                            byte[] audioChunk = new byte[audioLength];
                                            Array.Copy(buffer, audioStart, audioChunk, 0, audioLength);
                                            audioChunks.Add(audioChunk);
                                            audioReceived = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // 文本消息
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        if (message.Contains("Path:turn.end"))
                        {
                            if (Prefs.DevMode)
                            {
                                TTSLog.Message("[EdgeTTS] Received turn.end");
                            }
                            break;
                        }
                        
                        // 检查错误
                        if (message.Contains("Path:response") && message.Contains("error"))
                        {
                            TTSLog.Error($"[EdgeTTS] Server error: {message}");
                            throw new Exception($"Server error: {message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    TTSLog.Warning("[EdgeTTS] Operation cancelled (timeout)");
                    break;
                }
            }
            
            if (!audioReceived)
            {
                TTSLog.Warning("[EdgeTTS] No audio data received");
                return null;
            }
            
            // 合并所有音频块
            if (audioChunks.Count == 0)
            {
                return null;
            }
            
            int totalLength = 0;
            foreach (var chunk in audioChunks)
            {
                totalLength += chunk.Length;
            }
            
            byte[] audioData = new byte[totalLength];
            int offset = 0;
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, audioData, offset, chunk.Length);
                offset += chunk.Length;
            }
            
            if (Prefs.DevMode)
            {
                TTSLog.Message($"[EdgeTTS] Received {audioChunks.Count} audio chunks, total {totalLength} bytes");
            }
            
            return audioData;
        }
        
        /// <summary>
        /// 构建 SSML (来自 edge-tts 7.2.7)
        /// </summary>
        private string BuildSSML(string text, string voice, string rate, string volume)
        {
            // 转义 XML 特殊字符
            text = System.Security.SecurityElement.Escape(text);
            
            return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                   $"<voice name='{voice}'>" +
                   $"<prosody pitch='+0Hz' rate='{rate}' volume='{volume}'>" +
                   text +
                   "</prosody>" +
                   "</voice>" +
                   "</speak>";
        }
        
        /// <summary>
        /// 关闭 WebSocket 连接
        /// </summary>
        private void CloseWebSocket()
        {
            try
            {
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None).Wait(1000);
                }
            }
            catch { }
            finally
            {
                webSocket?.Dispose();
                webSocket = null;
                cts?.Dispose();
                cts = null;
            }
        }
        
        public void Dispose()
        {
            if (!isDisposed)
            {
                CloseWebSocket();
                isDisposed = true;
            }
        }
    }
}
