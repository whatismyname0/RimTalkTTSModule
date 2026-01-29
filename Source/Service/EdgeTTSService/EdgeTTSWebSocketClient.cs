using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// 基于 edge-tts 7.2.7 Python 库的协议实现
    /// https://github.com/rany2/edge-tts
    /// 
    /// Edge TTS 协议说明：
    /// 1. 连接到 wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1
    /// 2. 发送配置消息和 SSML
    /// 3. 接收二进制音频数据
    /// </summary>
    public class EdgeTTSWebSocketClient : IDisposable
    {
        // Edge TTS 端点 (来自 edge-tts 7.2.7)
        private const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
        
        // TrustedClientToken - 来自 Edge 浏览器
        private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        
        // WebSocket URL
        private static readonly string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";
        
        // Windows 文件时间纪元 (1601-01-01 到 1970-01-01 的秒数)
        private const long WIN_EPOCH = 11644473600;
        
        // Chrome 版本信息 (来自 edge-tts 7.2.7)
        private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
        private static readonly string CHROMIUM_MAJOR_VERSION = CHROMIUM_FULL_VERSION.Split('.')[0];
        private static readonly string SEC_MS_GEC_VERSION = $"1-{CHROMIUM_FULL_VERSION}";
        
        // 时钟偏移校正（秒）
        private static double clockSkewSeconds = 0.0;
        
        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private bool isDisposed = false;
        
        // 音频格式
        private const string OUTPUT_FORMAT = "audio-24khz-48kbitrate-mono-mp3";
        
        // 最大重试次数
        private const int MAX_RETRIES = 3;
        
        /// <summary>
        /// 生成 Sec-MS-GEC token
        /// 基于当前时间戳和 TrustedClientToken 生成 SHA256 哈希
        /// 算法来自 edge-tts 7.2.7 drm.py
        /// </summary>
        private static string GenerateSecMsGec()
        {
            // 获取当前 Unix 时间戳（秒）+ 时钟偏移校正
            double ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + clockSkewSeconds;
            
            // 转换为 Windows 文件时间纪元
            ticks += WIN_EPOCH;
            
            // 向下取整到最近的 5 分钟（300 秒）
            ticks -= ticks % 300;
            
            // 转换为 100 纳秒间隔（Windows 文件时间格式）
            ticks *= 1e9 / 100;
            
            // 创建要哈希的字符串
            string strToHash = $"{ticks:F0}{TRUSTED_CLIENT_TOKEN}";
            
            // 计算 SHA256 哈希并返回大写的十六进制字符串
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(strToHash));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
            }
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
        private static string BuildWebSocketUrl()
        {
            string connectId = GenerateConnectId();
            string secMsGec = GenerateSecMsGec();
            return $"{WSS_URL}&ConnectionId={connectId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SEC_MS_GEC_VERSION}";
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
            
            // 重试逻辑
            for (int retry = 0; retry < MAX_RETRIES; retry++)
            {
                try
                {
                    string wssUrl = BuildWebSocketUrl();
                    byte[] result = await TrySynthesizeAsync(wssUrl, text, voice, rate, volume);
                    if (result != null && result.Length > 0)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[EdgeTTS] Attempt {retry + 1}/{MAX_RETRIES} failed: {ex.Message}");
                    }
                    
                    // 如果是 403 错误，尝试调整时钟偏移
                    if (ex.Message.Contains("403"))
                    {
                        // 尝试调整时钟偏移
                        clockSkewSeconds += 300; // 增加 5 分钟
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[EdgeTTS] Adjusting clock skew to {clockSkewSeconds} seconds");
                        }
                    }
                    
                    // 如果是资源耗尽错误，等待一段时间后重试
                    if (ex.Message.Contains("ResourceExhausted") || ex.Message.Contains("1013"))
                    {
                        await Task.Delay(1000 * (retry + 1)); // 递增等待时间
                    }
                }
            }
            
            Log.Error("[EdgeTTS] All attempts failed");
            return null;
        }
        
        private async Task<byte[]> TrySynthesizeAsync(string wssUrl, string text, string voice, string rate, string volume)
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
                webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
                
                // 添加 MUID Cookie (来自 edge-tts 7.2.7)
                webSocket.Options.SetRequestHeader("Cookie", $"muid={GenerateMuid()};");
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EdgeTTS] Connecting to: {wssUrl.Substring(0, Math.Min(100, wssUrl.Length))}...");
                }
                
                // 连接
                await webSocket.ConnectAsync(new Uri(wssUrl), cts.Token);
                
                if (webSocket.State != WebSocketState.Open)
                {
                    throw new Exception($"WebSocket connection failed, state: {webSocket.State}");
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message("[EdgeTTS] WebSocket connected successfully");
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
                Log.Message("[EdgeTTS] Config message sent");
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
                Log.Message($"[EdgeTTS] SSML message sent, RequestId: {requestId}");
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
                            Log.Message($"[EdgeTTS] WebSocket closed: {result.CloseStatus} - {result.CloseStatusDescription}");
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
                                    // 检查 Content-Type
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
                                Log.Message("[EdgeTTS] Received turn.end");
                            }
                            break;
                        }
                        
                        // 检查错误
                        if (message.Contains("Path:response") && message.Contains("error"))
                        {
                            Log.Error($"[EdgeTTS] Server error: {message}");
                            throw new Exception($"Server error: {message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("[EdgeTTS] Operation cancelled (timeout)");
                    break;
                }
            }
            
            if (!audioReceived)
            {
                Log.Warning("[EdgeTTS] No audio data received");
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
                Log.Message($"[EdgeTTS] Received {audioChunks.Count} audio chunks, total {totalLength} bytes");
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
