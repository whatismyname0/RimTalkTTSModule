using RimTalk.Data;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Generation trigger: RimTalk moves finished AI responses into PawnState.TalkResponses here,
    /// once per tick, before any display attempt. This is the only append point (verified against
    /// both RimTalk builds), so requesting speech here guarantees a line can never reach display
    /// without its generation having been requested.
    /// </summary>
    internal static class DrainResponsesPatch
    {
        public static void Postfix(PawnState __instance)
        {
            RimTalkPatches.HandleDrainedResponses(__instance);
        }
    }
}
