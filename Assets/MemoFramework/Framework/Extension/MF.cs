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
            Base = GetOrAdd<BaseComponent>();
            Event = GetOrAdd<EventComponent>();
            Input = GetOrAdd<InputComponent>();
            ObjectPool = GetOrAdd<ObjectPoolComponent>();
            GameState = GetOrAdd<GameStateComponent>();
            Cutscene = GetOrAdd<CutsceneComponent>();
            Blackboard = GetOrAdd<BlackboardComponent>();
        }

        /// <summary>
        /// 获取或创建 MemoFramework 组件。
        /// 找不到时在 MF 根物体下新建子物体并挂载组件，
        /// 组件 Awake 时自动注册到 <see cref="MemoFrameworkEntry"/>。
        /// </summary>
        private T GetOrAdd<T>() where T : MemoFrameworkComponent
        {
            T component = MemoFrameworkEntry.GetComponent<T>();
            if (component != null)
                return component;

            GameObject child = new GameObject(typeof(T).Name);
            child.transform.SetParent(transform);
            return child.AddComponent<T>();
        }
    }
}