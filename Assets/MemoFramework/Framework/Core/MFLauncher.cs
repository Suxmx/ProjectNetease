using MemoFramework.GameState;

namespace MemoFramework
{
    public abstract class MFLauncher
    {
        /// <summary>
        /// 在这里注册所有的GameState状态机逻辑
        /// </summary>
        public abstract void InitGameStatesFsm(GameStateComponent gameStateComponent);
    }
}