using System;
using UnityEngine;

namespace MemoFramework
{
    public abstract class CutsceneAgent : MonoBehaviour
    {
        /// <summary>
        /// 该Cutscene控制的View
        /// </summary>
        public abstract Transform CutsceneView { get; }

        public bool IsPlaying { get; set; }

        /// <summary>
        /// 控制进入Cutscene，Cutscene进入完之后调用onEnd
        /// </summary>
        /// <param name="onEnd">当Cutscene播放完毕之后调用</param>
        public abstract void EnterCutscene(float duration,Action onEnd);

        private void Update()
        {
            if (IsPlaying)
            {
                OnUpdate();
            }
        }

        protected virtual void OnUpdate()
        {
        }

        /// <summary>
        /// 控制退出Cutscene，Cutscene退出完之后调用onEnd
        /// </summary>
        /// <param name="onEnd">当Cutscene播放完毕之后调用</param>
        public abstract void FadeCutscene(float duration,Action onEnd);
    }
}