#if TextMeshPro
using TMPro;
#endif

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MemoFramework.UI
{
    public static class UIMenuItem
    {
        // path
        private const string kFont = "Alibaba-PuHuiTi-Bold SDF";
        private const string kMaskPath = "UI/Skin/UIMask.psd";

        private const string k9Sliced =
            "MFBuiltin/9Sliced";

        // value
        private const float kWidth = 160f;
        private const float kThickHeight = 45f;
        private static readonly Vector2 s_ThickElementSize = new Vector2(kWidth, kThickHeight);

        private static readonly Color s_defaultFontColor = new Color(0.1960784f, 0.1960784f, 0.1960784f);
        // resources
        private static Sprite s_9Sliced;
        private static Sprite s_mask;
#if TextMeshPro
       
        private static TMP_FontAsset s_defaultFontAsset;

        static UIMenuItem()
        {
            s_mask = AssetDatabase.GetBuiltinExtraResource<Sprite>(kMaskPath);
            s_9Sliced = Resources.Load<Sprite>(k9Sliced);
            s_defaultFontAsset = Resources.Load<TMP_FontAsset>(kFont);
        }

        [MenuItem("GameObject/MF-UI/View", false, 0)]
        public static void CreateEmptyView(MenuCommand menuCommand)
        {
            var root = UIEditorUtils.CreateUIElementRoot("View", s_ThickElementSize);
            var rect = root.GetComponent<RectTransform>();
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 0);
        }

        [MenuItem("GameObject/MF-UI/Button", false, 1)]
        public static void CreateButtonPrefab(MenuCommand menuCommand)
        {
            var root = UIEditorUtils.CreateUIElementRoot("Button", s_ThickElementSize);
            var childText = UIEditorUtils.CreateUIObject("Text", root);
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);

            Image image = UIEditorUtils.AddComponent<Image>(root);
            image.sprite = s_9Sliced;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3;

            Button bt = UIEditorUtils.AddComponent<Button>(root);
            UIEditorUtils.SetDefaultColorTransitionValues(bt);

            TextMeshProUGUI text = UIEditorUtils.AddComponent<TextMeshProUGUI>(childText);
            text.text = "按钮";
            text.alignment = TextAlignmentOptions.Center;
            text.font = s_defaultFontAsset;
            text.fontSize = 24;
            text.color = s_defaultFontColor;

            RectTransform textRectTransform = childText.GetComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;
        }

        [MenuItem("GameObject/MF-UI/TextMeshPro-Text", false, 2)]
        public static void CreateTextMeshProText(MenuCommand menuCommand)
        {
            var root = UIEditorUtils.CreateUIElementRoot("Text", s_ThickElementSize);
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);

            TextMeshProUGUI text = UIEditorUtils.AddComponent<TextMeshProUGUI>(root);
            text.font = s_defaultFontAsset;
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 30;
            text.text = "文本";
        }

        [MenuItem("GameObject/MF-UI/TextMeshPro-InputField", false, 3)]
        public static void CreateInputField(MenuCommand menuCommand)
        {
            GameObject root = UIEditorUtils.CreateUIElementRoot("InputField", new Vector2(150, 37.5f));

            GameObject textArea = UIEditorUtils.CreateUIObject("TextArea", root);
            GameObject childPlaceholder = UIEditorUtils.CreateUIObject("Placeholder", textArea);
            GameObject childText = UIEditorUtils.CreateUIObject("Text", textArea);

            Image image = UIEditorUtils.AddComponent<Image>(root);
            image.sprite = s_9Sliced;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 3;

            TMP_InputField inputField = UIEditorUtils.AddComponent<TMP_InputField>(root);
            UIEditorUtils.SetDefaultColorTransitionValues(inputField);

            RectMask2D rectMask = UIEditorUtils.AddComponent<RectMask2D>(textArea);
            rectMask.padding = new Vector4(-8, -5, -8, -5);

            RectTransform textAreaRectTransform = textArea.GetComponent<RectTransform>();
            textAreaRectTransform.anchorMin = Vector2.zero;
            textAreaRectTransform.anchorMax = Vector2.one;
            textAreaRectTransform.sizeDelta = Vector2.zero;
            textAreaRectTransform.offsetMin = new Vector2(10, 6);
            textAreaRectTransform.offsetMax = new Vector2(-10, -7);


            TextMeshProUGUI text = UIEditorUtils.AddComponent<TextMeshProUGUI>(childText);
            text.text = "";
            // text.textWrappingMode = TextWrappingModes.NoWrap;
            text.extraPadding = true;
            text.richText = true;
            text.fontSize = 14;
            text.font = s_defaultFontAsset;
            text.color = Color.black;

            TextMeshProUGUI placeholder = UIEditorUtils.AddComponent<TextMeshProUGUI>(childPlaceholder);
            placeholder.text = "输入文本...";
            placeholder.fontSize = 14;
            placeholder.fontStyle = FontStyles.Italic;
            // placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.font = s_defaultFontAsset;
            placeholder.extraPadding = true;

            // Make placeholder color half as opaque as normal text color.
            Color placeholderColor = text.color;
            placeholderColor.a *= 0.5f;
            placeholder.color = placeholderColor;

            // Add Layout component to placeholder.
            UIEditorUtils.AddComponent<LayoutElement>(placeholder.gameObject).ignoreLayout = true;

            RectTransform textRectTransform = childText.GetComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;
            textRectTransform.offsetMin = new Vector2(0, 0);
            textRectTransform.offsetMax = new Vector2(0, 0);

            RectTransform placeholderRectTransform = childPlaceholder.GetComponent<RectTransform>();
            placeholderRectTransform.anchorMin = Vector2.zero;
            placeholderRectTransform.anchorMax = Vector2.one;
            placeholderRectTransform.sizeDelta = Vector2.zero;
            placeholderRectTransform.offsetMin = new Vector2(0, 0);
            placeholderRectTransform.offsetMax = new Vector2(0, 0);

            inputField.textViewport = textAreaRectTransform;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.fontAsset = text.font;
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);
        }
