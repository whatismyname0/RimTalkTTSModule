using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimTalk.TTS.Patch;
using UnityEngine;
using Verse;

namespace RimTalk.TTS.Service;

/// <summary>
/// Service for playing audio using Unity's audio system
/// </summary>
[StaticConstructorOnStartup]
public static class AudioPlaybackService
{
    private static readonly GameObject _audioPlayerObject;
    private static readonly AudioSource _audioSource;
    
    // === Audio State Tracking ===
    // Single source of truth: dialogue -> audio bytes (null means generation failed)
    private static readonly Dictionary<Guid, byte[]> _dialogueAudio = new Dictionary<Guid, byte[]>();
    
    // === Playback Control ===
    private static bool _isPlaying = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Static constructor - initializes Unity AudioSource on game startup (main thread)
    /// </summary>
    static AudioPlaybackService()
    {
        _audioPlayerObject = new GameObject("RimTalkAudioPlayer");
        UnityEngine.Object.DontDestroyOnLoad(_audioPlayerObject);
        _audioSource = _audioPlayerObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D sound
        // _audioSource.minDistance = 100f;
        // _audioSource.maxDistance = 10f;
        // _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.dopplerLevel = 0f;
    }
    
    /// <summary>
    /// Initialize the audio playback service (no-op now, kept for compatibility)
    /// </summary>
    public static void Initialize()
    {
        // Initialization now happens in static constructor
    }

    /// <summary>
    /// Record audio generation result for a dialogue (null = generation failed).
    /// Playback is triggered by TalkService.DisplayTalk().
    /// </summary>
    public static void SetAudioResult(Guid dialogueId, byte[] wavData)
    {
        if (dialogueId == Guid.Empty) return;

        Initialize();
        lock (_lock)
        {
            _dialogueAudio[dialogueId] = wavData; // wavData may be null on failure

            var len = wavData?.Length ?? 0;
        }
    }

    /// <summary>
    /// Check if audio is currently playing
    /// </summary>
    public static bool IsCurrentlyPlaying()
    {
        lock (_lock)
        {
            return _isPlaying;
        }
    }

