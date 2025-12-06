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
            - Identify emotions and tone from content inside parentheses.
            - Translate the text into {language}; the translation must remain faithful to the original.
            - Append (break) to the end of every sentence in the text.
            - Emotions: must appear at the beginning of a sentence, enclosed in parentheses, only one per sentence, and separated from the sentence by a single space.
            - Tone markers: may appear anywhere.
            - Audio effects: may be placed in appropriate positions, and you may add suitable verbal descriptions after an effect marker (for example, after (laughing) add "haha", after (groaning) add "aah", etc.).
            - All annotations may be combined.
            - All audio effects must appear as two consecutive identical markers, e.g., (moaning)(moaning).
            - Replace ellipses with (break) or (long-break).
            - Your output must not include parenthetical content that was present in the input.

            - If the input contains only parenthetical content, reasonably use a sound effect to give it scene context. For example: (After sighing, they turn away) can be converted to (sigh).

            EMOTIONS:
            (happy), (sad), (angry), (excited), (calm), (nervous), (confident), (surprised), (satisfied), (delighted), (scared), (worried), (upset), (frustrated), (depressed), (empathetic), (embarrassed), (disgusted), (moved), (proud), (relaxed), (grateful), (curious), (sarcastic), (disdainful), (unhappy), (anxious), (hysterical), (indifferent), (uncertain), (doubtful), (confused), (disappointed), (regretful), (guilty), (ashamed), (jealous), (envious), (hopeful), (optimistic), (pessimistic), (nostalgic), (lonely), (bored), (contemptuous), (sympathetic), (compassionate), (determined), (resigned)

            TONE MARKERS:
            (in a hurry tone), (shouting), (screaming), (whispering), (soft tone)

            AUDIO EFFECTS:
            (laughing), (chuckling), (sobbing), (crying loudly), (sighing), (groaning), (panting), (gasping), (yawning), (snoring)
            Special AUDIO EFFECTS: (break) for short pause, (long-break) for long pause

            INTENSITY MODIFIERS:
            Adjust emotions: (slightly sad), (very excited), (extremely angry)

            Only output the final translated words in {language} with markers in English, and nothing else. IMPORTANT: you must delete any parenthetical content from the original text; any parentheses in your output should contain only the annotations you generated.

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
