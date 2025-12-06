using System.Threading.Tasks;
using RimTalk.TTS.Data;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Translation service using TTS module's own LLM API configuration
    /// </summary>
    public static class TranslationService
    {
        /// <summary>
        /// Translate text to target language using configured LLM API
        /// </summary>
        public static async Task<string> TranslateAsync(string text, string targetLanguage, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimTalk.TTS] Translation settings is null");
                return text;
            }

            try
            {
                // Get TTS processing prompt from settings or use default
                string promptTemplate = TTSConstant.GetTTSProcessingPrompt(settings);
                
                // Build translation prompt
                string prompt = promptTemplate
                    .Replace("{language}", targetLanguage)
                    .Replace("{text}", text);

                // Call SimpleLLMClient directly with settings
                var (response, success) = await SimpleLLMClient.QueryAsync(prompt, settings);

                if (success && !string.IsNullOrEmpty(response))
                {
                    return response.Trim();
                }
                else
                {
                    Log.Warning("[RimTalk.TTS] Empty response from translation API");
                    return text;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimTalk.TTS] Translation failed - {ex.Message}");
                return text; // Return original text on error
            }
        }
    }
}
