
namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Per-tick main-thread work: apply a queued on/off toggle and abandon expired TTS requests.
    /// Kept as thin as possible; the actual work lives in <see cref="RimTalkPatches.RunTickWork"/>.
    /// </summary>
    internal static class TickPatch
    {
        public static void Postfix()
        {
            if (!(TtsRuntime.Current?.IsEnabled ?? false))
                return;

            RimTalkPatches.RunTickWork();
        }
    }
}
