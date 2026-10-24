using System;
using RimTalk.TTS.Config;
using RimTalk.TTS.Data;
using RimTalk.TTS.Pipeline;
using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Composition root: owns the settings object, the dialogue pipeline and the lifecycle
    /// callbacks. Created once by <see cref="Bootstrap.TtsBootstrap"/>; configuration is read on the
    /// main thread and handed to background work as a <see cref="SettingsSnapshot"/>.
    /// </summary>
    public sealed class TtsRuntime
    {
        private static TtsRuntime _current;

        public static TtsRuntime Current => _current;
        public static bool IsReady => _current != null;

        private readonly TTSSettings _settings;

        private TtsRuntime(TTSSettings settings)
        {
            _settings = settings ?? new TTSSettings();
            Pipeline = new DialoguePipeline();
        }

        public static TtsRuntime Create(TTSSettings settings)
        {
            if (_current != null) return _current;

            var runtime = new TtsRuntime(settings);
            // Expose before Initialize so a failing provider never leaves patches/UI with a null runtime.
            _current = runtime;
            try
            {
                runtime.Initialize();
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Runtime initialization failed: {ex}");
            }

            return runtime;
        }

        public TTSSettings Settings => _settings;

        /// <summary>Dialogue state machine + provider orchestration.</summary>
        public DialoguePipeline Pipeline { get; }

        /// <summary>TTS module enabled in the mod settings (provider may still be None).</summary>
        public bool IsEnabled => _settings.EnableTTS;

        /// <summary>TTS enabled and not switched off with the in-game button.</summary>
        public bool IsPlaybackOn => _settings.EnableTTS && _settings.isOnButton;

        /// <summary>The overlay control buttons should be drawn.</summary>
        public bool ShowControlButtons => _settings.EnableTTS && _settings.ControlButtonDisplay;

        private void Initialize()
        {
            _settings.EnsureCustomProviderDictionaries();

            // Output the configured endpoints once at startup (keys are truncated).
            TTSLog.Message("[RimTalk.TTS] ========== TTS API Configuration ==========");
            TTSLog.Message($"[RimTalk.TTS] Provider: {_settings.ApiProvider}");
            TTSLog.Message($"[RimTalk.TTS] Model: {_settings.Model ?? "(not set)"}");

            string baseUrl = _settings.ApiProvider == TTSApiProvider.Custom
                ? (_settings.CustomBaseUrl ?? "(not set)")
                : (_settings.ApiProvider == TTSApiProvider.DeepSeek
                    ? "https://api.deepseek.com"
                    : "https://api.openai.com");
            TTSLog.Message($"[RimTalk.TTS] BaseUrl: {baseUrl}");

            string apiKeyDisplay = string.IsNullOrEmpty(_settings.ApiKey)
                ? "(not set)"
                : $"{_settings.ApiKey.Substring(0, Math.Min(10, _settings.ApiKey.Length))}***";
            TTSLog.Message($"[RimTalk.TTS] ApiKey: {apiKeyDisplay}");
            TTSLog.Message("[RimTalk.TTS] ==========================================");

            Pipeline.SetProvider(_settings.Supplier, _settings);
            TTSLog.Message("[RimTalk.TTS] TTS Module initialized");
        }

        // =================================================================================
        // lifecycle callbacks
        // =================================================================================

        public void OnDialogueGenerated(string text, Pawn pawn, Guid dialogueId)
        {
            Pipeline.Request(text, dialogueId, pawn);
        }

        public void OnDialogueCancelled(Guid dialogueId)
        {
            Pipeline.Cancel(dialogueId);
        }

        public void OnGameLoaded()
        {
            TTSLog.Message("[RimTalk.TTS] Game loaded, resetting TTS state");
            Pipeline.ResetAll(permanentShutdown: false);
        }

        public void OnGameExit()
        {
            TTSLog.Message("[RimTalk.TTS] Game exiting, full shutdown");
            Pipeline.ResetAll(permanentShutdown: true);
        }
    }
}
