using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // Shared placing for Boss Slayer's wall particles (BossRisingParticles, BossFallingDebris). A particle lives in the
    // wall picture, in 8192x2160 mockup pixels from the bottom-left; `view` is the part of the picture on screen
    // (a RawImage uvRect, (0,0,1,1) for the whole picture). So when the boss select's camera zooms or floats
    // (BossSelectionPanel.WallZoom.cs) the particles move and grow with the hall instead of sticking to the screen.
    public static class BossHallSpace
    {
        public const float DesignW = 8192f, DesignH = 2160f;
        static Texture2D softSquare;

        // A full-size child layer of parent, found or made.
        public static RectTransform Layer(RectTransform parent, string name)
        {
            if (parent == null) return null;
            var layer = parent.Find(name) as RectTransform;
            if (layer == null)
            {
                layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(parent, false);
            }
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return layer;
        }

        // A particle image under layer, found or made, anchored at a point.
        public static RawImage Sprite(RectTransform layer, string name)
        {
            var rect = layer.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(layer, false);
            }
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            if (!rect.TryGetComponent(out RawImage image)) image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        // Puts a particle at a picture point (mockup pixels), `size` mockup pixels across, seen through `view`.
        // `unit` is screen units per mockup pixel at the whole-picture view (layer height / 2160).
        public static void Place(RectTransform rect, Vector2 point, float size, float angle, Rect view, float unit)
        {
            float u = point.x / DesignW, v = point.y / DesignH;
            rect.anchorMin = rect.anchorMax = new Vector2((u - view.x) / view.width, (v - view.y) / view.height);
            float zoom = 1f / Mathf.Max(.01f, view.height);
            rect.sizeDelta = new Vector2(size, size) * unit * zoom;
            rect.localEulerAngles = new Vector3(0, 0, angle);
        }

        // A white square with a soft glow edge, for particles that take any colour (the art team's sprite is red).
        public static Texture2D SoftSquare()
        {
            if (softSquare != null) return softSquare;
            const int size = 32;
            softSquare = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Boss Soft Square", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // solid core in the middle half, glow fading to the edge
                    float edge = Mathf.Max(Mathf.Abs(x + .5f - size / 2f), Mathf.Abs(y + .5f - size / 2f)) / (size / 2f);
                    float a = edge <= .5f ? 1f : Mathf.Pow(1f - (edge - .5f) / .5f, 2f) * .7f;
                    softSquare.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            softSquare.Apply();
            return softSquare;
        }
    }
}
