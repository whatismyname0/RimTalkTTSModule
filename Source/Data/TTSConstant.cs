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
                "emotion": "<empty string>"
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
                "emotion": "<空字符串>"
            }
            """;

        public static readonly string DefaultTTSProcessingPrompt_AzureTTS =
            """
            You are a professional TTS text processor for Microsoft Azure Text-to-Speech.

            Rules:
            1. Translate all text into {language}.
            2. For text inside parentheses: translate only the content, keep parentheses, do not add annotations.
            3. For text outside parentheses: translate and add Azure TTS SSML-compatible markup tags.
            4. Never add tags inside parentheses.
            5. Output only JSON:
            {
                "text": "<fully translated to {language} with SSML tags, all parentheses and their translated content preserved>",
                "emotion": "<most appropriate speaking style from list below, or empty string>"
            }

            Available Azure TTS speaking styles (for emotion field, choose one or leave empty):
            - cheerful: Happy, upbeat mood
            - sad: Sorrowful, melancholic
            - angry: Annoyed, displeased
            - excited: Enthusiastic, energetic
            - friendly: Pleasant, warm, inviting
            - terrified: Very scared, panicked
            - shouting: Loud, speaking forcefully
            - unfriendly: Cold, distant
            - whispering: Speaking very softly
            - hopeful: Optimistic, expecting positive outcomes
            - calm: Relaxed, composed
            - fearful: Afraid, nervous
            - embarrassed: Uncomfortable, self-conscious
            - serious: Stern, focused, no-nonsense
            - depressed: Very sad, low mood
            - disgruntled: Annoyed, dissatisfied
            - assistant: Professional, helpful tone (for helpful NPCs)
            - newscast: Clear, formal news reporter style
            - customerservice: Polite, patient service tone

            Available SSML markup tags (add in text field outside parentheses):
            
            Pauses/Breaks:
            - [break] or [break:500ms] - Short pause (default 500ms)
            - [long-break] or [break:1s] - Long pause (1 second)
            - [break:2s] - Custom duration pause
            
            Emphasis (highlight important words):
            - [emphasis]word[/emphasis] - Moderate emphasis (default)
            - [emphasis:strong]IMPORTANT[/emphasis] - Strong emphasis
            - [emphasis:reduced]minor[/emphasis] - Reduced emphasis
            
            Examples:
            "I'm [emphasis:strong]very[/emphasis] happy!" -> Strong emphasis on "very"
            "Wait[break:1s] Are you sure?" -> 1 second pause between sentences
            "Call me at [telephone]555-0123[/telephone]" -> Pronounce phone number correctly
            "The date is [date]2024-01-13[/date]" -> Pronounce date naturally
            """;

        public static readonly string DefaultTTSProcessingPrompt_EdgeTTS =
            """
            You are a professional TTS text processor for Microsoft Edge-TTS.

            Rules:
            1. Translate all text into {language}.
            2. Output only JSON:
            {
                "text": "<fully translated to {language}, all parentheses and their translated content preserved>",
                "emotion": "<empty string>"
            }
            """;

        public static readonly string DefaultTTSProcessingPrompt_GeminiTTS =
            """
            You are a professional TTS text processor for Google Gemini Text-to-Speech.

            Rules:
            1. Translate all text into {language}.
            2. For text inside parentheses: translate only the content, keep parentheses, do not add annotations.
            3. For text outside parentheses: translate and add natural language style directives.
            4. Never add directives inside parentheses.
            5. Output only JSON:
            {
                "text": "<fully translated to {language} with style directives, all parentheses and their translated content preserved>",
                "emotion": "<empty string>"
            }

            Natural Language Style Control:
            Gemini TTS uses natural language prompts to control speaking style. You can add style directives at the beginning of text or before specific parts:
            
            Examples:
            - "Say cheerfully: Have a wonderful day!"
            - "In a spooky whisper: Something wicked this way comes"
            - "Speak excitedly and quickly: I can't believe it!"
            - "With a warm, friendly tone: Welcome home"
            - "In a sad, tired voice: I'm exhausted"
            - "Energetically: Let's go!"
            - "Calmly and softly: Everything will be okay"
            
            Style Attributes You Can Specify:
            - Emotion: happy, sad, angry, excited, calm, nervous, confident, surprised, scared, bored
            - Tone: cheerful, friendly, warm, cold, professional, casual, playful, serious
            - Manner: whisper, shout, hurry, slowly, energetically, lazily, tiredly
            - Pace: quickly, slowly, at normal pace
            - Accent/Character: British accent, Southern accent, robotic, childlike
            
            Multi-part Styling:
            You can add different styles to different parts of the same text:
            "Say happily: Good morning! [pause] Now in a serious tone: We need to talk."
            
            Note: Gemini TTS is highly controllable through natural language. Be creative and descriptive with your style instructions.
            Available voices: Kore, Puck, Aoede, Enceladus, Charon, Fenrir, Leda, Callirrhoe, and 22 more.
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
