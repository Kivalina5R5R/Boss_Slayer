using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-27 "ทำใหม่ไปเลย เอาให้สวยงามกว่านี้": small building blocks for the "how to play" slides (TutorialSlides and
    // TutorialScenes). Everything is placed by fractions of its parent, so the slides fit the 3.8 : 1 wall and the
    // Editor's Game view alike. Pictures come from the game's own art folders through GameArt.
    public static class TutorialArt
    {
        public const string Ui = "Art/UI/";

        public static readonly Color Gold = UiThemeKit.Rgb(0xF2C14E);
        public static readonly Color GoldDim = UiThemeKit.Rgb(0xB98A3A);
        public static readonly Color Night = UiThemeKit.Rgb(0x0B0812);
        public static readonly Color Ember = UiThemeKit.Rgb(0xFF5B3A);

        static readonly Dictionary<Texture2D, Sprite> sprites = new();

        // An empty rect at these fractions of parent.
        public static RectTransform Box(RectTransform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = UiThemeKit.Child(parent, name);
            UiThemeKit.Place(rect, left, bottom, right, top);
            return rect;
        }

        // A picture kept at its own shape and fitted inside a box at these fractions. Scale or move the returned image's
        // rect to animate it; its box stays put.
        public static RawImage Picture(RectTransform parent, string name, string path, float left, float bottom, float right, float top)
        {
            var box = Box(parent, name, left, bottom, right, top);
            var rect = UiThemeKit.Child(box, "Art");
            UiThemeKit.Place(rect, 0, 0, 1, 1);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var texture = GameArt.Load<Texture2D>(path);
            image.texture = texture;
            image.enabled = texture != null;
            if (texture == null)
            {
                Debug.LogWarning($"[BossTutorial] Missing art {path}");
                return image;
            }
            var fit = rect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = texture.width / (float)Mathf.Max(1, texture.height);
            return image;
        }

        // A picture stretched over the whole of parent, cropped to cover a frame of frameAspect (width / height).
        public static RawImage Cover(RectTransform parent, string name, string path, float frameAspect, Color tint)
        {
            var rect = UiThemeKit.Child(parent, name);
            UiThemeKit.Stretch(rect, 0);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = tint;
            var texture = GameArt.Load<Texture2D>(path);
            image.texture = texture;
            image.enabled = texture != null;
            if (texture != null) TutorialSlides.Cover(image, texture, frameAspect);
            return image;
        }

        // A soft round glow (the theme kit's glow art), stretched to the box.
        public static RawImage Glow(RectTransform parent, string name, Color color, float left, float bottom, float right, float top)
        {
            var rect = Box(parent, name, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = GameArt.Load<Texture2D>(Ui + "Theme/Theme Glow");
            image.color = color;
            return image;
        }

        // A flat colour fill over the whole of parent.
        public static Image Fill(RectTransform parent, string name, Color color)
        {
            var rect = UiThemeKit.Child(parent, name);
            UiThemeKit.Stretch(rect, 0);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        // A bar that fills from the left: back art and fill art at these fractions. Set fillAmount on the result.
        public static Image Bar(RectTransform parent, string name, string backPath, string fillPath, float left, float bottom, float right, float top)
        {
            var box = Box(parent, name, left, bottom, right, top);
            var back = box.gameObject.AddComponent<RawImage>();
            back.raycastTarget = false;
            back.texture = GameArt.Load<Texture2D>(backPath);
            back.enabled = back.texture != null;
            var fillRect = UiThemeKit.Child(box, "Fill");
            UiThemeKit.Stretch(fillRect, 0);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.raycastTarget = false;
            fill.sprite = SpriteOf(GameArt.Load<Texture2D>(fillPath));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            return fill;
        }

        // Text in the game's pixel face (Jersey 15), or the Thai face when the slides are in Thai and thai is given.
        // It auto-sizes down to 45% of size to fit its box.
        public static TMP_Text Label(RectTransform parent, string name, string english, string thai, float left, float bottom, float right, float top,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            bool useThai = Lang.Thai && !string.IsNullOrEmpty(thai);
            var text = UiThemeKit.Text(parent, name, useThai ? GameShowcase.Thai() : PixelFont());
            UiThemeKit.Place((RectTransform)text.transform, left, bottom, right, top);
            text.textPreprocessor = LangUntranslated.Instance;   // already written in the chosen language
            text.text = useThai ? thai : english;
            text.color = color;
            text.alignment = align;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * .45f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // A dark outline so text reads over busy art (gives the label its own material).
        public static void Outline(TMP_Text text, float width)
        {
            text.outlineWidth = width;
            text.outlineColor = Night;
        }

        public static TMP_FontAsset PixelFont() => BossPlayerCards.Jersey() ?? TMP_Settings.defaultFontAsset;

        public static Sprite SpriteOf(Texture2D texture)
        {
            if (texture == null) return null;
            if (sprites.TryGetValue(texture, out var sprite) && sprite != null) return sprite;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            sprite.name = texture.name + " (tutorial)";
            return sprites[texture] = sprite;
        }

        // Moves a box so its centre sits at (x, y) in parent fractions, keeping its size.
        public static void CentreAt(RectTransform box, float x, float y)
        {
            var half = (box.anchorMax - box.anchorMin) * .5f;
            box.anchorMin = new Vector2(x, y) - half;
            box.anchorMax = new Vector2(x, y) + half;
        }

        // 0..1 smooth step between from and to.
        public static float Ease(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
    }
}
