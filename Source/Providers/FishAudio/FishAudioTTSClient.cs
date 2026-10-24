using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Collections.Generic;
using RimTalk.Util;

namespace RimTalk.TTS.Providers;

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
            
            TTSLog.Error("FishAudio TTS: Could not locate fish_audio_tts.py");
            return "";
        }
        catch (Exception ex)
        {
            TTSLog.Error($"FishAudio TTS: Failed to get Python script path - {ex.Message}");
            return "";
        }
    }
    
    private static System.Diagnostics.Process _serverProcess;
    private static HttpClient _httpClient;
    private static readonly object _lock = new object();
    private static bool _serverStarting = false;

    /// <summary>Loopback endpoint of the running helper (null until the handshake completes).</summary>
    private static string _serverUrl;

    /// <summary>Shared secret required by the helper.</summary>
    private static string _authToken;

    private static string CreateAuthToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Parse the single ready line printed by the Python server on stdout.</summary>
    private static bool TryParseReadyLine(string line, out int port)
    {
        port = 0;
        if (string.IsNullOrEmpty(line)) return false;

        string trimmed = line.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{') return false;
        if (trimmed.IndexOf("\"status\"", StringComparison.Ordinal) < 0) return false;
        if (trimmed.IndexOf("\"ready\"", StringComparison.Ordinal) < 0) return false;

        int portKey = trimmed.IndexOf("\"port\"", StringComparison.Ordinal);
        if (portKey < 0) return false;
        int colon = trimmed.IndexOf(':', portKey);
        if (colon < 0) return false;

        int i = colon + 1;
        while (i < trimmed.Length && !char.IsDigit(trimmed[i])) i++;
        int start = i;
        while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
        if (i == start) return false;

        int value;
        if (!int.TryParse(trimmed.Substring(start, i - start), out value)) return false;
        if (value <= 0 || value > 65535) return false;

        port = value;
        return true;
    }

    /// <summary>Release the stdin pipe so the helper sees EOF and exits.</summary>
    private static void CloseStandardInput(System.Diagnostics.Process process)
    {
        try
        {
            if (process != null && process.StartInfo.RedirectStandardInput)
            {
                process.StandardInput.Close();
            }
        }
        catch { }
    }

    /// <summary>
    /// Clear the live endpoint state, but only if it still belongs to <paramref name="expectedProcess"/>.
    /// A late shutdown from a previous provider swap must not erase a freshly started server.
    /// </summary>
    private static void ClearServerState(System.Diagnostics.Process expectedProcess)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_serverProcess, expectedProcess))
            {
                return;
            }

            _serverProcess = null;
            _serverUrl = null;
            _authToken = null;
            _httpClient?.Dispose();
            _httpClient = null;
        }
    }

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
            TTSLog.Warning($"FishAudio TTS: Failed to probe bundled python path - {ex.Message}");
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
                    TTSLog.Message($"FishAudio TTS: Using bundled Python at '{candidate}'");
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
                TTSLog.Message($"FishAudio TTS: Using system Python executable '{candidate}'");
                return candidate;
            }
        }

        TTSLog.Error("FishAudio TTS: No valid Python executable found. Set RIMTALK_TTS_PYTHON to a valid path or place a python_env next to the mod.");
        return "";
    }
    
    /// <summary>
    /// Check if required Python dependencies are installed
    /// </summary>
    private static async Task<bool> CheckPythonDependenciesAsync(string pythonExe)
    {
        try
        {
            string checkScript = Path.Combine(Path.GetDirectoryName(PythonScriptPath), "check_dependencies.py");
            
            // If check script doesn't exist, skip the check (backward compatibility)
            if (!File.Exists(checkScript))
            {
                TTSLog.Warning("FishAudio TTS: Dependency check script not found, skipping validation");
                return true;
            }
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{checkScript}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            using (var process = new System.Diagnostics.Process { StartInfo = processInfo })
            {
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                
                bool exited = process.WaitForExit(5000); // 5 second timeout
                
                if (!exited)
                {
                    TTSLog.Warning("FishAudio TTS: Dependency check timed out");
                    process.Kill();
                    return true; // Don't block if check fails
                }
                
                if (process.ExitCode != 0)
                {
                    TTSLog.Error($"FishAudio TTS: Dependency check failed:\n{output}\n{error}");
                    return false;
                }
                
                TTSLog.Message($"FishAudio TTS: Dependencies verified:\n{output}");
                return true;
            }
        }
        catch (Exception ex)
        {
            TTSLog.Warning($"FishAudio TTS: Failed to check dependencies - {ex.Message}");
            return true; // Don't block on check failure
        }
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
                        TTSLog.Warning("FishAudio TTS: Server startup failed while waiting");
                        return false;
                    }
                }
            }
            
            TTSLog.Warning("FishAudio TTS: Server startup timeout while waiting");
            return false;
        }
        
        try
        {
            // Validate Python script path
            if (string.IsNullOrEmpty(PythonScriptPath))
            {
                TTSLog.Error("FishAudio TTS: Python script path is not initialized");
                return false;
            }
            
            if (!File.Exists(PythonScriptPath))
            {
                TTSLog.Error($"FishAudio TTS: Python script not found at: {PythonScriptPath}");
                return false;
            }

            // Resolve Python executable (bundled env or system python)
            string pythonExe = ResolvePythonExecutablePath();
            if (string.IsNullOrEmpty(pythonExe))
            {
                return false;
            }
            
            // Check Python dependencies before starting server
            if (!await CheckPythonDependenciesAsync(pythonExe))
            {
                TTSLog.Error("FishAudio TTS: Python dependencies check failed. Please install: pip install fish-audio-sdk");
                return false;
            }
            
            TTSLog.Message("FishAudio TTS: Starting Python server...");

            string expectedToken = CreateAuthToken();

            // Get current process ID to pass to Python server
            int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{PythonScriptPath}\" --token-stdin --parent-pid {currentProcessId}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new System.Diagnostics.Process { StartInfo = processInfo };
            
            object readyLock = new object();
            int readyPort = 0;
            bool started = false;
            bool hasFatalError = false;
            StringBuilder errorOutput = new StringBuilder();
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                TTSLog.Message($"FishAudio TTS Server: {e.Data}");

                int parsedPort;
                if (TryParseReadyLine(e.Data, out parsedPort))
                {
                    lock (readyLock)
                    {
                        readyPort = parsedPort;
                        started = true;
                    }
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Collect error output for diagnosis
                    errorOutput.AppendLine(e.Data);
                    
                    // Check for fatal errors that indicate missing dependencies
                    if (e.Data.Contains("ModuleNotFoundError") || e.Data.Contains("No module named"))
                    {
                        hasFatalError = true;
                        TTSLog.Error($"FishAudio TTS: Python dependency missing - {e.Data}");
                        TTSLog.Error("FishAudio TTS: Please install fishaudio package: pip install fish-audio-sdk");
                    }
                    else if (e.Data.Contains("ImportError"))
                    {
                        hasFatalError = true;
                        TTSLog.Error($"FishAudio TTS: Python import error - {e.Data}");
                    }
                    // Python server logs HTTP requests to stderr - treat as debug, not error
                    else if (e.Data.Contains("[TTS Server]") || e.Data.Contains("POST /") || e.Data.Contains("GET /"))
                    {
                        TTSLog.Message($"FishAudio TTS Server: {e.Data}");
                    }
                    else
                    {
                        TTSLog.Warning($"FishAudio TTS Server stderr: {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                // The token goes over stdin only: never argv/env, so it does not show up in
                // process listings. Python refuses to start if this line never arrives.
                byte[] handshake = Encoding.UTF8.GetBytes("{\"token\":\"" + expectedToken + "\"}\n");
                process.StandardInput.BaseStream.Write(handshake, 0, handshake.Length);
                process.StandardInput.BaseStream.Flush();

                // Keep stdin open for the whole session: Python watches the pipe and exits as
                // soon as it reaches EOF, which happens immediately when this process dies
                // abruptly (Alt+F4, crash, task manager) even if Application.quitting never fires.
            }
            catch (Exception handshakeEx)
            {
                TTSLog.Error($"FishAudio TTS: Failed to send startup handshake - {handshakeEx.Message}");
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch { }
                CloseStandardInput(process);
                return false;
            }
            
            // Wait for the ready line (max 15 seconds - increased for slow systems)
            int waitCount = 0;
            while (waitCount < 150)
            {
                lock (readyLock)
                {
                    if (started) break;
                }

                await Task.Delay(100);
                waitCount++;

                // Check if process crashed during startup
                if (process.HasExited)
                {
                    TTSLog.Error($"FishAudio TTS: Python process exited during startup with code {process.ExitCode}");
                    return false;
                }
            }

            int serverPort;
            lock (readyLock)
            {
                started = started && readyPort > 0;
                serverPort = readyPort;
            }
            
            if (!started || hasFatalError)
            {
                if (hasFatalError)
                {
                    TTSLog.Error("FishAudio TTS: Server startup failed due to fatal error (see above)");
                    TTSLog.Error("FishAudio TTS: Complete error output:");
                    TTSLog.Error(errorOutput.ToString());
                }
                else
                {
                    TTSLog.Error("FishAudio TTS: Server failed to start within 15 seconds timeout");
                    if (errorOutput.Length > 0)
                    {
                        TTSLog.Error("FishAudio TTS: Error output:");
                        TTSLog.Error(errorOutput.ToString());
                    }
                }
                
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception killEx)
                {
                    TTSLog.Warning($"FishAudio TTS: Failed to kill non-responsive process - {killEx.Message}");
                }
                CloseStandardInput(process);
                return false;
            }
            
            lock (_lock)
            {
                _serverProcess = process;
                _serverUrl = $"http://127.0.0.1:{serverPort}";
                _authToken = expectedToken;
                // Use InfiniteTimeSpan - timeout is controlled per-request via CancellationToken
                // Create HttpClient with cookies disabled to avoid Mono/Win32 cookie/container codepaths
                try
                {
                    var handler = new HttpClientHandler { UseCookies = false };
                    _httpClient = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                }
                catch (Exception ex)
                {
                    // Fallback to default HttpClient if handler creation fails for any reason
                    TTSLog.Warning($"FishAudio TTS: Failed to create cookie-less HttpClient handler - {ex.Message}. Falling back to default HttpClient.");
                    _httpClient = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                }
            }

            TTSLog.Message($"FishAudio TTS: Server ready on 127.0.0.1:{serverPort} (token auth)");
            return true;
        }
        catch (Exception ex)
        {
            TTSLog.Error($"FishAudio TTS: Failed to start server - {ex.Message}");
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
            TTSLog.Warning("FishAudio TTS: Text or API key is empty");
            return null;
        }
        try
        {
            // Ensure server is running, with retry on failure
            bool serverReady = await EnsureServerRunningAsync();
            if (!serverReady)
            {
                TTSLog.Warning("FishAudio TTS: Server failed to start on first attempt, retrying once...");
                
                // Reset server state and try once more
                lock (_lock)
                {
                    if (_serverProcess != null && !_serverProcess.HasExited)
                    {
                        try
                        {
                            _serverProcess.Kill();
                        }
                        catch { }
                    }
                    CloseStandardInput(_serverProcess);
                    _serverProcess = null;
                    _serverUrl = null;
                    _authToken = null;
                    _httpClient?.Dispose();
                    _httpClient = null;
                }
                
                // Wait a bit before retry
                await Task.Delay(1000);
                
                serverReady = await EnsureServerRunningAsync();
                if (!serverReady)
                {
                    TTSLog.Error("FishAudio TTS: Server failed to start after retry");
                    return null;
                }
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
            
            string jsonContent = JsonUtil.SerializeToJson(requestData);
            
            // Check cancellation before sending request
            cancellationToken.ThrowIfCancellationRequested();

            TTSLog.Message($"FishAudio TTS: Sending request - {request.Input}");
            
            // Snapshot the live endpoint: the server can be restarted (new port + token) while
            // a request is being prepared, and shutdown can null these fields concurrently.
            string serverUrl;
            string authToken;
            HttpClient client;
            lock (_lock)
            {
                serverUrl = _serverUrl;
                authToken = _authToken;
                client = _httpClient;
            }

            if (string.IsNullOrEmpty(serverUrl) || client == null)
            {
                TTSLog.Error("FishAudio TTS: Server endpoint is not available");
                return null;
            }

            string responseText;
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, serverUrl))
            {
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(authToken))
                {
                    httpRequest.Headers.TryAddWithoutValidation("X-TTS-Token", authToken);
                }

                using (var response = await client.SendAsync(httpRequest, cancellationToken))
                {
                    responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // Parse error response to extract meaningful message
                        string errorMessage = ExtractErrorMessage(responseText, response.StatusCode);
                        TTSLog.Error($"FishAudio TTS: {errorMessage}");
                        return null;
                    }
                }
            }
            
            // Parse response
            var result = JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);
            
            if (result == null)
            {
                TTSLog.Error($"FishAudio TTS: Failed to parse response: {Truncate(responseText, 200)}");
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
                    TTSLog.Error($"FishAudio TTS: Invalid base64 audio data - {ex.Message}");
                    return null;
                }
            }
            else
            {
                string errorMsg = result.error ?? "Unknown error";
                if (!string.IsNullOrEmpty(result.traceback))
                {
                    // Keep one useful line here; the full Python traceback only goes to the verbose log.
                    string cause = ExtractRootCause(result.traceback);
                    TTSLog.Error($"FishAudio TTS: Failed - {errorMsg} ({cause})");
                    TTSLog.Message($"FishAudio TTS: full traceback: {result.traceback}");
                }
                else
                {
                    TTSLog.Error($"FishAudio TTS: Failed - {errorMsg}");
                }
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                TTSLog.Warning("FishAudio TTS: Request cancelled or timed out");
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            TTSLog.Error($"FishAudio TTS: HTTP request failed - {ex.Message}");
            
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
            TTSLog.Error($"FishAudio TTS: Unexpected error - {ex.Message}");
            TTSLog.Message($"FishAudio TTS: {ex}");
            return null;
        }
    }

    /// <summary>Last meaningful line of a Python traceback (usually the exception class).</summary>
    private static string ExtractRootCause(string traceback)
    {
        if (string.IsNullOrEmpty(traceback)) return "unknown";
        var lines = traceback.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].Trim();
            if (line.Length > 0) return Truncate(line, 200);
        }
        return "unknown";
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }

    /// <summary>
    /// Extract user-friendly error message from server response
    /// </summary>
    private static string ExtractErrorMessage(string responseText, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var errorResponse = JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);

            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
            {
                // User-facing: the error text plus the exception class from the Python traceback,
                // never the whole traceback (that goes to the verbose log only).
                string cause = ExtractRootCause(errorResponse.traceback);
                return $"{errorResponse.error} ({cause})";
            }

            return $"Server returned {statusCode}: {Truncate(responseText, 200)}";
        }
        catch
        {
            return $"Server returned {statusCode}: {Truncate(responseText, 200)}";
        }
    }
    
    /// <summary>
    /// Shutdown the Python TTS server gracefully
    /// </summary>
    public static void ShutdownServer()
    {
        System.Diagnostics.Process process;
        string serverUrl;
        string authToken;
        lock (_lock)
        {
            process = _serverProcess;
            serverUrl = _serverUrl;
            authToken = _authToken;
        }

        if (process == null || process.HasExited)
        {
            TTSLog.Message("FishAudio TTS: Server already stopped");
            CloseStandardInput(process);
            ClearServerState(process);
            return;
        }

        try
        {
            TTSLog.Message("FishAudio TTS: Sending shutdown command to server...");

            if (!string.IsNullOrEmpty(serverUrl))
            {
                // Shutdown is a normal request: it carries the same auth token.
                var shutdownRequest = new Dictionary<string, string>
                {
                    { "command", "shutdown" }
                };

                string jsonContent = JsonUtil.SerializeToJson(shutdownRequest);

                // Use short timeout for shutdown command and disable cookies to avoid native cookie/container calls
                try
                {
                    var shortHandler = new HttpClientHandler { UseCookies = false };
                    using (var timeoutClient = new HttpClient(shortHandler) { Timeout = TimeSpan.FromSeconds(2) })
                    using (var request = new HttpRequestMessage(HttpMethod.Post, serverUrl))
                    {
                        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                        if (!string.IsNullOrEmpty(authToken))
                        {
                            request.Headers.TryAddWithoutValidation("X-TTS-Token", authToken);
                        }

                        var task = timeoutClient.SendAsync(request);
                        task.Wait(TimeSpan.FromSeconds(2));

                        if (task.IsCompleted && !task.IsFaulted && task.Result.IsSuccessStatusCode)
                        {
                            TTSLog.Message("FishAudio TTS: Server shutdown command sent successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TTSLog.Warning($"FishAudio TTS: Failed to send shutdown via cookie-less HttpClient - {ex.Message}");
                }
            }

            // Wait a bit for graceful shutdown
            try
            {
                if (!process.WaitForExit(3000))
                {
                    TTSLog.Warning("FishAudio TTS: Server did not exit gracefully, forcing termination");
                    process.Kill();
                }
                else
                {
                    TTSLog.Message("FishAudio TTS: Server exited gracefully");
                }
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"FishAudio TTS: Error during server shutdown - {ex.Message}");
            }
        }
        finally
        {
            CloseStandardInput(process);
            ClearServerState(process);
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
