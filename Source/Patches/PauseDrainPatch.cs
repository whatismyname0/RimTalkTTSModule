using System;
using System.Collections.Concurrent;
using RimTalk.Data;
using RimTalk.TTS.Bridge;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Starts speech generation as soon as RimTalk finishes a line, even while the game is paused.
    ///
    /// RimTalk moves generated lines out of <c>PawnState._incomingTalkResponses</c> only in
    /// <c>TalkService.DisplayTalk()</c>, which is driven by <c>TickManager.DoSingleTick</c> and that
    /// does not run while paused. A line generated on the pause screen therefore stayed queued until
    /// the player unpaused (or forced one display pass with the overlay button).
    ///
    /// This adapter mirrors the normal drain without displaying anything:
    ///  - <c>QueueIncomingResponse</c> postfix runs when generation completes (may be a worker thread)
    ///    and only enqueues the pawn;
    ///  - <c>TickManagerUpdate</c> prefix runs every frame, including while paused (the pause
    ///    early-return is inside that method), and drains those pawns on the main thread. The existing
    ///    drain postfix then requests speech exactly like a display pass would.
    ///
    /// When the game is not paused the queue is discarded: the regular 0.5s display tick keeps
    /// driving generation, so unpaused behaviour is unchanged.
    /// </summary>
    internal static class PauseDrainPatch
    {
        private const int MaxDrainsPerFrame = 16;

        private static readonly ConcurrentQueue<PawnState> Pending = new ConcurrentQueue<PawnState>();

        /// <summary>Generation finished for this pawn (may run on a worker thread).</summary>
        public static void QueueIncomingPostfix(PawnState __instance)
        {
            if (__instance == null) return;

            TtsRuntime runtime = TtsRuntime.Current;
            if (runtime == null || !runtime.IsPlaybackOn) return;

            Pending.Enqueue(__instance);
        }

        /// <summary>Main-thread pump: only does work while the game is paused.</summary>
        public static void TickManagerUpdatePrefix()
        {
            if (Pending.IsEmpty) return;

            try
            {
                TickManager tickManager = Find.TickManager;
                if (tickManager == null || !tickManager.Paused)
                {
                    // Not paused: the regular display tick drains these; drop the bookkeeping.
                    ClearPending();
                    return;
                }

                int drained = 0;
                while (drained < MaxDrainsPerFrame && Pending.TryDequeue(out PawnState pawnState))
                {
                    drained++;
                    if (pawnState == null || RimTalkHooks.DrainIncomingTalkResponses == null) continue;

                    try
                    {
                        RimTalkHooks.DrainIncomingTalkResponses.Invoke(pawnState, null);
                    }
                    catch (Exception ex)
                    {
                        TTSLog.Warning($"[RimTalk.TTS] Pause-time drain failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // A frame-level prefix must never take the game update down with it.
                TTSLog.Error($"[RimTalk.TTS] Pause-time drain pump exception: {ex}");
            }
        }

        /// <summary>Drop bookkeeping on new game / load (pawn references must not survive a scene change).</summary>
        public static void ClearPending()
        {
            while (Pending.TryDequeue(out _))
            {
            }
        }
    }
}
