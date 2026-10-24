using UnityEngine;
using Verse;
using RimTalk.TTS.Data;
using RimWorld;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Editor window for configuring a CustomProviderConfig.
    /// Supports OpenAI-compatible settings, custom request body templates,
    /// and advanced TTS features like emotion, temperature, etc.
    /// </summary>
    public class CustomProviderEditorWindow : Window
    {
        private readonly CustomProviderConfig _config;
        private readonly System.Action _onSave;
        private readonly bool _isNew;
        private Vector2 _scrollPos = Vector2.zero;

        // Buffers for text fields
        private string _nameBuffer;
        private string _baseUrlBuffer;
        private string _endpointPathBuffer;
        private string _apiKeyBuffer;
        private string _modelBuffer;
        private string _defaultVoiceBuffer;
        private string _responseFormatBuffer;
        private string _emotionFieldNameBuffer;
        private string _instructTextFieldNameBuffer;
        private string _customRequestBodyBuffer;
        private string _authHeaderNameBuffer;
        private string _authHeaderPrefixBuffer;
        private string _timeoutBuffer;

        public override Vector2 InitialSize => new Vector2(650f, 700f);

        public CustomProviderEditorWindow(CustomProviderConfig config, System.Action onSave, bool isNew = false)
        {
            _config = config;
            _onSave = onSave;
            _isNew = isNew;

            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            forcePause = true;

            // Initialize buffers from config
            _nameBuffer = config.Name ?? "";
            _baseUrlBuffer = config.BaseUrl ?? "";
            _endpointPathBuffer = config.EndpointPath ?? "/audio/speech";
            _apiKeyBuffer = config.ApiKey ?? "";
            _modelBuffer = config.Model ?? "tts-1";
            _defaultVoiceBuffer = config.DefaultVoice ?? "alloy";
            _responseFormatBuffer = config.ResponseFormat ?? "mp3";
            _emotionFieldNameBuffer = config.EmotionFieldName ?? "emotion";
            _instructTextFieldNameBuffer = config.InstructTextFieldName ?? "instruct_text";
            _customRequestBodyBuffer = config.CustomRequestBodyTemplate ?? "";
            _authHeaderNameBuffer = config.AuthHeaderName ?? "Authorization";
            _authHeaderPrefixBuffer = config.AuthHeaderPrefix ?? "Bearer ";
            _timeoutBuffer = config.TimeoutSeconds.ToString();
        }

        public override void DoWindowContents(Rect inRect)
        {
            string title = _isNew
                ? "RimTalk.Settings.TTS.CustomProvider.AddTitle".Translate()
                : "RimTalk.Settings.TTS.CustomProvider.EditTitle".Translate();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), title);
            Text.Font = GameFont.Small;

            float contentHeight = 1600f;
            Rect viewRect = new Rect(0, 40f, inRect.width - 16f, contentHeight);
            Rect scrollOuterRect = new Rect(0, 40f, inRect.width, inRect.height - 80f);

            Widgets.BeginScrollView(scrollOuterRect, ref _scrollPos, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // === Basic Settings ===
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.BasicSettings".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Name
            listing.Label("RimTalk.Settings.TTS.CustomProvider.Name".Translate());
            _nameBuffer = ValidatedTextEntry.Draw(listing, "custom_provider_name", _nameBuffer, true);
            listing.Gap(4f);

            // Base URL
            listing.Label("RimTalk.Settings.TTS.CustomProvider.BaseUrl".Translate());
            _baseUrlBuffer = ValidatedTextEntry.Draw(listing, "custom_provider_base_url", _baseUrlBuffer, true);
            listing.Gap(4f);

            // Endpoint path
            listing.Label("RimTalk.Settings.TTS.CustomProvider.EndpointPath".Translate());
            _endpointPathBuffer = listing.TextEntry(_endpointPathBuffer);
            listing.Gap(4f);

            // Full URL preview
            string previewUrl = (_baseUrlBuffer ?? "").TrimEnd('/') + (_endpointPathBuffer ?? "/audio/speech");
            GUI.color = Color.cyan;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.FullUrlPreview".Translate() + previewUrl);
            GUI.color = Color.white;
            listing.Gap(6f);

            // Model
            listing.Label("RimTalk.Settings.TTS.CustomProvider.Model".Translate());
            _modelBuffer = listing.TextEntry(_modelBuffer);
            listing.Gap(4f);

            // Default voice
            listing.Label("RimTalk.Settings.TTS.CustomProvider.DefaultVoice".Translate());
            _defaultVoiceBuffer = listing.TextEntry(_defaultVoiceBuffer);
            listing.Gap(4f);

            // Response format
            listing.Label("RimTalk.Settings.TTS.CustomProvider.ResponseFormat".Translate());
            Rect fmtRect = listing.GetRect(Text.LineHeight);
            if (Widgets.ButtonText(fmtRect, _responseFormatBuffer))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var fmt in new[] { "mp3", "opus", "aac", "flac", "wav", "pcm" })
                {
                    var f = fmt;
                    options.Add(new FloatMenuOption(f, () => _responseFormatBuffer = f));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(6f);

            // === Authentication ===
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.AuthSettings".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.RequiresApiKey".Translate(), ref _config.RequiresApiKey);
            listing.Gap(4f);

            if (_config.RequiresApiKey)
            {
                listing.Label("RimTalk.Settings.TTS.CustomProvider.ApiKey".Translate());
                _apiKeyBuffer = ValidatedTextEntry.Draw(
                    listing, "custom_provider_api_key", _apiKeyBuffer, _config.RequiresApiKey);
                listing.Gap(4f);

                listing.Label("RimTalk.Settings.TTS.CustomProvider.AuthHeaderName".Translate());
                _authHeaderNameBuffer = listing.TextEntry(_authHeaderNameBuffer);
                listing.Gap(4f);

                listing.Label("RimTalk.Settings.TTS.CustomProvider.AuthHeaderPrefix".Translate());
                _authHeaderPrefixBuffer = listing.TextEntry(_authHeaderPrefixBuffer);
                listing.Gap(4f);
            }

            // === Optional TTS Features ===
            listing.Gap(6f);
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.OptionalFeatures".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableSpeed".Translate(), ref _config.EnableSpeed,
                "RimTalk.Settings.TTS.CustomProvider.EnableSpeedTooltip".Translate());
            listing.Gap(4f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableTemperature".Translate(), ref _config.EnableTemperature,
                "RimTalk.Settings.TTS.CustomProvider.EnableTemperatureTooltip".Translate());
            listing.Gap(4f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableTopP".Translate(), ref _config.EnableTopP,
                "RimTalk.Settings.TTS.CustomProvider.EnableTopPTooltip".Translate());
            listing.Gap(4f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableEmotion".Translate(), ref _config.EnableEmotion,
                "RimTalk.Settings.TTS.CustomProvider.EnableEmotionTooltip".Translate());
            if (_config.EnableEmotion)
            {
                listing.Label("RimTalk.Settings.TTS.CustomProvider.EmotionFieldName".Translate());
                _emotionFieldNameBuffer = listing.TextEntry(_emotionFieldNameBuffer);
            }
            listing.Gap(4f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableInstructText".Translate(), ref _config.EnableInstructText,
                "RimTalk.Settings.TTS.CustomProvider.EnableInstructTextTooltip".Translate());
            if (_config.EnableInstructText)
            {
                listing.Label("RimTalk.Settings.TTS.CustomProvider.InstructTextFieldName".Translate());
                _instructTextFieldNameBuffer = listing.TextEntry(_instructTextFieldNameBuffer);
            }
            listing.Gap(4f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.EnableStreaming".Translate(), ref _config.EnableStreaming,
                "RimTalk.Settings.TTS.CustomProvider.EnableStreamingTooltip".Translate());
            listing.Gap(4f);

            // Timeout
            listing.Label("RimTalk.Settings.TTS.CustomProvider.Timeout".Translate());
            _timeoutBuffer = listing.TextEntry(_timeoutBuffer);
            listing.Gap(6f);

            // === Custom Request Body ===
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk.Settings.TTS.CustomProvider.CustomRequestBody".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            listing.CheckboxLabeled("RimTalk.Settings.TTS.CustomProvider.UseCustomRequestBody".Translate(), ref _config.UseCustomRequestBody,
                "RimTalk.Settings.TTS.CustomProvider.UseCustomRequestBodyTooltip".Translate());

            if (_config.UseCustomRequestBody)
            {
                listing.Gap(4f);
                GUI.color = Color.cyan;
                listing.Label("RimTalk.Settings.TTS.CustomProvider.TemplatePlaceholders".Translate());
                GUI.color = Color.white;
                listing.Gap(4f);

                Rect textAreaRect = listing.GetRect(150f);
                _customRequestBodyBuffer = Widgets.TextArea(textAreaRect, _customRequestBodyBuffer);
                listing.Gap(4f);

                // Load default OpenAI template button
                if (listing.ButtonText("RimTalk.Settings.TTS.CustomProvider.LoadDefaultTemplate".Translate()))
                {
                    _customRequestBodyBuffer = GetDefaultOpenAITemplate();
                }
            }

            listing.Gap(12f);

            listing.End();
            Widgets.EndScrollView();

            // === Bottom Buttons ===
            float buttonWidth = 120f;
            float buttonHeight = 35f;
            float bottomY = inRect.height - buttonHeight - 5f;

            Rect saveRect = new Rect(inRect.width / 2f - buttonWidth - 5f, bottomY, buttonWidth, buttonHeight);
            Rect cancelRect = new Rect(inRect.width / 2f + 5f, bottomY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(saveRect, "RimTalk.TTS.Save".Translate()))
            {
                SaveAndClose();
            }

            if (Widgets.ButtonText(cancelRect, "RimTalk.TTS.Cancel".Translate()))
            {
                Close();
            }
        }

        private void SaveAndClose()
        {
            // Validate
            if (string.IsNullOrWhiteSpace(_nameBuffer))
            {
                Messages.Message("RimTalk.Settings.TTS.CustomProvider.NameRequired".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (string.IsNullOrWhiteSpace(_baseUrlBuffer))
            {
                Messages.Message("RimTalk.Settings.TTS.CustomProvider.UrlRequired".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Apply buffers to config
            _config.Name = _nameBuffer.Trim();
            _config.BaseUrl = _baseUrlBuffer.Trim();
            _config.EndpointPath = _endpointPathBuffer?.Trim() ?? "/audio/speech";
            _config.ApiKey = _apiKeyBuffer?.Trim() ?? "";
            _config.Model = _modelBuffer?.Trim() ?? "tts-1";
            _config.DefaultVoice = _defaultVoiceBuffer?.Trim() ?? "alloy";
            _config.ResponseFormat = _responseFormatBuffer?.Trim() ?? "mp3";
            _config.EmotionFieldName = _emotionFieldNameBuffer?.Trim() ?? "emotion";
            _config.InstructTextFieldName = _instructTextFieldNameBuffer?.Trim() ?? "instruct_text";
            _config.CustomRequestBodyTemplate = _customRequestBodyBuffer ?? "";
            _config.AuthHeaderName = _authHeaderNameBuffer?.Trim() ?? "Authorization";
            _config.AuthHeaderPrefix = _authHeaderPrefixBuffer ?? "Bearer ";
            
            if (int.TryParse(_timeoutBuffer, out int timeout) && timeout > 0)
                _config.TimeoutSeconds = timeout;
            else
                _config.TimeoutSeconds = 30;

            _onSave?.Invoke();
            Close();
        }

        private static string GetDefaultOpenAITemplate()
        {
            return @"{
  ""model"": ""{model}"",
  ""input"": ""{input}"",
  ""voice"": ""{voice}"",
  ""speed"": {speed},
  ""response_format"": ""{response_format}""
}";
        }
    }
}
