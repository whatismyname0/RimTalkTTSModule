using System.Text;
using System.Threading.Tasks;
using RimTalk.TTS.Config;

namespace RimTalk.TTS.Llm
{
    /// <summary>
    /// Translation/preprocessing step: builds the prompt from the snapshot options and calls the
    /// configured LLM endpoint.
    /// </summary>
    public static class InputPreProcessService
    {
        public static async Task<PreProcessResult> PreProcessAsync(string text, string targetLanguage, LlmRequestOptions options)
        {
            if (options == null)
            {
                TTSLog.Warning("[RimTalk.TTS] Preprocess options are null");
                return null;
            }

            try
            {
                string prompt = (options.PromptTemplate ?? string.Empty).Replace("{language}", targetLanguage);

                var (response, success) = await InputPreProcessClient.QueryAsync(prompt, text, options);

                if (!success || response == null || string.IsNullOrEmpty(response.Text))
                {
                    TTSLog.Warning("[RimTalk.TTS] Empty response from preprocess API");
                    return null;
                }

                response.Text = CleanText(response.Text, options.ConvertBracketsToParentheses);
                if (string.IsNullOrEmpty(response.Text))
                {
                    TTSLog.Warning("[RimTalk.TTS] Preprocess result was empty after cleaning");
                    return null;
                }

                return response;
            }
            catch (System.Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Preprocess failed - {ex.Message}");
                return null;
            }
        }

        private static string CleanText(string text, bool convertBrackets)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = System.Text.RegularExpressions.Regex.Replace(
                        System.Text.RegularExpressions.Regex.Replace(
                            text.Normalize(NormalizationForm.FormKC), @"\([^)]*\)", ""
                        )
                        , @"\s+", " "
                    ).Trim();

            // Old Fish Audio models read "[]" as SSML - those suppliers expect "()".
            if (convertBrackets)
            {
                text = text.Replace("[", "(").Replace("]", ")");
            }

            return text;
        }
    }
}
