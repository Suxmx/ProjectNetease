using System;
using MemoFramework.GameState;
using UnityEngine;

namespace MemoFramework.Extension
{
    public class MF : MonoBehaviour
    {
        public static BaseComponent Base { get; private set; }
        public static EventComponent Event { get; private set; }
        public static InputComponent Input { get; private set; }
        public static ObjectPoolComponent ObjectPool { get; private set; }
        public static GameStateComponent GameState { get; private set; }
        public static CutsceneComponent Cutscene { get; private set; }
        public static BlackboardComponent Blackboard { get; private set; }

        private void Start()
        {
            if(transform.parent == null) DontDestroyOnLoad(gameObject);
            Base = MemoFrameworkEntry.GetComponent<BaseComponent>();
            Event = MemoFrameworkEntry.GetComponent<EventComponent>();
            Input = MemoFrameworkEntry.GetComponent<InputComponent>();
            ObjectPool = MemoFrameworkEntry.GetComponent<ObjectPoolComponent>();
            GameState = MemoFrameworkEntry.GetComponent<GameStateComponent>();
            Cutscene = MemoFrameworkEntry.GetComponent<CutsceneComponent>();
            Blackboard = MemoFrameworkEntry.GetComponent<BlackboardComponent>();
        }
    }
}