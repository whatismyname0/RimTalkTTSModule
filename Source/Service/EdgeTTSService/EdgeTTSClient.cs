using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.TTS.Service.EdgeTTSService
{
    /// <summary>
    /// Client for Edge-TTS (Microsoft Edge's free TTS service)
    /// No API key required - uses the same voices as Azure TTS but through Edge browser endpoint
    /// Uses edge-tts Python package via subprocess
    /// </summary>
    public static class EdgeTTSClient
    {
        /// <summary>
        /// Generate speech using Edge-TTS via Python edge-tts package (free, no API key)
        /// </summary>
        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));

                string voiceName = request.Voice;
                if (string.IsNullOrWhiteSpace(voiceName))
                {
                    voiceName = "en-US-JennyNeural";
                }

                // Calculate rate parameter (edge-tts format: +50% or -50%)
                int ratePercent = (int)((request.Speed - 1.0f) * 100);
                string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

                // Create temp file for output
                string tempFile = Path.GetTempFileName();
                string outputFile = Path.ChangeExtension(tempFile, ".mp3");
                
                try
                {
                    // Get Python script path - go up from Assemblies directory to mod root, then to Source
                    string assemblyDir = Path.GetDirectoryName(typeof(EdgeTTSClient).Assembly.Location);
                    string modRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    string scriptPath = Path.Combine(modRoot, "Source", "Service", "EdgeTTSService", "edge_tts_client.py");

                    if (!File.Exists(scriptPath))
                    {
                        Log.Error($"[RimTalk.TTS] EdgeTTSClient: Python script not found at {scriptPath}");
                        return null;
                    }

                    // Run Python script
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" \"{request.Input}\" \"{voiceName}\" --rate \"{rateStr}\" --volume \"+0%\" --pitch \"+0Hz\" --output \"{outputFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        Log.Error("[RimTalk.TTS] EdgeTTSClient: Failed to start Python process");
                        return null;
                    }

                    // Wait for completion
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    await Task.WhenAll(outputTask, errorTask);
                    
                    // Wait for process to exit
                    await Task.Run(() => process.WaitForExit(), cancellationToken);

                    string output = await outputTask;
                    string error = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        Log.Error($"[RimTalk.TTS] EdgeTTSClient: Python process failed with code {process.ExitCode}");
                        Log.Error($"[RimTalk.TTS] EdgeTTSClient error: {error}");
                        return null;
                    }

                    if (!output.Contains("SUCCESS"))
                    {
                        Log.Error($"[RimTalk.TTS] EdgeTTSClient: Unexpected output: {output}");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Log.Error($"[RimTalk.TTS] EdgeTTSClient error: {error}");
                        }
                        return null;
                    }

                    // Read generated audio file
                    if (!File.Exists(outputFile))
                    {
                        Log.Error($"[RimTalk.TTS] EdgeTTSClient: Output file not found: {outputFile}");
                        return null;
                    }

                    byte[] audioData = await File.ReadAllBytesAsync(outputFile, cancellationToken);
                    return audioData;
                }
                finally
                {
                    // Clean up temp files
                    try
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                        if (File.Exists(outputFile)) File.Delete(outputFile);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.TTS] EdgeTTSClient.GenerateSpeechAsync exception: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log.Error($"[RimTalk.TTS] EdgeTTSClient inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }
    }
}
