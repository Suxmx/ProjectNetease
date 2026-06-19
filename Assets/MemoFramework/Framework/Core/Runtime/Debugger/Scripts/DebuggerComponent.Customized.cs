using System.Collections.Generic;
using MemoFramework.Debugger;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework
{
    public partial class DebuggerComponent
    {
        private static List<string> s_builtInTabs = new List<string>()
        {
            "Console",
            "Setting",
            "Information",
            "RefPool"
            
        };

        [SerializeField] private GameObject _defaultTabEntryPrefab;

        private RectTransform _tabEntriesRoot;
        private RectTransform _tabsRoot;
        private RectTransform _panel;
        private Text _rightHeaderTitle;

        /// <summary>
        /// 注册一个自定义的Tab，Tab的Prefab将会被实例化并显示在Tab栏中，Tab入口为默认的TitleTabEntry
        /// Tab 预制体上需要实现ICustomTab接口的脚本
        /// </summary>
        /// <param name="title">Tab标题</param>
        /// <param name="tabPrefab">Tab预制体</param>
        /// <param name="args">Tab参数</param>
        public void RegisterTab(string title, GameObject tabPrefab, object args = null)
        {
            RegisterTab(title, tabPrefab, _defaultTabEntryPrefab, args);
        }

        /// <summary>
        /// 册一个自定义的Tab，Tab的Prefab将会被实例化并显示在Tab栏中，Tab入口可以通过接口与Prefab自定义
        /// Tab入口Prefab需要实现ICustomTabEntry接口，Tab需要实现ICustomTab接口
        /// </summary>
        /// <param name="title">Tab标题</param>
        /// <param name="tabPrefab">Tab预制体</param>
        /// <param name="tabEntryPrefab">自定义Tab入口Prefab</param>
        /// /// <param name="args">Tab参数</param>
        public void RegisterTab(string title, GameObject tabPrefab, GameObject tabEntryPrefab, object args = null)
        {
            if (ContainsTab(title))
            {
                Debug.LogWarning($"已经注册过标题为{title}的Tab！");
                return;
            }

            // 实例化TabEntry
            GameObject tabEntryObj = Instantiate(tabEntryPrefab, _tabEntriesRoot);
            if (!tabEntryObj.TryGetComponent<TabEntryBase>(out var tabEntry))
            {
                Debug.LogError("TabEntry预制体上未找到TabEntryBase组件！");
                return;
            }

            // 实例化Tab
            GameObject tabObj = Instantiate(tabPrefab, _tabsRoot);
            if (!tabObj.TryGetComponent<TabBase>(out var tab))
            {
                Debug.LogError("Tab预制体上未找到TabBase组件！");
                return;
            }

            // 初始化Tab和TabEntry
            TabInfo tabInfo = new TabInfo()
            {
                Title = title,
                Tab = tab,
                TabEntry = tabEntry,
                Args = args
            };
            tab.TabInfo = tabInfo;
            tabEntry.TabInfo = tabInfo;
            var tabEntryBtn = tabEntry.GetComponent<Button>();
            if (tabEntryBtn != null)
            {
                tabEntryBtn.onClick.AddListener(() => SelectTab(title));
            }
            tabEntry.OnRegister();
            tab.OnRegister();
            tabObj.SetActive(false);
            // 添加到Tab列表
            _tabs.AddLast(tabInfo);
        }

        public void UnregisterTab(string title)
        {
            if (!ContainsTab(title))
            {
                Debug.LogWarning("未找到标题为" + title + "的Tab！");
                return;
            }

            if (s_builtInTabs.Contains(title))
            {
                Debug.LogWarning("内置Tab不允许注销！");
                return;
            }
            // 料理后事
            LinkedListNode<TabInfo> targetNode = default;
            
            for (var node = _tabs.First; node != null; node = node.Next)
            {
                if (node.Value.Title == title)
                {
                    targetNode = node;
                    break;
                }
            }
            if(targetNode is null) throw new MFException("未找到标题为" + title + "的Tab！");
            TabInfo targetInfo=targetNode.Value;
            // 如果注销的是当前选中的Tab，选中下一个或者上一个Tab
            if (_currentSelection == targetInfo)
            {
                var next = targetNode.Next ?? targetNode.Previous;
                if(next is null) throw new MFException("未找到下一个或者上一个Tab！");
                SelectTab(next.Value.Title);
            }
            // 防止更新过程中修改链表导致错误
            if (_cachedNode != null && _cachedNode.Value.Title == title)
            {
                _cachedNode = _cachedNode.Next;
            }
            // 注销Tab
            targetInfo.Tab.OnUnregister();
            targetInfo.TabEntry.OnUnregister();
            Destroy(targetInfo.Tab.gameObject);
            Destroy(targetInfo.TabEntry.gameObject);
            _tabs.Remove(targetNode);
        }
    }
}