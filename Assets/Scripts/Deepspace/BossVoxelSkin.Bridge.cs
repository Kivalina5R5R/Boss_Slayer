using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Deepspace
{
    // Lets the older effect code find the new model: it maps the old model's sockets (mouth, hands, heads...) to the new
    // bones, gives the renderers to measure, and keeps every renderer the old animation scripts create out of sight.
    public sealed partial class BossVoxelSkin
    {
        // Hydra: the old body starts with heads in slots 1, 3 and 5; the new one with head0 (middle), head1 and head2.
        public static readonly int[] HydraOldToNew = { 3, 1, 4, 0, 5, 2, 6 };

        readonly Dictionary<Transform, Transform> anchorMap = new();
        Dictionary<int, List<Vector3>> boneVertices;
        List<(int bone, Vector3 local)> allVertices;
        float hideSweep;

        // ---------------------------------------------------------------- lookups for other scripts
        public static BossVoxelSkin ActiveOn(Transform boss)
        {
            if (boss == null) return null;
            foreach (var skin in boss.GetComponentsInChildren<BossVoxelSkin>(false)) if (skin.isActiveAndEnabled) return skin;
            return null;
        }

        public static Renderer[] VisibleRenderers(Transform boss)
        {
            var skin = ActiveOn(boss);
            if (skin != null && skin.body != null) return new Renderer[] { skin.body };
            return boss != null ? boss.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }

        public static Transform Redirect(Transform boss, Transform anchor)
        {
            var skin = ActiveOn(boss);
            return skin != null && anchor != null && skin.anchorMap.TryGetValue(anchor, out var bone) ? bone : anchor;
        }

        public bool OwnsBone(Transform bone)
        {
            if (body == null || bone == null) return false;
            foreach (var b in body.bones) if (b == bone) return true;
            return false;
        }

        public bool HasBone(string boneName) { Bone(boneName); return boneByName.ContainsKey(boneName); }

        // First bone in the list that this model has.
        public Transform FirstBone(params string[] names)
        {
            foreach (var n in names) if (HasBone(n)) return Bone(n);
            return null;
        }

        public void MapAnchor(Transform oldAnchor, params string[] boneNames)
        {
            var bone = FirstBone(boneNames);
            if (oldAnchor != null && bone != null) anchorMap[oldAnchor] = bone;
        }

        // ---------------------------------------------------------------- the model's own surface, per bone
        void EnsureVertexCache()
        {
            if (boneVertices != null || body == null || body.sharedMesh == null) return;
            boneVertices = new Dictionary<int, List<Vector3>>();
            allVertices = new List<(int, Vector3)>();
            var mesh = body.sharedMesh;
            var vertices = mesh.vertices; var weights = mesh.boneWeights; var binds = mesh.bindposes;
            for (int i = 0; i < vertices.Length && i < weights.Length; i += 2) // every second vertex is plenty for surface picks
            {
                int bone = weights[i].boneIndex0;
                var local = binds[bone].MultiplyPoint3x4(vertices[i]);
                if (!boneVertices.TryGetValue(bone, out var list)) boneVertices[bone] = list = new List<Vector3>();
                list.Add(local);
                allVertices.Add((bone, local));
            }
        }

        int BoneIndex(Transform bone) => Array.IndexOf(body.bones, bone);

        // The farthest point of this bone's part along a world direction, in the bone's own space (a muzzle or a fingertip).
        public Vector3 LocalTip(Transform bone, Vector3 worldDirection, out float width)
        {
            width = .1f;
            EnsureVertexCache();
            int index = BoneIndex(bone);
            if (boneVertices == null || index < 0 || !boneVertices.TryGetValue(index, out var list) || list.Count == 0) return Vector3.zero;
            var direction = bone.InverseTransformDirection(worldDirection).normalized;
            Vector3 best = list[0]; float bestDot = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (var v in list)
            {
                float d = Vector3.Dot(v, direction);
                if (d > bestDot) { bestDot = d; best = v; }
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            }
            width = Mathf.Max(.001f, maxX - minX);
            return best;
        }

        public Vector3 Tip(string boneName, Vector3 worldDirection)
        {
            var bone = Bone(boneName);
            return bone.TransformPoint(LocalTip(bone, worldDirection, out _));
        }

        // A random point on the body's surface, for effects that cover the whole model.
        public Vector3 RandomSurfacePoint(string onlyBonePrefix = null)
        {
            EnsureVertexCache();
            if (allVertices == null || allVertices.Count == 0) return transform.position;
            for (int tries = 0; tries < 8; tries++)
            {
                var (bone, local) = allVertices[UnityEngine.Random.Range(0, allVertices.Count)];
                if (onlyBonePrefix != null && !body.bones[bone].name.StartsWith(onlyBonePrefix, StringComparison.Ordinal)) continue;
                return body.bones[bone].TransformPoint(local);
            }
            return transform.position;
        }

        // ---------------------------------------------------------------- voxel parts (Hydra floor strike draws the head in 2D)
        public void VoxelPart(string boneName, int chunk, out Vector3[] vertices, out Vector3[] normals, out Color[] colors, out int[] triangles)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            if (voxels != null && body != null)
            {
                string json = voxels.text;
                var boneNames = Names(json, "bones"); var slotNames = Names(json, "slots");
                var colors32 = ThemeColors(json, slotNames);
                int wanted = boneNames.IndexOf(boneName);
                int boneIndex = Array.FindIndex(body.bones, b => b.name == boneName);
                float size = Number(json, "voxelSize", .1f) * Mathf.Max(1, chunk);
                var seen = new HashSet<Vector3Int>();
                if (wanted >= 0 && boneIndex >= 0)
                {
                    var bind = body.sharedMesh.bindposes[boneIndex];
                    foreach (Match m in Regex.Matches(json, "\\[(-?[0-9.]+),\\s*(-?[0-9.]+),\\s*(-?[0-9.]+),\\s*([0-9]+),\\s*" + wanted + "\\]"))
                    {
                        var p = new Vector3(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                        if (!seen.Add(Vector3Int.FloorToInt(p / size))) continue;
                        int slot = int.Parse(m.Groups[4].Value);
                        AddCube(bind.MultiplyPoint3x4(p), size * .5f, slot < colors32.Length ? (Color)colors32[slot] : Color.gray, verts, norms, cols, tris);
                    }
                }
            }
            vertices = verts.ToArray(); normals = norms.ToArray(); colors = cols.ToArray(); triangles = tris.ToArray();
        }

        static readonly Vector3[] CubeNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        static void AddCube(Vector3 center, float half, Color color, List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris)
        {
            foreach (var n in CubeNormals)
            {
                var u = n.x != 0 ? Vector3.up : Vector3.right;
                var v = Vector3.Cross(n, u);
                int first = verts.Count;
                foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    verts.Add(center + (n + u * a + v * b) * half); norms.Add(n); cols.Add(color);
                }
                tris.Add(first); tris.Add(first + 1); tris.Add(first + 2); tris.Add(first); tris.Add(first + 2); tris.Add(first + 3);
            }
        }

        // ---------------------------------------------------------------- keep the old model's runtime effects hidden
        // The old animation scripts rebuild flames, lightning, water and shards under the old object whenever they like.
        void HideOldRenderers(float dt)
        {
            hideSweep -= dt;
            if (hideSweep > 0f || transform.parent == null) return;
            hideSweep = .25f;
            foreach (var r in transform.parent.GetComponentsInChildren<Renderer>(true))
                if (!r.forceRenderingOff && r.GetComponentInParent<BossVoxelSkin>(true) == null) r.forceRenderingOff = true;
        }
    }
}
