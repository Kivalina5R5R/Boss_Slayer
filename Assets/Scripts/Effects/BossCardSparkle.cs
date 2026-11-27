using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: "ปุ่มบอสถ้าเลือกตัวไหนอยู่ให้มี particle ที่ปุ่มนั้น" - pixel sparks drawn as UI quads around the picked
    // boss card (BossSelectionPanel.Cards.cs). Drawn by the canvas itself, so it works on an overlay canvas where a
    // ParticleSystem would not show. Each spark is born on the card's edge, drifts outward and up, twinkles and fades.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossCardSparkle : MaskableGraphic
    {
        const int Count = 36;
        const float MinLife = .7f, MaxLife = 1.4f;
        // Sizes and drift as fractions of the card's height.
        const float MinSize = .045f, MaxSize = .1f, DriftSpeed = .35f, RiseSpeed = .25f;

        readonly Vector2[] position = new Vector2[Count], velocity = new Vector2[Count];
        readonly float[] age = new float[Count], life = new float[Count], size = new float[Count];

        protected override void OnEnable()
        {
            base.OnEnable();
            // Start mid-life so the sparks are already spread around the card the moment it is picked.
            for (int i = 0; i < Count; i++) { Spawn(i); age[i] = Random.value * life[i]; }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, h = Mathf.Max(1f, rectTransform.rect.height);
            for (int i = 0; i < Count; i++)
            {
                age[i] += dt;
                if (age[i] >= life[i]) { Spawn(i); continue; }
                position[i] += velocity[i] * h * dt;
            }
            SetVerticesDirty();
        }

        // A new spark on a random point of the card's edge, heading out from the centre.
        void Spawn(int i)
        {
            var r = rectTransform.rect;
            float edge = Random.value * 2f * (r.width + r.height);
            Vector2 p;
            if (edge < r.width) p = new Vector2(r.xMin + edge, r.yMax);
            else if ((edge -= r.width) < r.height) p = new Vector2(r.xMax, r.yMax - edge);
            else if ((edge -= r.height) < r.width) p = new Vector2(r.xMax - edge, r.yMin);
            else p = new Vector2(r.xMin, r.yMin + edge - r.width);
            position[i] = p;
            var outward = (p - r.center); outward.x /= Mathf.Max(1f, r.width); outward.y /= Mathf.Max(1f, r.height);
            velocity[i] = outward.normalized * DriftSpeed * Random.Range(.4f, 1f) + Vector2.up * RiseSpeed * Random.Range(.3f, 1f);
            age[i] = 0f; life[i] = Random.Range(MinLife, MaxLife);
            size[i] = Random.Range(MinSize, MaxSize);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float h = Mathf.Max(1f, rectTransform.rect.height);
            for (int i = 0; i < Count; i++)
            {
                if (life[i] <= 0f) continue;
                float t = age[i] / life[i];
                // Pops in, twinkles, shrinks away.
                float alpha = Mathf.Clamp01(t * 6f) * (1f - t) * (.65f + .35f * Mathf.Sin(age[i] * 25f + i));
                float half = size[i] * h * .5f * (1f - t * .6f);
                var c = color; c.a *= alpha;
                // A plus-shaped pixel spark: one tall bar and one wide bar.
                Quad(vh, position[i], new Vector2(half * .35f, half), c);
                Quad(vh, position[i], new Vector2(half, half * .35f), c);
            }
        }

        static void Quad(VertexHelper vh, Vector2 centre, Vector2 half, Color32 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(centre + new Vector2(-half.x, -half.y), c, Vector2.zero);
            vh.AddVert(centre + new Vector2(-half.x, half.y), c, Vector2.up);
            vh.AddVert(centre + new Vector2(half.x, half.y), c, Vector2.one);
            vh.AddVert(centre + new Vector2(half.x, -half.y), c, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
