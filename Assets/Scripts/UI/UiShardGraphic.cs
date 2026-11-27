using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: a textured convex polygon for uGUI: one stone piece of the wall crumble (BossWallCrumble). Each corner
    // has its own UV, so a piece shows exactly its part of a picture wherever it moves or turns.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiShardGraphic : MaskableGraphic
    {
        Texture picture;
        Vector2[] points = new Vector2[0];
        Vector2[] uvs = new Vector2[0];

        public override Texture mainTexture => picture != null ? picture : s_WhiteTexture;

        // Corners in order, local to this RectTransform's pivot, and the picture UV of each corner.
        public void Set(Texture texture, Vector2[] corners, Vector2[] cornerUvs)
        {
            picture = texture;
            points = corners ?? new Vector2[0];
            uvs = cornerUvs ?? new Vector2[0];
            SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Length < 3 || uvs.Length != points.Length) return;
            Color32 c = color;
            for (int i = 0; i < points.Length; i++) vh.AddVert(points[i], c, uvs[i]);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
