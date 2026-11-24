using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // All the drawing for the Demon ultimate SEVERING JUDGEMENT (BossSlayerGameManager.DemonUltimate.cs), one mesh per display.
    // Floor: crackling cut scratches with sparks racing along them; on release a white slash that tears the floor open
    // (two edges pulling apart), spark showers and a lingering scar. The spear gets a spinning reticle with the three
    // blast rings, a falling streak, and per blast a flash, a shockwave, lava cracks and debris.
    // Wall: every cut set slices the whole wall with giant streaks after a short dark "time stop"; blasts flash it orange.
    // Widths drawn as danger always equal the real hit widths; everything wider is glow that fades before it matters.
    // Presentation only; the manager feeds it a clock and the shapes it spawned.
    public sealed class BossDemonUltimateGraphic : MaskableGraphic
    {
        public struct Cut { public Vector2 A, B; public float Width, WarnAt, ReleaseAt, Seed; }
        public readonly List<Cut> Cuts = new();
        public bool Wall, HasSpear;
        public Vector2 SpearTarget;
        public float SpearAimAt;
        public readonly List<float> BlastAt = new(), BlastRadius = new();
        public float Clock;
        public float LastEvent => Mathf.Max(LastCut, BlastAt.Count > 0 ? BlastAt[BlastAt.Count - 1] : 0);
        float LastCut { get { float m = 0; foreach (var c in Cuts) m = Mathf.Max(m, c.ReleaseAt); return m; } }

        static readonly Color White = Color.white, Hot = new(1f, .86f, .9f), Crimson = new(1f, .05f, .14f),
            Blood = new(.55f, 0f, .06f), Ember = new(1f, .45f, .12f), Lava = new(1f, .32f, .06f);

        public void Clear() { Cuts.Clear(); BlastAt.Clear(); BlastRadius.Clear(); HasSpear = false; Clock = 0; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var offset = (Vector2)rectTransform.localPosition;
            float t = Clock;
            // Release times of whole cut sets (all cuts of a set release together).
            var sets = new List<float>();
            foreach (var c in Cuts) if (!sets.Exists(s => Mathf.Abs(s - c.ReleaseAt) < .01f)) sets.Add(c.ReleaseAt);

            // "Time stop": the display dims just before each set cuts.
            foreach (var r in sets) { float before = r - t; if (before > 0 && before < .18f) FullRect(vh, new Color(0, 0, 0, (Wall ? .32f : .38f) * (1 - before / .18f))); }
            if (Wall) DrawWall(vh, t, sets);
            else
            {
                foreach (var c in Cuts) DrawCut(vh, c, t, offset);
                if (HasSpear) DrawSpear(vh, t, SpearTarget - offset);
            }
            foreach (var r in sets) { float age = t - r; if (age >= 0 && age < .1f) FullRect(vh, new Color(1, .75f, .8f, (Wall ? .16f : .2f) * (1 - age / .1f))); }
            if (HasSpear)
                for (int i = 0; i < BlastAt.Count; i++)
                {
                    float age = t - BlastAt[i];
                    if (age >= 0 && age < .15f) FullRect(vh, new Color(1, .5f, .2f, (Wall ? .12f : .14f) * (1 + i * .35f) * (1 - age / .15f)));
                }
        }

        // ---------------------------------------------------------------- floor cuts
        void DrawCut(VertexHelper vh, Cut c, float t, Vector2 offset)
        {
            if (t < c.WarnAt) return;
            Vector2 a = c.A - offset, b = c.B - offset; float w = c.Width;
            if (t < c.ReleaseAt)
            {
                float p = Mathf.Clamp01((t - c.WarnAt) / Mathf.Max(.01f, c.ReleaseAt - c.WarnAt));
                float flick = .5f + .5f * Mathf.Sin(t * (22 + 40 * p) + c.Seed * 9);
                bool last = c.ReleaseAt - t < .12f;
                GlowLine(vh, a, b, w * .07f * (.6f + p), w * (.35f + .65f * p),
                    last ? White : Color.Lerp(Crimson, Hot, flick * .5f) * new Color(1, 1, 1, .55f + .45f * flick),
                    new Color(Crimson.r, Crimson.g, Crimson.b, .12f + .22f * p));
                // Hairline fractures along the scratch, appearing as the warning builds.
                var dir = (b - a).normalized; var n = new Vector2(-dir.y, dir.x); float len = (b - a).magnitude;
                int ticks = Mathf.RoundToInt(22 * p);
                for (int k = 0; k < ticks; k++)
                {
                    float s = Hash(c.Seed, k), side = Hash(c.Seed + 3, k) > .5f ? 1 : -1;
                    var root = a + dir * len * s;
                    Segment(vh, root, root + (n * side * .9f + dir * (Hash(c.Seed + 7, k) - .5f)) * w * (.4f + .6f * Hash(c.Seed + 5, k)), w * .035f, new Color(1, .2f, .25f, .7f * p));
                }
                for (int k = 0; k < 3; k++)
                {
                    float s = Mathf.Repeat(t * (1.1f + p * 1.6f) + c.Seed + k / 3f, 1);
                    Diamond(vh, Vector2.Lerp(a, b, s), w * (.22f + .2f * p), new Color(1, .9f, .9f, .9f));
                }
                return;
            }
            float age = t - c.ReleaseAt;
            if (age > 1.5f) return;
            var d = (b - a).normalized; var nn = new Vector2(-d.y, d.x); float L = (b - a).magnitude;
            // Glow and the white slash (never wider than the hit width).
            float glow = Mathf.Clamp01(1 - age / .6f), core = 1 - Mathf.SmoothStep(0, 1, age / .32f);
            if (glow > 0) GlowLine(vh, a, b, w * .92f * core, w * 1.6f * glow, Color.Lerp(Hot, White, core), new Color(1, .02f, .1f, .55f * glow));
            if (age < .05f) GlowLine(vh, a, b, w, w * 1.8f, White, new Color(1, .3f, .35f, .6f));
            // The floor tears: two dark edges pull apart from the cut.
            float tear = Mathf.Clamp01(1 - age / .55f);
            if (tear > 0)
                for (int s = -1; s <= 1; s += 2)
                {
                    var o = nn * s * w * (.5f + age * 2.6f);
                    GlowLine(vh, a + o, b + o, w * .06f, w * .3f, new Color(Blood.r, Blood.g, Blood.b, .9f * tear), new Color(1, .1f, .1f, .25f * tear));
                }
            // Spark shower thrown off both sides.
            if (age < .55f)
                for (int k = 0; k < 18; k++)
                {
                    float s = Hash(c.Seed + 11, k), side = k % 2 == 0 ? 1 : -1, speed = w * (7 + 9 * Hash(c.Seed + 13, k));
                    var origin = a + d * L * s;
                    var vel = (nn * side + d * (Hash(c.Seed + 17, k) - .5f) * 1.4f).normalized * speed;
                    var pos = origin + vel * age * (1 - age * .9f);
                    float life = 1 - age / .55f;
                    Segment(vh, pos, pos - vel.normalized * w * 1.2f * life, w * .07f * life, Color.Lerp(Ember, White, life) * new Color(1, 1, 1, life));
                }
            // Scar: a thin glowing seam that cools off.
            float scar = Mathf.Clamp01(1 - age / 1.5f);
            if (age > .15f) GlowLine(vh, a, b, w * .1f, w * .45f, new Color(1, .3f, .2f, .8f * scar), new Color(Blood.r, Blood.g, Blood.b, .35f * scar));
        }

        // ---------------------------------------------------------------- spear
        void DrawSpear(VertexHelper vh, float t, Vector2 c)
        {
            if (BlastAt.Count == 0 || t < SpearAimAt) return;
            float first = BlastAt[0], last = BlastAt[BlastAt.Count - 1];
            float rMax = BlastRadius[BlastRadius.Count - 1];
            if (t < first)
            {
                float p = Mathf.Clamp01((t - SpearAimAt) / Mathf.Max(.01f, first - SpearAimAt));
                float pulse = .5f + .5f * Mathf.Sin(t * (10 + 18 * p));
                // Danger rings (the real blast sizes), innermost filled, each brightening as its turn nears.
                Disc(vh, c, BlastRadius[0], new Color(1, .1f, .1f, .12f + .18f * p), new Color(1, .1f, .1f, .28f + .2f * p));
                for (int i = 0; i < BlastRadius.Count; i++)
                    Ring(vh, c, BlastRadius[i], BlastRadius[0] * (.05f + .02f * i), new Color(1, .15f + .1f * i, .15f, (.35f + .45f * pulse) * (1 - i * .18f)), 72);
                // Spinning reticle arcs and ticks.
                float spin = t * 2.4f;
                for (int k = 0; k < 4; k++)
                    Arc(vh, c, BlastRadius[0] * 1.35f, BlastRadius[0] * .08f, spin + k * Mathf.PI * .5f, spin + k * Mathf.PI * .5f + .9f, new Color(1, .85f, .85f, .85f), 14);
                for (int k = 0; k < 4; k++)
                {
                    float ang = -spin * .6f + k * Mathf.PI * .5f; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Segment(vh, c + dir * rMax * 1.02f, c + dir * rMax * 1.18f, BlastRadius[0] * .07f, new Color(1, .3f, .3f, .9f));
                }
                // The spear streaks in during the last 0.3 s.
                float fall = Mathf.Clamp01(1 - (first - t) / .3f);
                if (fall > 0)
                {
                    var from = c + new Vector2(.55f, .85f).normalized * rMax * 4f;
                    var head = Vector2.Lerp(from, c, fall * fall);
                    GlowLine(vh, from, head, BlastRadius[0] * .05f, BlastRadius[0] * .35f, new Color(1, .8f, .6f, .9f), new Color(1, .3f, .1f, .35f));
                }
                return;
            }
            // Blasts still to come keep their danger ring, so players see how far they still have to run.
            for (int i = 0; i < BlastAt.Count; i++)
            {
                float until = BlastAt[i] - t; if (until <= 0) continue;
                float pulse = .5f + .5f * Mathf.Sin(t * 24);
                Ring(vh, c, BlastRadius[i], BlastRadius[0] * .07f, new Color(1, .2f, .15f, .55f + .4f * pulse), 80);
            }
            // Craters: lava cracks from the spear, growing with each blast, cooling after the last.
            float cool = Mathf.Clamp01(1 - (t - last) / 1.4f);
            for (int i = 0; i < BlastAt.Count; i++)
            {
                float age = t - BlastAt[i]; if (age < 0) continue;
                float r = BlastRadius[i];
                float grow = Mathf.Clamp01(age / .1f);
                for (int k = 0; k < 9; k++)
                {
                    float ang = (k + Hash(i, k) * .6f) / 9f * Mathf.PI * 2; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var n = new Vector2(-dir.y, dir.x); var p0 = c;
                    for (int seg = 1; seg <= 3; seg++)
                    {
                        var p1 = c + dir * r * grow * seg / 3f + n * r * (Hash(i * 7 + seg, k) - .5f) * .25f;
                        GlowLine(vh, p0, p1, r * .025f, r * .09f, new Color(1, .75f, .4f, .9f * cool), new Color(Lava.r, Lava.g, Lava.b, .35f * cool));
                        p0 = p1;
                    }
                }
                if (age > .9f) continue;
                float flash = Mathf.Clamp01(1 - age / .25f);
                if (flash > 0) Disc(vh, c, r * (.7f + .3f * flash), new Color(1, 1, .92f, .85f * flash), new Color(1, .5f, .15f, .45f * flash));
                float wave = Mathf.Clamp01(age / .42f), fade = 1 - wave;
                Ring(vh, c, r * (.35f + .85f * wave), r * .14f * fade, new Color(1, .82f, .55f, .9f * fade), 80);
                Ring(vh, c, r * (.25f + .7f * Mathf.Clamp01(age / .6f)), r * .06f * (1 - Mathf.Clamp01(age / .6f)), new Color(1, .15f, .1f, .8f * (1 - Mathf.Clamp01(age / .6f))), 80);
                if (age < .6f)
                    for (int k = 0; k < 20; k++)
                    {
                        float ang = Hash(i + 21, k) * Mathf.PI * 2; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float speed = r * (2.4f + 2.6f * Hash(i + 23, k)), life = 1 - age / .6f;
                        var pos = c + dir * speed * age * (1 - age * .7f);
                        Segment(vh, pos, pos - dir * r * .22f * life, r * .035f * life, Color.Lerp(Ember, White, life) * new Color(1, 1, 1, life));
                    }
            }
            // The planted spear glows through the blasts.
            if (t < last + 1.4f) Disc(vh, c, BlastRadius[0] * .16f, new Color(1, .95f, .85f, cool), new Color(1, .35f, .1f, 0));
        }

        // ---------------------------------------------------------------- wall
        void DrawWall(VertexHelper vh, float t, List<float> sets)
        {
            var rect = rectTransform.rect; float h = rect.height;
            for (int si = 0; si < sets.Count; si++)
            {
                float age = t - sets[si]; if (age < 0 || age > .7f) continue;
                for (int k = 0; k < 5; k++)
                {
                    float ang = Mathf.Lerp(-38, 38, Hash(si * 5 + 1, k)) * Mathf.Deg2Rad + (k % 2 == 0 ? 0 : Mathf.PI * .5f * .35f);
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var p = new Vector2(Mathf.Lerp(rect.xMin + rect.width * .12f, rect.xMax - rect.width * .12f, Hash(si * 5 + 2, k)), Mathf.Lerp(rect.yMin + h * .2f, rect.yMax - h * .2f, Hash(si * 5 + 3, k)));
                    // Each streak draws across within 40 ms, staggered slightly, then thins out.
                    float reach = Mathf.Clamp01((age - k * .018f) / .04f) * rect.width * .75f;
                    if (reach <= 0) continue;
                    float core = 1 - Mathf.SmoothStep(0, 1, age / .35f), glow = Mathf.Clamp01(1 - age / .7f);
                    GlowLine(vh, p - dir * reach, p + dir * reach, h * .022f * core + 2, h * .12f * glow, Color.Lerp(Hot, White, core) * new Color(1, 1, 1, .4f + .6f * core), new Color(1, .06f, .14f, .5f * glow));
                }
            }
        }

        // ---------------------------------------------------------------- primitives
        static float Hash(float a, float b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f + 1.7f) * 43758.5453f, 1f);

        void FullRect(VertexHelper vh, Color c)
        {
            var r = rectTransform.rect;
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), c, c, c, c);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero); vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a; if (d.sqrMagnitude < .0001f || width <= 0) return;
            var n = new Vector2(-d.y, d.x).normalized * width * .5f;
            Quad(vh, a - n, a + n, b + n, b - n, c, c, c, c);
        }

        // Solid core with a soft glow falling off to transparent on both sides.
        static void GlowLine(VertexHelper vh, Vector2 a, Vector2 b, float core, float glow, Color coreColor, Color glowColor)
        {
            var d = b - a; if (d.sqrMagnitude < .0001f) return;
            var n = new Vector2(-d.y, d.x).normalized;
            float hc = Mathf.Max(0, core) * .5f, hg = Mathf.Max(hc, glow * .5f);
            var clear = new Color(glowColor.r, glowColor.g, glowColor.b, 0);
            if (hg > hc)
            {
                Quad(vh, a + n * hg, a + n * hc, b + n * hc, b + n * hg, clear, glowColor, glowColor, clear);
                Quad(vh, a - n * hc, a - n * hg, b - n * hg, b - n * hc, glowColor, clear, clear, glowColor);
            }
            if (hc > 0) Quad(vh, a - n * hc, a + n * hc, b + n * hc, b - n * hc, coreColor, coreColor, coreColor, coreColor);
        }

        static void Diamond(VertexHelper vh, Vector2 c, float size, Color color)
        {
            float s = size * .5f;
            Quad(vh, c + new Vector2(-s, 0), c + new Vector2(0, s), c + new Vector2(s, 0), c + new Vector2(0, -s), color, color, color, color);
        }

        static void Disc(VertexHelper vh, Vector2 c, float r, Color centre, Color edge, int segments = 48)
        {
            int i0 = vh.currentVertCount;
            vh.AddVert(c, centre, Vector2.zero);
            for (int k = 0; k <= segments; k++)
            {
                float a = k / (float)segments * Mathf.PI * 2;
                vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, edge, Vector2.zero);
                if (k > 0) vh.AddTriangle(i0, i0 + k, i0 + k + 1);
            }
        }

        static void Ring(VertexHelper vh, Vector2 c, float r, float thickness, Color color, int segments)
            => Arc(vh, c, r, thickness, 0, Mathf.PI * 2, color, segments);

        static void Arc(VertexHelper vh, Vector2 c, float r, float thickness, float from, float to, Color color, int segments)
        {
            if (thickness <= 0 || r <= 0) return;
            float r0 = Mathf.Max(0, r - thickness * .5f), r1 = r + thickness * .5f;
            for (int k = 0; k < segments; k++)
            {
                float a0 = Mathf.Lerp(from, to, k / (float)segments), a1 = Mathf.Lerp(from, to, (k + 1) / (float)segments);
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)); var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Quad(vh, c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0, color, color, color, color);
            }
        }
    }
}
