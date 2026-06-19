using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    /// <summary>
    /// Debugger入口
    /// </summary>
    public class DebuggerEntry : UnityEngine.UI.Button
    {
        // 多次点击打开
        private float _lastTap;
        private int _tapCount;
        public int RequiredTapCount = 3;

        public float ResetTime = 0.5f;

        // FPS
        private GameObject _fps;
        private Text _fpsText;
        private FpsCounter _fpsCounter = new FpsCounter(1);
        private bool _enableFps;

        // DebuggerComponent
        [NonSerialized] public DebuggerComponent DebuggerComponent;
        private RectTransform _rectTransform;

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - _lastTap > ResetTime)
            {
                _tapCount = 0;
            }

            _lastTap = Time.unscaledTime;
            _tapCount++;

            if (_tapCount == RequiredTapCount)
            {
                DebuggerComponent?.OpenDebuggerPanel();
                _tapCount = 0;
            }
        }

        public void EnableFps(bool enable)
        {
            _enableFps = enable;
            if (_fps is null)
            {
                _fps = transform.Find("Fps").gameObject;
                _fpsText = _fps.transform.Find("Fps").GetComponent<Text>();
            }

            _fps.SetActive(enable);
        }

        private void Update()
        {
            if (_enableFps)
            {
                _fpsCounter.Update(Time.deltaTime, Time.unscaledDeltaTime);
                _fpsText.text = _fpsCounter.CurrentFps.ToString("F1");
            }
        }

        public void SetAnchor(int anchor)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();

            switch (anchor)
            {
                case 0: // 左上
                    rectTransform.pivot = new Vector2(0, 1);
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.position = new Vector3(0, Screen.height);
                    break;

                case 1: // 中上
                    rectTransform.pivot = new Vector2(0.5f, 1);
                    rectTransform.anchorMin = new Vector2(0.5f, 1);
                    rectTransform.anchorMax = new Vector2(0.5f, 1);
                    rectTransform.position = new Vector3(Screen.width * 0.5f, Screen.height);
                    break;

                case 2: // 右上
                    rectTransform.pivot = new Vector2(1, 1);
                    rectTransform.anchorMin = new Vector2(1, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.position = new Vector3(Screen.width, Screen.height);
                    break;

                case 3: // 左中
                    rectTransform.pivot = new Vector2(0, 0.5f);
                    rectTransform.anchorMin = new Vector2(0, 0.5f);
                    rectTransform.anchorMax = new Vector2(0, 0.5f);
                    rectTransform.position = new Vector3(0, Screen.height * 0.5f);
                    break;

                case 4: // 中中
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f);
                    break;

                case 5: // 右中
                    rectTransform.pivot = new Vector2(1, 0.5f);
                    rectTransform.anchorMin = new Vector2(1, 0.5f);
                    rectTransform.anchorMax = new Vector2(1, 0.5f);
                    rectTransform.position = new Vector3(Screen.width, Screen.height * 0.5f);
                    break;

                case 6: // 左下
                    rectTransform.pivot = new Vector2(0, 0);
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(0, 0);
                    rectTransform.position = new Vector3(0, 0);
                    break;

                case 7: // 中下
                    rectTransform.pivot = new Vector2(0.5f, 0);
                    rectTransform.anchorMin = new Vector2(0.5f, 0);
                    rectTransform.anchorMax = new Vector2(0.5f, 0);
                    rectTransform.position = new Vector3(Screen.width * 0.5f, 0);
                    break;

                case 8: // 右下
                    rectTransform.pivot = new Vector2(1, 0);
                    rectTransform.anchorMin = new Vector2(1, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    rectTransform.position = new Vector3(Screen.width, 0);
                    break;

                default:
                    Debug.LogWarning("Invalid anchor value: " + anchor);
                    break;
            }
        }
        
    }
}