    /// <summary>
    /// Play audio for a dialogue. Waits for previous playback and TTS generation.
    /// </summary>
    public static async void PlayAudio(Guid dialogueId, Pawn pawn, float volume = 1.0f)
    {
        if (dialogueId == Guid.Empty) return;

        try
        {
            // Step 1: Wait for any existing playback to finish (infinite wait)
            int playbackWaitCycles = 0;
            while (IsCurrentlyPlaying())
            {
                await Task.Delay(1000);
                playbackWaitCycles++;
            }

            // Step 2: Set playing flag to true
            lock (_lock)
            {
                _isPlaying = true;
            }

            // Step 3: Wait for TTS generation to complete (max 30 seconds)
            int ttsWaitCycles = 0;
            const int maxTtsWaitCycles = 30; // 300 * 100ms = 30 seconds
            
            while (RimTalkPatches.IsBlocked(dialogueId) && ttsWaitCycles < maxTtsWaitCycles)
            {
                await Task.Delay(1000);
                ttsWaitCycles++;
            }

            if (ttsWaitCycles >= maxTtsWaitCycles)
            {
                TTSLog.Warning($"[RimTalk.TTS] Timeout waiting for TTS (30s), skipping audio for dialogue {dialogueId}");
                lock (_lock)
                {
                    _isPlaying = false;
                    _dialogueAudio.Remove(dialogueId);
                }
                return;
            }

            // Small delay to ensure audio is fully stored
            await Task.Delay(100);

            // Step 4: Check if audio is valid
            byte[] wavData;
            lock (_lock)
            {
                if (!_dialogueAudio.TryGetValue(dialogueId, out wavData))
                {
                    TTSLog.Message($"[RimTalk.TTS] No audio found for dialogue {dialogueId}, skipping playback");
                    _isPlaying = false;
                    return;
                }

                if (wavData == null || wavData.Length == 0)
                {
                    _dialogueAudio.Remove(dialogueId);
                    TTSLog.Message($"[RimTalk.TTS] Audio is null or empty for dialogue {dialogueId}, skipping playback");
                    _isPlaying = false;
                    return;
                }
            }

            // Step 5: Play audio and wait for completion
            try
            {
                AudioClip clip = await LoadAudioClipFromData(wavData, dialogueId.ToString());
                if (clip != null && clip.length > 0)
                {
                    _audioSource.clip = clip;
                    _audioSource.volume = UnityEngine.Mathf.Clamp01(volume);
                    // var follower = _audioPlayerObject.GetComponent<FollowPawnBehaviour>() ?? _audioPlayerObject.AddComponent<FollowPawnBehaviour>();
                    // follower.pawn = pawn;
                    // follower.verticalOffset = 0.5f;
                    TTSLog.Message($"[RimTalk.TTS] Playback start [{dialogueId.ToString().Substring(0, 8)}]: {clip.length:F1}s, {wavData.Length} bytes");
                    _audioSource.Play();
                    // follower.pawn = null;

                    // Wait for playback to complete based on clip length
                    int playbackDelayMs = (int)(clip.length * 1000f);
                    await Task.Delay(playbackDelayMs);
                }
                else
                {
                    TTSLog.Error("[RimTalk.TTS] Failed to create audio clip from audio data or clip length is 0");
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService.PlayAudio - playback exception: {ex}");
            }
        }
        catch (Exception ex)
        {
            TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService.PlayAudio - outer exception: {ex}");
        }
        finally
        {
            // Step 6: Release playing flag and cleanup audio data
            lock (_lock)
            {
                _isPlaying = false;
                _dialogueAudio.Remove(dialogueId);
            }
        }
    }

    /// <summary>
    /// Complete reset of audio system - ONLY call when exiting/loading save game.
    /// Clears all state and resets sequences to allow fresh start.
    /// </summary>
    public static void FullReset()
    {
        lock (_lock)
        {
            // Clear all state
            _dialogueAudio.Clear();
            
            // Reset all counters and flags
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Load AudioClip from audio data (WAV or MP3).
    /// Any other format is rejected with a clear error log instead of failing obscurely in the MP3 decoder.
    /// </summary>
    private static async Task<AudioClip> LoadAudioClipFromData(byte[] audioData, string dialogueId)
    {
        try
        {
            if (audioData == null || audioData.Length == 0)
            {
                TTSLog.Error("[RimTalk.TTS] AudioPlaybackService: Received empty audio data");
                return null;
            }

            // WAV (RIFF...WAVE)
            bool isWav = audioData.Length > 12 &&
                         audioData[0] == (byte)'R' && audioData[1] == (byte)'I' &&
                         audioData[2] == (byte)'F' && audioData[3] == (byte)'F' &&
                         audioData[8] == (byte)'W' && audioData[9] == (byte)'A' &&
                         audioData[10] == (byte)'V' && audioData[11] == (byte)'E';

            if (isWav)
            {
                // Use existing WAV parser
                return LoadAudioClipFromWav(audioData);
            }

            // MP3: ID3 tag or MPEG frame sync
            if (LooksLikeMp3(audioData))
            {
                // Uses temporary file approach since Unity can't load MP3 from byte array directly
                return await LoadAudioClipFromMP3(audioData, dialogueId);
            }

            // Unsupported format - log a clear error with the detected format so the user can fix the provider config
            string detected = DetectAudioFormat(audioData);
            string preview = BitConverter.ToString(audioData, 0, Math.Min(audioData.Length, 8));
            TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService: Unsupported audio format detected ({detected}). Only WAV (8/16/24/32-bit) and MP3 are supported. Bytes: {preview}");
            return null;
        }
        catch (Exception ex)
        {
            TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService.LoadAudioClipFromData exception: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static bool LooksLikeMp3(byte[] data)
    {
        if (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
            return true;

        // MPEG audio frame sync: 11 set bits (0xFF Ex)
        return data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0;
    }

    private static string DetectAudioFormat(byte[] data)
    {
        if (data.Length >= 4 && data[0] == (byte)'O' && data[1] == (byte)'g' && data[2] == (byte)'g' && data[3] == (byte)'S')
            return "Ogg/Opus";
        if (data.Length >= 4 && data[0] == (byte)'f' && data[1] == (byte)'L' && data[2] == (byte)'a' && data[3] == (byte)'C')
            return "FLAC";
        // AAC ADTS sync (0xFF F1 / 0xFF F9) - checked before MP3 because both start with 0xFF
        if (data.Length >= 2 && data[0] == 0xFF && (data[1] == 0xF1 || data[1] == 0xF9))
            return "AAC (ADTS)";
        if (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
            return "MP3 (ID3)";
        if (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
            return "MP3/MPEG";
        if (data.Length >= 8 && data[4] == (byte)'f' && data[5] == (byte)'t' && data[6] == (byte)'y' && data[7] == (byte)'p')
            return "MP4/AAC (M4A)";
        if (data.Length >= 4 && data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3)
            return "WebM/Matroska";
        if (data.Length >= 4 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F')
            return "RIFF (non-WAVE)";
        return "Unknown or raw PCM";
    }

    /// <summary>
    /// Load AudioClip from MP3 byte array using temporary file
    /// </summary>
    private static async Task<AudioClip> LoadAudioClipFromMP3(byte[] mp3Data, string dialogueId)
    {
        string tempFile = null;
        try
        {
            // Write to temp file
            tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rimtalk_audio_{dialogueId}.mp3");
            await System.IO.File.WriteAllBytesAsync(tempFile, mp3Data);

            // Load using UnityWebRequestMultimedia on main thread
            AudioClip clip = null;
            await Task.Run(async () =>
            {
                using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file:///" + tempFile, UnityEngine.AudioType.MPEG))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(10);
                    }

                    if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    }
                    else
                    {
                        TTSLog.Error($"[RimTalk.TTS] Failed to load MP3: {www.error}");
                    }
                }
            });

            return clip;
        }
        catch (Exception ex)
        {
            TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService.LoadAudioClipFromMP3 exception: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (tempFile != null && System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// Load AudioClip from WAV byte array
    /// </summary>
    private static AudioClip LoadAudioClipFromWav(byte[] wavData)
    {
        try
        {
            // Basic header/logging to help debug malformed WAVs
            int totalLen = wavData?.Length ?? 0;

            // Parse WAV header (best-effort; some files may have extra chunks before fmt/data)
            int channels = -1;
            int sampleRate = -1;
            int bitsPerSample = -1;
            int audioFormat = 1; // 1=PCM, 3=IEEE float (resolved from fmt chunk / extensible SubFormat)

            try
            {
                if (wavData.Length >= 36)
                {
                    audioFormat = BitConverter.ToInt16(wavData, 20);
                    channels = BitConverter.ToInt16(wavData, 22);
                    sampleRate = BitConverter.ToInt32(wavData, 24);
                    bitsPerSample = BitConverter.ToInt16(wavData, 34);
                }
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] AudioPlaybackService: Failed to read basic header fields: {ex.GetType().Name}: {ex.Message}");
            }

            // Find data chunk safely
            int dataPos = 12; // start after RIFF header
            while (dataPos + 8 <= wavData.Length)
            {
                // read chunk id and size safely
                string chunkId;
                try
                {
                    chunkId = System.Text.Encoding.ASCII.GetString(wavData, dataPos, 4);
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService: Failed to read chunk id at pos {dataPos}: {ex.GetType().Name}: {ex.Message}");
                    return null;
                }

                int chunkSize = 0;
                try
                {
                    if (dataPos + 8 <= wavData.Length)
                        chunkSize = BitConverter.ToInt32(wavData, dataPos + 4);
                }
                catch (Exception ex)
                {
                    TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService: Failed to read chunk size for '{chunkId}' at pos {dataPos}: {ex.GetType().Name}: {ex.Message}");
                    return null;
                }

                if (chunkId == "fmt ")
                {
                    // attempt to parse fmt chunk for reliable format info
                    try
                    {
                        int fmtPos = dataPos + 8;
                        if (fmtPos + 16 <= wavData.Length)
                        {
                            // audio format (2 bytes), channels (2), sampleRate (4), byteRate (4), blockAlign (2), bitsPerSample (2)
                            audioFormat = BitConverter.ToInt16(wavData, fmtPos);
                            channels = BitConverter.ToInt16(wavData, fmtPos + 2);
                            sampleRate = BitConverter.ToInt32(wavData, fmtPos + 4);
                            bitsPerSample = BitConverter.ToInt16(wavData, fmtPos + 14);

                            // WAVE_FORMAT_EXTENSIBLE (0xFFFE): real format is the first 2 bytes of the SubFormat GUID
                            if (audioFormat == 0xFFFE && fmtPos + 26 <= wavData.Length)
                            {
                                audioFormat = BitConverter.ToInt16(wavData, fmtPos + 24);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TTSLog.Warning($"[RimTalk.TTS] AudioPlaybackService: Failed to parse fmt chunk: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                if (chunkId == "data")
                {
                    dataPos += 8;
                    break;
                }

                // advance to next chunk with bounds check
                long nextPos = (long)dataPos + 8 + chunkSize;
                if (nextPos <= dataPos || nextPos > wavData.Length)
                {
                    TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService: Invalid chunk size leading to overflow: chunkId='{chunkId}', chunkSize={chunkSize}, pos={dataPos}, len={wavData.Length}");
                    return null;
                }
                dataPos = (int)nextPos;
            }

            if (dataPos >= wavData.Length)
            {
                TTSLog.Error("AudioPlaybackService: Could not find data chunk in WAV file");
                return null;
            }

            // Convert byte data to float array
            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0)
            {
                TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService: Invalid bitsPerSample={bitsPerSample}");
                return null;
            }

            int sampleCount = (wavData.Length - dataPos) / bytesPerSample;
            float[] audioData = new float[sampleCount];

            if (bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = BitConverter.ToInt16(wavData, dataPos + i * 2);
                    audioData[i] = sample / 32768f;
                }
            }
            else if (bitsPerSample == 8)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    audioData[i] = (wavData[dataPos + i] - 128) / 128f;
                }
            }
            else if (bitsPerSample == 24)
            {
                // 24-bit PCM: 3 bytes little-endian, sign-extended
                for (int i = 0; i < sampleCount; i++)
                {
                    int offset = dataPos + i * 3;
                    int sample = wavData[offset] | (wavData[offset + 1] << 8) | (wavData[offset + 2] << 16);
                    if ((sample & 0x800000) != 0)
                        sample |= unchecked((int)0xFF000000);
                    audioData[i] = sample / 8388608f;
                }
            }
            else if (bitsPerSample == 32 && audioFormat == 3)
            {
                // 32-bit IEEE float: values are already normalized, clamp defensively for Unity
                for (int i = 0; i < sampleCount; i++)
                {
                    audioData[i] = Mathf.Clamp(BitConverter.ToSingle(wavData, dataPos + i * 4), -1f, 1f);
                }
            }
            else if (bitsPerSample == 32)
            {
                // 32-bit PCM int (audioFormat == 1 or unknown)
                for (int i = 0; i < sampleCount; i++)
                {
                    int sample = BitConverter.ToInt32(wavData, dataPos + i * 4);
                    audioData[i] = sample / 2147483648f;
                }
            }
            else
            {
                TTSLog.Warning($"[RimTalk.TTS] AudioPlaybackService: Unsupported bitsPerSample={bitsPerSample} (audioFormat={audioFormat}), playback will be silent");
            }

            // Create AudioClip
            AudioClip clip = AudioClip.Create("RimTalkTTS", sampleCount / channels, channels, sampleRate, false);
            clip.SetData(audioData, 0);

            return clip;
        }
        catch (Exception ex)
        {
            TTSLog.Error($"[RimTalk.TTS] AudioPlaybackService.LoadAudioClipFromWav exception: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Stop playback and clear all state. Called on game exit, save load, or map change.
    /// Marks all pending dialogues as spoken to prevent them from blocking future dialogues.
    /// Preserves sequence counters to maintain ordering across cleanup events.
    /// </summary>
    public static void StopAndClear()
    {
        // Stop audio playback immediately
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
        }
        
        lock (_lock)
        {
            
            // Clear all state dictionaries and collections
            // Do NOT mark as AudioSpoken - let dialogues display normally without audio
            _dialogueAudio.Clear();
            
            // Reset playback state
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Set playback volume (0.0 to 1.0)
    /// </summary>
    public static void SetVolume(float volume)
    {
        Initialize();
        _audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Remove a dialogue's audio data from pending queue (thread-safe).
    /// Used for cleanup when dialogue is cancelled or ignored.
    /// </summary>
    public static void RemovePendingAudio(Guid dialogueId)
    {
        if (dialogueId == Guid.Empty) return;

        lock (_lock)
        {
            _dialogueAudio.Remove(dialogueId);
        }
    }

    public class FollowPawnBehaviour : MonoBehaviour
    {
        public Pawn pawn;
        public float verticalOffset = 0.0f;

        void Update()
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            // 使用 DrawPos 以获得平滑的位置（会随 pawn 移动）
            transform.position = pawn.DrawPos + Vector3.up * verticalOffset;
        }
    }
}
