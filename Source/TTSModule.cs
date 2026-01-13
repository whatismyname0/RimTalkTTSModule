using System;
using RimTalk.TTS.Data;
using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Main implementation of TTS module with lifecycle management
    /// </summary>
    public class TTSModule : ITTSModule
    {
        private static TTSModule _instance;
        private TTSSettings _settings;
        
        public static TTSModule Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TTSModule();
                return _instance;
            }
        }

        private TTSModule()
        {
            // Initialize with default settings to prevent null reference
            // Will be replaced with actual mod settings when Initialize() is called
            _settings = new TTSSettings();
        }
        
        public TTSSettings GetSettings()
        {
            return _settings;
        }

        public void Initialize()
        {
            // Load TTS settings
            var modInstance = LoadedModManager.GetMod(typeof(TTSMod)) as TTSMod;
            if (modInstance != null)
            {
                _settings = modInstance.GetSettings<TTSSettings>();
            }
            else
            {
                _settings = new TTSSettings();
            }

            // Apply configured provider implementation
            try
            {
                Service.TTSService.SetProvider(_settings.Supplier, _settings);
            }
            catch { }

            // Output TTS API configuration
            Log.Message("[RimTalk.TTS] ========== TTS API Configuration ==========");
            Log.Message($"[RimTalk.TTS] Provider: {_settings.ApiProvider}");
            Log.Message($"[RimTalk.TTS] Model: {(_settings.Model ?? "(not set)")}");
            
            string baseUrl = _settings.ApiProvider == Data.TTSApiProvider.Custom 
                ? (_settings.CustomBaseUrl ?? "(not set)")
                : (_settings.ApiProvider == Data.TTSApiProvider.DeepSeek 
                    ? "https://api.deepseek.com" 
                    : "https://api.openai.com");
            Log.Message($"[RimTalk.TTS] BaseUrl: {baseUrl}");
            
            string apiKeyDisplay = string.IsNullOrEmpty(_settings.ApiKey) 
                ? "(not set)" 
                : $"{_settings.ApiKey.Substring(0, Math.Min(10, _settings.ApiKey.Length))}***";
            Log.Message($"[RimTalk.TTS] ApiKey: {apiKeyDisplay}");
            Log.Message("[RimTalk.TTS] ==========================================");

            Log.Message("[RimTalk.TTS] TTS Module initialized");
        }

        public void OnDialogueGenerated(string text, Pawn pawn, Guid dialogueId)
        {
            if (!IsActive) return;
            if (string.IsNullOrEmpty(text)) return;
            if (pawn == null) return;

            // Start TTS generation asynchronously
            Service.TTSService.ProcessDialogue(text, pawn, dialogueId, _settings);
        }

        public void OnDialogueCancelled(Guid dialogueId)
        {
            if (!IsActive) return;
            if (dialogueId == Guid.Empty) return;

            Service.TTSService.CancelDialogue(dialogueId);
        }

        public void OnGameLoaded()
        {
            if (!IsActive) return;
            
            Log.Message("[RimTalk.TTS] Game loaded, resetting TTS state");
            Service.TTSService.StopAll(permanentShutdown: false);
        }

        public void OnGameExit()
        {
            Log.Message("[RimTalk.TTS] Game exiting, full shutdown");
            
            Service.TTSService.StopAll(permanentShutdown: true);
            Service.AudioPlaybackService.FullReset();
        }

        public bool IsActive => _settings?.EnableTTS ?? false;

        public TTSSettings Settings => _settings;
    }
}