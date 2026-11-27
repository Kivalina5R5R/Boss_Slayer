using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26: a full-size layer over the wall drawn by one shader, shown only while the wall shows one picture.
    // Used for the Water wall's side caustics (BossWaterCaustics.shader) and the Final Boss wall's shockwaves
    // (BossShockwave.shader). The layer's texture is the wall picture, for shaders that light the painting.
    public sealed class BossWallShaderOverlay : MonoBehaviour
    {
        RawImage image;
        string wallName;

        // layerName names the child of the wall; shaderPath is the GameArt path; shownOnWall is the wall picture's name.
        public static BossWallShaderOverlay Attach(RawImage wall, string layerName, string shaderPath, string shownOnWall)
        {
            if (wall == null) return null;
            var layer = BossHallSpace.Layer(wall.rectTransform, layerName);
            if (!layer.TryGetComponent(out BossWallShaderOverlay overlay)) overlay = layer.gameObject.AddComponent<BossWallShaderOverlay>();
            overlay.Setup(shaderPath, shownOnWall);
            return overlay;
        }

        void Setup(string shaderPath, string shownOnWall)
        {
            wallName = shownOnWall;
            if (!TryGetComponent(out image)) image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var shader = GameArt.Load<Shader>(shaderPath);
            if (shader == null || !shader.isSupported) { image.material = null; gameObject.SetActive(false); return; }
            if (image.material == null || image.material.shader != shader)
                image.material = new Material(shader) { name = shader.name, hideFlags = HideFlags.HideAndDontSave };
        }

        // Shows the layer over its own wall picture only.
        public void Show(Texture wallArt)
        {
            bool on = wallArt != null && wallArt.name == wallName && image != null && image.material != null;
            gameObject.SetActive(on);
            if (on) image.texture = wallArt;
        }
    }
}
