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
            - delete ALL non-speech content in the original text!
            - Translate the text into {language}
            - Append (long-break) after the text
            - Append (break) after every sentence
            - Emotions: must appear at the beginning of a sentence, only one per sentence, and separated from the sentence by a single space.
            - Tone markers: may appear anywhere.
            - Audio effects: may be placed in appropriate positions, and you may add suitable verbal descriptions after an effect marker (for example, after (laughing) add "haha", after (groaning) add "aah", etc.).
            - All annotations may be combined.
            - All audio effects must appear as two consecutive identical markers, e.g., (moaning)(moaning).
            - Replace ellipses with (break) or (long-break).

            EMOTIONS:
            (happy), (sad), (angry), (excited), (calm), (nervous), (confident), (surprised), (satisfied), (delighted), (scared), (worried), (upset), (frustrated), (depressed), (empathetic), (embarrassed), (disgusted), (moved), (proud), (relaxed), (grateful), (curious), (sarcastic), (disdainful), (unhappy), (anxious), (hysterical), (indifferent), (uncertain), (doubtful), (confused), (disappointed), (regretful), (guilty), (ashamed), (jealous), (envious), (hopeful), (optimistic), (pessimistic), (nostalgic), (lonely), (bored), (contemptuous), (sympathetic), (compassionate), (determined), (resigned)

            TONE MARKERS:
            (in a hurry tone), (shouting), (screaming), (whispering), (soft tone)

            AUDIO EFFECTS:
            (laughing), (chuckling), (sobbing), (crying loudly), (sighing), (groaning), (panting), (gasping), (yawning), (snoring)
            Special AUDIO EFFECTS: (break) for short pause, (long-break) for long pause

            INTENSITY MODIFIERS:
            Adjust emotions: (slightly sad), (very excited), (extremely angry) etc.

            Only output the final translated words in {language} with markers in English, and nothing else.
            - delete ALL non-speech content in the original text!
            - output MUST contain only speech, words that indicate vocalizations, and annotations you add.

            input:

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
