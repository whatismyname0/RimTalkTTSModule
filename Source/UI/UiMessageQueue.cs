using System;
using System.Collections.Concurrent;
using Verse;

namespace RimTalk.TTS.UI
{
    /// <summary>
    /// Queues for work produced by background tasks (uploads, voice sync) that must run on the main
    /// thread. Extracted from the settings window so other UI (overlay, windows) can use it too.
    /// </summary>
    internal static class UiMessageQueue
    {
        private static readonly ConcurrentQueue<(string text, MessageTypeDef type)> _messages =
            new ConcurrentQueue<(string, MessageTypeDef)>();

        private static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        public static void EnqueueMessage(string text, MessageTypeDef type)
        {
            if (string.IsNullOrEmpty(text)) return;
            _messages.Enqueue((text, type));
        }

        /// <summary>Run on the next main-thread drain (e.g. after a background upload finished).</summary>
        public static void EnqueueAction(Action action)
        {
            if (action != null) _actions.Enqueue(action);
        }

        /// <summary>Drain both queues; call once per frame from the main thread (settings window/tick).</summary>
        public static void Drain()
        {
            while (_actions.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception ex) { TTSLog.Error($"[RimTalk.TTS] UI action failed: {ex}"); }
            }

            while (_messages.TryDequeue(out var message))
            {
                Messages.Message(message.text, message.type, false);
            }
        }
    }
}
