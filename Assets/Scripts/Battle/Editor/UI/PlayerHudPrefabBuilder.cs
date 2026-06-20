#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.Editor
{
    /// <summary>
    /// 把 PlayerHudView 的完整 HUD 层级（World Space Canvas + 血条 + CD 条）烘焙进 NetPlayer Prefab 的编辑器工具。
    /// 层级持久化存在 prefab 文件里，运行时零动态构建。通过菜单触发，用 PrefabUtility 安全编辑并保存，
    /// 自动创建所有 GameObject/Image/TMP 并连好 <see cref="PlayerHudView"/> 的引用，无需手动拖拽。
    /// </summary>
    public static class PlayerHudPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/NetPlayer.prefab";
        private const string FontPath = "MFBuiltin/Alibaba-PuHuiTi-Bold SDF";

        private const float BarWidth = 1f;
        private const float BarHeight = 0.1f;
        private const float CanvasYOffset = 2.2f;
        private const float HealthBarLocalY = 0.075f;
        private const float CooldownBarLocalY = -0.075f;
        private const float FontSize = 0.07f;

        /// <summary>菜单入口：在 NetPlayer 的 Visual 子级下烘焙 PlayerHud 层级并挂载组件，保存 prefab。</summary>
        [MenuItem("Tools/Battle/Bake PlayerHud Into NetPlayer", false, 100)]
        public static void BakeHud()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"PlayerHudPrefabBuilder: 找不到 Prefab {PrefabPath}");
                return;
            }

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning($"PlayerHudPrefabBuilder: 字体 {FontPath} 未找到，TMP 文字将用默认字体。");
            Sprite white = CreateWhiteSprite();

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform visual = FindVisual(root.transform);
                if (visual == null)
                {
                    Debug.LogError("PlayerHudPrefabBuilder: NetPlayer 下找不到 Visual 子级，烘焙中止。");
                    return;
                }

                // --- 清理旧的 HUD 层级（重复烘焙时幂等）---
                Transform oldHud = visual.Find("PlayerHud");
                if (oldHud != null)
                    Object.DestroyImmediate(oldHud.gameObject);

                // --- 构建 Canvas + 两条进度条层级 ---
                GameObject hud = BuildHudHierarchy(visual, font, white);

                // --- 挂载 PlayerHudView 组件并连接引用 ---
                PlayerHudView view = root.GetComponent<PlayerHudView>();
                if (view == null)
                    view = root.AddComponent<PlayerHudView>();
                AssignReferences(view, hud);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("PlayerHudPrefabBuilder: 已烘焙 PlayerHud 层级到 NetPlayer。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>构建 PlayerHud Canvas + HealthBar + CooldownBar 完整层级，挂到 Visual 下。</summary>
        private static GameObject BuildHudHierarchy(Transform visual, TMP_FontAsset font, Sprite white)
        {
            // --- Canvas 根 ---
            GameObject hud = new GameObject("PlayerHud");
            hud.transform.SetParent(visual, false);
            hud.transform.localPosition = new Vector3(0f, CanvasYOffset, 0f);

            Canvas canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            RectTransform canvasRt = hud.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(BarWidth, BarHeight * 2f + 0.05f);
            canvasRt.localScale = Vector3.one;
            canvasRt.pivot = new Vector2(0.5f, 0.5f);

            // --- 血条组 ---
            GameObject healthBar = CreateBar(hud.transform, HealthBarLocalY, font, white);
            healthBar.name = "HealthBar";

            // --- CD 条组 ---
            GameObject cooldownBar = CreateBar(hud.transform, CooldownBarLocalY, font, white);
            cooldownBar.name = "CooldownBar";
            cooldownBar.transform.Find("Fill").GetComponent<Image>().color = Color.cyan;

            return hud;
        }

        /// <summary>创建一条进度条：背景 + 填充 + 文字，返回根 GameObject。</summary>
        private static GameObject CreateBar(Transform canvasRoot, float localY, TMP_FontAsset font, Sprite white)
        {
            GameObject bar = new GameObject("Bar");
            RectTransform barRt = bar.AddComponent<RectTransform>();
            barRt.SetParent(canvasRoot, false);
            barRt.anchorMin = new Vector2(0f, 0.5f);
            barRt.anchorMax = new Vector2(1f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.sizeDelta = new Vector2(0f, BarHeight);
            barRt.anchoredPosition = new Vector2(0f, localY);

            // 背景
            Image bg = CreateImage("Background", barRt, white, new Color(0f, 0f, 0f, 0.6f));
            bg.raycastTarget = false;
            StretchFull(bg.rectTransform);

            // 填充
            Image fill = CreateImage("Fill", barRt, white, Color.white);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            StretchFull(fill.rectTransform);

            // 文字
            GameObject textGo = new GameObject("Text");
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(barRt, false);
            StretchFull(textRt);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = FontSize;
            text.enableAutoSizing = false;
            if (font != null) text.font = font;
            text.color = Color.white;
            text.text = "";

            return bar;
        }

        /// <summary>用 SerializedObject 把各引用写入 PlayerHudView，确保序列化进 prefab。</summary>
        private static void AssignReferences(PlayerHudView view, GameObject hud)
        {
            SerializedObject ser = new SerializedObject(view);
            ser.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<Canvas>();
            ser.FindProperty("_healthBar").objectReferenceValue = hud.transform.Find("HealthBar").gameObject;
            ser.FindProperty("_healthFill").objectReferenceValue = hud.transform.Find("HealthBar/Fill").GetComponent<Image>();
            ser.FindProperty("_healthText").objectReferenceValue = hud.transform.Find("HealthBar/Text").GetComponent<TextMeshProUGUI>();
            ser.FindProperty("_cooldownBar").objectReferenceValue = hud.transform.Find("CooldownBar").gameObject;
            ser.FindProperty("_cooldownFill").objectReferenceValue = hud.transform.Find("CooldownBar/Fill").GetComponent<Image>();
            ser.FindProperty("_cooldownText").objectReferenceValue = hud.transform.Find("CooldownBar/Text").GetComponent<TextMeshProUGUI>();
            ser.FindProperty("_showLocalPlayer").boolValue = true;
            ser.ApplyModifiedProperties();
        }

        /// <summary>把 RectTransform 拉伸填满父级。</summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>创建带 Image 的 GameObject 并挂到 parent。</summary>
        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        /// <summary>生成 4x4 纯白 Sprite 供 Image 使用（避免依赖项目内置 UI 资源）。</summary>
        private static Sprite CreateWhiteSprite()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>查找 Visual 子级：detach 后是 root 直接子级，detach 前在 Presentation 下。</summary>
        private static Transform FindVisual(Transform root)
        {
            Transform t = root.Find("Visual");
            if (t != null) return t;
            Transform presentation = root.Find("Presentation");
            if (presentation != null)
            {
                Transform v = presentation.Find("Visual");
                if (v != null) return v;
            }
            return null;
        }
    }
}
#endif
