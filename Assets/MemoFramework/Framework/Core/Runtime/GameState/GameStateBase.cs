using System;
using UnityEngine;

namespace MemoFramework.GameState
{
    public class GameStateBase : MFStateBase<string>
    {
        public GameStateBase(Action<State<string, string>> onEnter = null, Action<State<string, string>> onLogic = null,
            Action<State<string, string>> onExit = null, Func<State<string, string>, bool> canExit = null,
            bool needsExitTime = false, bool isGhostState = false) : base(onEnter, onLogic, onExit, canExit,
            needsExitTime, isGhostState)
        {
        }

        public GameStateComponent GameStateComponent { get; set; }

        protected override void OnStateEnter()
        {
            Debug.Log(MFUtils.Text.Format("GameState:进入State:[{0}]", name));
        }

        protected override void OnStateExit()
        {
            Debug.Log(MFUtils.Text.Format("GameState:退出State:[{0}]", name));
        }
    }
}