using UnityEngine;
using UnityEngine.Rendering;

namespace Deepspace
{
    // Reused wall-display cast cue. Kept outside model bounds and driven by scaled game time.
    public sealed class BossChargeVfx : MonoBehaviour
    {
        const float ReleaseSeconds = 0.8f;
        const int HeadCount = 96;
        const int TrailSamples = 4;
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[HeadCount * TrailSamples];
        Transform boss;
        Camera wall;
        Renderer[] body;
        Mesh mesh;
        MeshRenderer surface;
        MeshRenderer burstSurface;
        Material material;
        Material burstMaterial;
        Material particleMaterial;
        ParticleSystem motes;
        Color color;
        float started, duration, scale;
        bool playing;
        public bool IsPlaying => playing;
        public int Theme { get; private set; }

        public bool Play(Transform target, Camera camera, Renderer[] renderers, Color accent, float seconds, float size)
        {
            if (target == null || camera == null || renderers == null || renderers.Length == 0) return false;
            if (!CreateVisuals()) return false;
            boss = target;
            wall = camera;
            body = renderers;
            Theme = BossAttackVfx.ThemeFor(target.name);
            float maximum = Mathf.Max(0.1f, Mathf.Max(accent.r, Mathf.Max(accent.g, accent.b)));
            color = new Color(accent.r / maximum, accent.g / maximum, accent.b / maximum, 1);
            material.SetColor("_Color", color);
            material.SetFloat("_Theme", Theme);
            burstMaterial.SetColor("_Color", color);
            burstMaterial.SetFloat("_Theme", Theme);
            particleMaterial.SetFloat("_Theme", Theme);
            started = Time.time;
            duration = Mathf.Max(0.1f, seconds);
            scale = Mathf.Clamp(size, 0.5f, 2f);
            playing = true;
            surface.enabled = true;
            burstSurface.enabled = true;
            motes.Play();
            LateUpdate();
            return true;
        }

        bool CreateVisuals()
        {
            if (material != null) return true;
            var shader = Resources.Load<Shader>("BossCharge");
            var sparkShader = Resources.Load<Shader>("BossAttackSpark");
            var burstShader = Resources.Load<Shader>("BossChargeBurst");
            if (shader == null || sparkShader == null || burstShader == null
                || !shader.isSupported || !sparkShader.isSupported || !burstShader.isSupported) return false;
            material = new Material(shader) { name = "Boss cast energy (runtime)" };
            burstMaterial = new Material(burstShader) { name = "Boss wide elemental release (runtime)" };
            particleMaterial = new Material(sparkShader) { name = "Boss cast motes (runtime)" };
            mesh = new Mesh { name = "Boss cast surface" };
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0,2,1,2,3,1 };
            mesh.RecalculateBounds();
            var panel = new GameObject("VFX == Elemental cast", typeof(MeshFilter), typeof(MeshRenderer));
            panel.layer = Visual3DFollower.BossLayer;
            panel.transform.SetParent(transform, false);
            panel.GetComponent<MeshFilter>().sharedMesh = mesh;
            surface = panel.GetComponent<MeshRenderer>();
            surface.sharedMaterial = material;
            ConfigureRenderer(surface);
            var burstPanel = new GameObject("VFX == Wide energy surge", typeof(MeshFilter), typeof(MeshRenderer));
            burstPanel.layer = Visual3DFollower.BossLayer;
            burstPanel.transform.SetParent(transform, false);
            burstPanel.GetComponent<MeshFilter>().sharedMesh = mesh;
            burstSurface = burstPanel.GetComponent<MeshRenderer>();
            burstSurface.sharedMaterial = burstMaterial;
            ConfigureRenderer(burstSurface);
            var sparks = new GameObject("VFX == Converging energy", typeof(ParticleSystem));
            sparks.layer = Visual3DFollower.BossLayer;
            sparks.transform.SetParent(transform, false);
            motes = sparks.GetComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = particles.Length;
            main.startSpeed = 0;
            var emission = motes.emission;
            emission.enabled = false;
            var shape = motes.shape;
            shape.enabled = false;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            ConfigureRenderer(renderer);
            return true;
        }

