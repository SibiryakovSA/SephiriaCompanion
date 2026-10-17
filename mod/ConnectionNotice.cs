using UnityEngine;
using UnityEngine.UI;

namespace SephiriaCompanion
{
    internal sealed class ConnectionNotice
    {
        readonly Transform parent;
        GameObject root;
        Text label;
        float expires;
        public ConnectionNotice(Transform parent) { this.parent = parent; }

        public void Show(string language)
        {
            if (root == null) Create();
            label.text = language == "en" ? "Sephiria Companion · connection works" : "Sephiria Companion · связь работает";
            root.SetActive(true);
            expires = Time.unscaledTime + 4;
        }

        public void Update()
        {
            if (root != null && root.activeSelf && Time.unscaledTime > expires) root.SetActive(false);
        }

        void Create()
        {
            root = new GameObject("Companion notice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scale = root.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080);
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -24);
            rect.sizeDelta = new Vector2(800, 70);
            var text = label.GetComponent<Text>();
            this.label = text;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "Sephiria Companion · связь работает";
            text.color = new Color(1, .91f, .7f);
        }
    }
}
