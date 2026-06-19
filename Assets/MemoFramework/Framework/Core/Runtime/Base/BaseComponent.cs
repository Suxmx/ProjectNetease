using System;
using UnityEngine;

namespace MemoFramework
{
    /// <summary>
    /// 基础组件，用于掌控游戏的基础设置
    /// </summary>
    public class BaseComponent : MemoFrameworkComponent
    {
        public int MaxFrameRate
        {
            get => Application.targetFrameRate;
            set => Application.targetFrameRate = value;
        }

        public float GameSpeed
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            MaxFrameRate = 165;
            GameSpeed = 1;
        }
    }
}