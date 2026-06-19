using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.Debugger
{
    public class SettingTab : TabBase
    {
        [SerializeField] private Text _scaleText;
        [SerializeField] private Text _entryScaleText;
        [SerializeField] private Text _allowDragText;
        [SerializeField] private Text _dragProtectText;
        [Header("Entry Anchor")] private Color _anchorDisableColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] List<Button> _anchorButtons = new List<Button>();

        private DebuggerComponent _debuggerComponent;

        public override void OnRegister()
        {
            _debuggerComponent = MemoFrameworkEntry.GetComponent<DebuggerComponent>();
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
            _allowDragText.text = MFUtils.Text.Format("允许拖拽：{0}", _debuggerComponent.AllowDrag ? "√" : "×");
            _dragProtectText.text = MFUtils.Text.Format("防止拖出屏幕：{0}", _debuggerComponent.DragProtect ? "√" : "×");
            // 注册锚点按钮事件
            UpdateAnchorButtonImg(_debuggerComponent.EntryAnchor);
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                var button = GetAnchorButtonByIndex(i);
                if (button == null) continue;
                button.onClick.AddListener(() =>
                {
                    _debuggerComponent.EntryAnchor = index;
                    UpdateAnchorButtonImg(_debuggerComponent.EntryAnchor);
                });
            }
        }

        public override void OnUnregister()
        {
        }

        public override void OnSelect()
        {
        }

        public override void OnDeselect()
        {
        }

        public override void OnUpdate()
        {
        }

        #region Scale

        public void AddScale0_1()
        {
            _debuggerComponent.Scale += 0.1f;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void MinusScale0_1()
        {
            _debuggerComponent.Scale -= 0.1f;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void SetScaleTo1()
        {
            _debuggerComponent.Scale = 1;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void SetScaleTo1_5()
        {
            _debuggerComponent.Scale = 1.5f;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void SetScaleTo2()
        {
            _debuggerComponent.Scale = 2;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void SetScaleTo3()
        {
            _debuggerComponent.Scale = 3;
            _scaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.Scale);
        }

        public void ChangeAllowDrag()
        {
            _debuggerComponent.AllowDrag = !_debuggerComponent.AllowDrag;
            _allowDragText.text = MFUtils.Text.Format("允许拖拽：{0}", _debuggerComponent.AllowDrag ? "√" : "×");
        }

        public void ChangeDragProtect()
        {
            _debuggerComponent.DragProtect = !_debuggerComponent.DragProtect;
            _dragProtectText.text = MFUtils.Text.Format("防止拖出屏幕：{0}", _debuggerComponent.DragProtect ? "√" : "×");
        }

        //TODO:
        public void FullScreen()
        {
        }
        
        public void EntryAddScale0_1()
        {
            _debuggerComponent.EntryScale += 0.1f;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        public void EntryMinusScale0_1()
        {
            _debuggerComponent.EntryScale -= 0.1f;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        public void EntrySetScaleTo1()
        {
            _debuggerComponent.EntryScale = 1;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        public void EntrySetScaleTo1_5()
        {
            _debuggerComponent.EntryScale = 1.5f;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        public void EntrySetScaleTo2()
        {
            _debuggerComponent.EntryScale = 2;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        public void EntrySetScaleTo3()
        {
            _debuggerComponent.EntryScale = 3;
            _entryScaleText.text = MFUtils.Text.Format("当前尺寸：{0}", _debuggerComponent.EntryScale);
        }

        #endregion
       
        private void UpdateAnchorButtonImg(int index)
        {
            for (int i = 0; i < 9; i++)
            {
                var button = GetAnchorButtonByIndex(i);
                if (button == null) continue;
                button.image.color = i == index ? Color.white : _anchorDisableColor;
            }
        }

        private Button GetAnchorButtonByIndex(int index)
        {
            if (index < 4 && index >= 0) return _anchorButtons[index];
            if (index > 4 && index < 9) return _anchorButtons[index - 1];
            return null;
        }
    }
}