using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Game lifecycle: RimTalk resets its cache on new game and on load, so TTS state is reset at
    /// the same points; a discarded pawn's voice assignment is dropped as well.
    /// </summary>
    internal static class GameLifecyclePatches
    {
        public static void GameResetPostfix()
        {
            RimTalkPatches.HandleGameLoaded();
        }

        public static void PawnDiscardPrefix(Pawn __instance)
        {
            RimTalkPatches.HandlePawnDiscarded(__instance);
        }
    }
}
