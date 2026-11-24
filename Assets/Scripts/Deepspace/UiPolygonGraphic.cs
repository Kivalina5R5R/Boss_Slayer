using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: a filled convex polygon for uGUI, in the RectTransform's local space. Used as the stencil of a Mask to
    // cut a UI screen along any line (BossWallSlash's two halves).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiPolygonGraphic : MaskableGraphic
    {
        Vector2[] points = new Vector2[0];

        // Corners in order (clockwise or anticlockwise), local to this RectTransform's pivot.
        public void SetPoints(Vector2[] corners)
        {
            points = corners ?? new Vector2[0];
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Length < 3) return;
            Color32 c = color;
            foreach (var p in points) vh.AddVert(p, c, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
