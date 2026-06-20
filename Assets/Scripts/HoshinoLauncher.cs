using MemoFramework;
using MemoFramework.GameState;

namespace DefaultNamespace
{
    public class HoshinoLauncher : MFLauncher
    {
        public override void InitGameStatesFsm(GameStateComponent gameStateComponent)
        {
            gameStateComponent.GameStateFsm.AddState("Empty",new EmptyGameState());
        }
    }

    public class EmptyGameState : GameStateBase
    {
    }
}