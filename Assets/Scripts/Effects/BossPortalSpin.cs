using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-25: turns the inside of a wall's portal clockwise (BossPortalSpin.shader), because the video loops barely
    // rotate their vortex. A disc of the still wall art over the portal, between the centre video and the glow /
    // particles. 2026-11-26: one disc per wall picture (Portals below), measured on each 8192x2160 picture; the disc
    // stays a little inside the rune ring. Walls without an entry show no disc. The Final Boss wall's ring turns as
    // light instead (BossPortalOrbit).
    public sealed class BossPortalSpin : MonoBehaviour
    {
        const float WallW = 8192f, WallH = 2160f, DegreesPerSecond = 22f;

        readonly struct Portal
        {
            public readonly string WallName;
            public readonly float CentreX, CentreFromTop, Radius;

            public Portal(string wallName, float centreX, float centreFromTop, float radius)
            {
                WallName = wallName; CentreX = centreX; CentreFromTop = centreFromTop; Radius = radius;
            }
        }

        static readonly Portal[] Portals =
        {
            new Portal("BGLandWall", 4096f, 860f, 560f),
            new Portal("BGEarthScene", 4110f, 850f, 560f),
            // BGWaterScene has none: its Fortal loop already turns the vortex clockwise (about 12 degrees a second).
        };

        RawImage image;
        RectTransform rect;
        Material material;

        public static BossPortalSpin Attach(RawImage wall)
        {
            if (wall == null) return null;
            var rect = wall.rectTransform.Find("Portal Spin") as RectTransform;
            if (rect == null)
            {
                rect = new GameObject("Portal Spin", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
                rect.SetParent(wall.rectTransform, false);
            }
            if (!rect.TryGetComponent(out BossPortalSpin spin)) spin = rect.gameObject.AddComponent<BossPortalSpin>();
            spin.Setup(rect);
            return spin;
        }

        void Setup(RectTransform layer)
        {
            rect = layer;
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            var shader = GameArt.Load<Shader>("Art/Shaders/BossPortalSpin");
            if (shader == null || !shader.isSupported) { gameObject.SetActive(false); return; }
            if (image.material == null || image.material.shader != shader)
                image.material = new Material(shader) { name = "Portal Spin", hideFlags = HideFlags.HideAndDontSave };
            material = image.material;
            material.SetFloat("_DegreesPerSecond", DegreesPerSecond);
        }

        // Spins over a wall picture that has a portal entry; hidden on every other wall picture.
        public void Show(Texture wallArt)
        {
            int index = wallArt == null || material == null ? -1 : System.Array.FindIndex(Portals, p => p.WallName == wallArt.name);
            gameObject.SetActive(index >= 0);
            if (index < 0) return;
            var portal = Portals[index];
            float cx = portal.CentreX / WallW, cy = 1f - portal.CentreFromTop / WallH, rx = portal.Radius / WallW, ry = portal.Radius / WallH;
            var disc = new Rect(cx - rx, cy - ry, rx * 2f, ry * 2f);
            rect.anchorMin = new Vector2(disc.xMin, disc.yMin);
            rect.anchorMax = new Vector2(disc.xMax, disc.yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.uvRect = disc;
            material.SetVector("_UvRect", new Vector4(disc.x, disc.y, disc.width, disc.height));
            image.texture = wallArt;
        }
    }
}
