using System.Text;
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
        public static async Task<PreProcessResult> PreProcessAsync(string text, string targetLanguage, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimTalk.TTS] preprocess settings is null");
                return null;
            }

            try
            {
                // Get TTS processing prompt from settings or use default
                string promptTemplate = TTSConstant.GetTTSProcessingPrompt(settings);
                
                // Build translation prompt
                string prompt = promptTemplate
                    .Replace("{language}", targetLanguage);

                // Call SimpleLLMClient directly with settings
                var (response, success) = await InputPreProcessClient.QueryAsync(prompt, text, settings);
                response.Text = CleanText(response.Text);

                if (success && !string.IsNullOrEmpty(response.Text))
                {
                    return response;
                }
                else
                {
                    Log.Warning("[RimTalk.TTS] Empty response from preprocess API");
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimTalk.TTS] preprocess failed - {ex.Message}");
                return null;
            }
        }
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = System.Text.RegularExpressions.Regex.Replace(
                        System.Text.RegularExpressions.Regex.Replace(
                            text.Normalize(NormalizationForm.FormKC), @"\([^)]*\)", ""
                        )
                        , @"\s+", " "
                    ).Trim();

            if (TTSModule.Instance.GetSettings().Supplier  == TTSSettings.TTSSupplier.FishAudio)
            {
                text = text.Replace("[","(").Replace("]",")");
            }

            return text;
        }
    }
}
