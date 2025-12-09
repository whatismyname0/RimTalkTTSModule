using System;
using Verse;

namespace RimTalk.TTS
{
    /// <summary>
    /// Public interface for TTS module lifecycle and coordination.
    /// Main RimTalk mod uses reflection to discover and call these methods.
    /// </summary>
    public interface ITTSModule
    {
        /// <summary>
        /// Initialize the TTS module (called on game start)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called when a dialogue response is generated and queued for display.
        /// Starts TTS generation in parallel if TTS is enabled.
        /// </summary>
        /// <param name="text">Dialogue text to synthesize</param>
        /// <param name="pawn">Pawn speaking the dialogue</param>
        /// <param name="dialogueId">Unique identifier for this dialogue</param>
        void OnDialogueGenerated(string text, Pawn pawn, Guid dialogueId);


        /// <summary>
        /// Called when a dialogue is ignored/cancelled.
        /// Cancels TTS generation and removes pending audio.
        /// </summary>
        /// <param name="dialogueId">Unique identifier for this dialogue</param>
        void OnDialogueCancelled(Guid dialogueId);

        /// <summary>
        /// Called when entering/loading a game.
        /// Resets TTS state for fresh start.
        /// </summary>
        void OnGameLoaded();

        /// <summary>
        /// Called when exiting the game completely.
        /// Performs full TTS shutdown and cleanup.
        /// </summary>
        void OnGameExit();

        /// <summary>
        /// Check if TTS module is active (enabled in settings)
        /// </summary>
        bool IsActive { get; }
    }
}