using RimTalk.Data;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Display-step patches. Exactly one of these is registered, depending on which hook the
    /// installed RimTalk build exposes (see <see cref="Bridge.RimTalkHooks.DisplayHook"/>).
    ///
    /// Returning false refuses to consume the line, so RimTalk keeps it at the head of
    /// PawnState.TalkResponses and retries every 0.5s until the speech is ready.
    /// </summary>
    internal static class DisplayHooksPatch
    {
        public static bool MarkTalkSpokenPrefix(PawnState pawnState)
        {
            return RimTalkPatches.HandleDisplayAtHead(pawnState);
        }

        public static bool CreateInteractionPrefix(Pawn pawn, TalkResponse talk)
        {
            return RimTalkPatches.HandleDisplay(pawn, talk, "CreateInteraction");
        }
    }
}
