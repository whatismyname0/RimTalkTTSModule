using System.Collections.Generic;

namespace RimTalk.TTS.Service
{
    public class TTSRequest
    {
        // API key (placed here so callers only need to pass one object)
        public string ApiKey { get; set; }

        public string Model { get; set; }
        public string Input { get; set; }
        public string InstructText { get; set; }

        // voice URI or preset (can be empty string for dynamic references)
        public string Voice { get; set; }

        // optional synthesis parameters
        public float Speed { get; set; } = 1.0f;
        public float Volume { get; set; } = 1.0f;
        public float Temperature { get; set; } = 0f;
        public float TopP { get; set; } = 0f;

        // Only used when caller wants to supply reference audio for dynamic voice
        public List<ReferenceAudio> References { get; set; }

        public class ReferenceAudio
        {
            public string Audio { get; set; }
            public string Text { get; set; }
        }
    }
}
