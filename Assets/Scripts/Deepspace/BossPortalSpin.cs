using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-25: turns the inside of the Land wall's portal clockwise (BossPortalSpin.shader), because its video loop
    // barely rotates while Fire's and Water's do. A disc of the still wall art over the portal, between the centre
    // video and the glow / particles. Measured on BGLandWall (8192x2160): portal centre x 4096, 860 px from the top;
    // the inside is about 580 px across its radius, so the disc stays a little inside the rune ring.
    public sealed class BossPortalSpin : MonoBehaviour
    {
        const float WallW = 8192f, WallH = 2160f, CentreX = 4096f, CentreFromTop = 860f, Radius = 560f;
        const float DegreesPerSecond = 22f;
        const string LandWallName = "BGLandWall";

        RawImage image;

        public static BossPortalSpin Attach(RawImage wall)
        {
            if (wall == null) return null;
            var rect = wall.rectTransform.Find("Portal Spin") as RectTransform;
            if (rect == null)
            {
                rect = new GameObject("Portal Spin", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
                rect.SetParent(wall.rectTransform, false);
            }
            float cx = CentreX / WallW, cy = 1f - CentreFromTop / WallH, rx = Radius / WallW, ry = Radius / WallH;
            rect.anchorMin = new Vector2(cx - rx, cy - ry);
            rect.anchorMax = new Vector2(cx + rx, cy + ry);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (!rect.TryGetComponent(out BossPortalSpin spin)) spin = rect.gameObject.AddComponent<BossPortalSpin>();
            spin.Setup(new Rect(cx - rx, cy - ry, rx * 2f, ry * 2f));
            return spin;
        }

        void Setup(Rect disc)
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.uvRect = disc;
            var shader = Resources.Load<Shader>("BossPortalSpin");
            if (shader == null || !shader.isSupported) { gameObject.SetActive(false); return; }
            if (image.material == null || image.material.shader != shader)
                image.material = new Material(shader) { name = "Portal Spin", hideFlags = HideFlags.HideAndDontSave };
            image.material.SetVector("_UvRect", new Vector4(disc.x, disc.y, disc.width, disc.height));
            image.material.SetFloat("_DegreesPerSecond", DegreesPerSecond);
        }

        // Spins only over the Land wall; hidden on every other wall picture.
        public void Show(Texture wallArt)
        {
            bool land = wallArt != null && wallArt.name == LandWallName;
            gameObject.SetActive(land);
            if (land) image.texture = wallArt;
        }
    }
}
