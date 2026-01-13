namespace RimTalk.TTS.Data
{
    /// <summary>
    /// Centralized configuration access helper
    /// Provides a single point of access to TTS settings throughout the application
    /// </summary>
    public static class TTSConfig
    {
        /// <summary>
        /// Get current TTS settings (may be null if not initialized)
        /// </summary>
        public static TTSSettings Settings => TTSModule.Instance?.GetSettings();

        /// <summary>
        /// Check if TTS is enabled and active
        /// </summary>
        public static bool IsEnabled => TTSModule.Instance?.IsActive ?? false;

        /// <summary>
        /// Get current supplier
        /// </summary>
        public static TTSSettings.TTSSupplier CurrentSupplier => Settings?.Supplier ?? TTSSettings.TTSSupplier.None;

        /// <summary>
        /// Check if settings are initialized
        /// </summary>
        public static bool IsInitialized => Settings != null;
    }
}
