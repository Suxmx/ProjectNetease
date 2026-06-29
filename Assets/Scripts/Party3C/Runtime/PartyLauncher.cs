using MemoFramework;
using MemoFramework.GameState;

namespace Party3C
{
    public class PartyLauncher : MFLauncher
    {
        private const string EmptyStateName = "Empty";
        public override void InitGameStatesFsm(GameStateComponent gameStateComponent)
        {
            gameStateComponent.GameStateFsm.AddState(EmptyStateName, new PartyEmptyState());
            
        }

        private class PartyEmptyState : GameStateBase
        {
            
        }
    }
}