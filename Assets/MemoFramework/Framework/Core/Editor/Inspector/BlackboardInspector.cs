//这个文件如果有bug全都是AI干的

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MemoFramework
{
    [CustomEditor(typeof(BlackboardComponent))]
    public class BlackboardInspector : Editor
    {
        private FieldInfo _intDictField;
        private FieldInfo _floatDictField;
        private FieldInfo _stringDictField;
        private FieldInfo _boolDictField;

        private void OnEnable()
        {
            // 获取私有字段
            _intDictField =
                typeof(BlackboardComponent).GetField("_intDict", BindingFlags.NonPublic | BindingFlags.Instance);
            _floatDictField =
                typeof(BlackboardComponent).GetField("_floatDict", BindingFlags.NonPublic | BindingFlags.Instance);
            _stringDictField =
                typeof(BlackboardComponent).GetField("_stringDict", BindingFlags.NonPublic | BindingFlags.Instance);
            _boolDictField =
                typeof(BlackboardComponent).GetField("_boolDict", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("此检视窗口仅在PlayMode下工作。", MessageType.Info);
                return;
            }

            BlackboardComponent blackboard = (BlackboardComponent)target;
            DrawIntDictionary(blackboard);
            DrawFloatDictionary(blackboard);
            DrawStringDictionary(blackboard);
            DrawBoolDictionary(blackboard);
        }

        private void DrawIntDictionary(BlackboardComponent blackboard)
        {
            GUILayout.Label("Int", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // 通过反射获取字典
            Dictionary<string, int> intDict = _intDictField.GetValue(blackboard) as Dictionary<string, int>;
            if (intDict != null)
            {
                // 创建键的副本以避免遍历时修改集合
                List<string> keys = new List<string>(intDict.Keys);
                if (keys.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前IntDict为空", MessageType.Info);
                }

                // 显示并允许编辑现有的键值对
                foreach (var key in keys)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(key, GUILayout.Width(120));
                    int value = intDict[key];
                    // 使用LabelField代替IntField，使其不可编辑
                    EditorGUILayout.LabelField(value.ToString(), GUILayout.Width(120));

                    // if (GUILayout.Button("删除", GUILayout.Width(50)))
                    // {
                    //     intDict.Remove(key);
                    // }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFloatDictionary(BlackboardComponent blackboard)
        {
            GUILayout.Label("Float", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // 通过反射获取字典
            Dictionary<string, float> floatDict = _floatDictField.GetValue(blackboard) as Dictionary<string, float>;
            if (floatDict != null)
            {
                // 创建键的副本以避免遍历时修改集合
                List<string> keys = new List<string>(floatDict.Keys);
                if (keys.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前FloatDict为空", MessageType.Info);
                }

                // 显示并允许编辑现有的键值对
                foreach (var key in keys)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(key, GUILayout.Width(120));
                    float value = floatDict[key];
                    // 使用LabelField代替FloatField，使其不可编辑
                    EditorGUILayout.LabelField(value.ToString(), GUILayout.Width(120));

                    // if (GUILayout.Button("删除", GUILayout.Width(50)))
                    // {
                    //     floatDict.Remove(key);
                    // }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawStringDictionary(BlackboardComponent blackboard)
        {
            GUILayout.Label("String", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // 通过反射获取字典
            Dictionary<string, string> stringDict = _stringDictField.GetValue(blackboard) as Dictionary<string, string>;
            if (stringDict != null)
            {
                // 创建键的副本以避免遍历时修改集合
                List<string> keys = new List<string>(stringDict.Keys);
                if (keys.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前StringDict为空", MessageType.Info);
                }

                // 显示并允许编辑现有的键值对
                foreach (var key in keys)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(key, GUILayout.Width(120));
                    string value = stringDict[key];
                    // 使用LabelField代替TextField，使其不可编辑
                    EditorGUILayout.LabelField(value, GUILayout.Width(120));

                    // if (GUILayout.Button("删除", GUILayout.Width(50)))
                    // {
                    //     stringDict.Remove(key);
                    // }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawBoolDictionary(BlackboardComponent blackboard)
        {
            GUILayout.Label("Bool", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // 通过反射获取字典
            Dictionary<string, bool> boolDict = _boolDictField.GetValue(blackboard) as Dictionary<string, bool>;
            if (boolDict != null)
            {
                // 创建键的副本以避免遍历时修改集合
                List<string> keys = new List<string>(boolDict.Keys);
                if (keys.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前BoolDict为空", MessageType.Info);
                }

                // 显示并允许编辑现有的键值对
                foreach (var key in keys)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(key, GUILayout.Width(120));
                    bool value = boolDict[key];
                    // 使用LabelField代替Toggle，使其不可编辑
                    EditorGUILayout.LabelField(value ? "True" : "False", GUILayout.Width(120));

                    // if (GUILayout.Button("删除", GUILayout.Width(50)))
                    // {
                    //     boolDict.Remove(key);
                    // }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}