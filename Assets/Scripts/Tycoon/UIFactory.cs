using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// Stateless UI construction helpers shared by HudController,
    /// PropertyPopupController, and SettingsController (and the world-space
    /// price tags in WorldBuilder), so every panel/badge/label in the game
    /// reuses the same procedurally-generated sprites and the same TMP text
    /// setup instead of each screen building its own. Text uses TextMeshPro
    /// (signed-distance-field rendering) rather than the legacy UI.Text -
    /// legacy Text rasterizes at a fixed pixel size and visibly blurs/
    /// pixelates once the CanvasScaler scales it for a different resolution
    /// than it was authored at; TMP stays crisp at any scale. CreateBubblePanel
    /// is the preferred way to build a card/pill now - it adds a soft blurred
    /// shadow behind the panel for the "floating bubble" look.
    /// </summary>
    public static class UIFactory
    {
        static Sprite roundedRectSprite;
        static Sprite shadowSprite;
        static Sprite circleSprite;

        public static Image CreateImage(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 sizeDelta, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : GetRoundedRectSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return img;
        }

        /// <summary>The "floating bubble" panel: a soft blurred shadow sitting
        /// behind (added first, so it renders first/behind in sibling order)
        /// a slightly smaller rounded panel on top - use this instead of
        /// CreateImage for any card/pill that should read as floating above
        /// the scene rather than flat-pasted onto it. Returns the front panel;
        /// the shadow needs no further handling from the caller.</summary>
        public static Image CreateBubblePanel(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var shadow = CreateImage(parent, name + "Shadow", new Color(0f, 0f, 0f, 0.32f),
                anchoredPos + new Vector2(0f, -8f), sizeDelta + new Vector2(24f, 24f), GetShadowSprite());
            shadow.raycastTarget = false;

            return CreateImage(parent, name, color, anchoredPos, sizeDelta);
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

        public static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = content;
            txt.font = TMP_Settings.defaultFontAsset;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAlignmentOptions.Center;
            // Auto-size within a range around the requested fontSize instead
            // of a single fixed size: this game's numbers vary wildly in
            // length ("$25" vs "$2.3M", "+$1.2K" vs "-$450"), and a size
            // hand-picked to survive the longest case reads as permanently
            // small/cramped for the (much more common) short case - which is
            // exactly what made the bottom HUD chips illegible. This way
            // short content renders at up to 1.35x larger, long content
            // shrinks only as far as it actually needs to (down to 0.6x) to
            // still fit its box, rather than every value settling for a
            // worst-case compromise size.
            txt.enableAutoSizing = true;
            txt.fontSizeMin = fontSize * 0.6f;
            txt.fontSizeMax = fontSize * 1.35f;
            var rt = txt.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return txt;
        }

        public static Button CreateButton(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 sizeDelta, out TextMeshProUGUI label)
        {
            var img = CreateImage(parent, name, color, anchoredPos, sizeDelta);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            img.gameObject.AddComponent<ButtonPressFeedback>();
            label = CreateText(img.transform, "Label", "", 26, Color.white, Vector2.zero, sizeDelta - new Vector2(20, 20));
            return btn;
        }

        /// <summary>Shared rounded-rect alpha falloff, parameterized so the crisp
        /// panel sprite and the soft shadow sprite can reuse the exact same
        /// math instead of two hand-tuned copies. cornerBlur is the width (in
        /// texture pixels) of the anti-aliasing band at each rounded corner -
        /// small (a couple px) for a crisp panel edge, large (tens of px) for
        /// a soft blurred shadow. edgeBlur, if > 0, additionally fades the
        /// flat edges (not just the corners) inward from the texture border -
        /// only used for the shadow; a plain panel's straight edges stay hard,
        /// which is what "rounded rectangle" is supposed to look like.</summary>
        static Texture2D BuildRoundedTexture(int size, int corner, float cornerBlur, float edgeBlur, float baseAlpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha;
                    bool nearLeft = x < corner, nearRight = x > size - corner;
                    bool nearBottom = y < corner, nearTop = y > size - corner;
                    if ((nearLeft || nearRight) && (nearBottom || nearTop))
                    {
                        float cx = nearLeft ? corner : size - corner;
                        float cy = nearBottom ? corner : size - corner;
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        alpha = Mathf.Clamp01((corner - dist) / cornerBlur + 0.5f);
                    }
                    else
                    {
                        alpha = 1f;
                    }
                    if (edgeBlur > 0f)
                    {
                        float edgeDist = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                        alpha *= Mathf.Clamp01(edgeDist / edgeBlur);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * baseAlpha));
                }
            }
            tex.Apply();
            return tex;
        }

        public static Sprite GetRoundedRectSprite()
        {
            if (roundedRectSprite != null) return roundedRectSprite;
            // 256px source (up from the old 64px) with a wide-relative corner
            // radius: the old sprite's edges looked jagged/pixelated once the
            // CanvasScaler stretched a large card (e.g. the 760-wide popup)
            // off a tiny 64px source. A gentle 3px AA band keeps corners
            // smooth without reading as blurry.
            const int size = 256;
            const int corner = 88;
            var tex = BuildRoundedTexture(size, corner, 3f, 0f, 1f);
            var border = new Vector4(corner, corner, corner, corner);
            roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return roundedRectSprite;
        }

        /// <summary>Soft, wide-blurred rounded rect for CreateBubblePanel's
        /// drop shadow - same shape family as the panel sprite above, just
        /// with a much wider falloff band and edges that fade too, not just
        /// the corners, so it reads as a blurred shadow rather than a second
        /// hard-edged rectangle sitting behind the first.</summary>
        public static Sprite GetShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            const int size = 256;
            const int corner = 104;
            var tex = BuildRoundedTexture(size, corner, 42f, 34f, 0.6f);
            var border = new Vector4(corner, corner, corner, corner);
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return shadowSprite;
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
