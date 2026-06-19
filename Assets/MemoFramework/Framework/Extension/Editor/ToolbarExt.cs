using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemoFramework.Extension
{
    /// <summary>
    /// 编辑器主工具栏扩展，提供快捷启动入口场景的功能。
    /// 使用 Unity 6 官方 UnityEditor.Toolbars API 注册工具栏元素。
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarExt
    {
        private const string k_LaunchToolbarElementPath = "MemoFramework/Launch";

        /// <summary>
        /// 在主工具栏中部停靠区创建"Launch"按钮，用于启动入口场景。
        /// 中部停靠区为 Play 按钮所在区域；defaultDockIndex 取 101（内置元素保留 ≤100），
        /// 使按钮紧贴内置元素右侧，尽可能靠近 Play 按钮簇。
        /// 按钮在运行模式下禁用，播放模式切换时通过 MainToolbar.Refresh 刷新启用状态。
        /// </summary>
        /// <returns>主工具栏元素描述符。</returns>
        [MainToolbarElement(k_LaunchToolbarElementPath,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 101)]
        public static MainToolbarElement OnLaunchToolbarElement()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Launch", "启动入口场景"),
                () => SceneHelper.StartScene(SceneHelper.EntryScenePath))
            {
                enabled = !EditorApplication.isPlaying,
            };
        }

        static ToolbarExt()
        {
            // 注册播放模式状态变更回调，切换时刷新按钮启用状态
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// 播放模式状态变化时刷新工具栏元素，使启用状态与当前播放状态保持一致。
        /// </summary>
        /// <param name="state">播放模式状态变化信息。</param>
        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            MainToolbar.Refresh(k_LaunchToolbarElementPath);
        }

        /// <summary>
        /// 场景启动辅助类，负责在播放模式下加载入口场景。
        /// </summary>
        private static class SceneHelper
        {
            public static readonly string EntryScenePath = "Assets/Res/Scene/Launcher.unity";
            private const string UnityEditorSceneToOpenKey = "UnityEditorSceneToOpen";

            /// <summary>
            /// 进入播放模式前记录待加载场景路径，在场景加载前触发加载。
            /// </summary>
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            private static void OnBeforeSceneLoad()
            {
                if (EditorPrefs.HasKey(UnityEditorSceneToOpenKey))
                {
                    string scenePath = EditorPrefs.GetString(UnityEditorSceneToOpenKey);
                    if (!SceneManager.GetActiveScene().path.Equals(scenePath))
                    {
                        SceneManager.LoadScene(scenePath);
                    }
                }
            }

            /// <summary>
            /// 播放模式场景加载完成后清理临时记录的场景路径。
            /// </summary>
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void OnAfterSceneLoad()
            {
                if (EditorPrefs.HasKey(UnityEditorSceneToOpenKey))
                {
                    EditorPrefs.DeleteKey(UnityEditorSceneToOpenKey);
                }
            }

            /// <summary>
            /// 记录入口场景路径并进入播放模式，由运行时回调完成实际场景加载。
            /// </summary>
            /// <param name="scenePathName">要启动的场景路径。</param>
            public static void StartScene(string scenePathName)
            {
                if (EditorApplication.isPlaying)
                {
                    return;
                }

                EditorPrefs.SetString(UnityEditorSceneToOpenKey, scenePathName);
                EditorApplication.isPlaying = true;
            }
        }
    }
}
