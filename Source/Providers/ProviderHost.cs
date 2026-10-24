using System;
using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Owns the active provider and serializes provider swaps: the old instance is shut down only
    /// after the pipeline has cancelled in-flight requests, so a request can never keep using a
    /// provider that is already being disposed.
    /// </summary>
    public sealed class ProviderHost
    {
        private readonly object _lock = new object();
        private ITTSProvider _provider = new NoneProvider();

        public ITTSProvider Current
        {
            get { lock (_lock) { return _provider; } }
        }

        public ProviderCapabilities Capabilities => Current.Capabilities;

        /// <summary>
        /// Replace the provider. <paramref name="cancelInFlight"/> runs after the swap but before the
        /// old provider is shut down, so in-flight requests fail fast instead of racing disposal.
        /// </summary>
        public void Apply(TTSSettings.TTSSupplier supplier, TTSSettings settings, Action cancelInFlight)
        {
            ITTSProvider fresh = CreateProvider(supplier, settings);

            ITTSProvider old;
            lock (_lock)
            {
                old = _provider;
                _provider = fresh;
            }

            try
            {
                cancelInFlight?.Invoke();
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] Provider swap: cancelling in-flight requests failed: {ex.Message}");
            }

            Shutdown(old);

            TTSLog.Message($"[RimTalk.TTS] TTS provider set to {supplier}");
        }

        /// <summary>Shut down the current provider and fall back to the no-op provider.</summary>
        public void ShutdownCurrent()
        {
            ITTSProvider old;
            lock (_lock)
            {
                old = _provider;
                _provider = new NoneProvider();
            }
            Shutdown(old);
        }

        public Task<byte[]> GenerateAsync(TTSRequest request, CancellationToken cancellationToken)
        {
            return Current.GenerateSpeechAsync(request, cancellationToken);
        }

        private static void Shutdown(ITTSProvider provider)
        {
            if (provider == null || provider is NoneProvider) return;

            try
            {
                // Fire-and-forget: shutdown may kill a helper process; the caller is on the main thread.
                _ = provider.ShutdownAsync();
            }
            catch (Exception ex)
            {
                TTSLog.Warning($"[RimTalk.TTS] Error shutting down provider: {ex.Message}");
            }
        }

        private static ITTSProvider CreateProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            switch (supplier)
            {
                case TTSSettings.TTSSupplier.FishAudio:
                    return new FishAudioProvider();
                case TTSSettings.TTSSupplier.CosyVoice:
                    return new CosyVoiceProvider();
                case TTSSettings.TTSSupplier.IndexTTS:
                    return new IndexTTSProvider();
                case TTSSettings.TTSSupplier.AzureTTS:
                    var azureProvider = new AzureTTSProvider();
                    if (settings != null)
                    {
                        azureProvider.SetRegion(settings.GetSupplierRegion(supplier));
                    }
                    return azureProvider;
                case TTSSettings.TTSSupplier.EdgeTTS:
                    return new EdgeTTSProvider();
                case TTSSettings.TTSSupplier.GeminiTTS:
                    return new GeminiTTSProvider();
                case TTSSettings.TTSSupplier.Custom:
                    if (settings != null)
                    {
                        var customConfig = settings.GetCurrentCustomProvider();
                        if (customConfig != null)
                        {
                            return new CustomTTSProvider(customConfig);
                        }
                        TTSLog.Warning("[RimTalk.TTS] No custom provider selected");
                    }
                    return new NoneProvider();
                case TTSSettings.TTSSupplier.None:
                default:
                    return new NoneProvider();
            }
        }
    }
}
