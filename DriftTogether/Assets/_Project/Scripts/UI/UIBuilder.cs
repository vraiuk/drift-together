using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftTogether.UI
{
    /// <summary>Code-first uGUI construction helpers (no prefabs, no missing refs).</summary>
    public static class UIBuilder
    {
        public static readonly Color Panel = new Color(0.07f, 0.12f, 0.13f, 0.92f);
        public static readonly Color PanelSoft = new Color(0.07f, 0.12f, 0.13f, 0.6f);
        public static readonly Color Accent = new Color(0.45f, 0.85f, 0.7f);
        public static readonly Color TextMain = new Color(0.92f, 0.96f, 0.94f);
        public static readonly Color TextDim = new Color(0.65f, 0.75f, 0.73f);

        static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateInvisiblePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static Text CreateText(Transform parent, string name, string content,
            int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 size, System.Action onClick)
        {
            var rect = CreatePanel(parent, name, new Color(0.14f, 0.24f, 0.24f, 0.95f));
            rect.sizeDelta = size;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.6f, 0.9f, 0.8f);
            colors.pressedColor = new Color(0.35f, 0.6f, 0.52f);
            button.colors = colors;

            var text = CreateText(rect, "Label", label, 30, TextMain);
            Stretch(text.rectTransform);

            button.onClick.AddListener(() =>
            {
                var am = Core.AudioManager.Instance;
                if (am != null)
                    am.PlayClick();
                onClick?.Invoke();
            });
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, float value,
            System.Action<float> onChanged)
        {
            var rect = CreateInvisiblePanel(parent, name);
            rect.sizeDelta = new Vector2(420f, 36f);

            var bg = CreatePanel(rect, "Background", new Color(0.1f, 0.16f, 0.17f, 1f));
            Stretch(bg);
            bg.offsetMin = new Vector2(0f, 12f);
            bg.offsetMax = new Vector2(0f, -12f);

            var fillArea = CreateInvisiblePanel(rect, "FillArea");
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6f, 12f);
            fillArea.offsetMax = new Vector2(-6f, -12f);
            var fill = CreatePanel(fillArea, "Fill", Accent);
            Stretch(fill);

            var handleArea = CreateInvisiblePanel(rect, "HandleArea");
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = CreatePanel(handleArea, "Handle", Color.white);
            handle.sizeDelta = new Vector2(22f, 0f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
