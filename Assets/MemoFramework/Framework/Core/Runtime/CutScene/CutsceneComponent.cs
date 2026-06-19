using System;
using DG.Tweening;
using MemoFramework.GameState;
using UnityEngine;

namespace MemoFramework
{
    public class CutsceneComponent : MemoFrameworkComponent
    {
        public RectTransform CutsceneRoot => cutsceneRoot;
        [SerializeField] private RectTransform cutsceneRoot;
        [SerializeField] private CutsceneAgent defaultCutscene;

        private CutsceneAgent _currentCutscene;
        public bool IsPlaying { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (defaultCutscene)
            {
                SetCutSceneAgent(defaultCutscene);
            }
        }

        /// <summary>
        /// 设置用于播放Cutscene的Agent（需自行编写继承自CutsceneBase的Cutscene逻辑）
        /// </summary>
        /// <param name="cutscene"></param>
        public void SetCutSceneAgent(CutsceneAgent cutscene)
        {
            if (_currentCutscene)
            {
                // 如果不是默认的Cutscene，销毁之前的Cutscene
                if (_currentCutscene != defaultCutscene)
                {
                    Destroy(_currentCutscene.CutsceneView.gameObject);
                }
                else
                {
                    _currentCutscene.CutsceneView.gameObject.SetActive(false);
                }

                IsPlaying = false;
            }

            _currentCutscene = cutscene;
            Vector3 scale = cutscene.CutsceneView.localScale;
            cutscene.CutsceneView.SetParent(cutsceneRoot);
            cutscene.CutsceneView.localScale = scale;
            cutscene.CutsceneView.gameObject.SetActive(false);
        }

        public void DefaultCutscene()
        {
            if (defaultCutscene)
            {
                SetCutSceneAgent(defaultCutscene);
            }
            else
            {
                Debug.LogWarning("未设置默认Cutscene");
            }
        }


        /// <summary>
        /// 开始播放Cutscene，当Cutscene进入动画播放完毕之后调用onEnd
        /// </summary>
        /// <param name="onEnd"></param>
        /// <exception cref="MFException"></exception>
        public void EnterCutScene(float duration,Action onEnd = null)
        {
            if (_currentCutscene is null)
            {
                throw new MFException("未设置Cutscene");
            }

            if (IsPlaying)
            {
                Debug.LogError("已经调用过EnterCutScene，请先调用FadeCutScene");
                return;
            }

            _currentCutscene.CutsceneView.gameObject.SetActive(true);
            IsPlaying = true;
            _currentCutscene.EnterCutscene(duration,onEnd);
            _currentCutscene.IsPlaying = true;
        }

        /// <summary>
        /// 开始褪去Cutscene，当Cutscene退出动画播放完毕之后调用onEnd
        /// </summary>
        /// <param name="onEnd"></param>
        /// <exception cref="MFException"></exception>
        public void FadeCutScene(float duration,Action onEnd = null)
        {
            if (_currentCutscene is null)
            {
                throw new MFException("未设置Cutscene");
            }

            if (!IsPlaying)
            {
                Debug.LogError("在调用FadeCutScene之前，请先调用EnterCutScene");
                return;
            }

            if (!MemoFrameworkEntry.GetComponent<GameStateComponent>()) return;

            onEnd += () => { _currentCutscene.CutsceneView.gameObject.SetActive(false); };
            IsPlaying = false;
            _currentCutscene.FadeCutscene(duration,onEnd);
            _currentCutscene.IsPlaying = false;
        }
    }
}