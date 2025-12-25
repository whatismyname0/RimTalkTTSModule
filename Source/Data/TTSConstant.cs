using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Constants for TTS module, including default prompts
    /// </summary>
    public static class TTSConstant
    {
        public static readonly string Lang = LanguageDatabase.activeLanguage.info.friendlyNameNative;

        public static readonly string DefaultTTSProcessingPrompt =
            """
            You are a professional TTS text processor.

            Rules:
            1. Translate all text into {language}.
            2. For text inside parentheses: translate only the content, keep parentheses, do not add annotations.
            3. For text outside parentheses: translate and add suitable annotations (see list below).
            - Emotions: at the start of each sentence, one per sentence, separated by a space.
            - Tone markers, audio effects: anywhere in the sentence.
            - Replace ellipses (...) with [break] or [long-break], then remove the ellipses.
            - Add [break] after every sentence outside parentheses.
            4. Never add annotations inside parentheses.
            5. Output only JSON:
            {
                "text": "<fully translated to {language} and annotated text, all parentheses and their translated content preserved>",
                "emotion": ""
            }

            Available annotations:
            Emotions: [happy], [sad], [angry], [excited], [calm], [nervous], [confident], [surprised], [satisfied], [delighted], [scared], [worried], [upset], [frustrated], [depressed], [empathetic], [embarrassed], [disgusted], [moved], [proud], [relaxed], [grateful], [curious], [sarcastic], [disdainful], [unhappy], [anxious], [hysterical], [indifferent], [uncertain], [doubtful], [confused], [disappointed], [regretful], [guilty], [ashamed], [jealous], [envious], [hopeful], [optimistic], [pessimistic], [nostalgic], [lonely], [bored], [contemptuous], [sympathetic], [compassionate], [determined], [resigned]
            Tone markers: [in a hurry tone], [shouting], [screaming], [whispering], [soft tone]
            Audio effects: [laughing], [chuckling], [sobbing], [crying loudly], [sighing], [groaning], [panting], [gasping], [yawning], [snoring]
            Pauses: [break], [long-break]
            """;
        
        public static readonly string DefaultTTSProcessingPrompt_CosyVoice =
            """
            你是一名专业的TTS文本处理专家.

            规则:
            1. 将所有文本翻译为{language}.
            2. 括号内内容:只翻译内容,保留括号,不添加任何标注.
            3. 括号外内容:翻译并在合适位置添加标注(见下方列表).
            4. 不要在括号内添加任何标注.
            5. 只输出JSON格式:
            {
                "text": "<完整翻译为 {language} 并加标注的文本,所有括号及其翻译内容均保留>",
                "emotion": "<最贴切的情感词>"
            }

            可用标注:
            情感(emotion字段,仅选一个):Happy, Sad, Angry, Excited, Calm, Fearful, Disgusted, Confused
            语气/音效(可在text字段括号外添加):[breath], <strong></strong>, [noise], [laughter], [cough], [clucking], [accent], [quick_breath], <laughter></laughter>, [hissing], [sigh], [vocalized-noise], [lipsmack]
            """;

        public static readonly string DefaultTTSProcessingPrompt_IndexTTS =
            """
            你是一名专业翻译家.

            规则:
            1. 将所有文本翻译为{language}.
            2. 括号内内容:只翻译内容,保留括号.
            3. 括号外内容:翻译为{language}.
            4. 只输出JSON格式:
            {
                "text": "<完整翻译为 {language} 的文本,所有括号及其翻译内容均保留>",
                "emotion": ""
            }
            """;

        /// <summary>
        /// Get the current TTS processing prompt from settings or fallback to default
        /// </summary>
        public static string GetTTSProcessingPrompt(TTSSettings settings)
        {
            if (settings == null)
                return DefaultTTSProcessingPrompt;

            return string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                ? DefaultTTSProcessingPrompt
                : settings.CustomTTSProcessingPrompt;
        }
    }
}
