using System;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// What a provider needs and can do. Validation is data, not behaviour, so the pipeline and the
    /// settings UI share one capability table.
    /// </summary>
    public sealed class ProviderCapabilities
    {
        public bool RequiresApiKey;

        /// <summary>Optional format check, only invoked when <see cref="RequiresApiKey"/> is true.</summary>
        public Func<string, bool> IsApiKeyAcceptable = key => !string.IsNullOrWhiteSpace(key);

        public bool SupportsInstruct;
        public bool SupportsSpeed;
        public bool SupportsEmotion;

        /// <summary>True for providers that own a local helper process (FishAudio).</summary>
        public bool IsLocalProcess;

        public static ProviderCapabilities NoKey()
        {
            return new ProviderCapabilities { RequiresApiKey = false };
        }

        public static ProviderCapabilities Key(Func<string, bool> acceptable = null, bool localProcess = false)
        {
            return new ProviderCapabilities
            {
                RequiresApiKey = true,
                IsApiKeyAcceptable = acceptable ?? (key => !string.IsNullOrWhiteSpace(key)),
                IsLocalProcess = localProcess,
            };
        }
    }
}
