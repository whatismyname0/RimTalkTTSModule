using UnityEngine;
using Verse;

namespace RimTalk.TTS.UI.Overlay
{
    internal enum OverlayButton
    {
        None,
        Reset,
        Generate,
        Ignore,
        DisplayNext,
    }

    /// <summary>
    /// Layout and drawing for the four TTS control buttons next to RimTalk's gear icon.
    ///
    /// Geometry is recomputed from the gear rect every time it is needed (draw and hit-test), so the
    /// buttons can never be hit at stale screen positions. Only the buttons/actions live here; the
    /// actual command handling stays in the patch layer.
    /// </summary>
    internal readonly struct OverlayButtonBar
    {
        private const float ResetWidth = 120f;
        private const float GenerateWidth = 150f;
        private const float IgnoreWidth = 150f;
        private const float DisplayWidth = 150f;
        private const float Padding = 6f;
        private const float MinHeight = 28f;

        private readonly Rect _reset;
        private readonly Rect _generate;
        private readonly Rect _ignore;
        private readonly Rect _display;

        private OverlayButtonBar(Rect reset, Rect generate, Rect ignore, Rect display)
        {
            _reset = reset;
            _generate = generate;
            _ignore = ignore;
            _display = display;
        }

        /// <summary>Place the bar to the left of the gear icon.</summary>
        public static OverlayButtonBar NextTo(Rect gearRect)
        {
            float height = Mathf.Max(gearRect.height, MinHeight);
            float x = gearRect.x;

            x -= ResetWidth + Padding;
            var reset = new Rect(x, gearRect.y, ResetWidth, height);

            x -= GenerateWidth + Padding;
            var generate = new Rect(x, gearRect.y, GenerateWidth, height);

            x -= IgnoreWidth + Padding;
            var ignore = new Rect(x, gearRect.y, IgnoreWidth, height);

            x -= DisplayWidth + Padding;
            var display = new Rect(x, gearRect.y, DisplayWidth, height);

            return new OverlayButtonBar(reset, generate, ignore, display);
        }

        /// <summary>Draws the buttons; returns the one clicked this frame.</summary>
        public OverlayButton Draw()
        {
            if (Widgets.ButtonText(_reset, "RimTalk.TTS.Reset".Translate()))
                return OverlayButton.Reset;
            if (Widgets.ButtonText(_generate, "RimTalk.TTS.Generate".Translate()))
                return OverlayButton.Generate;
            if (Widgets.ButtonText(_ignore, "RimTalk.TTS.Ignore".Translate()))
                return OverlayButton.Ignore;
            if (Widgets.ButtonText(_display, "RimTalk.TTS.Display".Translate()))
                return OverlayButton.DisplayNext;

            return OverlayButton.None;
        }

        public OverlayButton HitTest(Vector2 mousePosition)
        {
            if (_reset.Contains(mousePosition)) return OverlayButton.Reset;
            if (_generate.Contains(mousePosition)) return OverlayButton.Generate;
            if (_ignore.Contains(mousePosition)) return OverlayButton.Ignore;
            if (_display.Contains(mousePosition)) return OverlayButton.DisplayNext;
            return OverlayButton.None;
        }
    }
}
