using UnityEngine;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// Stateless UI construction helpers shared by HudController and
    /// PropertyPopupController (and the world-space price tags in
    /// WorldBuilder), so every rounded panel/circle badge in the game reuses
    /// the same two procedurally-generated sprites instead of each screen
    /// building its own.
    /// </summary>
    public static class UIFactory
    {
        static Sprite roundedRectSprite;
        static Sprite circleSprite;

        public static Image CreateImage(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return img;
        }

        public static Image CreateFullscreenImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        public static Image CreateIconBadge(Transform parent, Color color, Vector2 anchoredPos, float diameter)
        {
            var go = new GameObject("IconBadge", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetCircleSprite();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);
            return img;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 anchoredPos, Vector2 sizeDelta, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            var rt = txt.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return txt;
        }

        public static Button CreateButton(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 sizeDelta, Font font, out Text label)
        {
            var img = CreateImage(parent, name, color, anchoredPos, sizeDelta);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            img.gameObject.AddComponent<ButtonPressFeedback>();
            label = CreateText(img.transform, "Label", "", 26, Color.white, Vector2.zero, sizeDelta - new Vector2(20, 20), font);
            return btn;
        }

        public static Sprite GetRoundedRectSprite()
        {
            if (roundedRectSprite != null) return roundedRectSprite;
            const int size = 64;
            const int corner = 20;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    bool nearLeft = x < corner, nearRight = x > size - corner;
                    bool nearBottom = y < corner, nearTop = y > size - corner;
                    if ((nearLeft || nearRight) && (nearBottom || nearTop))
                    {
                        float cx = nearLeft ? corner : size - corner;
                        float cy = nearBottom ? corner : size - corner;
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        alpha = Mathf.Clamp01(corner - dist + 0.5f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            var border = new Vector4(corner, corner, corner, corner);
            roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return roundedRectSprite;
        }

        public static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circleSprite;
        }
    }
}
