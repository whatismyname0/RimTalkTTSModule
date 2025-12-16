using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Verse;
using RimTalk.Util;

namespace RimTalk.TTS.Service.FishAudioService;

/// <summary>
/// Client for Fish Audio TTS API using Python SDK via local HTTP server
/// Manages a persistent Python server process for handling concurrent requests
/// </summary>
public static class FishAudioTTSClient
{
    private static readonly string PythonScriptPath = GetPythonScriptPath();
    private static string _pythonExecutablePath;
    
    private static string GetPythonScriptPath()
    {
        try
        {
            // Method 1: Try Assembly.Location
            string assemblyLocation = typeof(FishAudioTTSClient).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                string assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string scriptPath = Path.Combine(assemblyDir, "..", "..", "Source", "Service", "FishAudioService", "fish_audio_tts.py");
                    if (File.Exists(scriptPath))
                    {
                        return scriptPath;
                    }
                }
            }
            
            // Method 2: Try from RimWorld Mods directory structure
            // Assembly is in: Mods/rimtalk/1.6/Assemblies/RimTalk.dll
            // Script is in:   Mods/rimtalk/Source/Service/fish_audio_tts.py
            var loadedMods = Verse.LoadedModManager.RunningMods;
            foreach (var mod in loadedMods)
            {
                if (mod.Name.Contains("RimTalk") || mod.PackageId.Contains("rimtalk"))
                {
                    string scriptPath = Path.Combine(mod.RootDir.ToString(), "Source", "Service", "FishAudioService", "fish_audio_tts.py");
                    if (File.Exists(scriptPath))
                    {
                        return scriptPath;
                    }
                }
            }
            
