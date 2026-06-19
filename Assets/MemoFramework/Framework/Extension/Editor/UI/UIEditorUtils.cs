using System;
using UnityEditor;
using UnityEngine;

namespace MemoFramework.UI
{
    public static partial class UIEditorUtils
    {
        public static GameObject CreateUIObject(string name, GameObject parent, params Type[] components)
        {
            GameObject go = ObjectFactory.CreateGameObject(name, components);
            SetParentAndAlign(go, parent);
            return go;
        }

        public static GameObject CreateUIElementRoot(string name, Vector2 size, params Type[] components)
        {
            GameObject child = ObjectFactory.CreateGameObject(name, components);
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            return child;
        }

        public static GameObject CreateUIElementRoot(string name, Vector2 size)
        {
            GameObject root;

            root = ObjectFactory.CreateGameObject(name, typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            return root;
        }

        public static GameObject CreateUIObject(string name, GameObject parent)
        {
            GameObject go;
            go = ObjectFactory.CreateGameObject(name, typeof(RectTransform));

            SetParentAndAlign(go, parent);
            return go;
        }
    }
}