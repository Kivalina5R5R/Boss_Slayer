using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class BossEnvironmentMotion : MonoBehaviour
    {
        [SerializeField] Texture originalArtwork;
        [SerializeField] bool floorDisplay;
        [SerializeField, Range(0, 1)] float atmosphere = .85f;
        RawImage target;
        Material motionMaterial, previousMaterial;
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        public Texture OriginalArtwork => originalArtwork;
        public static bool TryApply(RawImage image, Texture artwork)
        {
            if (image == null || artwork == null) return false;
            var shader = GameArt.Load<Shader>("Art/Shaders/BossEnvironmentMotion");
            if (shader == null || !shader.isSupported) return false;
            var motion = image.GetComponent<BossEnvironmentMotion>();
            if (motion == null) motion = image.gameObject.AddComponent<BossEnvironmentMotion>();
            motion.Configure(artwork);
            motion.enabled = true;
            return !Application.isPlaying || motion.ApplyMaterial(shader);
        }
        public void Configure(Texture artwork)
        {
            originalArtwork = artwork;
            floorDisplay = artwork != null && (artwork.name.Contains("Floor") || artwork.name.Contains("Grass"));
            if (target == null) target = GetComponent<RawImage>();
            target.texture = artwork;
            target.uvRect = new Rect(0, 0, 1, 1);
            if (Application.isPlaying) ApplyMaterial(GameArt.Load<Shader>("Art/Shaders/BossEnvironmentMotion"));
        }
        public static Material CreateMaterial(Texture artwork, float strength = .85f)
        {
            var shader = GameArt.Load<Shader>("Art/Shaders/BossEnvironmentMotion");
            if (shader == null || !shader.isSupported || artwork == null) return null;
            var material = new Material(shader) { name = artwork.name + " Atmosphere", hideFlags = HideFlags.HideAndDontSave };
            SetTheme(material, artwork, strength);
            return material;
        }
        static void SetTheme(Material material, Texture artwork, float strength)
        {
            bool floor = artwork.name.Contains("Floor") || artwork.name.Contains("Grass");
            material.mainTexture = artwork;
            material.SetFloat("_Theme", artwork.name.Contains("Water") ? 1 : artwork.name.Contains("Hell") ? 2 : 0);
            material.SetFloat("_Floor", floor ? 1 : 0);
            material.SetFloat("_Strength", strength * (floor ? .85f : 1));
            material.SetFloat("_UsePreviewTime", 0);
            material.SetFloat(AspectId, (float)artwork.width / Mathf.Max(1, artwork.height));
        }
        bool ApplyMaterial(Shader shader)
        {
            if (shader == null || !shader.isSupported || originalArtwork == null) return false;
            if (target == null) target = GetComponent<RawImage>();
            Material retired = null;
            if (motionMaterial == null || motionMaterial.mainTexture != originalArtwork)
            {
                if (motionMaterial == null) previousMaterial = target.material;
                else retired = motionMaterial;
                motionMaterial = new Material(shader) { name = name + " Atmosphere", hideFlags = HideFlags.HideAndDontSave };
            }
            SetTheme(motionMaterial, originalArtwork, atmosphere);
            target.texture = originalArtwork;
            target.material = motionMaterial;
            target.SetMaterialDirty();
            UpdateMaterial();
            if (retired != null) Destroy(retired);
            return true;
        }
        void OnEnable()
        {
            if (Application.isPlaying) ApplyMaterial(GameArt.Load<Shader>("Art/Shaders/BossEnvironmentMotion"));
        }
        void OnRectTransformDimensionsChange() { UpdateMaterial(); }
        void UpdateMaterial()
        {
            if (motionMaterial == null || target == null) return;
            var rect = target.rectTransform.rect;
            motionMaterial.SetFloat(AspectId, rect.height > .001f ? rect.width / rect.height : 1);
        }
        void OnDisable()
        {
            if (target != null && target.material == motionMaterial) target.material = previousMaterial;
        }
        void OnDestroy()
        {
            if (motionMaterial == null) return;
            if (Application.isPlaying) Destroy(motionMaterial); else DestroyImmediate(motionMaterial);
        }
    }
}
