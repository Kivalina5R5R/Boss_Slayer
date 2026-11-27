using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-10-20: the 15-minute session's last boss, built entirely in code like BossHydra (no imported model).
    // An obsidian king floating on an abyss vortex: crowned helm, glowing core, tattered cape, a halo of orbiting
    // shards and two giant detached gauntlets that do the slams, claps and sweeps. Front is -Z, up is +Y.
    // Gameplay never reads this rig; the manager only drives BeginCast / Release / Stun / Awaken / death.
    public sealed class BossAbyssalKing : MonoBehaviour
    {
        public const string ModelName = "AbyssalKing";
        public const float DeathPoseSeconds = 3.2f;
        public const float AwakenSeconds = 2.8f;
        const float Cell = .12f;
        const int CapeStrips = 7, VortexRings = 5, HaloShards = 12, CrownSpikes = 9;

        // ---------------------------------------------------------------- rig
        Transform body, head, crown, crownFlame, cape, vortex, halo, haloAwakened, core, eyes, eyesAwakened;
        readonly Transform[] hands = new Transform[2];
        readonly Transform[][] fingers = new Transform[2][];
        readonly Transform[] capeStrips = new Transform[CapeStrips];
        readonly Transform[] rings = new Transform[VortexRings];
        readonly Transform[] shards = new Transform[HaloShards];
        readonly Transform[] spikes = new Transform[CrownSpikes];
        readonly Vector3[] spikeRest = new Vector3[CrownSpikes];
        readonly Vector3[] handRest = { new Vector3(-2.9f, .1f, -.45f), new Vector3(2.9f, .1f, -.45f) };
        readonly List<Mesh> meshes = new List<Mesh>();
        Material material;

        // ---------------------------------------------------------------- motion state
        float clock, castLeft, castTotal, releaseAge = -1, stunLeft, stunTotal, awakenAge = -1, deathAge = -1;
        int castVariant, releaseVariant;
        bool awakened;
        Vector3[] handFrom = new Vector3[2];
        Vector3 bodyOffset;
        public bool IsCasting => castLeft > 0f;
        public bool IsAwakened => awakened;

        public static BossAbyssalKing Create(Transform reference)
        {
            var go = new GameObject(ModelName);
            go.SetActive(false);
            var king = go.AddComponent<BossAbyssalKing>();
            king.Build();
            go.transform.localScale = Vector3.one;
            if (reference != null)
            {
                var renderers = reference.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(reference.position, Vector3.one * 5);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                }
                // The king is ~6.6 units tall (vortex tip to crown) and should loom larger than a regular boss.
                go.transform.position = bounds.center;
                go.transform.localScale = Vector3.one * Mathf.Max(.01f, bounds.size.y / 6.6f * 1.25f);
            }
            Visual3DFollower.SetLayerRecursive(go, Visual3DFollower.BossLayer);
            return king;
        }

        // ---------------------------------------------------------------- manager API
        public void BeginCast(int variant, float seconds)
        {
            if (deathAge >= 0) return;
            castVariant = variant; castTotal = castLeft = Mathf.Max(.05f, seconds);
            for (int i = 0; i < 2; i++) handFrom[i] = hands[i].localPosition;
        }

        public void Release(int variant)
        {
            if (deathAge >= 0) return;
            if (releaseAge >= 0 && releaseAge < .3f) return; // a barrage lands many hits in a row: keep one strike going
            releaseVariant = variant; releaseAge = 0; castLeft = 0;
            for (int i = 0; i < 2; i++) handFrom[i] = hands[i].localPosition;
        }

        public void Stun(float seconds) { stunTotal = stunLeft = Mathf.Max(.01f, seconds); castLeft = 0; releaseAge = -1; }
        public void Awaken() { awakenAge = 0; castLeft = 0; releaseAge = -1; }
        public void ResetMotion()
        {
            clock = 0; castLeft = 0; releaseAge = -1; stunLeft = 0; awakenAge = -1; deathAge = -1; awakened = false;
            SetAwakenedLook(false);
            if (body == null) return;
            body.localScale = vortex.localScale = Vector3.one;
            foreach (var hand in hands) hand.localScale = Vector3.one;
            for (int i = 0; i < CrownSpikes; i++) { spikes[i].localPosition = spikeRest[i]; spikes[i].localRotation = SpikeRotation(i); spikes[i].gameObject.SetActive(true); }
            for (int i = 0; i < HaloShards; i++) shards[i].gameObject.SetActive(true);
            transform.Find("Rig")?.gameObject.SetActive(true);
            Pose(0);
        }

        // With the new model on, effects start from its gloves, chest and head instead of the hidden built one.
        BossVoxelSkin Skin => BossVoxelSkin.ActiveOn(transform);
        public Vector3 HandWorld(int side) => Skin != null ? Skin.Tip(side <= 0 ? "glove_L" : "glove_R", Skin.transform.forward) : hands[Mathf.Clamp(side, 0, 1)].position;
        public Vector3 CoreWorld => Skin != null ? Skin.Tip("chest", Skin.transform.forward) : core.position;
        public Vector3 EyeWorld => Skin != null ? Skin.Tip("head", Skin.transform.forward) : eyes.position;
        public Vector3 CrownWorld => Skin != null ? Skin.Tip("head", Vector3.up) : crown.position;

        public void BeginDeathPose() { deathAge = 0; castLeft = 0; releaseAge = -1; stunLeft = 0; }
        public void SampleDeathPose(float age) { deathAge = Mathf.Max(0, age); Pose(0); }
        public void SampleDeathDissolve(float progress)
        {
            float k = 1 - Mathf.SmoothStep(0, 1, progress);
            body.localScale = Vector3.one * Mathf.Max(.001f, k);
            vortex.localScale = Vector3.one * Mathf.Max(.001f, k * k);
            foreach (var hand in hands) hand.localScale = Vector3.one * Mathf.Max(.001f, k);
        }

        void LateUpdate()
        {
            if (body == null || deathAge >= 0) return;
            float dt = Time.deltaTime;
            clock += dt;
            if (castLeft > 0) castLeft = Mathf.Max(0, castLeft - dt);
            if (releaseAge >= 0) { releaseAge += dt; if (releaseAge > 1.5f) releaseAge = -1; }
            if (stunLeft > 0) stunLeft = Mathf.Max(0, stunLeft - dt);
            if (awakenAge >= 0)
            {
                awakenAge += dt;
                if (!awakened && awakenAge >= AwakenSeconds * .55f) { awakened = true; SetAwakenedLook(true); }
                if (awakenAge >= AwakenSeconds) awakenAge = -1;
            }
            Pose(dt);
        }

        // ---------------------------------------------------------------- posing
        void Pose(float dt)
        {
            float t = clock, fury = awakened ? 1 : 0;
            // Idle: a slow breathing hover; the vortex and halo never stop turning.
            var bodyPos = new Vector3(0, Mathf.Sin(t * 1.3f) * .08f, 0);
            var bodyRot = new Vector3(Mathf.Sin(t * .9f) * 2f, Mathf.Sin(t * .6f) * 4f, Mathf.Sin(t * .7f) * 1.5f);
            var headRot = new Vector3(Mathf.Sin(t * 1.1f) * 3f, Mathf.Sin(t * .7f) * 6f, 0);
            var hand = new Vector3[2];
            var handRot = new Vector3[2];
            var curl = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1 : 1, ph = i * 1.7f;
                hand[i] = handRest[i] + new Vector3(Mathf.Sin(t * .8f + ph) * .12f, Mathf.Sin(t * 1.6f + ph) * .2f, Mathf.Cos(t * 1.1f + ph) * .1f);
                handRot[i] = new Vector3(Mathf.Sin(t * 1.2f + ph) * 6f, s * -12f, s * (10f + Mathf.Sin(t + ph) * 5f));
                curl[i] = .25f + .12f * Mathf.Sin(t * 1.4f + ph);
            }
            float haloSpin = 22f + 30f * fury, haloScale = 1 + .12f * fury, coreGlow = 1 + .08f * Mathf.Sin(t * 4f);

            // Cast wind-up: blend toward the variant's charged pose as the release approaches.
            float charge = castTotal > 0 && castLeft > 0 ? Ease(1 - castLeft / castTotal) : 0;
            if (charge > 0) ChargedPose(Family(castVariant), charge, ref bodyPos, ref bodyRot, ref headRot, hand, handRot, curl, ref haloSpin, ref haloScale, ref coreGlow);
            // Release: a fast strike (0-0.35 s), then settle back (to 1.5 s).
            if (releaseAge >= 0)
            {
                float strike = Ease(Mathf.Clamp01(releaseAge / .35f)), settle = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1.5f, releaseAge));
                StrikePose(Family(releaseVariant), strike, settle, ref bodyPos, ref bodyRot, ref headRot, hand, handRot, curl, ref haloSpin, ref coreGlow);
            }
            // Awakening roar at 50% HP: arms flung wide, head thrown back, the whole body shudders.
            if (awakenAge >= 0)
            {
                float a = Mathf.Sin(Mathf.Clamp01(awakenAge / AwakenSeconds) * Mathf.PI);
                bodyPos += new Vector3(Mathf.Sin(t * 47) * .05f * a, .45f * a, 0);
                bodyRot += new Vector3(-14f * a, 0, Mathf.Sin(t * 39) * 3 * a);
                headRot += new Vector3(-28f * a, Mathf.Sin(t * 31) * 4 * a, 0);
                for (int i = 0; i < 2; i++)
                {
                    float s = i == 0 ? -1 : 1;
                    hand[i] = Vector3.Lerp(hand[i], new Vector3(s * 3.9f, 2.1f, .2f), a);
                    handRot[i] += new Vector3(0, 0, s * 40f * a);
                    curl[i] = Mathf.Lerp(curl[i], 0, a);
                }
                haloSpin += 260f * a; haloScale += .35f * a; coreGlow += .8f * a;
            }
            // Stunned: slumped, gauntlets hanging, halo wobbling and slowing.
            if (stunLeft > 0)
            {
                float s = Mathf.Clamp01(Mathf.Min(stunLeft, stunTotal - stunLeft) / .35f);
                bodyPos = Vector3.Lerp(bodyPos, new Vector3(0, -.35f, 0), s);
                bodyRot = Vector3.Lerp(bodyRot, new Vector3(16f, Mathf.Sin(t * 2.3f) * 5f, Mathf.Sin(t * 1.7f) * 6f), s);
                headRot = Vector3.Lerp(headRot, new Vector3(28f, Mathf.Sin(t * 1.9f) * 10f, 0), s);
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    hand[i] = Vector3.Lerp(hand[i], new Vector3(side * 2.2f, -1.7f + Mathf.Sin(t * 2f + i) * .06f, -.1f), s);
                    handRot[i] = Vector3.Lerp(handRot[i], new Vector3(70f, side * -20f, side * 30f), s);
                    curl[i] = Mathf.Lerp(curl[i], .05f, s);
                }
                haloSpin *= 1 - .8f * s; coreGlow *= 1 - .45f * s;
            }

            body.localPosition = bodyPos + bodyOffset;
            body.localRotation = Quaternion.Euler(bodyRot);
            head.localRotation = Quaternion.Euler(headRot);
            core.localScale = Vector3.one * coreGlow;
            eyes.localScale = eyesAwakened.localScale = new Vector3(1, stunLeft > 0 ? .35f : 1, 1);
            for (int i = 0; i < 2; i++)
            {
                hands[i].localPosition = hand[i];
                hands[i].localRotation = Quaternion.Euler(handRot[i]);
                for (int f = 0; f < fingers[i].Length; f++)
                {
                    bool thumb = f == fingers[i].Length - 1;
                    float c = Mathf.Clamp01(curl[i]) * (thumb ? 50f : 95f);
                    fingers[i][f].localRotation = Quaternion.Euler(thumb ? 0 : -c, 0, thumb ? (i == 0 ? c : -c) : 0);
                    if (fingers[i][f].childCount > 0) fingers[i][f].GetChild(0).localRotation = Quaternion.Euler(thumb ? 0 : -c * .8f, 0, 0);
                }
            }
            for (int i = 0; i < CapeStrips; i++)
                capeStrips[i].localRotation = Quaternion.Euler(10f + Mathf.Sin(t * 1.8f + i * .6f) * (7f + 6f * fury) - bodyRot.x * .5f, 0, Mathf.Sin(t * 1.3f + i) * 3f);
            for (int i = 0; i < VortexRings; i++)
                rings[i].localRotation = Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * t * (55f + i * 18f) * (1 + fury), 0);
            halo.localRotation = Quaternion.Euler(0, 0, Repeat360(halo.localEulerAngles.z + haloSpin * Mathf.Max(dt, 0)));
            halo.localScale = Vector3.one * haloScale;
            haloAwakened.localRotation = Quaternion.Euler(0, 0, -t * 40f);
            haloAwakened.localScale = Vector3.one * (haloScale * (1 + .04f * Mathf.Sin(t * 6f)));
            for (int i = 0; i < HaloShards; i++)
                shards[i].localPosition = ShardRest(i) + new Vector3(0, 0, Mathf.Sin(t * 2f + i) * .08f);
            if (crownFlame.gameObject.activeSelf) crownFlame.localScale = new Vector3(1, 1 + .18f * Mathf.Sin(t * 9f) + .1f * Mathf.Sin(t * 23f), 1);
            if (deathAge >= 0) DeathPose(deathAge);
        }

        // Five readable motion families; the extended technique variants (6-13) reuse them.
        static int Family(int variant) => variant <= 0 ? 2 : variant <= 5 ? variant : 1 + (variant % 5);

        void ChargedPose(int family, float c, ref Vector3 bodyPos, ref Vector3 bodyRot, ref Vector3 headRot, Vector3[] hand, Vector3[] handRot, float[] curl,
            ref float haloSpin, ref float haloScale, ref float coreGlow)
        {
            switch (family)
            {
                case 1: // sweep: both gauntlets wound back to the right
                    hand[0] = Vector3.Lerp(hand[0], new Vector3(.6f, 1.1f, -1.4f), c); hand[1] = Vector3.Lerp(hand[1], new Vector3(3.4f, 1.3f, .3f), c);
                    bodyRot += new Vector3(0, -24f * c, -6f * c); curl[0] = curl[1] = Mathf.Lerp(curl[0], .1f, c); break;
                case 2: // slam: fists raised high over the crown
                    hand[0] = Vector3.Lerp(hand[0], new Vector3(-1.5f, 2.9f, -.7f), c); hand[1] = Vector3.Lerp(hand[1], new Vector3(1.5f, 2.9f, -.7f), c);
                    handRot[0] += new Vector3(-60f * c, 0, 0); handRot[1] += new Vector3(-60f * c, 0, 0);
                    curl[0] = curl[1] = Mathf.Lerp(curl[1], 1f, c); bodyPos.y += .25f * c; headRot.x -= 12f * c; break;
                case 3: // projectile: right palm thrust forward, charging
                    hand[1] = Vector3.Lerp(hand[1], new Vector3(1.1f, .7f, -2.1f), c); handRot[1] = Vector3.Lerp(handRot[1], new Vector3(-80f, 0, 0), c);
                    hand[0] = Vector3.Lerp(hand[0], new Vector3(-1.3f, .2f, -1.1f), c); curl[1] = Mathf.Lerp(curl[1], 0, c);
                    headRot += new Vector3(10f * c, 8f * c, 0); coreGlow += .3f * c; break;
                case 4: // summon: arms spread, rising, halo blazing
                    hand[0] = Vector3.Lerp(hand[0], new Vector3(-3.7f, 1.9f, -.1f), c); hand[1] = Vector3.Lerp(hand[1], new Vector3(3.7f, 1.9f, -.1f), c);
                    handRot[0] += new Vector3(0, 0, -35f * c); handRot[1] += new Vector3(0, 0, 35f * c);
                    curl[0] = curl[1] = Mathf.Lerp(curl[0], 0, c); bodyPos.y += .5f * c; headRot.x -= 15f * c;
                    haloSpin += 200f * c; haloScale += .3f * c; coreGlow += .6f * c; break;
                default: // clap: gauntlets pulled wide apart
                    hand[0] = Vector3.Lerp(hand[0], new Vector3(-4f, .6f, -1.2f), c); hand[1] = Vector3.Lerp(hand[1], new Vector3(4f, .6f, -1.2f), c);
                    handRot[0] += new Vector3(0, 40f * c, 0); handRot[1] += new Vector3(0, -40f * c, 0);
                    curl[0] = curl[1] = Mathf.Lerp(curl[0], 0, c); bodyRot.x -= 6f * c; break;
            }
        }

        void StrikePose(int family, float strike, float settle, ref Vector3 bodyPos, ref Vector3 bodyRot, ref Vector3 headRot, Vector3[] hand, Vector3[] handRot, float[] curl,
            ref float haloSpin, ref float coreGlow)
        {
            float w = settle;
            switch (family)
            {
                case 1: // sweep across to the left
                    hand[0] = Vector3.Lerp(hand[0], Vector3.Lerp(handFrom[0], new Vector3(-3.6f, -.4f, -1.4f), strike), w);
                    hand[1] = Vector3.Lerp(hand[1], Vector3.Lerp(handFrom[1], new Vector3(-1.1f, -.2f, -1.9f), strike), w);
                    bodyRot += new Vector3(0, 26f * strike * w, 5f * w); break;
                case 2: // slam into the floor
                    hand[0] = Vector3.Lerp(hand[0], Vector3.Lerp(handFrom[0], new Vector3(-1.9f, -1.6f, -1.7f), strike), w);
                    hand[1] = Vector3.Lerp(hand[1], Vector3.Lerp(handFrom[1], new Vector3(1.9f, -1.6f, -1.7f), strike), w);
                    handRot[0] += new Vector3(70f * w, 0, 0); handRot[1] += new Vector3(70f * w, 0, 0);
                    curl[0] = curl[1] = Mathf.Lerp(curl[0], 1, w); bodyPos.y -= .3f * strike * w; bodyRot.x += 12f * strike * w; headRot.x += 14f * strike * w; break;
                case 3: // blast recoil
                    hand[1] = Vector3.Lerp(hand[1], Vector3.Lerp(handFrom[1], new Vector3(1.4f, .9f, -1.4f), strike), w);
                    handRot[1] = Vector3.Lerp(handRot[1], new Vector3(-60f, 0, 0), w); curl[1] = Mathf.Lerp(curl[1], 0, w);
                    bodyRot.x -= 8f * strike * w; coreGlow += .5f * (1 - strike) * w; break;
                case 4: // summon released: fists drive down, the halo flares
                    hand[0] = Vector3.Lerp(hand[0], Vector3.Lerp(handFrom[0], new Vector3(-2.6f, -.8f, -1.2f), strike), w);
                    hand[1] = Vector3.Lerp(hand[1], Vector3.Lerp(handFrom[1], new Vector3(2.6f, -.8f, -1.2f), strike), w);
                    curl[0] = curl[1] = Mathf.Lerp(curl[0], 1, w); haloSpin += 300f * w * (1 - strike); coreGlow += .7f * w * (1 - strike); break;
                default: // clap together in front of the core
                    hand[0] = Vector3.Lerp(hand[0], Vector3.Lerp(handFrom[0], new Vector3(-.45f, .5f, -1.8f), strike), w);
                    hand[1] = Vector3.Lerp(hand[1], Vector3.Lerp(handFrom[1], new Vector3(.45f, .5f, -1.8f), strike), w);
                    handRot[0] += new Vector3(0, -70f * w, 90f * w); handRot[1] += new Vector3(0, 70f * w, -90f * w);
                    curl[0] = curl[1] = Mathf.Lerp(curl[0], 0, w); bodyRot.x += 6f * w; break;
            }
        }

        // Death: the crown bursts apart, the halo scatters, the gauntlets crumble and the body sinks into the vortex.
        void DeathPose(float age)
        {
            float u = Mathf.Clamp01(age / DeathPoseSeconds), early = Mathf.Clamp01(age / 1.1f);
            body.localPosition = new Vector3(Mathf.Sin(age * 37) * .06f * (1 - u), -1.4f * u * u, 0);
            body.localRotation = Quaternion.Euler(-22f * early + 30f * u * u, Mathf.Sin(age * 5) * 6f, Mathf.Sin(age * 3) * 8f * u);
            head.localRotation = Quaternion.Euler(-30f * early + 55f * u * u, 0, 12f * u);
            for (int i = 0; i < CrownSpikes; i++)
            {
                float a = (i / (float)(CrownSpikes - 1) - .5f) * 2.4f;
                var dir = new Vector3(Mathf.Sin(a), 1.2f, -Mathf.Cos(a) * .4f).normalized;
                spikes[i].localPosition = spikeRest[i] + dir * (3.2f * early) + Vector3.down * (2.5f * early * early);
                spikes[i].localRotation = SpikeRotation(i) * Quaternion.Euler(age * (200 + i * 40), age * 90, 0);
            }
            for (int i = 0; i < HaloShards; i++)
            {
                var rest = ShardRest(i);
                shards[i].localPosition = rest * (1 + 1.6f * early) + Vector3.down * (3.4f * u * u);
                shards[i].localRotation = Quaternion.Euler(age * (120 + i * 25), age * 80, rest.x * 20);
            }
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1 : 1;
                hands[i].localPosition = new Vector3(s * (2.9f + .8f * early), .1f - 4.2f * u * u, -.45f);
                hands[i].localRotation = Quaternion.Euler(age * 140f, s * age * 60f, s * age * 90f);
            }
            vortex.localScale = Vector3.one * Mathf.Max(.001f, 1 - .7f * u);
            halo.localScale = Vector3.one * (1 + .3f * early);
        }

        void SetAwakenedLook(bool on)
        {
            if (crownFlame == null) return;
            crownFlame.gameObject.SetActive(on);
            haloAwakened.gameObject.SetActive(on);
            eyes.gameObject.SetActive(!on);
            eyesAwakened.gameObject.SetActive(on);
            bodyOffset = on ? new Vector3(0, .15f, 0) : Vector3.zero;
        }

        static float Ease(float x) => 1 - (1 - x) * (1 - x) * (1 - x);
        static float Repeat360(float a) => Mathf.Repeat(a, 360f);
        Vector3 ShardRest(int i) { float a = i / (float)HaloShards * Mathf.PI * 2; return new Vector3(Mathf.Cos(a) * 2.35f, Mathf.Sin(a) * 2.35f, 0); }
        Quaternion SpikeRotation(int i) { float a = (i / (float)(CrownSpikes - 1) - .5f) * 2.4f; return Quaternion.Euler(0, -a * Mathf.Rad2Deg * .8f, -a * 14f); }

        // ---------------------------------------------------------------- sculpt
        static Color Lin(int hex) => BossThemePalette.Hex(hex).linear;
        static readonly Color Obsidian = Lin(0x1B1026), ObsidianLight = Lin(0x352349), Gold = Lin(0xE3A83A), GoldDark = Lin(0x9A6418);
        static readonly Color CapeDark = Lin(0x2A0E3C), CapeHem = Lin(0x6A2A9A), Steel = Lin(0x4A3F5E);
        // HDR vertex colours: the boss camera renders with post-processing, so these bloom.
        static readonly Color VoidGlow = new Color(2.6f, .7f, 4.2f), EyeGold = new Color(4.5f, 2.9f, .7f), EyeRage = new Color(5f, .9f, 3.6f);
        static readonly Color CrownFire = new Color(4.8f, 1.1f, 3.2f), VortexDeep = Lin(0x1A0630);

        void Build()
        {
            material = new Material(GameArt.Load<Shader>("Art/Shaders/BossThemeSurface")) { name = "Abyssal King obsidian" };
            var rig = new GameObject("Rig").transform; rig.SetParent(transform, false);
            body = Node("Body", rig, Vector3.zero);
            BuildTorso();
            head = Node("Head", body, new Vector3(0, 1.45f, -.05f));
            BuildHead();
            cape = Node("Cape", body, new Vector3(0, .95f, .62f));
            BuildCape();
            vortex = Node("Abyss Vortex", rig, new Vector3(0, -1.2f, 0));
            BuildVortex();
            halo = Node("Shard Halo", rig, new Vector3(0, 1.5f, .9f));
            haloAwakened = Node("Awakened Halo", rig, new Vector3(0, 1.5f, .95f));
            BuildHalo();
            for (int i = 0; i < 2; i++) BuildHand(i, rig);
            SetAwakenedLook(false);
            Pose(0);
        }

        void BuildTorso()
        {
            var g = new Sculpt();
            // Armoured chest: an obsidian egg with gold collar and belt bands and a carved socket for the core.
            g.Ellipsoid(Vector3.zero, new Vector3(1.25f, 1.1f, .78f), p =>
                Mathf.Abs(p.y - .62f) < .07f || Mathf.Abs(p.y + .55f) < .06f ? Gold : p.z < -.55f && Mathf.Abs(p.x) < .55f && p.y > -.3f ? ObsidianLight : Obsidian);
            g.Carve(new Vector3(0, .05f, -.72f), .36f);
            // Rib plates
            for (int r = 0; r < 3; r++) for (int s = -1; s <= 1; s += 2)
                g.Box(new Vector3(s * (.55f + r * .05f) - .17f, .3f - r * .3f, -.82f), new Vector3(s * (.55f + r * .05f) + .17f, .38f - r * .3f, -.7f), GoldDark);
            Mesh("Torso", body, Vector3.zero, g);
            var coreSculpt = new Sculpt();
            coreSculpt.Ellipsoid(Vector3.zero, new Vector3(.3f, .3f, .3f), p => VoidGlow);
            core = Mesh("Abyss Core", body, new Vector3(0, .05f, -.68f), coreSculpt);
            for (int s = -1; s <= 1; s += 2)
            {
                var pad = new Sculpt();
                pad.Ellipsoid(Vector3.zero, new Vector3(.62f, .42f, .58f), p => p.y < -.25f ? Gold : Obsidian);
                for (int k = 0; k < 3; k++) pad.Spike(new Vector3(s * (.1f + k * .18f), .3f, -.05f + k * .1f), new Vector3(s * (.25f + k * .3f), 1.1f - k * .2f, k * .12f), .13f, k == 0 ? Gold : ObsidianLight);
                Mesh(s < 0 ? "Left Pauldron" : "Right Pauldron", body, new Vector3(s * 1.28f, .78f, 0), pad);
            }
        }

        void BuildHead()
        {
            var helm = new Sculpt();
            helm.Ellipsoid(Vector3.zero, new Vector3(.56f, .64f, .56f), p => p.z < -.35f && Mathf.Abs(p.y + .05f) < .06f ? Gold : p.y < -.38f ? Steel : Obsidian);
            // Jaw guard and cheek blades
            helm.Box(new Vector3(-.38f, -.78f, -.52f), new Vector3(.38f, -.5f, -.18f), Steel);
            for (int s = -1; s <= 1; s += 2) helm.Spike(new Vector3(s * .45f, -.1f, -.1f), new Vector3(s * 1.05f, .35f, .25f), .12f, Gold);
            // Eye slits cut into the faceplate
            for (int s = -1; s <= 1; s += 2) helm.Carve(new Vector3(s * .2f, .05f, -.55f), .1f);
            Mesh("Helm", head, Vector3.zero, helm);
            var eyeSculpt = new Sculpt(); var rage = new Sculpt();
            for (int s = -1; s <= 1; s += 2)
            {
                eyeSculpt.Box(new Vector3(s * .2f - .12f, .01f, -.6f), new Vector3(s * .2f + .12f, .09f, -.48f), EyeGold);
                rage.Box(new Vector3(s * .2f - .13f, -.01f, -.62f), new Vector3(s * .2f + .13f, .11f, -.48f), EyeRage);
            }
            eyes = Mesh("Eyes", head, Vector3.zero, eyeSculpt);
            eyesAwakened = Mesh("Eyes Awakened", head, Vector3.zero, rage);
            crown = Node("Crown", head, new Vector3(0, .5f, 0));
            var band = new Sculpt();
            // บาส 2026-11-02 build bug hunt: centred on y = 0 the 0.1-tall band fell between the voxel layers (cell
            // centres at ±Cell/2 = ±0.06), came out empty and the King had no crown band ("part 'Crown Band' has no
            // voxels" in every build log). Centred on a cell row it is exactly one voxel layer, as designed.
            band.Ring(new Vector3(0, Cell * .5f, 0), .5f, .08f, .1f, p => Gold);
            Mesh("Crown Band", crown, Vector3.zero, band);
            var flame = new Sculpt();
            for (int i = 0; i < CrownSpikes; i++)
            {
                float a = (i / (float)(CrownSpikes - 1) - .5f) * 2.4f;
                var at = new Vector3(Mathf.Sin(a) * .5f, .05f, -Mathf.Cos(a) * .5f);
                float h = i == CrownSpikes / 2 ? .95f : i % 2 == 0 ? .55f : .72f;
                var spike = new Sculpt();
                spike.Spike(Vector3.zero, new Vector3(0, h, 0), .1f, Gold);
                spike.Box(new Vector3(-.05f, .02f, -.08f), new Vector3(.05f, .12f, -.02f), VoidGlow);
                spikes[i] = Mesh("Crown Spike " + (i + 1), crown, at, spike);
                spikeRest[i] = at; spikes[i].localRotation = SpikeRotation(i);
                flame.Spike(at + Vector3.up * h * .55f, at + Vector3.up * (h + .55f), .09f, CrownFire);
            }
            crownFlame = Mesh("Crown Flame", crown, Vector3.zero, flame);
        }

        void BuildCape()
        {
            for (int i = 0; i < CapeStrips; i++)
            {
                float x = (i - (CapeStrips - 1) * .5f) * .34f;
                float length = 2.5f + ((i * 37) % 5) * .22f + (i == CapeStrips / 2 ? .3f : 0);
                var strip = new Sculpt();
                strip.Box(new Vector3(-.17f, -length, -.05f), new Vector3(.17f, 0, .05f), CapeDark);
                // A tattered hem: a violet band with notches.
                strip.Box(new Vector3(-.17f, -length, -.06f), new Vector3(.17f, -length + .22f, .06f), CapeHem);
                strip.Carve(new Vector3(((i * 13) % 3 - 1) * .1f, -length, 0), .12f);
                capeStrips[i] = Mesh("Cape Strip " + (i + 1), cape, new Vector3(x, 0, Mathf.Abs(x) * .25f), strip);
            }
        }

        void BuildVortex()
        {
            for (int i = 0; i < VortexRings; i++)
            {
                float u = i / (float)(VortexRings - 1);
                float radius = Mathf.Lerp(1.05f, .22f, u), y = -u * 1.9f;
                var ring = new Sculpt();
                ring.Ring(Vector3.zero, radius, .12f + .05f * (1 - u), .12f, p =>
                {
                    float swirl = Mathf.Repeat(Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2) * 6 + u, 1);
                    return swirl < .25f ? VoidGlow * (.5f + u * .6f) : Color.Lerp(VortexDeep, CapeHem, swirl * (1 - u));
                });
                rings[i] = Mesh("Vortex Ring " + (i + 1), vortex, new Vector3(0, y, 0), ring);
            }
        }

        void BuildHalo()
        {
            for (int i = 0; i < HaloShards; i++)
            {
                var shard = new Sculpt();
                shard.Spike(new Vector3(0, -.45f, 0), new Vector3(0, .55f, 0), .16f, Obsidian);
                shard.Spike(new Vector3(0, -.3f, -.05f), new Vector3(0, .35f, -.05f), .06f, VoidGlow);
                var rest = ShardRest(i);
                shards[i] = Mesh("Halo Shard " + (i + 1), halo, rest, shard);
                shards[i].localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(rest.y, rest.x) * Mathf.Rad2Deg - 90);
            }
            var ring = new Sculpt();
            ring.RingFlat(Vector3.zero, 2.75f, .06f, p => CrownFire);
            Mesh("Awakened Halo Ring", haloAwakened, Vector3.zero, ring);
        }

        void BuildHand(int i, Transform rig)
        {
            float s = i == 0 ? -1 : 1;
            hands[i] = Node(i == 0 ? "Left Gauntlet" : "Right Gauntlet", rig, handRest[i]);
            var palm = new Sculpt();
            palm.Box(new Vector3(-.34f, -.42f, -.16f), new Vector3(.34f, .38f, .16f), Obsidian);
            palm.Box(new Vector3(-.3f, -.3f, -.21f), new Vector3(.3f, .3f, -.15f), Gold);       // back-of-hand plate
            palm.Spike(new Vector3(0, .05f, -.22f), new Vector3(0, .1f, -.55f), .08f, GoldDark);
            palm.Ring(new Vector3(0, -.48f, 0), .34f, .07f, .1f, p => VoidGlow);                 // glowing wrist cuff
            Mesh("Palm", hands[i], Vector3.zero, palm);
            fingers[i] = new Transform[5];
            for (int f = 0; f < 4; f++)
            {
                float x = -.25f + f * .167f;
                var root = Node("Finger " + (f + 1), hands[i], new Vector3(x, .4f, 0));
                var segment = new Sculpt(); segment.Box(new Vector3(-.065f, 0, -.08f), new Vector3(.065f, .34f, .08f), f % 2 == 0 ? Obsidian : ObsidianLight);
                Mesh("Segment", root, Vector3.zero, segment);
                var tip = Node("Tip", root, new Vector3(0, .34f, 0));
                var claw = new Sculpt(); claw.Box(new Vector3(-.055f, 0, -.07f), new Vector3(.055f, .22f, .07f), Obsidian); claw.Spike(new Vector3(0, .2f, -.02f), new Vector3(0, .46f, -.12f), .06f, Gold);
                Mesh("Claw", tip, Vector3.zero, claw);
                fingers[i][f] = root;
            }
            var thumb = Node("Thumb", hands[i], new Vector3(-s * .36f, -.05f, -.02f));
            var thumbSculpt = new Sculpt(); thumbSculpt.Box(new Vector3(-.07f, 0, -.08f), new Vector3(.07f, .3f, .08f), Obsidian); thumbSculpt.Spike(new Vector3(0, .28f, 0), new Vector3(0, .5f, -.1f), .06f, Gold);
            Mesh("Thumb", thumb, Vector3.zero, thumbSculpt);
            thumb.localRotation = Quaternion.Euler(0, 0, s * 50f);
            fingers[i][4] = thumb;
        }

        Transform Node(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        Transform Mesh(string name, Transform parent, Vector3 position, Sculpt sculpt)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var mesh = sculpt.Build(Cell); mesh.name = "Abyssal King voxels - " + name; meshes.Add(mesh);
            if (mesh.vertexCount == 0)
            {
                // A solid thinner than one voxel can come out empty; the death burst samples every renderer's vertices.
                Debug.LogWarning("Abyssal King: part '" + name + "' has no voxels", this);
                Destroy(go.GetComponent<MeshRenderer>()); Destroy(go.GetComponent<MeshFilter>());
                return go.transform;
            }
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear();
            if (material != null) Destroy(material);
        }

        // ---------------------------------------------------------------- voxel grid
        // A sparse grid filled by simple solids, meshed with hidden-face culling and the same two-tone checker shading
        // the other voxel bosses use. Colours are stored ready for the shader (linear; HDR values glow).
        sealed class Sculpt
        {
            readonly Dictionary<Vector3Int, Color> cells = new Dictionary<Vector3Int, Color>();
            static Vector3Int Cellof(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
            static Vector3 Center(Vector3Int c) => new Vector3((c.x + .5f) * Cell, (c.y + .5f) * Cell, (c.z + .5f) * Cell);

            public void Box(Vector3 min, Vector3 max, Color color)
            {
                var a = Cellof(Vector3.Min(min, max)); var b = Cellof(Vector3.Max(min, max) - Vector3.one * .0001f);
                for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) for (int z = a.z; z <= b.z; z++) cells[new Vector3Int(x, y, z)] = color;
            }

            public void Ellipsoid(Vector3 center, Vector3 radii, System.Func<Vector3, Color> color)
            {
                var a = Cellof(center - radii); var b = Cellof(center + radii);
                for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) for (int z = a.z; z <= b.z; z++)
                {
                    var c = new Vector3Int(x, y, z); var p = Center(c) - center;
                    var q = new Vector3(p.x / radii.x, p.y / radii.y, p.z / radii.z);
                    if (q.sqrMagnitude <= 1f) cells[c] = color(p);
                }
            }

            // Tapered spike from a to b (square cross-section shrinking to a point).
            public void Spike(Vector3 a, Vector3 b, float baseHalf, Color color)
            {
                float length = Vector3.Distance(a, b); int steps = Mathf.Max(2, Mathf.CeilToInt(length / (Cell * .5f)));
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps; var p = Vector3.Lerp(a, b, t); float half = Mathf.Max(Cell * .45f, baseHalf * (1 - t));
                    Box(p - Vector3.one * half, p + Vector3.one * half, color);
                }
            }

            // Horizontal torus (around Y).
            public void Ring(Vector3 center, float radius, float thickness, float height, System.Func<Vector3, Color> color)
            {
                float outer = radius + thickness;
                var a = Cellof(center - new Vector3(outer, height, outer)); var b = Cellof(center + new Vector3(outer, height, outer));
                for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) for (int z = a.z; z <= b.z; z++)
                {
                    var c = new Vector3Int(x, y, z); var p = Center(c) - center;
                    float d = new Vector2(p.x, p.z).magnitude;
                    if (Mathf.Abs(d - radius) <= thickness && Mathf.Abs(p.y) <= height * .5f + .001f) cells[c] = color(p);
                }
            }

            // Vertical ring facing the camera (in the XY plane).
            public void RingFlat(Vector3 center, float radius, float thickness, System.Func<Vector3, Color> color)
            {
                float outer = radius + thickness;
                var a = Cellof(center - new Vector3(outer, outer, Cell * .5f)); var b = Cellof(center + new Vector3(outer, outer, Cell * .5f));
                for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) for (int z = a.z; z <= b.z; z++)
                {
                    var c = new Vector3Int(x, y, z); var p = Center(c) - center;
                    if (Mathf.Abs(new Vector2(p.x, p.y).magnitude - radius) <= thickness) cells[c] = color(p);
                }
            }

            public void Carve(Vector3 center, float radius)
            {
                var a = Cellof(center - Vector3.one * radius); var b = Cellof(center + Vector3.one * radius);
                for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) for (int z = a.z; z <= b.z; z++)
                {
                    var c = new Vector3Int(x, y, z);
                    if ((Center(c) - center).sqrMagnitude <= radius * radius) cells.Remove(c);
                }
            }

            static readonly Vector3Int[] Dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            static readonly Vector3[] AxisU = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            static readonly Vector3[] AxisV = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

            public Mesh Build(float cell)
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                foreach (var pair in cells)
                {
                    var c = pair.Key; var center = Center(c);
                    bool glow = pair.Value.maxColorComponent > 1.01f;
                    // Glowing voxels skip the checker so the bloom stays even.
                    float shade = glow ? 1f : ((c.x + c.y + c.z) % 2 == 0 ? 1f : .84f) * (.92f + (Mathf.Abs(c.x * 17 + c.y * 31 + c.z * 13) % 7) * .025f);
                    var tint = new Color(pair.Value.r * shade, pair.Value.g * shade, pair.Value.b * shade, 1f);
                    for (int f = 0; f < 6; f++)
                    {
                        if (cells.ContainsKey(c + Dirs[f])) continue;
                        Vector3 n = Dirs[f];
                        var faceCenter = center + n * cell * .5f; var u = AxisU[f] * cell * .5f; var v = AxisV[f] * cell * .5f;
                        int first = vertices.Count;
                        vertices.Add(faceCenter - u - v); vertices.Add(faceCenter + u - v); vertices.Add(faceCenter + u + v); vertices.Add(faceCenter - u + v);
                        for (int k = 0; k < 4; k++) { normals.Add(n); colors.Add(tint); }
                        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                        triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                    }
                }
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
