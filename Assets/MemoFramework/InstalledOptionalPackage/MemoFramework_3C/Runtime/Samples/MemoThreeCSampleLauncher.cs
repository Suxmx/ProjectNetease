using MemoFramework.GameState;

namespace MemoFramework.ThreeC.Samples
{
    /// <summary>
    /// MemoFramework_3C 本地样例使用的最小启动器，只注册一个空闲 GameState。
    /// </summary>
    public sealed class MemoThreeCSampleLauncher : MFLauncher
    {
        private const string IdleStateName = "Idle";

        /// <summary>
        /// 为 3C 样例场景注册最小 GameState，避免 MF 根对象缺少 launcher 时初始化失败。
        /// </summary>
        /// <param name="gameStateComponent">MemoFramework 的 GameState 组件。</param>
        public override void InitGameStatesFsm(GameStateComponent gameStateComponent)
        {
            gameStateComponent.PushGameState(IdleStateName, new GameStateBase());
            gameStateComponent.SetAsStartState(IdleStateName);
        }
    }
}
