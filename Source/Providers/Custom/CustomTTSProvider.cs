using System.Threading;
using System.Threading.Tasks;
using RimTalk.TTS.Data;

namespace RimTalk.TTS.Providers
{
    /// <summary>
    /// Provider for user-defined TTS endpoints (OpenAI-compatible request body by default).
    /// </summary>
    public class CustomTTSProvider : ITTSProvider
    {
        private readonly CustomProviderConfig _config;

        public CustomTTSProvider(CustomProviderConfig config)
        {
            _config = config;
            if (config != null)
            {
                TTSLog.Message($"[RimTalk.TTS] CustomTTSProvider initialized: {config.GetDisplayName()} ({config.GetFullUrl()})");
            }
        }

        public CustomProviderConfig Config => _config;

        public ProviderCapabilities Capabilities => new ProviderCapabilities
        {
            RequiresApiKey = _config != null && _config.RequiresApiKey,
            IsApiKeyAcceptable = key =>
                !string.IsNullOrWhiteSpace(_config?.ApiKey) || !string.IsNullOrWhiteSpace(key),
        };

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            if (_config == null)
            {
                TTSLog.Error("[RimTalk.TTS] CustomTTSProvider: No config set");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_config.ApiKey) && string.IsNullOrWhiteSpace(request.ApiKey))
            {
                request.ApiKey = _config.ApiKey;
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                request.Model = _config.Model;
            }

            return await CustomTTSClient.GenerateSpeechAsync(request, _config, cancellationToken);
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}