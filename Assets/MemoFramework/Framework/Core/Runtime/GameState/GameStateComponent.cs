using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MemoFramework.GameState
{
    public class GameStateComponent : MemoFrameworkComponent
    {
        [SerializeField] private string m_LauncherTypeName = null;
        public StateMachine<string> GameStateFsm { get; private set; }
        public bool IsGameStateFsmRunning { get; private set; }
        public string GameState => IsGameStateFsmRunning ? GameStateFsm.ActiveStateName : string.Empty;
        private Dictionary<string, object> _blackboard;

        private IEnumerator Start()
        {
            _blackboard = new();
            IsGameStateFsmRunning = false;
            GameStateFsm = new StateMachine<string>();
            Type launcherType = MFUtils.Assembly.GetType(m_LauncherTypeName);
            MFLauncher launch = (MFLauncher)Activator.CreateInstance(launcherType);
            launch.InitGameStatesFsm(this);
            yield return new WaitForEndOfFrame();
            Debug.Log("MF GameState Run...");
            GameStateFsm.Init();
            IsGameStateFsmRunning = true;
        }

        private void Update()
        {
            if (IsGameStateFsmRunning)
            {
                GameStateFsm.OnLogic();
            }
        }

        private void OnDestroy()
        {
            if (IsGameStateFsmRunning)
            {
                GameStateFsm.OnExit();
            }
#if DOTWEEN
            DOTween.KillAll();
            var dotween = GameObject.Find("[DOTween]");
            if (dotween) Destroy(dotween);
#endif
        }


        public void PushGameState(string stateName, GameStateBase gameState)
        {
            if (IsGameStateFsmRunning)
            {
                throw new MFException("游戏状态机已经运行，无法添加新的状态，请在游戏状态机初始化之前添加状态");
            }

            gameState.GameStateComponent = this;
            GameStateFsm.AddState(stateName, gameState);
        }

        public void SetAsStartState(string stateName)
        {
            if (IsGameStateFsmRunning)
            {
                throw new MFException("游戏状态机已经运行，无法设置初始状态，请在游戏状态机初始化之前设置初始状态");
            }

            GameStateFsm.SetStartState(stateName);
        }

        public void AddTransition(string from, string to, Func<bool> condition)
        {
            GameStateFsm.AddTransition(from, to, _ => condition());
        }

        public void RequestStateChange(string stateName, bool forceInstant = false)
        {
            GameStateFsm.RequestStateChange(stateName, forceInstant);
        }

        public void SetValue<T>(string valueName, T value)
        {
            _blackboard[valueName] = value;
        }

        public bool HasValue(string valueName)
        {
            return _blackboard.ContainsKey(valueName);
        }

        public bool TryGetValue<T>(string valueName, out T value)
        {
            if (!HasValue(valueName))
            {
                value = default;
                return false;
            }

            value = (T)_blackboard[valueName];
            return true;
        }

        public T GetValue<T>(string valueName)
        {
            if (!TryGetValue(valueName, out T value))
            {
                throw new MFException($"无法获取名为{valueName}的值");
            }

            return value;
        }
    }
}