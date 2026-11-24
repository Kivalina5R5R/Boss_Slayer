using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: the sword cut of BossWallSlash: a blade-shaped streak (pointed at both ends, widest in the middle) that
    // grows from where the sword enters to its tip, a white core inside a warm glow. Local space of its RectTransform.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossSlashStreak : MaskableGraphic
    {
        static readonly Color Core = new(1f, .98f, .92f, 1f), Glow = new(1f, .62f, .2f, .45f);
        const float GlowScale = 3.2f;

        Vector2 from, to;
        float coreWidth, progress, fade = 1f;

        // The cut runs from `start` to `end`; width is the core's widest point.
        public void Configure(Vector2 start, Vector2 end, float width)
        {
            from = start; to = end; coreWidth = width;
            SetVerticesDirty();
        }

        // How far the blade has travelled (0..1) and how visible the streak still is (0..1).
        public void Show(float travelled, float visible)
        {
            progress = Mathf.Clamp01(travelled); fade = Mathf.Clamp01(visible);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (progress <= 0f || fade <= 0f) return;
            Vector2 tip = Vector2.Lerp(from, to, progress);
            Blade(vh, tip, coreWidth * GlowScale, Glow);
            Blade(vh, tip, coreWidth, Core);
        }

        void Blade(VertexHelper vh, Vector2 tip, float width, Color tint)
        {
            Vector2 along = tip - from;
            if (along.sqrMagnitude < 1f) return;
            Vector2 side = new Vector2(-along.y, along.x).normalized * width * .5f;
            Vector2 middle = from + along * .55f;
            tint.a *= fade;
            Color32 c = tint;
            int start = vh.currentVertCount;
            vh.AddVert(from, c, Vector2.zero);
            vh.AddVert(middle + side, c, Vector2.zero);
            vh.AddVert(tip, c, Vector2.zero);
            vh.AddVert(middle - side, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
