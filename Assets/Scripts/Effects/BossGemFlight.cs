using UnityEngine;
using UnityEngine.Rendering;

namespace BossSlayer
{
    public sealed class BossGemFlight : MonoBehaviour
    {
        Camera floorCamera;
        Camera wallCamera;
        Transform target;
        Renderer[] targetRenderers;
        Vector3 floorStart;
        float crossingX;
        float floorDepth;
        Beam floorBeam;
        Beam wallBeam;
        Material material;
        Mesh quad;
        bool onWall;
        float elapsed;

        sealed class Beam
        {
            public Transform Root;
            public Transform Core;
            public TrailRenderer Trail;
            public ParticleSystem Sparks;
            public float Size;
        }

        public bool Initialize(Camera floor, Camera wall, Vector3 origin, Transform boss, Color color)
        {
            var shader = GameArt.Load<Shader>("Art/Shaders/BossGemParticle");
            if (floor == null || wall == null || boss == null || shader == null) return false;
            floorCamera = floor;
            wallCamera = wall;
            target = boss;
            targetRenderers = BossVoxelSkin.VisibleRenderers(boss);
            floorStart = floor.WorldToViewportPoint(origin);
            floorDepth = Mathf.Clamp(floorStart.z, floor.nearClipPlane + 0.1f, floor.farClipPlane - 0.1f);
            crossingX = Mathf.Clamp(floorStart.x, 0.06f, 0.94f);
            material = new Material(shader) { name = "Gem Flight (runtime)" };
            material.SetColor("_Tint", color);
            quad = new Mesh { name = "Gem Flight Diamond" };
            quad.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0),
                new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0,2,1,2,3,1 };
            quad.RecalculateBounds();
            floorBeam = CreateBeam("Floor gem launch", Visual3DFollower.FloorLayer, floor, floorDepth);
            wallBeam = CreateBeam("Wall gem homing", Visual3DFollower.BossLayer, wall, WallTarget().z);
            wallBeam.Root.gameObject.SetActive(false);
            Tick(0, 0);
            return true;
        }

        Beam CreateBeam(string name, int layer, Camera camera, float depth)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.gameObject.layer = layer;
            float height = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(0.5f,0,depth)),
                camera.ViewportToWorldPoint(new Vector3(0.5f,1,depth)));
            var core = new GameObject("Gem", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            core.SetParent(root, false);
            core.gameObject.layer = layer;
            core.GetComponent<MeshFilter>().sharedMesh = quad;
            var renderer = core.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.24f;
            trail.widthMultiplier = height * 0.022f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.minVertexDistance = height * 0.003f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            var sparks = root.gameObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f,0.38f);
            main.startSpeed = height * 0.018f;
            main.startSize = new ParticleSystem.MinMaxCurve(height * 0.005f,height * 0.012f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0,Mathf.PI);
            main.maxParticles = 64;
            var emission = sparks.emission;
            emission.rateOverTime = 100;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = height * 0.005f;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,0));
            var particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return new Beam { Root = root, Core = core, Trail = trail, Sparks = sparks, Size = height * 0.085f };
        }

        Vector3 WallTarget()
        {
            var bounds = new Bounds(target.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in targetRenderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var point = wallCamera.WorldToViewportPoint(bounds.center);
            point.z = Mathf.Clamp(point.z - bounds.extents.magnitude - 0.1f,
                wallCamera.nearClipPlane + 0.1f, wallCamera.farClipPlane - 0.1f);
            return point;
        }

        // Two viewport paths meet at matching X coordinates on the physical display edges.
        public void Tick(float progress, float deltaTime)
        {
            if (floorBeam == null || target == null) return;
            elapsed += deltaTime;
            bool wallPhase = progress >= 0.5f;
            if (wallPhase && !onWall)
            {
                onWall = true;
                floorBeam.Core.gameObject.SetActive(false);
                floorBeam.Trail.emitting = false;
                floorBeam.Sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                wallBeam.Root.gameObject.SetActive(true);
            }
            var beam = wallPhase ? wallBeam : floorBeam;
            var camera = wallPhase ? wallCamera : floorCamera;
            float t = Mathf.Clamp01(wallPhase ? (progress - 0.5f) * 2 : progress * 2);
            Vector3 start = wallPhase ? new Vector3(crossingX, -0.04f, WallTarget().z) : floorStart;
            Vector3 end = wallPhase ? WallTarget() : new Vector3(crossingX, 1.04f, floorDepth);
            var position = Vector3.Lerp(start, end, t * t * (3 - 2 * t));
            position.x += Mathf.Sin(t * Mathf.PI) * (wallPhase ? 0.055f : -0.025f);
            beam.Root.position = camera.ViewportToWorldPoint(position);
            beam.Core.rotation = camera.transform.rotation * Quaternion.Euler(0, 0, elapsed * 220);
            beam.Core.localScale = Vector3.one * beam.Size * (1 + 0.12f * Mathf.Sin(elapsed * 24));
            if (!beam.Trail.emitting)
            {
                beam.Trail.Clear();
                beam.Trail.emitting = true;
                beam.Sparks.Play();
            }
        }

        void OnDestroy()
        {
            if (material != null) Release(material);
            if (quad != null) Release(quad);
        }

        static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
