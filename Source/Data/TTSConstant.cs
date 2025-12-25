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
            You are to process input text for a TTS (text-to-speech) model.
            - Translate the text into {language}
            - Emotions: must appear at the beginning of a sentence, only one per sentence, and separated from the sentence by a single space.
            - Tone markers: may appear anywhere.
            - Audio effects: may be placed in appropriate positions, and you may add suitable verbal descriptions after an effect marker (for example, after [laughing] add "haha", after [groaning] add "aah", etc.).
            - All annotations may be combined.
            - Replace ellipses with [break] or [long-break].
            - Append [break] after EVERY sentences
            - ONLY ANNOTATIONS MENTIONED BELOW ARE ALLOWED
            - LEAVE ALL BRACKETS INTACT IN THE INPUT

            avalable annotations:
            {
                EMOTIONS (can add intensity modifiers: for example, [slightly sad], [very excited], [extremely angry] etc.): [happy], [sad], [angry], [excited], [calm], [nervous], [confident], [surprised], [satisfied], [delighted], [scared], [worried], [upset], [frustrated], [depressed], [empathetic], [embarrassed], [disgusted], [moved], [proud], [relaxed], [grateful], [curious], [sarcastic], [disdainful], [unhappy], [anxious], [hysterical], [indifferent], [uncertain], [doubtful], [confused], [disappointed], [regretful], [guilty], [ashamed], [jealous], [envious], [hopeful], [optimistic], [pessimistic], [nostalgic], [lonely], [bored], [contemptuous], [sympathetic], [compassionate], [determined], [resigned]

                TONE MARKERS: [in a hurry tone], [shouting], [screaming], [whispering], [soft tone]

                AUDIO EFFECTS: [laughing], [chuckling], [sobbing], [crying loudly], [sighing], [groaning], [panting], [gasping], [yawning], [snoring]

                PAUSES: [break], [long-break]

            }

            Only output JSON format:
            "text": translated input in {language} with English annotations
            "emotion": leave it empty
            """;
        
        public static readonly string DefaultTTSProcessingPrompt_CosyVoice =
            """
            处理输入并翻译为{language}
            不得更改原文括号.
            从下列词语中选择最贴切输入的填入"emotion"字段: Happy, Sad, Angry, Excited, Calm, Fearful, Disgusted, Confused
            在text合适地方添加以下符号,必须符合原文本的情绪与内容: [breath],<strong></strong>(将括起内容语气增强),[noise],[laughter],[cough],[clucking],[accent],[quick_breath],<laughter></laughter>(将括起内容带笑意),[hissing],[sigh],[vocalized-noise],[lipsmack]
            输出JSON格式包含字段:
            'emotion': 情感单词
            'text': 翻译后加上音效标签的{language}文本
            """;

        public static readonly string DefaultTTSProcessingPrompt_IndexTTS =
            """
            将输入翻译为{language}
            输出JSON格式包含字段:
            'emotion': 留空
            'text': 翻译后的{language}文本
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
