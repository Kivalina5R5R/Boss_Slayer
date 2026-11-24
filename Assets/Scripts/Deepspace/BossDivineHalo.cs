using UnityEngine;
using UnityEngine.Rendering;

namespace Deepspace
{
    [DisallowMultipleComponent]
    public sealed class BossDivineHalo : MonoBehaviour
    {
        Transform target;
        Camera displayCamera;
        Renderer[] bodyRenderers;
        Transform halo;
        MeshRenderer haloRenderer;
        Material material;
        Mesh mesh;
        bool visible;
        static readonly int AnimationTimeId = Shader.PropertyToID("_AnimationTime");

        public void Bind(Transform boss, Camera camera, Color color)
        {
            target = boss;
            displayCamera = camera;
            bodyRenderers = boss != null ? BossVoxelSkin.VisibleRenderers(boss) : null;
            if (halo == null)
            {
                var shader = Resources.Load<Shader>("BossDivineHalo");
                if (shader == null)
                {
                    Debug.LogError("Boss Divine Halo shader is missing from Resources.", this);
                    return;
                }
                material = new Material(shader) { name = "Boss Divine Halo (runtime)" };
                mesh = new Mesh { name = "Boss Divine Halo Quad" };
                mesh.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0),
                    new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.triangles = new[] { 0,2,1,2,3,1 };
                mesh.RecalculateBounds();
                var go = new GameObject("VFX == Divine Halo", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = Visual3DFollower.BossLayer;
                halo = go.transform;
                halo.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                haloRenderer = go.GetComponent<MeshRenderer>();
                haloRenderer.sharedMaterial = material;
                haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
                haloRenderer.receiveShadows = false;
                haloRenderer.lightProbeUsage = LightProbeUsage.Off;
                haloRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            material.SetColor("_Color", color);
            visible = true;
            Tick(Time.time);
        }

        public void Hide()
        {
            visible = false;
            if (haloRenderer != null) haloRenderer.enabled = false;
        }

        void LateUpdate() { Tick(Time.time); }

        public void Tick(float time)
        {
            if (haloRenderer == null) return;
            bool show = visible && target != null && target.gameObject.activeInHierarchy
                && displayCamera != null && bodyRenderers != null;
            haloRenderer.enabled = show;
            if (!show) return;
            var bounds = new Bounds();
            bool found = false;
            foreach (var renderer in bodyRenderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found || bounds.extents.sqrMagnitude < 0.00001f)
            {
                haloRenderer.enabled = false;
                return;
            }
            var extents = bounds.extents;
            float radius = RadiusForBounds(bounds, displayCamera, bodyRenderers);
            halo.SetPositionAndRotation(bounds.center + displayCamera.transform.forward * (extents.magnitude + 0.12f),
                displayCamera.transform.rotation);
            // A sibling of the boss keeps the aura out of gameplay/VFX bounds calculations.
            halo.localScale = Vector3.one;
            var parentScale = halo.lossyScale;
            float size = radius * 2 / 0.72f;
            halo.localScale = new Vector3(size / Mathf.Max(Mathf.Abs(parentScale.x),0.0001f),
                size / Mathf.Max(Mathf.Abs(parentScale.y),0.0001f), 1);
            material.SetFloat(AnimationTimeId, time);
        }

        public static float RadiusForBounds(Bounds bounds, Camera camera, Renderer[] renderers = null)
        {
            var right = camera.transform.right;
            var up = camera.transform.up;
            if (renderers != null)
            {
                float radiusSquared = 0;
                // Project local bounds directly; rotated world AABBs count empty corners twice.
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer is ParticleSystemRenderer) continue;
                    var local = renderer.localBounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = local.center + Vector3.Scale(local.extents,new Vector3(
                            (corner & 1) == 0 ? -1 : 1,(corner & 2) == 0 ? -1 : 1,(corner & 4) == 0 ? -1 : 1));
                        var offset = renderer.transform.TransformPoint(point)-bounds.center;
                        float x = Vector3.Dot(offset,right), y = Vector3.Dot(offset,up);
                        radiusSquared = Mathf.Max(radiusSquared,x*x+y*y);
                    }
                }
                if (radiusSquared > 0) return Mathf.Sqrt(radiusSquared)*1.12f;
            }
            var extents = bounds.extents;
            float width = Mathf.Abs(right.x)*extents.x + Mathf.Abs(right.y)*extents.y + Mathf.Abs(right.z)*extents.z;
            float height = Mathf.Abs(up.x)*extents.x + Mathf.Abs(up.y)*extents.y + Mathf.Abs(up.z)*extents.z;
            return Mathf.Sqrt(width * width + height * height) * 1.08f;
        }

        void OnDisable() { Hide(); }
        void OnDestroy()
        {
            if (halo != null) Release(halo.gameObject);
            if (material != null) Release(material);
            if (mesh != null) Release(mesh);
        }
        static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
