namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Keeps the player pawn's TTS voice in sync after RimTalk rebuilds its cache (new game / load).
    /// </summary>
    internal static class CacheInitializePatch
    {
        public static void Postfix()
        {
            RimTalkPatches.HandlePlayerPawnInitialized();
        }
    }
}
