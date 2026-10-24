using System;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Cancel sync: RimTalk marks a response as ignored here (user pressed ignore / danger).
    /// Used to abandon the matching speech request without waiting for playback.
    /// </summary>
    internal static class IgnoredPatch
    {
        public static void Prefix(Guid id)
        {
            RimTalkPatches.HandleTalkIgnored(id);
        }
    }
}
