using System;
using RimTalk.Data;
using RimTalk.TTS.Data;
using RimWorld;
using Verse;

namespace RimTalk.TTS.Patches
{
    /// <summary>
    /// Thin adapters between RimTalk's callbacks and the runtime/pipeline.
    ///
    /// No dialogue state lives here any more: pending speech, audio and timeouts are owned by
    /// <see cref="Pipeline.DialogueSpeechRegistry"/> and driven by <see cref="Pipeline.DialoguePipeline"/>.
    /// </summary>
    public static class RimTalkPatches
    {
        // ---------------------------------------------------------------------------------
        // pending on/off toggle (a widget click is queued and applied on the next tick)
        // ---------------------------------------------------------------------------------
        private static bool _pendingToggle;
        private static bool _pendingToggleValue;
        private static readonly object _pendingToggleLock = new object();
        private static string _pendingToggleMessage = "";

        private const int TimeoutSweepIntervalTicks = 30;
        private static int _ticksSinceTimeoutSweep;

        // =================================================================================
        // patch entry points
        // =================================================================================

        internal static bool HandleDisplay(Pawn pawn, TalkResponse talk, string hook)
        {
            return TtsRuntime.Current?.Pipeline?.ShouldDisplay(pawn, talk, hook) ?? true;
        }

        internal static bool HandleDisplayAtHead(PawnState pawnState, string hook = "MarkTalkSpoken")
        {
            return TtsRuntime.Current?.Pipeline?.ShouldDisplay(pawnState?.Pawn, Head(pawnState), hook) ?? true;
        }

        private static TalkResponse Head(PawnState pawnState)
        {
            if (pawnState == null || pawnState.TalkResponses.Count == 0)
                return null;
            return pawnState.TalkResponses[0];
        }

        /// <summary>Generation trigger: new responses were drained into the pawn queue.</summary>
        internal static void HandleDrainedResponses(PawnState pawnState)
        {
            try
            {
                var pipeline = TtsRuntime.Current?.Pipeline;
                if (pipeline == null || pawnState?.Pawn == null)
                    return;

                var responses = pawnState.TalkResponses;
                for (int i = 0; i < responses.Count; i++)
                {
                    var talk = responses[i];
                    if (talk == null) continue;

                    // The first line of a batch must not wait behind the others in the supplier
                    // cooldown queue: request it with priority so the batch can start speaking ASAP.
                    pipeline.Request(talk.Text, talk.Id, pawnState.Pawn, priority: i == 0);
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] HandleDrainedResponses exception: {ex}");
            }
        }

        /// <summary>Cancel sync: RimTalk ignored a response.</summary>
        internal static void HandleTalkIgnored(Guid id)
        {
            TtsRuntime.Current?.Pipeline?.Cancel(id);
        }

        /// <summary>New game / load: drop all runtime TTS state.</summary>
        internal static void HandleGameLoaded()
        {
            PauseDrainPatch.ClearPending();
            TtsRuntime.Current?.OnGameLoaded();
        }

        /// <summary>Pawn permanently removed: drop its voice assignment.</summary>
        internal static void HandlePawnDiscarded(Pawn pawn)
        {
            try
            {
                if (pawn != null)
                {
                    PawnVoiceManager.RemovePawn(pawn);
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] HandlePawnDiscarded exception: {ex}");
            }
        }

        /// <summary>RimTalk rebuilt its cache: refresh the player pawn's voice.</summary>
        internal static void HandlePlayerPawnInitialized()
        {
            UpdatePlayerPawnVoice();
        }

        /// <summary>Queue an on/off toggle; it is applied on the next tick.</summary>
        internal static void RequestToggle(bool onOff)
        {
            lock (_pendingToggleLock)
            {
                _pendingToggle = true;
                _pendingToggleValue = onOff;
                _pendingToggleMessage = "RimTalk.TTS.OnOffUpdated".Translate(
                    onOff ? "RimTalk.TTS.On".Translate() : "RimTalk.TTS.Off".Translate());
            }
        }

        /// <summary>Per-tick main-thread work (see <see cref="TickPatch"/>).</summary>
        internal static void RunTickWork()
        {
            ApplyPendingToggle();

            if (++_ticksSinceTimeoutSweep < TimeoutSweepIntervalTicks)
                return;

            _ticksSinceTimeoutSweep = 0;
            try
            {
                TtsRuntime.Current?.Pipeline?.SweepTimeouts();
            }
            catch (Exception ex)
            {
                // A tick postfix must never take the game tick down with it.
                TTSLog.Error($"[RimTalk.TTS] Timeout sweep exception: {ex}");
            }
        }

        private static void ApplyPendingToggle()
        {
            if (!_pendingToggle) return;

            bool onOff;
            string msg;
            lock (_pendingToggleLock)
            {
                onOff = _pendingToggleValue;
                msg = _pendingToggleMessage;
                _pendingToggle = false;
                _pendingToggleMessage = "";
            }

            try
            {
                Messages.Message(msg, MessageTypeDefOf.TaskCompletion, false);
                if (!onOff)
                {
                    TtsRuntime.Current?.Pipeline?.ResetAll(false);
                }
            }
            catch (Exception ex)
            {
                TTSLog.Error($"[RimTalk.TTS] ApplyPendingToggle exception: {ex}");
            }
        }

        /// <summary>Keep the player pawn's voice in sync after RimTalk rebuilds its cache.</summary>
        public static void UpdatePlayerPawnVoice()
        {
            var settings = TtsRuntime.Current?.Settings;
            if (settings == null)
                return;

            var pawn = Cache.GetPlayer();
            if (pawn == null)
                return;

            PawnVoiceManager.SetVoiceModel(pawn, settings.PlayerReferenceVoiceModelId);
        }
    }
}
