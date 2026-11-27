using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // Independent articulated heads; no physics contacts or gameplay random draws.
    public sealed partial class BossHydra : MonoBehaviour
    {
        public const int MaximumHeads = 7;
        public const float RegrowSeconds = 2.2f;
        const float GrowSeconds = .75f;
        readonly Transform[] necks = new Transform[MaximumHeads];
        readonly Transform[] skulls = new Transform[MaximumHeads];
        readonly bool[] alive = new bool[MaximumHeads];
        readonly float[] growth = new float[MaximumHeads];
        readonly float[] strike = new float[MaximumHeads];
        readonly List<Regrowth> pending = new List<Regrowth>();
        readonly List<Fragment> fragments = new List<Fragment>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Material voxelMaterial;
        float elapsed, damageBank, threshold, cutCooldown;
        bool built, defeated;
        int nextHead;
        float locomotion, stunMotion, stunMotionDuration;
        readonly float[] chargeLeft = new float[MaximumHeads], chargeTotal = new float[MaximumHeads];
        public void SetLocomotion(float speed) { locomotion = Mathf.Clamp(speed,-1,1); }
        public void BeginHeadCharge(int index, float seconds) { if(IsHeadReady(index) && stunMotion<=0) chargeLeft[index]=chargeTotal[index]=Mathf.Max(.01f,seconds); }
        public void Stun(float seconds) { CancelSevenHeadStrike(); stunMotionDuration=stunMotion=Mathf.Max(0,seconds); System.Array.Clear(chargeLeft,0,MaximumHeads); System.Array.Clear(strike,0,MaximumHeads); }
        public int ActiveHeads { get; private set; }
        public int PendingRegrowth => pending.Count;
        public Transform Skull(int index) => skulls[index];
        public bool HeadAlive(int index) => alive[index];
        public float HeadGrowth(int index) => growth[index];
        public bool IsHeadReady(int index) => built && !defeated && index >= 0 && index < MaximumHeads && alive[index] && growth[index] >= 1f;
        public void AttackHead(int index) { if (IsHeadReady(index) && stunMotion<=0) { strike[index] = StrikeSeconds; chargeLeft[index] = 0; } }
        struct Regrowth { public int Slot; public float Remaining; }
        struct Fragment { public Transform View; public Vector3 Start; public Quaternion Rotation; public float Age; }

        public static BossHydra Create(Transform reference)
        {
            var go = new GameObject("HydraBoss");
            go.SetActive(false);
            var hydra = go.AddComponent<BossHydra>();
            hydra.Build();
            go.transform.localScale = Vector3.one * 1.3f;
            if (reference != null)
            {
                var renderers = reference.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(reference.position, Vector3.one * 5);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                }
                go.transform.position = bounds.center - Vector3.up * (bounds.size.y * .05f);
                go.transform.localScale = Vector3.one * Mathf.Max(.01f, bounds.size.y / 8f * 1.3f);
            }
            Visual3DFollower.SetLayerRecursive(go, Visual3DFollower.BossLayer);
            hydra.ResetHeads();
            return hydra;
        }

        static Color Finish(string label, Color color, bool glow = false) => color;

        Transform Part(string label, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            bool rounded = label == "Body" || label == "Haunch" || label == "Skull" || label == "Neck scale" || label == "Tail segment";
            var mesh = VoxelMesh(size, color, rounded); mesh.name = "Hydra voxels - " + label;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = voxelMaterial;
            meshes.Add(mesh);
            return go.transform;
        }

        static Mesh VoxelMesh(Vector3 size, Color color, bool rounded)
        {
            const float cell = .16f;
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
            int ny = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));
            int nz = Mathf.Max(1, Mathf.CeilToInt(size.z / cell));
            var step = new Vector3(size.x / nx, size.y / ny, size.z / nz);
            var filled = new bool[nx, ny, nz];
            for (int x = 0; x < nx; x++) for (int y = 0; y < ny; y++) for (int z = 0; z < nz; z++)
            {
                var q = new Vector3((x + .5f) / nx * 2 - 1, (y + .5f) / ny * 2 - 1, (z + .5f) / nz * 2 - 1);
                filled[x, y, z] = !rounded || q.sqrMagnitude <= 1.12f;
            }
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var triangles = new List<int>();
            var dirs = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };
            var axesU = new[] { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            var axesV = new[] { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };
            for (int x = 0; x < nx; x++) for (int y = 0; y < ny; y++) for (int z = 0; z < nz; z++)
            {
                if (!filled[x,y,z]) continue;
                var center = new Vector3((x + .5f) * step.x, (y + .5f) * step.y, (z + .5f) * step.z) - size * .5f;
                // Deterministic voxel shading does not consume gameplay random numbers. บาส 2026-10-02: a two-tone
                // checker plus jitter, like the other voxel bosses (the old +-9% jitter read as smooth low poly).
                float shade = ((x + y + z) % 2 == 0 ? 1f : .84f) * (.92f + ((x * 17 + y * 31 + z * 13) % 7) * .025f);
                var tint = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                for (int face = 0; face < 6; face++)
                {
                    var d = dirs[face]; int xx=x+d.x, yy=y+d.y, zz=z+d.z;
                    if (xx>=0 && yy>=0 && zz>=0 && xx<nx && yy<ny && zz<nz && filled[xx,yy,zz]) continue;
                    Vector3 normal = d;
                    var c = center + Vector3.Scale(normal,step) * .5f;
                    var u = Vector3.Scale(axesU[face],step) * .5f; var v = Vector3.Scale(axesV[face],step) * .5f;
                    int first = vertices.Count;
                    vertices.Add(c-u-v); vertices.Add(c+u-v); vertices.Add(c+u+v); vertices.Add(c-u+v);
                    for (int j=0;j<4;j++) { normals.Add(normal); colors.Add(tint.linear); }
                    triangles.Add(first); triangles.Add(first+1); triangles.Add(first+2);
                    triangles.Add(first); triangles.Add(first+2); triangles.Add(first+3);
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            return mesh;
        }

        void BakeParts(Transform parent, Transform animatedChild = null)
        {
            var parts = new List<Transform>(); var combine = new List<CombineInstance>();
            foreach (Transform child in parent)
            {
                if (child == animatedChild) continue;
                var filter = child.GetComponent<MeshFilter>(); if (filter == null) continue;
                parts.Add(child);
                combine.Add(new CombineInstance { mesh = filter.sharedMesh, transform = parent.worldToLocalMatrix * child.localToWorldMatrix });
            }
            if (combine.Count == 0) return;
            var baked = new Mesh { name = "Hydra Voxel " + parent.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            baked.CombineMeshes(combine.ToArray(), true, true); meshes.Add(baked);
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = baked;
            parent.gameObject.AddComponent<MeshRenderer>().sharedMaterial = voxelMaterial;
            foreach (var part in parts)
            {
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                meshes.Remove(mesh); Dispose(mesh); part.gameObject.SetActive(false); Dispose(part.gameObject);
            }
        }
        void Build() { BuildReferenceModel(); }

        void OnEnable() { if (built) ResetHeads(); }
        void OnDisable() { pending.Clear(); ClearFragments(); }
        public void ResetHeads()
        {
            ManualDeathPlayback=false;
            CancelSevenHeadStrike();
            pending.Clear(); ClearFragments();
            elapsed = damageBank = cutCooldown = locomotion = stunMotion = stunMotionDuration = 0; System.Array.Clear(chargeLeft,0,MaximumHeads);
            deathTime = swimPhase = swimSpeed = 0; System.Array.Clear(displayedCharge,0,MaximumHeads);
            defeated = false; nextHead = 0; ActiveHeads = 0; System.Array.Clear(headAttackStyles,0,MaximumHeads);
            PoseSerpentineBody(0);
            for (int i = 0; i < MaximumHeads; i++)
            {
                alive[i] = i == 1 || i == 3 || i == 5;
                if (alive[i]) ActiveHeads++;
                growth[i] = 1; strike[i] = 0;
                skulls[i].localRotation = Quaternion.identity;
                necks[i].localScale = Vector3.one;
                necks[i].localRotation = bodyRig.localRotation * BodySectionRotation(FrontNeckSocket(i).z);
                necks[i].gameObject.SetActive(alive[i]);
                PoseReferenceHead(i,0,1,0);
            }
            PoseSerpentineBody(0);
        }

        public void ReceiveDamage(int actualDamage, int remainingHp, int maximumHp)
        {
            if (remainingHp <= 0) { BeginDeathPose(); return; }
            if (defeated || actualDamage <= 0) return;
            threshold = Mathf.Max(1, maximumHp * .12f);
            damageBank += actualDamage;
            TrySever();
        }

        void TrySever()
        {
            if (defeated || threshold <= 0 || damageBank < threshold || cutCooldown > 0 || ActiveHeads <= 1) return;
            for (int n = 0; n < MaximumHeads; n++)
            {
                int i = (nextHead + n) % MaximumHeads;
                if (!alive[i] || growth[i] < 1) continue;
                var clone = Instantiate(skulls[i].gameObject).transform;
                clone.name = "Hydra severed head";
                clone.SetPositionAndRotation(skulls[i].position, skulls[i].rotation);
                clone.localScale = skulls[i].lossyScale;
                if (BossVoxelSkin.ActiveOn(transform) is BossVoxelSkin severSkin) // the new model loses its own head instead
                {
                    foreach (var r in clone.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
                    severSkin.SeverHead(i);
                }
                fragments.Add(new Fragment { View = clone, Start = clone.position, Rotation = clone.rotation });
                CancelSevenHeadStrike();
                alive[i] = false; ActiveHeads--;
                necks[i].gameObject.SetActive(false);
                pending.Add(new Regrowth { Slot = i, Remaining = RegrowSeconds });
                nextHead = (i + 1) % MaximumHeads;
                damageBank -= threshold; cutCooldown = .7f;
                return;
            }
        }

        void Update() { if(!ManualDeathPlayback) Advance(Time.deltaTime); }
        public void Advance(float dt)
        {
            if (dt <= 0 || !built) return;
            elapsed += dt;
            if(sevenHeadAge>=0)sevenHeadAge+=dt;
            cutCooldown = Mathf.Max(0, cutCooldown - dt);
            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                var f = fragments[i]; f.Age += dt;
                if (f.Age >= 1.1f) { Dispose(f.View.gameObject); fragments.RemoveAt(i); continue; }
                float scale = transform.lossyScale.y;
                f.View.position = f.Start + new Vector3(f.Age * .6f, .8f * f.Age - 3 * f.Age * f.Age, -.15f * f.Age) * scale;
                f.View.rotation = f.Rotation * Quaternion.Euler(f.Age * 180, f.Age * 120, f.Age * 90);
                fragments[i] = f;
            }
            if (defeated)
            {
                deathTime += dt;
                PoseSerpentineBody(dt);
                for(int i=0;i<MaximumHeads;i++) if(alive[i]) PoseReferenceHead(i,0,0,dt,Mathf.SmoothStep(0,1,Mathf.Clamp01((deathTime-.08f*i)/2.3f)));
                return;
            }
            stunMotion = Mathf.Max(0,stunMotion-dt);
            PoseSerpentineBody(dt);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var item = pending[i]; item.Remaining -= dt; pending[i] = item;
                if (item.Remaining > 0) continue;
                Grow(item.Slot);
                for (int spare = 0; spare < MaximumHeads; spare++)
                {
                    if (alive[spare] || pending.Exists(p => p.Slot == spare)) continue;
                    Grow(spare); break;
                }
                pending.RemoveAt(i);
            }
            for (int i = 0; i < MaximumHeads; i++)
            {
                if (!alive[i]) continue;
                growth[i] = Mathf.Min(1, growth[i] + dt / GrowSeconds);
                strike[i] = Mathf.Max(0, strike[i] - dt);
                chargeLeft[i] = Mathf.Max(0,chargeLeft[i]-dt);
                float charge = chargeLeft[i]>0 ? 1-chargeLeft[i]/Mathf.Max(.01f,chargeTotal[i]) : 0;
                float quiet = Mathf.Lerp(1f,.12f,BossModelAnimation.StunWeight(stunMotion,stunMotionDuration));

                necks[i].localScale = Vector3.one * Mathf.SmoothStep(.05f, 1, growth[i]);
                PoseReferenceHead(i,charge,quiet,dt);
            }
            TrySever();
        }
        void Grow(int slot)
        {
            if (alive[slot]) return;
            alive[slot] = true; ActiveHeads++; growth[slot] = 0; strike[slot] = chargeLeft[slot] = displayedCharge[slot] = 0;
            PoseReferenceHead(slot,0,1,0);
            necks[slot].localScale = Vector3.one * .05f;
            necks[slot].gameObject.SetActive(true);
        }
        static void Dispose(Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        void ClearFragments() { foreach (var f in fragments) if (f.View != null) Dispose(f.View.gameObject); fragments.Clear(); }
        public void ReleaseResources() { ClearFragments(); foreach (var mesh in meshes) if (mesh != null) Dispose(mesh); meshes.Clear(); foreach (var material in materials) if (material != null) Dispose(material); materials.Clear(); }
        void OnDestroy() { ReleaseResources(); }
    }
}
