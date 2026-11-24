using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: "ควันมันต้องเป็นฝุ่นซิ อันนี้มันเป็น sprite เปลี่ยนเป็น Effect หน่อย" - billowing smoke and dust for the wall
    // crumble (BossWallCrumble), drawn by the canvas itself like BossCardSparkle, so it needs no particle material and
    // shows on any canvas. Each burst throws several soft puffs that swell, turn, drift and fade; a picture split into
    // cells (a flipbook sheet) gives every puff a random cell. Textures: Resources/BossSlayer/Transition (Smoke, Dust,
    // from the project's GameVFX Collection).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossSmokeGraphic : MaskableGraphic
    {
        Texture picture;
        int columns = 1, rows = 1;

        struct Puff
        {
            public Vector2 Position, Velocity;
            public float Age, Life, Size, Grow, Angle, Spin, Alpha;
            public int Cell;
        }

        readonly List<Puff> puffs = new List<Puff>();

        public override Texture mainTexture => picture != null ? picture : s_WhiteTexture;

        public void Use(Texture texture, int sheetColumns, int sheetRows)
        {
            picture = texture; columns = Mathf.Max(1, sheetColumns); rows = Mathf.Max(1, sheetRows);
            SetMaterialDirty();
        }

        // A burst of `count` puffs around `at` (local units). Sizes and speeds are in local units too.
        public void Burst(Vector2 at, int count, float spread, float size, float speed, float life, float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle;
                puffs.Add(new Puff
                {
                    Position = at + dir * spread,
                    Velocity = new Vector2(dir.x * speed, Random.Range(-.6f, .35f) * speed),
                    Life = life * Random.Range(.7f, 1.3f),
                    Size = size * Random.Range(.6f, 1.3f),
                    Grow = Random.Range(1.6f, 2.6f),
                    Angle = Random.Range(0f, 360f),
                    Spin = Random.Range(-25f, 25f),
                    Alpha = alpha * Random.Range(.6f, 1f),
                    Cell = Random.Range(0, columns * rows),
                });
            }
        }

        public bool Busy => puffs.Count > 0;

        void Update()
        {
            if (puffs.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                p.Age += dt;
                if (p.Age >= p.Life) { puffs.RemoveAt(i); continue; }
                p.Position += p.Velocity * dt;
                p.Velocity *= 1f - Mathf.Min(1f, 1.6f * dt);   // air drag: a quick burst that hangs and spreads
                p.Angle += p.Spin * dt;
                puffs[i] = p;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 cell = new Vector2(1f / columns, 1f / rows);
            foreach (var p in puffs)
            {
                float u = p.Age / p.Life;
                float half = p.Size * Mathf.Lerp(.5f, p.Grow, 1f - (1f - u) * (1f - u)) * .5f;
                var c = (Color32)new Color(color.r, color.g, color.b, color.a * p.Alpha * Mathf.Clamp01(u / .1f) * (1f - u) * (1f - u));
                int cx = p.Cell % columns, cy = p.Cell / columns;
                var uv0 = new Vector2(cx * cell.x, 1f - (cy + 1) * cell.y);
                float rad = p.Angle * Mathf.Deg2Rad, cs = Mathf.Cos(rad) * half, sn = Mathf.Sin(rad) * half;
                Vector2 ax = new Vector2(cs, sn), ay = new Vector2(-sn, cs);
                int start = vh.currentVertCount;
                vh.AddVert(p.Position - ax - ay, c, uv0);
                vh.AddVert(p.Position - ax + ay, c, uv0 + new Vector2(0f, cell.y));
                vh.AddVert(p.Position + ax + ay, c, uv0 + cell);
                vh.AddVert(p.Position + ax - ay, c, uv0 + new Vector2(cell.x, 0f));
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
