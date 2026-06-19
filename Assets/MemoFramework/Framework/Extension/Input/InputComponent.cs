using System.Collections.Generic;
using M2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace MemoFramework.Extension
{
    public class InputComponent : MemoFrameworkComponent
    {
        public MainInputs InputMap { get; private set; }
        private MainInputs.GamePlayActions _gameplayActions { get; set; }
        private MainInputs.UIActions _uiActions { get; set; }

        private Vector2 m_LastFrameMoveInput;
        private Dictionary<InputEvent, InputAction> _eventActionDict;
        private Dictionary<UIInputEvent, InputAction> _uiEventActionDict;

        protected override void Awake()
        {
            base.Awake();
            InputMap = new MainInputs();
            InputMap.Enable();
            _gameplayActions = InputMap.GamePlay;
            _uiActions = InputMap.UI;

            _eventActionDict = new()
            {
                { InputEvent.Shoot, _gameplayActions.Shoot }
            };
            _uiEventActionDict = new()
            {
                { UIInputEvent.UIReturn, _uiActions.Return },
                { UIInputEvent.ViewStat, _uiActions.PlayerStat }
            };
        }

        private void Update()
        {
            m_LastFrameMoveInput = InputData.MoveInput;

            InputEvent endEvent = InputEvent.None;
            UIInputEvent endUIEvent = UIInputEvent.None;
            // 处理普通输入事件
            foreach (var pair in _eventActionDict)
            {
                if (InputData.HasEvent(pair.Key) && !(pair.Value.phase == InputActionPhase.Performed ||
                                                      pair.Value.phase == InputActionPhase.Started))
                {
                    endEvent |= pair.Key;
                }
            }

            // 处理UI输入事件
            foreach (var pair in _uiEventActionDict)
            {
                if (InputData.HasEvent(pair.Key) && !(pair.Value.phase == InputActionPhase.Performed ||
                                                      pair.Value.phase == InputActionPhase.Started))
                {
                    endUIEvent |= pair.Key;
                }
            }

            InputData.Clear();
            InputData.MouseScreenPosition = Mouse.current.position.ReadValue();
            CheckEventStart();
            CheckHasEvent();
            
           InputData.AddEventEnd(endEvent);
            InputData.AddEventEnd(endUIEvent);

            void CheckEventStart()
            {
                if (m_LastFrameMoveInput == Vector2.zero && _gameplayActions.Move.ReadValue<Vector2>() != Vector2.zero)
                {
                    InputData.AddEventStart(InputEvent.Move);
                }

                foreach (var pair in _eventActionDict)
                {
                    if (pair.Value.triggered)
                    {
                        InputData.AddEventStart(pair.Key);
                    }
                }

                foreach (var pair in _uiEventActionDict)
                {
                    if (pair.Value.triggered)
                    {
                        InputData.AddEventStart(pair.Key);
                    }
                }
            }

            void CheckHasEvent()
            {
                //Move
                if (_gameplayActions.Move.ReadValue<Vector2>() != Vector2.zero)
                {
                    InputData.AddEvent(InputEvent.Move);
                    InputData.MoveInput = _gameplayActions.Move.ReadValue<Vector2>();
                    InputData.RawInput.x = InputData.MoveInput.x > 0.1f ? 1 : InputData.MoveInput.x < -0.1f ? -1 : 0;
                    InputData.RawInput.y = InputData.MoveInput.y > 0.1f ? 1 : InputData.MoveInput.y < -0.1f ? -1 : 0;
                }

                foreach (var pair in _eventActionDict)
                {
                    if (pair.Value.phase == InputActionPhase.Performed ||
                        pair.Value.phase == InputActionPhase.Started)
                    {
                        InputData.AddEvent(pair.Key);
                    }
                }

                foreach (var pair in _uiEventActionDict)
                {
                    if (pair.Value.phase == InputActionPhase.Performed ||
                        pair.Value.phase == InputActionPhase.Started)
                    {
                        InputData.AddEvent(pair.Key);
                    }
                }
            }
            
        }

        #region Rebind

        public void StartRebind(string actionName, int bindingIndex,
            Text statusText, bool excludeMouse)
        {
            InputAction action = InputMap.asset.FindAction(actionName);
            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.Log("Couldn't find action or binding");
                return;
            }

            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isComposite)
                    DoRebind(action, bindingIndex, statusText, true, excludeMouse);
            }
            else
                DoRebind(action, bindingIndex, statusText, false, excludeMouse);
        }

        private void DoRebind(InputAction actionToRebind, int bindingIndex, Text statusText,
            bool allCompositeParts, bool excludeMouse)
        {
            if (actionToRebind == null || bindingIndex < 0)
                return;

            statusText.text = $"Press a {actionToRebind.expectedControlType}";

            actionToRebind.Disable();

            var rebind = actionToRebind.PerformInteractiveRebinding(bindingIndex);

            rebind.OnComplete(operation =>
            {
                actionToRebind.Enable();
                operation.Dispose();

                if (allCompositeParts)
                {
                    var nextBindingIndex = bindingIndex + 1;
                    if (nextBindingIndex < actionToRebind.bindings.Count &&
                        actionToRebind.bindings[nextBindingIndex].isComposite)
                        DoRebind(actionToRebind, nextBindingIndex, statusText, allCompositeParts, excludeMouse);
                }

                SaveBindingOverride(actionToRebind);
                // rebindComplete?.Invoke();
            });

            rebind.OnCancel(operation =>
            {
                actionToRebind.Enable();
                operation.Dispose();

                // rebindCanceled?.Invoke();
            });

            rebind.WithCancelingThrough("<Keyboard>/escape");

            if (excludeMouse)
                rebind.WithControlsExcluding("Mouse");

            // rebindStarted?.Invoke(actionToRebind, bindingIndex);
            rebind.Start(); //actually starts the rebinding process
        }

        public string GetBindingName(string actionName, int bindingIndex)
        {
            InputAction action = InputMap.asset.FindAction(actionName);
            return action.GetBindingDisplayString(bindingIndex);
        }

        private void SaveBindingOverride(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                PlayerPrefs.SetString(action.actionMap + action.name + i, action.bindings[i].overridePath);
            }
        }

        public void LoadBindingOverride(string actionName)
        {
            InputAction action = InputMap.asset.FindAction(actionName);

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(action.actionMap + action.name + i)))
                    action.ApplyBindingOverride(i, PlayerPrefs.GetString(action.actionMap + action.name + i));
            }
        }

        public void ResetBinding(string actionName, int bindingIndex)
        {
            InputAction action = InputMap.asset.FindAction(actionName);

            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.Log("Could not find action or binding");
                return;
            }

            if (action.bindings[bindingIndex].isComposite)
            {
                for (int i = bindingIndex; i < action.bindings.Count && action.bindings[i].isComposite; i++)
                    action.RemoveBindingOverride(i);
            }
            else
                action.RemoveBindingOverride(bindingIndex);

            SaveBindingOverride(action);
        }

        #endregion
    }
}