using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MemoFramework.GameState;
using UnityEditor;
using UnityEngine;

namespace MemoFramework
{
    [CustomEditor(typeof(GameStateComponent))]
    public class GameStateInspector : Editor
    {
        private bool m_IsCompiling = false;
        private SerializedProperty m_EntranceProcedureTypeName = null;

        private string[] m_LauncherTypeNames = null;
        private int m_EntranceProcedureIndex = -1;

        private void OnEnable()
        {
            m_EntranceProcedureTypeName = serializedObject.FindProperty("m_LauncherTypeName");

            RefreshTypeNames();
        }

        /// <summary>
        /// 编译开始事件。
        /// </summary>
        protected virtual void OnCompileStart()
        {
        }

        /// <summary>
        /// 编译完成事件。
        /// </summary>
        protected virtual void OnCompileComplete()
        {
            RefreshTypeNames();
        }

        protected virtual void PaintGUI()
        {
            serializedObject.Update();

            GameStateComponent t = (GameStateComponent)target;

            if (string.IsNullOrEmpty(m_EntranceProcedureTypeName.stringValue) && m_LauncherTypeNames.Length > 0)
            {
                EditorGUILayout.HelpBox("please choose a launcher type.", MessageType.Error);
            }

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
            {
                if (m_LauncherTypeNames.Length > 0)
                {
                    EditorGUILayout.Separator();

                    int selectedIndex =
                        EditorGUILayout.Popup("Launcher", m_EntranceProcedureIndex, m_LauncherTypeNames);
                    if (selectedIndex != m_EntranceProcedureIndex)
                    {
                        m_EntranceProcedureIndex = selectedIndex;
                        m_EntranceProcedureTypeName.stringValue = m_LauncherTypeNames[selectedIndex];
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("There is not launcher valid.", MessageType.Error);
                }
            }
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            Repaint();
        }

        public override void OnInspectorGUI()
        {
            if (m_IsCompiling && !EditorApplication.isCompiling)
            {
                m_IsCompiling = false;
                OnCompileComplete();
                return;
            }
            else if (!m_IsCompiling && EditorApplication.isCompiling)
            {
                m_IsCompiling = true;
                OnCompileStart();
                return;
            }

            PaintGUI();
        }

        private void RefreshTypeNames()
        {
            m_LauncherTypeNames = GetRuntimeTypeNames(typeof(MFLauncher));
            if (!string.IsNullOrEmpty(m_EntranceProcedureTypeName.stringValue))
            {
                m_EntranceProcedureIndex =
                    m_LauncherTypeNames.ToList().IndexOf(m_EntranceProcedureTypeName.stringValue);
                if (m_EntranceProcedureIndex < 0)
                {
                    m_EntranceProcedureTypeName.stringValue = null;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Static

        private static readonly string[] RuntimeAssemblyNames =
        {
#if UNITY_2017_3_OR_NEWER
            "UnityGameFramework.Runtime",
#endif
            "Assembly-CSharp",
        };

        private static readonly string[] RuntimeOrEditorAssemblyNames =
        {
#if UNITY_2017_3_OR_NEWER
            "UnityGameFramework.Runtime",
#endif
            "Assembly-CSharp",
#if UNITY_2017_3_OR_NEWER
            "UnityGameFramework.Editor",
#endif
            "Assembly-CSharp-Editor",
        };

        internal static string[] GetRuntimeTypeNames(System.Type typeBase)
        {
            return GetTypeNames(typeBase, RuntimeAssemblyNames);
        }

        /// <summary>
        /// 在运行时或编辑器程序集中获取指定基类的所有子类的名称。
        /// </summary>
        /// <param name="typeBase">基类类型。</param>
        /// <returns>指定基类的所有子类的名称。</returns>
        internal static string[] GetRuntimeOrEditorTypeNames(System.Type typeBase)
        {
            return GetTypeNames(typeBase, RuntimeOrEditorAssemblyNames);
        }

        private static string[] GetTypeNames(System.Type typeBase, string[] assemblyNames)
        {
            List<string> typeNames = new List<string>();
            foreach (string assemblyName in assemblyNames)
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly.Load(assemblyName);
                }
                catch
                {
                    continue;
                }

                if (assembly == null)
                {
                    continue;
                }

                System.Type[] types = assembly.GetTypes();
                foreach (System.Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                    {
                        typeNames.Add(type.FullName);
                    }
                }
            }

            typeNames.Sort();
            return typeNames.ToArray();
        }

        #endregion
    }
}