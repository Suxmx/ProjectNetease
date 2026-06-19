using System;
using UnityEngine;

namespace MemoFramework
{
    public class MFStateBase<TId> : State<TId>
    {
        public MFStateBase(Action<State<TId, string>> onEnter = null, Action<State<TId, string>> onLogic = null,
            Action<State<TId, string>> onExit = null, Func<State<TId, string>, bool> canExit = null,
            bool needsExitTime = false, bool isGhostState = false) : base(onEnter, onLogic, onExit, canExit,
            needsExitTime, isGhostState)
        {
        }

        public sealed override void OnEnter()
        {
            base.OnEnter();
            // Debug.Log($"On Enter State: {name}");
            OnStateEnter();
        }

        public sealed override void OnExit()
        {
            base.OnExit();
            // Debug.Log($"On Exit State: {name}");
            OnStateExit();
        }

        public sealed override void OnLogic()
        {
            base.OnLogic();
            OnStateUpdate();
        }

        protected virtual void OnStateEnter()
        {
        }

        protected virtual void OnStateUpdate()
        {
        }

        protected virtual void OnStateExit()
        {
        }
    }
}