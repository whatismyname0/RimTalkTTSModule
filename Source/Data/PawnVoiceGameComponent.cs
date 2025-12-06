using Verse;

namespace RimTalk.TTS.Data
{
    /// <summary>
    /// GameComponent to hook PawnVoiceManager.ExposeData into the save/load cycle per game.
    /// </summary>
    public class PawnVoiceGameComponent : GameComponent
    {
        public PawnVoiceGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            PawnVoiceManager.ExposeData();
        }
    }
}