#endif


        [MenuItem("GameObject/MF-UI/FullBackground", false, 3)]
        public static void CreateFullBackground(MenuCommand menuCommand)
        {
            GameObject root = UIEditorUtils.CreateUIElementRoot("Background", new Vector2(0, 0));
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 0);

            root.AddComponent<Image>();
        }

        [MenuItem("GameObject/MF-UI/Scroll View", false, 4)]
        public static void CreateScrollView(MenuCommand menuCommand)
        {
            GameObject root = UIEditorUtils.CreateUIElementRoot("Scroll View", new Vector2(200, 200), typeof(Image),
                typeof(ScrollRect));

            GameObject viewport = UIEditorUtils.CreateUIObject("Viewport", root, typeof(Image), typeof(Mask));
            GameObject content = UIEditorUtils.CreateUIObject("Content", viewport, typeof(RectTransform));
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(0, 0);
            rootRect.offsetMax = new Vector2(0, 0);

            // Sub controls.

            GameObject hScrollbar = CreateScrollbar();
            hScrollbar.name = "Scrollbar Horizontal";
            UIEditorUtils.SetParentAndAlign(hScrollbar, root);
            RectTransform hScrollbarRT = hScrollbar.GetComponent<RectTransform>();
            hScrollbarRT.anchorMin = Vector2.zero;
            hScrollbarRT.anchorMax = Vector2.right;
            hScrollbarRT.pivot = Vector2.zero;
            hScrollbarRT.sizeDelta = new Vector2(0, 3);


            GameObject vScrollbar = CreateScrollbar();
            vScrollbar.name = "Scrollbar Vertical";
            UIEditorUtils.SetParentAndAlign(vScrollbar, root);
            vScrollbar.GetComponent<Scrollbar>().SetDirection(Scrollbar.Direction.BottomToTop, true);
            RectTransform vScrollbarRT = vScrollbar.GetComponent<RectTransform>();
            vScrollbarRT.anchorMin = Vector2.right;
            vScrollbarRT.anchorMax = Vector2.one;
            vScrollbarRT.pivot = Vector2.one;
            vScrollbarRT.sizeDelta = new Vector2(3, 0);


            // Setup RectTransforms.

            // Make viewport fill entire scroll view.
            RectTransform viewportRT = viewport.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.pivot = Vector2.up;

            // Make context match viewpoprt width and be somewhat taller.
            // This will show the vertical scrollbar and not the horizontal one.
            RectTransform contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = Vector2.up;
            contentRT.anchorMax = Vector2.one;
            contentRT.sizeDelta = new Vector2(0, 300);
            contentRT.pivot = Vector2.up;

            // Setup UI components.

            ScrollRect scrollRect = root.GetComponent<ScrollRect>();
            scrollRect.content = contentRT;
            scrollRect.viewport = viewportRT;
            scrollRect.horizontalScrollbar = hScrollbar.GetComponent<Scrollbar>();
            scrollRect.verticalScrollbar = vScrollbar.GetComponent<Scrollbar>();
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.horizontalScrollbarSpacing = -3;
            scrollRect.verticalScrollbarSpacing = -3;

            Image rootImage = root.GetComponent<Image>();
            rootImage.sprite = null;
            rootImage.type = Image.Type.Sliced;
            rootImage.enabled = false;

            Mask viewportMask = viewport.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = s_mask;
            viewportImage.type = Image.Type.Sliced;
            UIEditorUtils.PlaceUIElementRoot(root, menuCommand);

            hScrollbar.GetComponent<Scrollbar>().size = 0.5f;
            hScrollbar.GetComponent<Scrollbar>().value = 0f;
            vScrollbar.GetComponent<Scrollbar>().size = 0.5f;
            vScrollbar.GetComponent<Scrollbar>().value = 0f;
        }

        public static GameObject CreateScrollbar()
        {
            // Create GOs Hierarchy
            GameObject scrollbarRoot =
                UIEditorUtils.CreateUIElementRoot("Scrollbar", new Vector2(60, 20), typeof(Image), typeof(Scrollbar));

            GameObject sliderArea = UIEditorUtils.CreateUIObject("Sliding Area", scrollbarRoot, typeof(RectTransform));
            GameObject handle = UIEditorUtils.CreateUIObject("Handle", sliderArea, typeof(Image));

            Image bgImage = scrollbarRoot.GetComponent<Image>();
            bgImage.sprite = s_9Sliced;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.7f, 0.7f, 0.7f, 0.3f);
            bgImage.pixelsPerUnitMultiplier = 40;

            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = s_9Sliced;
            handleImage.type = Image.Type.Sliced;
            handleImage.color = Color.white;
            handleImage.pixelsPerUnitMultiplier = 40;

            RectTransform sliderAreaRect = sliderArea.GetComponent<RectTransform>();
            sliderAreaRect.anchorMin = Vector2.zero;
            sliderAreaRect.anchorMax = Vector2.one;
            sliderAreaRect.offsetMin = new Vector2(0, 0);
            sliderAreaRect.offsetMax = new Vector2(0, 0);

            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = new Vector2(0, 0);
            handleRect.offsetMax = new Vector2(0, 0);

            Scrollbar scrollbar = scrollbarRoot.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            UIEditorUtils.SetDefaultColorTransitionValues(scrollbar);

            return scrollbarRoot;
        }
    }
}