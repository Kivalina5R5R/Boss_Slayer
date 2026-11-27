using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BossSlayer
{
    // The Design team's rigged voxel boss, shown in place of the old model. The old boss object stays underneath with its
    // renderers switched off, so positions, hit shapes and attack timing keep working exactly as before.
    // Built by Editor/BossSkinBuilder into Resources/BossSlayer/Skins. No prefab = the old model stays visible.
    public sealed partial class BossVoxelSkin : MonoBehaviour
    {
        [Serializable] public struct Theme { public string name; public Material material; }

        public SkinnedMeshRenderer body;
        public Animator animator;
        public AnimationClip[] clips;
        public Theme[] themes;
        public TextAsset voxels;
        public Material shardMaterial;
        public Mesh shardMesh;

        const float DefaultFade = .3f;
        const float HaloRadiansPerSecond = .45f, HaloChargeBoost = 5f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
        int liveSlot;
        float blend = 1f, blendRate = 1f;
        AnimationClip current;
        bool oneShot, holdAtEnd, moving, moveRight, locked;
        float returnAfter = -1f;
        Transform halo;
        BossHydra hydra;
        readonly Transform[] hydraNecks = new Transform[7];
        readonly List<(float time, string theme)> themeCues = new();
        float haloBoost = 1f;

        public bool IsBusy => oneShot && slots[liveSlot].IsValid() && !IsFinished;
        bool IsFinished => current == null || slots[liveSlot].GetTime() >= current.length;
        public float Length(string clip) { var c = Find(clip); return c != null ? c.length : 0f; }
        public bool Has(string clip) => Find(clip) != null;

        AnimationClip Find(string name)
        {
            foreach (var clip in clips) if (clip != null && clip.name == name) return clip;
            return null;
        }

        // ---------------------------------------------------------------- placement
        public static BossVoxelSkin Attach(Transform owner, string prefabName, Camera camera, float sizeMultiplier = 1f)
        {
            var prefab = GameArt.Load<GameObject>("Prefab/Bosses/" + prefabName);
            if (prefab == null || prefab.GetComponent<BossVoxelSkin>() == null) return null;

            bool wasActive = owner.gameObject.activeSelf;
            owner.gameObject.SetActive(true);
            var old = new List<Renderer>();
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true)) if (r.GetComponentInParent<BossVoxelSkin>(true) == null) old.Add(r);

            bool any = false; var target = new Bounds(owner.position, Vector3.zero);
            foreach (var r in old)
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { target = r.bounds; any = true; } else target.Encapsulate(r.bounds);
            }

            var instance = Application.isPlaying ? Instantiate(prefab, owner, false) : (GameObject)Instantiate((UnityEngine.Object)prefab, owner, false);
            instance.name = prefabName;
            foreach (var part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = owner.gameObject.layer; // the boss camera only sees the boss layer
            var skin = instance.GetComponent<BossVoxelSkin>();
            skin.Fit(owner, target, any, camera);
            if (sizeMultiplier != 1f) { var centre = skin.MeshWorldBounds().center; skin.transform.localScale *= sizeMultiplier; skin.transform.position += centre - skin.MeshWorldBounds().center; }
            foreach (var r in old) r.forceRenderingOff = true;
            owner.gameObject.SetActive(wasActive);
            return skin;
        }

        Bounds MeshWorldBounds()
        {
            var local = body.sharedMesh.bounds;
            var space = body.rootBone != null ? transform : body.transform;
            var result = new Bounds(space.TransformPoint(local.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(space.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }

        void Fit(Transform owner, Bounds target, bool hasTarget, Camera camera)
        {
            if (camera != null)
            {
                var toCamera = Vector3.ProjectOnPlane(-camera.transform.forward, Vector3.up);
                if (toCamera.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
            float ownerScale = Mathf.Max(.0001f, Mathf.Abs(owner.lossyScale.x));
            transform.localScale = Vector3.one / ownerScale;
            if (body == null || body.sharedMesh == null || !hasTarget) return;
            var size = MeshWorldBounds().size;
            float scale = Mathf.Min(target.size.y / Mathf.Max(.01f, size.y), target.size.x / Mathf.Max(.01f, size.x));
            if (scale <= 0f) return;
            transform.localScale = Vector3.one * (scale / ownerScale);
            transform.position += target.center - MeshWorldBounds().center;
        }

        // ---------------------------------------------------------------- playback
        void Ensure()
        {
            if (graph.IsValid()) return;
            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Skin", animator).SetSourcePlayable(mixer);
            graph.Play();
            foreach (var bone in body.bones)
            {
                if (bone.name == "halo") halo = bone;
                for (int i = 0; i < hydraNecks.Length; i++) if (bone.name == "neck" + i) hydraNecks[i] = bone;
            }
            hydra = GetComponentInParent<BossHydra>(true);
        }

        void StartClip(AnimationClip clip, float speed, float fade, bool isOneShot)
        {
            if (clip == null) return;
            Ensure();
            int next = 1 - liveSlot;
            if (slots[next].IsValid()) { graph.Disconnect(mixer, next); slots[next].Destroy(); }
            slots[next] = AnimationClipPlayable.Create(graph, clip);
            slots[next].SetSpeed(speed);
            slots[next].SetApplyFootIK(false);
            graph.Connect(slots[next], 0, mixer, next);
            liveSlot = next; current = clip; oneShot = isOneShot;
            blend = 0f; blendRate = 1f / Mathf.Max(.01f, fade);
            ApplyBlend();
        }

        void ApplyBlend()
        {
            mixer.SetInputWeight(liveSlot, blend);
            mixer.SetInputWeight(1 - liveSlot, 1f - blend);
        }

        void PlayBase(float fade = DefaultFade)
        {
            string name = "idle";
            if (moving) name = Has("move") ? "move" : moveRight ? "move_r" : "move_l";
            var clip = Find(name) ?? Find("idle");
            if (current == clip && !oneShot) return;
            StartClip(clip, 1f, fade, false);
        }

        public void ResetForFight()
        {
            Ensure();
            locked = holdAtEnd = false; returnAfter = -1f; haloBoost = 1f; themeCues.Clear(); ultimateQueued = false;
            ResetFx();
            if (themes != null && themes.Length > 0) SetTheme(themes[0].name);
            if (body != null) body.enabled = true;
            oneShot = false; current = null;
            PlayBase(.01f);
        }

        public void SetMoving(bool isMoving, bool right)
        {
            moving = isMoving; moveRight = right;
            if (!oneShot && !locked && returnAfter < 0f) PlayBase();
        }

        public void Play(string clip, float speed = 1f)
        {
            themeCues.RemoveAll(cue => true);
            locked = false; holdAtEnd = false; returnAfter = -1f;
            StartClip(Find(clip), speed, DefaultFade, true);
        }

        // บาส 2026-11-12 "แปลงร่างก่อนใช้ท่าอัลติ": an ultimate that starts during a transformation (Golem / Demon / King at
        // half HP) waits for the transformation to end, then plays, instead of being dropped.
        bool ultimateQueued;
        public bool IsPlayingClip(string clip) => current != null && current.name.EndsWith(clip) && !IsFinished;

        public void PlayAttack(string clip, float speed)
        {
            if (locked || returnAfter >= 0f)
            {
                if (clip == "ultimate" && locked && !holdAtEnd && returnAfter < 0f) ultimateQueued = true;
                return;
            }
            Play(clip, Mathf.Clamp(speed, .5f, 2f));
        }

        public void Stun(float seconds)
        {
            themeCues.Clear(); locked = false; holdAtEnd = false; ultimateQueued = false; // a stun cancels the ultimate
            StartClip(Find("stun"), 1f, .15f, false);
            returnAfter = seconds;
        }

        // A transformation owns the body until it ends, and may change colour part way through its clip.
        public void Transform2(string clip, float seconds, float themeTime, string theme)
        {
            float length = Length(clip);
            Play(clip, length > 0f ? length / Mathf.Max(.1f, seconds) : 1f);
            locked = true;
            if (!string.IsNullOrEmpty(theme)) themeCues.Add((themeTime, theme));
        }

        public void EndTransform() { locked = false; }

        public void CueTheme(float clipTime, string theme) => themeCues.Add((clipTime, theme));

        public void PlayHeld(string clip, float speed = 1f, float fade = DefaultFade)
        {
            themeCues.Clear(); returnAfter = -1f; ultimateQueued = false;
            StartClip(Find(clip), speed, fade, true);
            locked = true; holdAtEnd = true;
        }

        public void PlayLoop(string clip)
        {
            themeCues.Clear(); returnAfter = -1f; holdAtEnd = false;
            StartClip(Find(clip), 1f, DefaultFade, false);
            locked = true;
            haloBoost = HaloChargeBoost;
        }

        public void SetTheme(string themeName)
        {
            foreach (var theme in themes)
                if (theme.name == themeName && theme.material != null) { body.sharedMaterial = theme.material; return; }
        }

        void Update()
        {
            if (!graph.IsValid() || Time.timeScale <= 0f) return;
            if (blend < 1f) { blend = Mathf.Min(1f, blend + Time.deltaTime * blendRate); ApplyBlend(); }
            TickFx();

            if (slots[liveSlot].IsValid() && themeCues.Count > 0)
            {
                double time = slots[liveSlot].GetTime();
                for (int i = themeCues.Count - 1; i >= 0; i--)
                    if (time >= themeCues[i].time) { WipeTo(themeCues[i].theme); themeCues.RemoveAt(i); }
            }
            if (returnAfter >= 0f)
            {
                returnAfter -= Time.deltaTime;
                if (returnAfter <= 0f) { returnAfter = -1f; PlayBase(.4f); }
                return;
            }
            if (oneShot && IsFinished && !holdAtEnd)
            {
                locked = false; oneShot = false;
                if (ultimateQueued) { ultimateQueued = false; Play("ultimate"); } else PlayBase();
            }
        }

        void LateUpdate()
        {
            if (halo != null) halo.Rotate(0f, 0f, HaloRadiansPerSecond * Mathf.Rad2Deg * haloBoost * Time.deltaTime, Space.Self);
            if (hydra != null) // each new neck follows the old head slot it stands for: hidden when severed, growing back in
                for (int slot = 0; slot < BossHydra.MaximumHeads; slot++)
                {
                    var neck = hydraNecks[HydraOldToNew[slot]];
                    if (neck != null) neck.localScale = Vector3.one * (hydra.HeadAlive(slot) ? Mathf.Clamp01(hydra.HeadGrowth(slot)) : 0f);
                }
            HideOldRenderers(Time.deltaTime);
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        // ---------------------------------------------------------------- death: the body breaks into its voxels
        struct Shard { public Vector3 pos, vel; public Color32 color; public float delay, fade; public bool flying; }

        static float Number(string json, string key, float fallback)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)");
            return match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
        }

        static List<string> Names(string json, string key)
        {
            var list = new List<string>();
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (match.Success) foreach (Match m in Regex.Matches(match.Groups[1].Value, "\"([^\"]+)\"")) list.Add(m.Groups[1].Value);
            return list;
        }

        Color32[] ThemeColors(string json, List<string> slotNames)
        {
            string theme = body.sharedMaterial != null && themes != null ? themes[0].name : "";
            foreach (var t in themes) if (t.material == body.sharedMaterial) theme = t.name;
            int start = json.IndexOf("\"themes\"", StringComparison.Ordinal);
            int from = Mathf.Max(start, json.IndexOf("\"" + theme + "\"", Mathf.Max(0, start), StringComparison.Ordinal));
            var colors = new Color32[slotNames.Count];
            for (int i = 0; i < slotNames.Count; i++)
            {
                colors[i] = new Color32(200, 200, 200, 255);
                var m = new Regex("\"" + slotNames[i] + "\"\\s*:\\s*\\{\\s*\"c\"\\s*:\\s*\"#([0-9a-fA-F]{6})").Match(json, from);
                if (m.Success && ColorUtility.TryParseHtmlString("#" + m.Groups[1].Value, out var color)) colors[i] = color;
            }
            return colors;
        }

        static void StepShards(Shard[] shards, ParticleSystem.Particle[] particles, float age, float dt, Vector3 core, float power, float gravity, float drag, float length, float size)
        {
            for (int i = 0; i < shards.Length; i++)
            {
                ref var s = ref shards[i];
                if (!s.flying && age >= s.delay)
                {
                    s.flying = true;
                    var away = s.pos - core; away = away.sqrMagnitude < .0001f ? Vector3.up : away.normalized;
                    s.vel = (away + UnityEngine.Random.insideUnitSphere * .35f) * (power * UnityEngine.Random.Range(.4f, 1f));
                }
                if (s.flying)
                {
                    s.vel += Vector3.up * (gravity * dt);
                    s.vel *= Mathf.Exp(-drag * dt);
                    s.pos += s.vel * dt;
                }
                float shrink = Mathf.Clamp01(1f - (age - s.fade) / Mathf.Max(.05f, length - s.fade));
                particles[i] = new ParticleSystem.Particle { position = s.pos, startColor = s.color, startSize = size * shrink, remainingLifetime = 5f, startLifetime = 6f, rotation3D = Vector3.zero };
            }
        }

        public IEnumerator Shatter()
        {
            if (voxels == null || shardMaterial == null || shardMesh == null || body == null) yield break;
            string json = voxels.text;
            int effect = json.IndexOf("\"deathEffect\"", StringComparison.Ordinal);
            string fx = effect >= 0 ? json.Substring(effect, Mathf.Min(400, json.Length - effect)) : "";
            float voxelSize = Number(json, "voxelSize", .1f), chunk = Mathf.Max(1f, Number(fx, "chunk", 2f));
            float crumble = Number(fx, "crumble", .2f), burst = Number(fx, "burst", .3f), power = Number(fx, "power", 8f);
            float fade = Number(fx, "fade", 1f), spread = Number(fx, "fadeSpread", .4f), gravity = Number(fx, "gravity", -9.8f), drag = Number(fx, "drag", .5f), length = Number(fx, "len", 2f);
            var coreMatch = Regex.Match(fx, "\"core\"\\s*:\\s*\\[(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)");
            var core = transform.position;
            if (coreMatch.Success)
                core = transform.TransformPoint(new Vector3(float.Parse(coreMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(coreMatch.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(coreMatch.Groups[3].Value, CultureInfo.InvariantCulture)));

            var slotNames = Names(json, "slots"); var boneNames = Names(json, "bones");
            var colors = ThemeColors(json, slotNames);
            var bones = new Dictionary<string, int>();
            for (int i = 0; i < body.bones.Length; i++) bones[body.bones[i].name] = i;
            var binds = body.sharedMesh.bindposes;
            float scale = transform.lossyScale.x, cell = voxelSize * chunk;

            var grouped = new Dictionary<Vector3Int, Shard>();
            float top = float.MinValue, bottom = float.MaxValue;
            foreach (Match m in Regex.Matches(json, "\\[(-?[0-9.]+),\\s*(-?[0-9.]+),\\s*(-?[0-9.]+),\\s*([0-9]+),\\s*([0-9]+)\\]"))
            {
                var local = new Vector3(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                var key = Vector3Int.FloorToInt(local / cell);
                if (grouped.ContainsKey(key)) continue;
                int slot = int.Parse(m.Groups[4].Value), boneIndex = int.Parse(m.Groups[5].Value);
                var world = transform.TransformPoint(local);
                if (boneIndex < boneNames.Count && bones.TryGetValue(boneNames[boneIndex], out int bone))
                    world = (body.bones[bone].localToWorldMatrix * binds[bone]).MultiplyPoint3x4(local);
                top = Mathf.Max(top, world.y); bottom = Mathf.Min(bottom, world.y);
                grouped[key] = new Shard { pos = world, color = slot < colors.Length ? colors[slot] : new Color32(200, 200, 200, 255), fade = fade + UnityEngine.Random.Range(0f, spread) };
            }
            var shards = new List<Shard>(grouped.Values).ToArray();
            for (int i = 0; i < shards.Length; i++)
                shards[i].delay = burst + crumble * (1f - Mathf.InverseLerp(bottom, top, shards[i].pos.y));

            var host = new GameObject(name + " shatter");
            // 2026-11-25 "สะเก็ดที่บอสระเบิดตรงจอกำแพง ตรงจอพื้นต้องมองไม่เห็น": the shards were on the Default layer, which
            // the floor camera draws too. On the boss's own layer only the wall's boss camera sees them.
            host.layer = gameObject.layer;
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = shards.Length; main.startLifetime = length + 2f; main.simulationSpeed = 0f;
            var emission = system.emission; emission.enabled = false;
            var view = host.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Mesh; view.mesh = shardMesh; view.sharedMaterial = shardMaterial;
            body.enabled = false;

            var particles = new ParticleSystem.Particle[shards.Length];
            float size = cell * scale;
            try
            {
                for (float age = 0f; age < length; age += Time.deltaTime)
                {
                    StepShards(shards, particles, age, Time.deltaTime, core, power * scale, gravity * scale, drag, length, size);
                    system.SetParticles(particles, particles.Length);
                    yield return null;
                }
            }
            finally { Destroy(host); }
        }
    }
}
