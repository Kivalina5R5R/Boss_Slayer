using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26: the Final Boss wall's rune ring turns clockwise as light (BossPortalOrbit.shader): comets of violet
    // light race round the ring over its centre video (the shockwaves are BossWallShockwave). Its video loop floats the
    // debris and pulses the eye but leaves the ring still. Measured on BGFinalBossScene (8192x2160): eye centre x 4180,
    // 900 px from the top; the rune band runs about 580..720 px from the centre. Shown only over that picture.
    public sealed class BossPortalOrbit : MonoBehaviour
    {
        const float WallW = 8192f, WallH = 2160f, CentreX = 4180f, CentreFromTop = 900f, Radius = 780f;
        const string FinalWallName = "BGFinalBossScene";

        public static BossPortalOrbit Attach(RawImage wall)
        {
            if (wall == null) return null;
            var rect = wall.rectTransform.Find("Portal Orbit") as RectTransform;
            if (rect == null)
            {
                rect = new GameObject("Portal Orbit", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
                rect.SetParent(wall.rectTransform, false);
            }
            float cx = CentreX / WallW, cy = 1f - CentreFromTop / WallH, rx = Radius / WallW, ry = Radius / WallH;
            rect.anchorMin = new Vector2(cx - rx, cy - ry);
            rect.anchorMax = new Vector2(cx + rx, cy + ry);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (!rect.TryGetComponent(out BossPortalOrbit orbit)) orbit = rect.gameObject.AddComponent<BossPortalOrbit>();
            orbit.Setup();
            return orbit;
        }

        void Setup()
        {
            var image = GetComponent<RawImage>();
            image.raycastTarget = false;
            var shader = GameArt.Load<Shader>("Art/Shaders/BossPortalOrbit");
            if (shader == null || !shader.isSupported) { gameObject.SetActive(false); return; }
            if (image.material == null || image.material.shader != shader)
                image.material = new Material(shader) { name = "Portal Orbit", hideFlags = HideFlags.HideAndDontSave };
        }

        // Shows the orbiting light over the Final Boss wall only.
        public void Show(Texture wallArt)
        {
            var image = GetComponent<RawImage>();
            bool on = wallArt != null && wallArt.name == FinalWallName && image.material != null;
            gameObject.SetActive(on);
        }
    }
}