            Log.Error("FishAudio TTS: Could not locate fish_audio_tts.py");
            return "";
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Failed to get Python script path - {ex.Message}");
            return "";
        }
    }
    
    private static Process _serverProcess;
    private static HttpClient _httpClient;
    private static readonly object _lock = new object();
    private static bool _serverStarting = false;
    private const int ServerPort = 5678;
    private static readonly string ServerUrl = $"http://127.0.0.1:{ServerPort}";

    /// <summary>
    /// Resolve Python executable path. Prefer bundled virtualenv under the mod, then env override, then system python.
    /// </summary>
    private static string ResolvePythonExecutablePath()
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_pythonExecutablePath))
            {
                return _pythonExecutablePath;
            }
        }

        var candidates = new List<string>();

        // Environment override
        var envPython = Environment.GetEnvironmentVariable("RIMTALK_TTS_PYTHON");
        if (!string.IsNullOrWhiteSpace(envPython))
        {
            candidates.Add(envPython.Trim());
        }

        // Bundled python environment alongside the mod (e.g., Mods/RimTalkTTS/python_env/python.exe)
        try
        {
            if (!string.IsNullOrEmpty(PythonScriptPath))
            {
                string scriptDir = Path.GetDirectoryName(PythonScriptPath);
                string modRoot = Directory.GetParent(scriptDir)?.Parent?.FullName; // Service -> Source -> ModRoot

                if (!string.IsNullOrEmpty(modRoot))
                {
                    candidates.Add(Path.Combine(modRoot, "python_env", "python.exe"));
                    candidates.Add(Path.Combine(modRoot, "python_env", "Scripts", "python.exe"));
                    candidates.Add(Path.Combine(modRoot, "python", "python.exe"));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"FishAudio TTS: Failed to probe bundled python path - {ex.Message}");
        }

        // Fallback to system python on PATH
        candidates.Add("python");
        candidates.Add("python.exe");

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            // If candidate is a full path, ensure it exists
            if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains("/"))
            {
                if (File.Exists(candidate))
                {
                    lock (_lock)
                    {
                        _pythonExecutablePath = candidate;
                    }
                    Log.Message($"FishAudio TTS: Using bundled Python at '{candidate}'");
                    return candidate;
                }
            }
            else
            {
                // Assume it's available via PATH
                lock (_lock)
                {
                    _pythonExecutablePath = candidate;
                }
                Log.Message($"FishAudio TTS: Using system Python executable '{candidate}'");
                return candidate;
            }
        }

        Log.Error("FishAudio TTS: No valid Python executable found. Set RIMTALK_TTS_PYTHON to a valid path or place a python_env next to the mod.");
        return "";
    }
    
    /// <summary>
    /// Start the Python TTS server if not already running
    /// </summary>
    private static async Task<bool> EnsureServerRunningAsync()
    {
        // Check if server is already running
        lock (_lock)
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                return true;
            }
        }
        
        // Check if another thread is starting the server
        bool shouldWait = false;
        lock (_lock)
        {
            if (_serverStarting)
            {
                shouldWait = true;
            }
            else
            {
                _serverStarting = true;
            }
        }
        
        // If another thread is starting, wait for it to complete
        if (shouldWait)
        {
            int waitCount = 0;
            while (waitCount < 100) // Wait up to 10 seconds
            {
                await Task.Delay(100);
                waitCount++;
                
                lock (_lock)
                {
                    // Check if server is now running
                    if (_serverProcess != null && !_serverProcess.HasExited)
                    {
                        return true;
                    }
                    
                    // Check if startup failed (flag cleared but no process)
                    if (!_serverStarting)
                    {
                        Log.Warning("FishAudio TTS: Server startup failed while waiting");
                        return false;
                    }
                }
            }
            
            Log.Warning("FishAudio TTS: Server startup timeout while waiting");
            return false;
        }
        
        try
        {
            // Validate Python script path
            if (string.IsNullOrEmpty(PythonScriptPath))
            {
                Log.Error("FishAudio TTS: Python script path is not initialized");
                return false;
            }
            
            if (!File.Exists(PythonScriptPath))
            {
                Log.Error($"FishAudio TTS: Python script not found at: {PythonScriptPath}");
                return false;
            }

            // Resolve Python executable (bundled env or system python)
            string pythonExe = ResolvePythonExecutablePath();
            if (string.IsNullOrEmpty(pythonExe))
            {
                return false;
            }
            
            Log.Message("FishAudio TTS: Starting Python server...");
            
            // Get current process ID to pass to Python server
            int currentProcessId = Process.GetCurrentProcess().Id;
            
            var processInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{PythonScriptPath}\" {ServerPort} {currentProcessId}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = processInfo };
            
            bool started = false;
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log.Message($"FishAudio TTS Server: {e.Data}");
                    if (e.Data.Contains("\"status\": \"ready\""))
                    {
                        started = true;
                    }
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Python server logs HTTP requests to stderr - treat as debug, not error
                    if (e.Data.Contains("[TTS Server]") || e.Data.Contains("POST /") || e.Data.Contains("GET /"))
                    {
                        Log.Message($"FishAudio TTS Server: {e.Data}");
                    }
                    else
                    {
                        Log.Warning($"FishAudio TTS Server stderr: {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // Wait for server to be ready (max 5 seconds)
            int waitCount = 0;
            while (!started && waitCount < 50)
            {
                await Task.Delay(100);
                waitCount++;
            }
            
            if (!started)
            {
                Log.Error("FishAudio TTS: Server failed to start within timeout");
                process.Kill();
                return false;
            }
            
            lock (_lock)
            {
                _serverProcess = process;
                // Use InfiniteTimeSpan - timeout is controlled per-request via CancellationToken
                // This prevents shared HttpClient timeout from affecting concurrent requests
                _httpClient = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Failed to start server - {ex.Message}");
            return false;
        }
        finally
        {
            lock (_lock)
            {
                _serverStarting = false;
            }
        }
    }
    
    /// <summary>
    /// Generate speech from text using Fish Audio TTS API via Python SDK
    /// Supports concurrent requests through HTTP server
    /// </summary>
    /// <param name="request">TTSRequest containing all parameters</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request</param>
    public static async Task<byte[]> GenerateSpeechAsync(
        TTSRequest request,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrEmpty(request.Input) || string.IsNullOrEmpty(request.ApiKey))
        {
            Log.Warning("FishAudio TTS: Text or API key is empty");
            return null;
        }
        try
        {
            // Ensure server is running
            if (!await EnsureServerRunningAsync())
            {
                Log.Warning("FishAudio TTS: Server not running");
                return null;
            }
            
            // Build request object mapping from TTSRequest
            var requestData = new PythonTTSRequest
            {
                api_key = request.ApiKey,
                text = request.Input,
                reference_id = request.Voice,
                model = request.Model,
                latency = "normal",
                speed = request.Speed,
                normalize = false,
                temperature = request.Temperature,
                top_p = request.TopP
            };
            
            string jsonContent =Util.JsonUtil.SerializeToJson(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            // Check cancellation before sending request
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Debug($"FishAudio TTS: Sending request - {request.Input}");
            
            // Send HTTP request
            var response = await _httpClient.PostAsync(ServerUrl, content, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                // Parse error response to extract meaningful message
                string errorMessage = ExtractErrorMessage(responseText, response.StatusCode);
                Log.Error($"FishAudio TTS: {errorMessage}");
                return null;
            }
            
            // Parse response
            var result = Util.JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);
            
            if (result == null)
            {
                Log.Error($"FishAudio TTS: Failed to parse response: {responseText}");
                return null;
            }
            
            if (result.success && !string.IsNullOrEmpty(result.audio))
            {
                try
                {
                    byte[] audioData = Convert.FromBase64String(result.audio);
                    return audioData;
                }
                catch (FormatException ex)
                {
                    Log.Error($"FishAudio TTS: Invalid base64 audio data - {ex.Message}");
                    return null;
                }
            }
            else
            {
                string errorMsg = result.error ?? "Unknown error";
                if (!string.IsNullOrEmpty(result.traceback))
                {
                    Log.Error($"FishAudio TTS: Failed - {errorMsg}\nTraceback: {result.traceback}");
                }
                else
                {
                    Log.Error($"FishAudio TTS: Failed - {errorMsg}");
                }
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("FishAudio TTS: Request timed out (30 seconds)");
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            Log.Error($"FishAudio TTS: HTTP request failed - {ex.Message}");
            
            // Server might have crashed, reset for next request
            lock (_lock)
            {
                if (_serverProcess != null && _serverProcess.HasExited)
                {
                    _serverProcess = null;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Unexpected error - {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Extract user-friendly error message from server response
    /// </summary>
    private static string ExtractErrorMessage(string responseText, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var errorResponse = Util.JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);
            
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
            {
                string error = errorResponse.error;
                
                // Return error message with first line of traceback if available
                string errorMessage = error;
                if (!string.IsNullOrEmpty(errorResponse.traceback))
                {
                    var tracebackLines = errorResponse.traceback.Split('\n');
                    if (tracebackLines.Length > 0)
                    {
                        errorMessage = $"{error}\n{tracebackLines[tracebackLines.Length - 1]}";
                    }
                }
                
                return errorMessage;
            }
            
            return $"Server returned {statusCode}: {responseText}";
        }
        catch
        {
            return $"Server returned {statusCode}: {responseText}";
        }
    }
    
    /// <summary>
    /// Shutdown the Python TTS server gracefully
    /// </summary>
    public static void ShutdownServer()
    {
        lock (_lock)
        {
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                Log.Message("FishAudio TTS: Server already stopped");
                return;
            }
            
            try
            {
                Log.Message("FishAudio TTS: Sending shutdown command to server...");
                
                // Try to send shutdown command via HTTP
                var shutdownRequest = new
                {
                    command = "shutdown"
                };
                
                string jsonContent = Util.JsonUtil.SerializeToJson(shutdownRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Use short timeout for shutdown command
                using (var timeoutClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
                {
                    var task = timeoutClient.PostAsync(ServerUrl, content);
                    task.Wait(TimeSpan.FromSeconds(2));
                    
                    if (task.IsCompleted && task.Result.IsSuccessStatusCode)
                    {
                        Log.Message("FishAudio TTS: Server shutdown command sent successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"FishAudio TTS: Failed to send shutdown command - {ex.Message}");
            }
            
            // Wait a bit for graceful shutdown
            try
            {
                if (!_serverProcess.WaitForExit(3000))
                {
                    Log.Warning("FishAudio TTS: Server did not exit gracefully, forcing termination");
                    _serverProcess.Kill();
                }
                else
                {
                    Log.Message("FishAudio TTS: Server exited gracefully");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"FishAudio TTS: Error during server shutdown - {ex.Message}");
            }
            finally
            {
                _serverProcess = null;
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }
    }

    [DataContract]
    private class PythonTTSRequest
    {
        [DataMember(Name = "api_key")]
        public string api_key { get; set; }
        
        [DataMember(Name = "text")]
        public string text { get; set; }
        
        [DataMember(Name = "reference_id")]
        public string reference_id { get; set; }
        
        [DataMember(Name = "model")]
        public string model { get; set; }
        
        [DataMember(Name = "latency")]
        public string latency { get; set; }

        [DataMember(Name = "speed")]
        public float speed { get; set; }
        
        [DataMember(Name = "normalize")]
        public bool normalize { get; set; }
        
        [DataMember(Name = "temperature")]
        public float temperature { get; set; }
        
        [DataMember(Name = "top_p")]
        public float top_p { get; set; }
    }
    
    [DataContract]
    private class PythonTTSResponse
    {
        [DataMember(Name = "success")]
        public bool success { get; set; }
        
        [DataMember(Name = "audio")]
        public string audio { get; set; }
        
        [DataMember(Name = "size")]
        public int size { get; set; }
        
        [DataMember(Name = "error")]
        public string error { get; set; }
        
        [DataMember(Name = "traceback")]
        public string traceback { get; set; }
    }
}
