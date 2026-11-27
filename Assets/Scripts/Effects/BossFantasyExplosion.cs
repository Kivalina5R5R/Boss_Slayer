using UnityEngine;
using UnityEngine.Rendering;

namespace BossSlayer
{
    public sealed class BossFantasyExplosion : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        MeshRenderer surface;
        float started, duration;
        public bool IsPlaying { get; private set; }
        public int Theme { get; private set; }
        public bool Play(Vector3 position, Camera camera, Color accent, int theme, float radius, bool finale)
        {
            if (camera == null) return false;
            if (material == null)
            {
                var shader = GameArt.Load<Shader>("Art/Shaders/BossFantasyExplosion");
                if (shader == null || !shader.isSupported) return false;
                material = new Material(shader) { name = "Toon elemental explosion (runtime)" };
                mesh = new Mesh { name = "Toon explosion billboard" };
                mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.triangles = new[] { 0,2,1,2,3,1 };
                mesh.RecalculateBounds();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                surface = gameObject.AddComponent<MeshRenderer>();
                surface.sharedMaterial = material;
                surface.shadowCastingMode = ShadowCastingMode.Off;
                surface.receiveShadows = false;
                surface.lightProbeUsage = LightProbeUsage.Off;
                surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            gameObject.layer = Visual3DFollower.BossLayer;
            transform.SetPositionAndRotation(position, camera.transform.rotation);
            transform.localScale = Vector3.one;
            var inherited = transform.lossyScale;
            transform.localScale = new Vector3(radius / Mathf.Max(.0001f, Mathf.Abs(inherited.x)), radius / Mathf.Max(.0001f, Mathf.Abs(inherited.y)), 1);
            float max = Mathf.Max(.01f, Mathf.Max(accent.r, Mathf.Max(accent.g, accent.b)));
            accent = new Color(accent.r / max, accent.g / max, accent.b / max, 1);
            material.SetColor("_Color", accent);
            material.SetFloat("_Theme", theme);
            material.SetFloat("_Seed", Random.value * 6.28f);
            Theme = theme;
            started = Time.time;
            duration = finale ? 1.2f : .8f;
            IsPlaying = true;
            surface.enabled = true;
            LateUpdate();
            return true;
        }
        void LateUpdate()
        {
            if (!IsPlaying) return;
            float progress = Mathf.Clamp01((Time.time - started) / duration);
            material.SetFloat("_Progress", progress);
            if (progress >= 1) Stop();
        }
        public void Stop() { IsPlaying = false; if (surface != null) surface.enabled = false; }
        void OnDisable() { Stop(); }
        void OnDestroy() { if (material != null) Destroy(material); if (mesh != null) Destroy(mesh); }
    }
}
