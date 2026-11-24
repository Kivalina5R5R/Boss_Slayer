using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Deepspace
{
    // The effects README_Unity.md describes for the new bosses, made of voxel cubes so they match the models.
    // Times are seconds into the named clip, taken from the README.
    public sealed partial class BossVoxelSkin
    {
        public Material shieldMaterial, wipeMaterial;
        public Mesh sphereMesh;

        const float ShieldRadius = 2.25f;
        static readonly Color Ember = new Color(1f, .55f, .15f), Gold = new Color(1f, .82f, .3f), Aqua = new Color(.45f, .9f, 1f), Violet = new Color(.72f, .4f, 1f);

        ParticleSystem fx;
        Transform shieldView, orbView;
        readonly Dictionary<string, Transform> boneByName = new();
        string lastClip;
        double lastTime;
        float ambientCarry, chargeProgress;
        // Theme change that spreads over the body from one bone.
        Material wipeInstance;
        string wipeTarget;
        Transform wipeCore;
        float wipeAge = -1f, wipeSeconds;

        struct Ambient { public string bone; public int count; public float rise, life; public Color color; }

        Ambient? AmbientForBoss()
        {
            switch (name)
            {
                case "Boss_Demon_RedBoy": return new Ambient { bone = "hair", count = 18, rise = 1.1f, life = 1.2f, color = Ember };
                case "Boss_GiantFish": return new Ambient { bone = "chest", count = 26, rise = 2.6f, life = 2f, color = Aqua };
                case "Boss_VoidWraith": return new Ambient { bone = "pelvis", count = 30, rise = 2.4f, life = 2f, color = Violet };
                default: return null;
            }
        }

        float Scale => Mathf.Abs(transform.lossyScale.x);

        Transform Bone(string boneName)
        {
            if (boneByName.Count == 0 && body != null) foreach (var bone in body.bones) boneByName[bone.name] = bone;
            return boneByName.TryGetValue(boneName, out var found) ? found : transform;
        }

        void EnsureFx()
        {
            if (fx != null) return;
            var host = new GameObject("Skin Fx");
            host.transform.SetParent(transform, false);
            host.layer = gameObject.layer;
            fx = host.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fx.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 4000; main.loop = true; main.playOnAwake = false;
            var emission = fx.emission; emission.enabled = false;
            var view = host.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Mesh; view.mesh = shardMesh; view.sharedMaterial = shardMaterial;
            fx.Play();
            shieldView = MakeBall("Skin Shield");
            orbView = MakeBall("Skin Charge Orb");
        }

        Transform MakeBall(string ballName)
        {
            var ball = new GameObject(ballName, typeof(MeshFilter), typeof(MeshRenderer));
            ball.transform.SetParent(transform, false);
            ball.layer = gameObject.layer;
            ball.GetComponent<MeshFilter>().sharedMesh = sphereMesh;
            ball.GetComponent<MeshRenderer>().sharedMaterial = shieldMaterial;
            ball.SetActive(false);
            return ball.transform;
        }

        void ResetFx()
        {
            EnsureFx();
            shieldView.gameObject.SetActive(false);
            orbView.gameObject.SetActive(false);
            chargeProgress = 0f; lastClip = null;
            CancelWipe();
        }

        void Emit(Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            var settings = new ParticleSystem.EmitParams { position = position, velocity = velocity, startColor = color, startSize = size * Scale, startLifetime = life };
            fx.Emit(settings, 1);
        }

        void Burst(Vector3 at, int count, float speed, Color color, float size = .14f, float life = .7f)
        {
            for (int i = 0; i < count; i++)
                Emit(at, Random.onUnitSphere * (speed * Random.Range(.4f, 1f) * Scale), color, size, life);
        }

        // Emits a stream of cubes for this frame at the given rate per second.
        void Stream(Vector3 from, Vector3 direction, float rate, float speed, float spread, System.Func<Color> color, float size, float life, float dt)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(rate * dt));
            for (int i = 0; i < count; i++)
                Emit(from, (direction.normalized + Random.insideUnitSphere * spread) * (speed * Scale), color(), size, life);
        }

        static Color Flame()
        {
            float heat = Random.value;
            return heat < .4f ? new Color(1f, .9f, .3f) : heat < .75f ? new Color(1f, .5f, .1f) : new Color(.9f, .15f, .05f);
        }

        void TickFx()
        {
            EnsureFx();
            float dt = Time.deltaTime;
            string clip = current != null ? current.name : null;
            double time = slots[liveSlot].IsValid() ? slots[liveSlot].GetTime() : 0d;
            if (clip != lastClip) { lastClip = clip; lastTime = -.001d; }

            var ambient = AmbientForBoss();
            if (ambient.HasValue)
            {
                ambientCarry += ambient.Value.count / ambient.Value.life * dt;
                var at = Bone(ambient.Value.bone).position;
                for (; ambientCarry >= 1f; ambientCarry -= 1f)
                    Emit(at + Random.insideUnitSphere * (.45f * Scale), Vector3.up * (ambient.Value.rise / ambient.Value.life * Scale), ambient.Value.color, .07f, ambient.Value.life);
            }

            if (shieldView.gameObject.activeSelf) shieldView.position = Bone("bell").position;
            if (orbView.gameObject.activeSelf)
            {
                orbView.position = Bone("head").position + Vector3.up * (1.4f * Scale);
                orbView.localScale = Vector3.one * Mathf.Lerp(.5f, 5f, chargeProgress);
            }
            if (halo != null && clip == "intro") haloBoost = time >= 1.95d && time <= 2.7d ? HaloChargeBoost : 1f;

            if (clip != null) ClipFx(clip, time, dt);
            lastTime = time;
            BodyFx(dt);
            TickWipe(dt);
        }

        bool Crossed(double time, double moment) => lastTime < moment && time >= moment;

        void ClipFx(string clip, double t, float dt)
        {
            bool During(double from, double to) => t >= from && t <= to;
            var forward = transform.forward;

            if ((clip == "atk_light" || clip == "atk_light_l" || clip == "atk_heavy") && Crossed(t, 2d))
                Burst(Bone(Has("atk_heavy") && name.Contains("Jelly") ? "bell" : "chest").position, clip == "atk_heavy" ? 26 : 14, 7f, Ember);

            switch (name)
            {
                case "Boss_Dragon":
                    if (clip == "fire" && During(2d, 3.5d)) Stream(Bone("jaw").position, forward, 180f, 16f, .16f, Flame, .22f, .9f, dt);
                    break;
                case "Boss_SeaSerpent":
                    if (clip == "ultimate" && During(1.5d, 3.6d))
                    {
                        float sweep = Mathf.InverseLerp(1.5f, 3.6f, (float)t);
                        Stream(Bone("head").position, forward + transform.right * Mathf.Lerp(-1f, 1f, sweep), 260f, 26f, .02f, () => Aqua, .2f, .8f, dt);
                    }
                    break;
                case "Boss_Sentinel":
                    if ((clip == "atk_heavy" && During(.45d, 2d)) || (clip == "ultimate" && During(3.1d, 4.2d)))
                    {
                        var chest = Bone("chest").position;
                        var from = chest + Random.onUnitSphere * (1.1f * Scale);
                        Emit(from, (chest - from) * 3f, Gold, .1f, .32f);
                        Emit(chest, Vector3.zero, Color.white, .3f, .08f);
                    }
                    if (clip == "ultimate" && During(4.2d, 5.1d)) Stream(Bone("chest").position, forward, 280f, 30f, .025f, () => Gold, .26f, .75f, dt);
                    if (clip == "escape") Stream(Bone("pelvis").position, -Vector3.up, 90f, 8f, .2f, Flame, .16f, .5f, dt);
                    break;
                case "Boss_Jellyfish":
                    if (clip == "ultimate" && (Crossed(t, 2d) || Crossed(t, 2.75d) || Crossed(t, 3.5d))) Burst(Bone("bell").position, 60, 14f, Aqua, .12f, .55f);
                    break;
                case "Boss_Demon_RedBoy":
                case "Boss_Demon_YakshaKing":
                    if (clip == "ultimate" && During(3.7d, 4.3d)) Burst(Bone("spear").position, 2, 3f, Ember, .08f, .5f);
                    break;
                case "Boss_AbyssalKing":
                    if (clip == "blast" && Crossed(t, .6d))
                        for (int wave = 0; wave < 3; wave++)
                            for (int i = 0; i < 90; i++)
                            {
                                var around = Quaternion.Euler(0f, i * 4f, 0f) * Vector3.forward;
                                Emit(Bone("root").position, around * ((9f + wave * 6f) * Scale), Color.Lerp(new Color(1f, .15f, .2f), new Color(.2f, 1f, .4f), Random.value), .32f, 1.4f);
                            }
                    break;
            }
        }

        public void ShowShield(bool visible)
        {
            EnsureFx();
            shieldView.gameObject.SetActive(visible);
            shieldView.localScale = Vector3.one * (ShieldRadius * 2f / Mathf.Max(.0001f, Mathf.Abs(transform.localScale.x)) * Mathf.Abs(transform.localScale.x));
        }

        public void SetChargeProgress(float progress)
        {
            EnsureFx();
            chargeProgress = Mathf.Clamp01(progress);
            orbView.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- theme wipe
        static float WipeSecondsFor(string theme)
        {
            switch (theme) { case "ember": return .6f; case "lava": return .75f; case "mist": case "void": return .35f; default: return 1f; }
        }

        Material ThemeMaterial(string theme)
        {
            foreach (var entry in themes) if (entry.name == theme) return entry.material;
            return null;
        }

        void CancelWipe()
        {
            wipeAge = -1f;
            if (wipeInstance != null) Destroy(wipeInstance);
            wipeInstance = null;
        }

        public void WipeTo(string theme)
        {
            var target = ThemeMaterial(theme);
            if (target == null) return;
            var from = body.sharedMaterial;
            CancelWipe();
            if (wipeMaterial == null || from == null || !Application.isPlaying) { SetTheme(theme); return; }
            wipeInstance = new Material(wipeMaterial);
            foreach (var map in new[] { "_BaseMap", "_EmissionMap", "_MetallicGlossMap" })
            {
                wipeInstance.SetTexture(map, from.GetTexture(map));
                wipeInstance.SetTexture(map + "B", target.GetTexture(map));
            }
            wipeInstance.SetTexture("_OcclusionMap", from.GetTexture("_OcclusionMap"));
            body.sharedMaterial = wipeInstance;
            wipeTarget = theme;
            wipeSeconds = WipeSecondsFor(theme);
            wipeCore = Bone(name == "Boss_AbyssalKing" ? "head" : boneByName.ContainsKey("chest") ? "chest" : "pelvis");
            wipeAge = 0f;
        }

        void TickWipe(float dt)
        {
            if (wipeAge < 0f || wipeInstance == null) return;
            wipeAge += dt;
            float reach = body.bounds.extents.magnitude * 1.3f;
            wipeInstance.SetVector("_WipeCenter", wipeCore.position);
            wipeInstance.SetFloat("_WipeRadius", reach * Mathf.Clamp01(wipeAge / wipeSeconds));
            wipeInstance.SetFloat("_WipeEdge", reach * .12f);
            if (wipeAge < wipeSeconds) return;
            string target = wipeTarget;
            CancelWipe();
            SetTheme(target);
        }
    }
}
