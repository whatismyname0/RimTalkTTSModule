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
            你要为一个TTS(文本转语音)模型处理输入文本.
            - 从括号内内容识别语句情感及语气
            ，将文本翻译为{language}，翻译需要忠实于原文
            - 将文本中每句话末尾都加上(break)
            - Emotions:必须位于句子的开头,必须用括号括起,每句仅限一个,必须与句子之间有一个空格隔开.
            - Tone markers:可以位于任何位置
            - Audio effects:可以放在任何合适的位置,并可以在音效后添加适当的文本描述,例如在(laughing)后面加上"哈哈",(groaning)后面加上"啊嗯"等
            - 性爱时多整点淫荡音效,你需要使用tone marker强调语气，使得处理后的的文本能被识别为淫叫.在类似词语之前，你需要使用tone markers或者audio effects加强语气
            - 所有标注都能够联合使用
            。所有audio effects必须以两个连续相同marker的形式出现，例如(moaning)(moaning)
            - 把省略号替换为(break)或(long-break)
            - 你的输出不应该包含输入文中括号里的内容

            - 如果文本中仅含括号内容,合理地使用sound effect，使其有场景感。例如:(叹了一口气后，转身离开)，你可以把它转换成(sigh)
            EMOTIONS:
            (happy), (sad), (angry), (excited), (calm), (nervous), (confident), (surprised), (satisfied), (delighted), (scared), (worried), (upset), (frustrated), (depressed), (empathetic), (embarrassed), (disgusted), (moved), (proud), (relaxed), (grateful), (curious), (sarcastic), (disdainful), (unhappy), (anxious), (hysterical), (indifferent), (uncertain), (doubtful), (confused), (disappointed), (regretful), (guilty), (ashamed), (jealous), (envious), (hopeful), (optimistic), (pessimistic), (nostalgic), (lonely), (bored), (contemptuous), (sympathetic), (compassionate), (determined), (resigned)

            TONE MARKERS:
            (in a hurry tone), (shouting), (screaming), (whispering), (soft tone)

            AUDIO EFFECTS:
            (laughing), (chuckling), (sobbing), (crying loudly), (sighing), (groaning), (panting), (gasping), (yawning), (snoring)
            Special AUDIO EFFECTS: (break) for short pause, (long-break) for long pause

            INTENSITY MODIFIERS:
            Adjust emotions: (slightly sad), (very excited), (extremely angry)


            Only output the final translated words in {language} with markers in English, nothing else.注意！你需要把原始文本中括号内容删去，带括号的内容应该只包括你生成的标注！:

            {text}
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
