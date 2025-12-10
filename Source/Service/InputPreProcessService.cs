using System.Threading.Tasks;
using RimTalk.TTS.Data;
using Verse;

namespace RimTalk.TTS.Service
{
    /// <summary>
    /// Translation service using TTS module's own LLM API configuration
    /// </summary>
    public static class InputPreProcessService
    {
        /// <summary>
        /// Translate text to target language using configured LLM API
        /// </summary>
        public static async Task<string> PreProcessAsync(string text, string targetLanguage, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimTalk.TTS] preprocess settings is null");
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
                var (response, success) = await InputPreProcessClient.QueryAsync(prompt, settings);

                if (success && !string.IsNullOrEmpty(response))
                {
                    return response.Trim();
                }
                else
                {
                    Log.Warning("[RimTalk.TTS] Empty response from preprocess API");
                    return text;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimTalk.TTS] preprocess failed - {ex.Message}");
                return text; // Return original text on error
            }
        }
    }
}
