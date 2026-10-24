using UnityEngine;
using Verse;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Text entry that flags a required-but-empty field: red border plus a short hint drawn inside
    /// the box. The hint is display-only - it is never part of the returned value - and disappears
    /// while the field has focus, so it can never be mistaken for input.
    /// </summary>
    internal static class ValidatedTextEntry
    {
        private const string RequiredHintKey = "RimTalk.Settings.TTS.Validation.Required";

        private static readonly Color BorderColor = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>
        /// Draw the entry and return its new value. When <paramref name="required"/> is true and the
        /// value is blank while the field is unfocused, a red border and the required hint are
        /// drawn on top of the box.
        /// </summary>
        public static string Draw(Listing_Standard listing, string controlName, string value, bool required)
        {
            Rect rect = listing.GetRect(Text.LineHeight);

            GUI.SetNextControlName(controlName);
            string newValue = Widgets.TextField(rect, value ?? "");
            bool focused = GUI.GetNameOfFocusedControl() == controlName;

            if (required && !focused && string.IsNullOrWhiteSpace(newValue))
            {
                Color oldColor = GUI.color;

                GUI.color = BorderColor;
                Widgets.DrawBox(rect, 2);

                GUI.color = HintColor;
                // Inset horizontally only: the hint must use the full line height of the box or the
                // glyphs get clipped.
                Widgets.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), RequiredHintKey.Translate());

                GUI.color = oldColor;
            }

            // Same trailing spacing as Listing_Standard.TextEntry, so swapping one for the other
            // does not change the layout of the surrounding settings page.
            listing.Gap(listing.verticalSpacing);

            return newValue;
        }
    }
}
