using System;
using System.Collections.Generic;
using MemoFramework.Debugger;
using UnityEngine;

namespace MemoFramework
{
    public partial class DebuggerComponent : MemoFrameworkComponent
    {
        [SerializeField] private GameObject _debuggerPanelCanvas;
        [SerializeField] private GameObject _consoleTab;
        [SerializeField] private GameObject _informationTab;
        [SerializeField] private GameObject _settingTab;
        [SerializeField] private GameObject _refPoolTab;

        private MFLinkedList<TabInfo> _tabs = new MFLinkedList<TabInfo>();
        private LinkedListNode<TabInfo> _cachedNode;
        private TabInfo _currentSelection;
        private DebuggerEntry _debuggerEntry;

        protected override void Awake()
        {
            base.Awake();
            var canvas = Instantiate(_debuggerPanelCanvas,transform);
            DebuggerPanel panel = canvas.GetComponentInChildren<DebuggerPanel>();
            _panel = panel.GetComponent<RectTransform>();
            if (panel is null)
            {
                throw new MFException("DebuggerPanel初始化错误！_debuggerPanelCanvas子物体中未找到DebuggerPanel组件！");
            }
            _debuggerEntry = canvas.GetComponentInChildren<DebuggerEntry>();
            _debuggerEntry.DebuggerComponent = this;
            if (_debuggerEntry is null)
            {
                throw new MFException("DebuggerEntry初始化错误！_debuggerPanelCanvas子物体中未找到DebuggerEntry组件！");
            }
            panel.CloseBtn.onClick.AddListener(CloseDebuggerPanel);
            _tabEntriesRoot = panel.TabEntriesRoot;
            _tabsRoot = panel.TabsRoot;
            _rightHeaderTitle = panel.RightHeaderTitle;
            LoadSettings();
            // 注册内置Tab
            RegisterTab("Information",_informationTab);
            RegisterTab("Console", _consoleTab);
            RegisterTab("Setting", _settingTab);
            RegisterTab("RefPool", _refPoolTab);

            SelectTab("Information");
            CloseDebuggerPanel();
        }

        private void OnDestroy()
        {
            foreach (var t in _tabs)
            {
                t.TabEntry.OnUnregister();
                t.Tab.OnUnregister();
            }
        }

        private void Update()
        {
            // 防止更新过程中修改链表
            LinkedListNode<TabInfo> current = _tabs.First;
            while (current != null)
            {
                _cachedNode = current.Next;
                current.Value.Tab.OnUpdate();
                current.Value.TabEntry.OnUpdate();
                current = _cachedNode;
                _cachedNode = null;
            }
        }

        public bool ContainsTab(string title)
        {
            foreach (var tab in _tabs)
            {
                if (tab.Title == title)
                {
                    return true;
                }
            }

            return false;
        }

        public TabInfo GetTab(string title)
        {
            foreach (var tab in _tabs)
            {
                if (tab.Title == title)
                {
                    return tab;
                }
            }

            return null;
        }

        public void SelectTab(string title)
        {
            if (!ContainsTab(title))
            {
                Debug.LogWarning("未找到标题为" + title + "的Tab！");
                return;
            }

            if (_currentSelection != null && title == _currentSelection.Title) return;
            // 取消当前选中
            if (_currentSelection != null)
            {
                _currentSelection.Selected = false;
                _currentSelection.TabEntry.OnDeselect();
                _currentSelection.Tab.OnDeselect();
                _currentSelection.Tab.gameObject.SetActive(false);
            }

            // 选中新的
            _currentSelection = GetTab(title);
            _currentSelection.Tab.gameObject.SetActive(true);
            _currentSelection.Selected = true;
            _currentSelection.TabEntry.OnSelect();
            _currentSelection.Tab.OnSelect();
            _rightHeaderTitle.text = title;
        }

        public void OpenDebuggerPanel()
        {
            _debuggerEntry.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
        }
        
        public void CloseDebuggerPanel()
        {
            _debuggerEntry.gameObject.SetActive(true);
            _panel.gameObject.SetActive(false);
        }
    }
}