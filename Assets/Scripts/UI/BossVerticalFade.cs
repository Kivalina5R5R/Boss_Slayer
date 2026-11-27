using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-24: fades a UI picture out toward its edges. Its alpha eases down to 0 over the top `fade` part, and (when
    // `fadeBottom` > 0) over the bottom `fadeBottom` part too; in between the picture shows as is. Used on the fire
    // wall's Red Alpha glow (top only, "ค่า alpha เริ่มลดตั้งแต่ 30% ก่อนจะถึงตรงกลาง ค่อยๆ fade ไป") and on the VICTORY
    // screen's gold glow band (both edges, "ทำ Bg Light ให้จางๆ เหมือน Red alpha").
    // Each fade is a smoothstep curve drawn as Steps bands, so it has no visible start or end line.
    // Works on any single-quad Graphic (RawImage, simple Image).
    [DisallowMultipleComponent]
    public sealed class BossVerticalFade : BaseMeshEffect
    {
        const int Steps = 12;
        [Range(0f, 1f)] public float fade = .6f;
        [Range(0f, 1f)] public float fadeBottom;

        readonly List<UIVertex> quad = new();
        readonly List<float> heights = new(), alphas = new();

        public void SetFade(float top, float bottom = 0f)
        {
            fade = Mathf.Clamp01(top);
            fadeBottom = Mathf.Clamp01(bottom);
            if (fade + fadeBottom > 1f) { float k = 1f / (fade + fadeBottom); fade *= k; fadeBottom *= k; }
            if (graphic != null) graphic.SetVerticesDirty();
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4) return;
            quad.Clear();
            for (int i = 0; i < 4; i++) { var v = new UIVertex(); vh.PopulateUIVertex(ref v, i); quad.Add(v); }

            // Corners by position, so this does not depend on the Graphic's vertex order.
            var min = quad[0].position; var max = quad[0].position;
            var uvMin = (Vector2)quad[0].uv0; var uvMax = (Vector2)quad[0].uv0;
            foreach (var v in quad)
            {
                min = Vector3.Min(min, v.position); max = Vector3.Max(max, v.position);
                uvMin = Vector2.Min(uvMin, v.uv0); uvMax = Vector2.Max(uvMax, v.uv0);
            }
            var template = quad[0];
            Color32 color = template.color;

            // Rows from the bottom (0) to the top (1), with their alpha.
            heights.Clear(); alphas.Clear();
            float bottom = Mathf.Clamp01(fadeBottom), top = 1f - Mathf.Clamp01(fade);
            if (bottom > 0f)
                for (int i = 0; i <= Steps; i++) { float t = i / (float)Steps; heights.Add(t * bottom); alphas.Add(Smooth(t)); }
            else { heights.Add(0f); alphas.Add(1f); }
            if (top < 1f)
                for (int i = 0; i <= Steps; i++) { float t = i / (float)Steps; heights.Add(Mathf.Lerp(top, 1f, t)); alphas.Add(1f - Smooth(t)); }
            else { heights.Add(1f); alphas.Add(1f); }

            vh.Clear();
            for (int row = 0; row < heights.Count; row++)
            {
                var c = color; c.a = (byte)Mathf.RoundToInt(color.a * alphas[row]);
                for (int side = 0; side < 2; side++)
                {
                    var v = template;
                    v.position = new Vector3(side == 0 ? min.x : max.x, Mathf.Lerp(min.y, max.y, heights[row]), min.z);
                    v.uv0 = new Vector4(side == 0 ? uvMin.x : uvMax.x, Mathf.Lerp(uvMin.y, uvMax.y, heights[row]), 0f, 0f);
                    v.color = c;
                    vh.AddVert(v);
                }
            }
            // vertex index = row * 2 + side; neighbouring rows make a band
            for (int row = 0; row < heights.Count - 1; row++)
            {
                int a = row * 2, b = a + 2;
                vh.AddTriangle(a, b, b + 1); vh.AddTriangle(b + 1, a + 1, a);
            }
        }
    }
}