        static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void LateUpdate()
        {
            if (!playing) return;
            float elapsed = Time.time - started;
            if (boss == null || !boss.gameObject.activeInHierarchy || wall == null || elapsed >= duration + ReleaseSeconds)
            {
                Stop();
                return;
            }
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in body)
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) { Stop(); return; }
            float radius = BossDivineHalo.RadiusForBounds(bounds, wall, body) * scale * 1.15f;
            var cam = wall.transform;
            // Put the surface behind the silhouette; its centre remains transparent.
            var center = bounds.center + cam.forward * (bounds.extents.magnitude + 0.08f);
            float depth = Vector3.Dot(center - cam.position, cam.forward);
            if (depth <= wall.nearClipPlane) { Stop(); return; }
            float halfHeight = wall.orthographic ? wall.orthographicSize : depth * Mathf.Tan(wall.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var offset = center - (cam.position + cam.forward * depth);
            float margin = Mathf.Min(halfHeight - Mathf.Abs(Vector3.Dot(offset, cam.up)),
                halfHeight * wall.aspect - Mathf.Abs(Vector3.Dot(offset, cam.right)));
            radius = Mathf.Min(radius, Mathf.Max(0.01f, margin) * 0.96f);
            float extent = Mathf.Max(0.01f, radius / 0.82f);
            var panel = surface.transform;
            panel.SetPositionAndRotation(center, cam.rotation);
            panel.localScale = Vector3.one;
            var inherited = panel.lossyScale;
            panel.localScale = new Vector3(extent / Mathf.Max(0.0001f, Mathf.Abs(inherited.x)),
                extent / Mathf.Max(0.0001f, Mathf.Abs(inherited.y)), 1);
            float progress = Mathf.Clamp01(elapsed / duration);
            float release = Mathf.Clamp01((elapsed - duration) / ReleaseSeconds);
            float fade = Mathf.SmoothStep(0, 1, elapsed / 0.16f) * (1 - Mathf.SmoothStep(0, 1, release));
            // Keep the gathering seal distinct from the expanding release fronts.
            panel.localScale *= release > 0 ? Mathf.Lerp(1, 0.78f, Mathf.Min(1, release * 4))
                : 1 + 0.025f * Mathf.Sin(progress * progress * 22) * progress;
            material.SetFloat("_Clock", elapsed);
            material.SetFloat("_Progress", progress);
            material.SetFloat("_Release", release);
            material.SetFloat("_Fade", fade);
            var surge = burstSurface.transform;
            surge.SetPositionAndRotation(center + cam.forward * 0.01f, cam.rotation);
            surge.localScale = Vector3.one;
            var surgeScale = surge.lossyScale;
            float wide = Mathf.Max(radius, Mathf.Min(radius * 3.3f,
                (halfHeight * wall.aspect - Mathf.Abs(Vector3.Dot(offset, cam.right))) * 0.94f));
            float tall = Mathf.Max(0.01f, (halfHeight - Mathf.Abs(Vector3.Dot(offset, cam.up))) * 1.02f);
            surge.localScale = new Vector3(wide / Mathf.Max(0.0001f, Mathf.Abs(surgeScale.x)),
                tall / Mathf.Max(0.0001f, Mathf.Abs(surgeScale.y)), 1);
            burstMaterial.SetVector("_Extent", new Vector4(wide / radius, tall / radius, 0, 0));
            burstMaterial.SetFloat("_Clock", elapsed);
            burstMaterial.SetFloat("_Progress", progress);
            burstMaterial.SetFloat("_Release", release);
            burstMaterial.SetFloat("_Fade", fade);

            for (int i = 0; i < HeadCount; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f, 1);
                float travel = Mathf.Repeat(seed + elapsed * (0.3f + progress * 0.65f), 1);
                for (int sample = 0; sample < TrailSamples; sample++)
                {
                    int index = i * TrailSamples + sample;
                    float along = Mathf.Clamp01(travel - sample * 0.018f);
                    float angle = i * 2.39996f + elapsed * (Theme == 7 ? -0.8f : 0.35f) + along * 1.1f;
                    float orbit = Mathf.Lerp(i % 3 == 0 ? 2.8f : 1.6f, 0.45f, along);
                    if (Theme == 4 || Theme == 6) angle = Mathf.Round(angle / (Mathf.PI / 3)) * (Mathf.PI / 3) + seed * 0.12f;
                    if (Theme == 2) angle += Mathf.Sin(along * 26 + i) * 0.045f;
                    if (release > 0)
                    {
                        float flight = Mathf.Max(0, release - sample * 0.012f);
                        angle = i * 2.39996f + seed * 0.3f;
                        orbit = 0.6f + flight * (1.8f + seed * 2.4f);
                    }
                    float x = Mathf.Cos(angle) * radius * orbit;
                    float y = Mathf.Sin(angle) * radius * orbit * (i % 3 == 0 ? 0.5f : 1);
                    float edge = Mathf.Clamp01((wide - Mathf.Abs(x)) / (radius * 0.3f))
                        * Mathf.Clamp01((tall - Mathf.Abs(y)) / (radius * 0.2f));
                    particles[index].position = center + cam.right * x + cam.up * y;
                    particles[index].velocity = Vector3.zero;
                    particles[index].startLifetime = 10;
                    particles[index].remainingLifetime = 10;
                    float taper = 1 - sample / (float)TrailSamples;
                    particles[index].startSize = radius * Mathf.Lerp(0.03f, 0.075f, seed)
                        * (0.6f + progress * 0.55f) * taper * (release > 0 ? 1.35f : 1);
                    particles[index].rotation = angle * Mathf.Rad2Deg;
                    var tint = Color.Lerp(color, Color.white, seed * 0.45f);
                    tint.a = (release > 0 ? 1 : Mathf.Sin(travel * Mathf.PI)) * fade * taper * edge;
                    particles[index].startColor = tint;
                }
            }
            motes.SetParticles(particles, particles.Length);
        }

        public void Stop()
        {
            playing = false;
            if (surface != null) surface.enabled = false;
            if (burstSurface != null) burstSurface.enabled = false;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (burstMaterial != null) Destroy(burstMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (mesh != null) Destroy(mesh);
        }
    }
}
