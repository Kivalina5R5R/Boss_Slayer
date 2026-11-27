using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // Elemental finish for every BOSS ULTIMATE except the Demon's (which has BossDemonUltimateGraphic). The manager hands
    // over each ultimate attack's footprint (circle or quad, read from its telegraph view) with its warning progress or
    // release age; this draws, in the boss's element:
    //   warning  - a glowing rim exactly on the footprint plus an element motif inside (embers, ripples, bolts, shards,
    //              void spiral, scan line, bubbles);
    //   release  - an in-footprint flash, a shockwave, an element particle burst;
    //   residue  - scorch / wet sheen / crackle / stuck shards / shrinking hole / fading grid / venom puddle.
    // Wall: each release burst tints the wall and draws the element's streak pattern.
    // Rims sit on the real hit edge; anything drawn outside it is glow that fades within ~0.4 s. Presentation only.
    public sealed class BossUltimateOverlayGraphic : MaskableGraphic
    {
        public enum Element { Fire, Water, Lightning, Crystal, Void, Tech, Venom }

        public struct Item
        {
            public bool Circle;
            public Vector2 Center; public float Radius;           // circle
            public Vector2 C0, C1, C2, C3;                        // quad corners (local)
            public float Warn;                                    // 0..1 while warning, -1 otherwise
            public float Age;                                     // seconds since release, -1 before
            public float Seed;
        }

        public Element Style;
        public bool Wall;
        public float Clock;
        public readonly List<Item> Items = new();
        public readonly List<float> Bursts = new();   // wall: release times (on Clock)

        public void Clear() { Items.Clear(); Bursts.Clear(); SetVerticesDirty(); }

        // ------------------------------------------------------------------ palette
        Color Main => Style switch
        {
            Element.Fire => new Color(1f, .45f, .08f), Element.Water => new Color(.2f, .75f, 1f), Element.Lightning => new Color(.7f, .55f, 1f),
            Element.Crystal => Crimson ? new Color(1f, .12f, .25f) : new Color(.35f, .95f, 1f), Element.Void => new Color(.6f, .25f, 1f),
            Element.Tech => new Color(1f, .78f, .15f), _ => new Color(.45f, 1f, .25f),
        };
        Color Accent => Style switch
        {
            Element.Fire => new Color(1f, .88f, .35f), Element.Water => new Color(.85f, 1f, 1f), Element.Lightning => new Color(.75f, 1f, 1f),
            Element.Crystal => Crimson ? new Color(1f, .7f, .75f) : new Color(.9f, 1f, 1f), Element.Void => new Color(.95f, .6f, 1f),
            Element.Tech => new Color(1f, 1f, .8f), _ => new Color(.85f, .55f, 1f),
        };
        Color Deep => Style switch
        {
            Element.Fire => new Color(.35f, .05f, 0f), Element.Water => new Color(0f, .15f, .35f), Element.Lightning => new Color(.15f, .05f, .35f),
            Element.Crystal => Crimson ? new Color(.35f, 0f, .05f) : new Color(0f, .25f, .35f), Element.Void => new Color(.05f, 0f, .1f),
            Element.Tech => new Color(.3f, .2f, 0f), _ => new Color(.1f, .25f, .02f),
        };
        public bool Crimson;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Wall) { DrawWall(vh); return; }
            foreach (var it in Items)
            {
                if (it.Warn >= 0) DrawWarning(vh, it);
                else if (it.Age >= 0) DrawRelease(vh, it);
            }
        }

        // ------------------------------------------------------------------ warning
        void DrawWarning(VertexHelper vh, Item it)
        {
            float p = it.Warn, t = Clock, pulse = .5f + .5f * Mathf.Sin(t * (10 + 20 * p) + it.Seed * 5);
            var rim = Color.Lerp(Main, Accent, pulse * .6f); rim.a = .55f + .45f * p;
            var glow = new Color(Main.r, Main.g, Main.b, .15f + .3f * p);
            if (it.Circle)
            {
                float r = it.Radius, th = Mathf.Max(3, r * .05f);
                Disc(vh, it.Center, r, new Color(Main.r, Main.g, Main.b, .06f + .12f * p), new Color(Main.r, Main.g, Main.b, .18f + .2f * p));
                Ring(vh, it.Center, r + th * 1.2f, th * 2.4f, glow, 64, true);
                Ring(vh, it.Center, r - th * .5f, th, rim, 64);
            }
            else
            {
                Poly(vh, it, new Color(Main.r, Main.g, Main.b, .07f + .12f * p));
                QuadEdges(vh, it, Mathf.Max(3, ShortSide(it) * .05f), rim, glow);
            }
            Motif(vh, it, p, t);
        }

        void Motif(VertexHelper vh, Item it, float p, float t)
        {
            var c = it.Circle ? it.Center : Mid(it); float s = it.Circle ? it.Radius : ShortSide(it) * .5f;
            float L = it.Circle ? s : LongSide(it) * .5f; var along = it.Circle ? Vector2.right : Axis(it); var across = new Vector2(-along.y, along.x);
            int n;
            switch (Style)
            {
                case Element.Fire: // embers drifting up (screen up = +y)
                    n = 10;
                    for (int k = 0; k < n; k++)
                    {
                        float ph = Mathf.Repeat(t * .8f + Hash(it.Seed, k), 1);
                        var pos = c + along * L * (Hash(it.Seed + 1, k) * 2 - 1) * .85f + across * s * (Hash(it.Seed + 2, k) * 2 - 1) * .7f + Vector2.up * s * ph * .5f;
                        if (it.Circle && (pos - c).magnitude > s * .95f) continue;
                        Diamond(vh, pos, s * .07f * (1 - ph), new Color(1, .8f, .3f, (1 - ph) * (.4f + .6f * p)));
                    }
                    break;
                case Element.Water: // ripples
                    for (int k = 0; k < 3; k++)
                    {
                        float ph = Mathf.Repeat(t * .7f + k / 3f, 1);
                        if (it.Circle) Ring(vh, c, s * ph, s * .03f, new Color(.8f, 1, 1, (1 - ph) * .7f * p + .1f), 48);
                        else { var o = across * s * (ph * 2 - 1); Seg(vh, c - along * L + o, c + along * L + o, s * .04f, new Color(.8f, 1, 1, (1 - Mathf.Abs(ph * 2 - 1)) * .6f)); }
                    }
                    break;
                case Element.Lightning: // flickering bolts
                    if (Hash(Mathf.Floor(t * 14), it.Seed) < .35f + .5f * p)
                        for (int k = 0; k < 2; k++)
                        {
                            float ang = Hash(Mathf.Floor(t * 14) + k, it.Seed) * Mathf.PI * 2;
                            var dir = it.Circle ? new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) : along * (k == 0 ? 1 : -1);
                            Bolt(vh, c - dir * (it.Circle ? s * .9f : L), c + dir * (it.Circle ? s * .9f : L), s * .18f, 6, Mathf.Floor(t * 14) + k * 7 + it.Seed, s * .025f, new Color(.85f, .9f, 1, .9f));
                        }
                    break;
                case Element.Crystal: // shards growing in from the rim
                    n = 8;
                    for (int k = 0; k < n; k++)
                    {
                        Vector2 root, dir;
                        if (it.Circle) { float ang = k / (float)n * Mathf.PI * 2 + it.Seed; dir = -new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); root = c - dir * s; }
                        else { root = c + along * L * (k / (n - 1f) * 2 - 1) * .9f + across * s * (k % 2 == 0 ? 1 : -1); dir = across * (k % 2 == 0 ? -1 : 1); }
                        Shard(vh, root, dir, s * .35f * p, s * .1f, new Color(Accent.r, Accent.g, Accent.b, .5f + .4f * p));
                    }
                    break;
                case Element.Void: // spiral arms turning inward
                    for (int k = 0; k < 3; k++)
                    {
                        float a0 = t * 2.2f + k * Mathf.PI * 2 / 3;
                        for (int seg = 0; seg < 10; seg++)
                        {
                            float f0 = seg / 10f, f1 = (seg + 1) / 10f;
                            var q0 = c + Rot(a0 + f0 * 2.4f) * (it.Circle ? s : Mathf.Min(s, L)) * (1 - f0);
                            var q1 = c + Rot(a0 + f1 * 2.4f) * (it.Circle ? s : Mathf.Min(s, L)) * (1 - f1);
                            Seg(vh, q0, q1, s * .04f * (1 - f0), new Color(.8f, .45f, 1, .6f * (1 - f0) * (.4f + .6f * p)));
                        }
                    }
                    Disc(vh, c, s * .25f * p, new Color(0, 0, 0, .55f * p), new Color(.1f, 0, .2f, 0));
                    break;
                case Element.Tech: // sweeping scan line and corner brackets
                    {
                        float ph = Mathf.PingPong(t * 1.6f, 1);
                        if (it.Circle)
                        {
                            float y = (ph * 2 - 1) * s, half = Mathf.Sqrt(Mathf.Max(0, s * s - y * y));
                            Seg(vh, c + new Vector2(-half, y), c + new Vector2(half, y), s * .03f, new Color(1, 1, .7f, .8f));
                            for (int k = 0; k < 3; k++) Arc(vh, c, s * 1.08f, s * .04f, t * 1.8f + k * 2.1f, t * 1.8f + k * 2.1f + .8f, new Color(1, .85f, .3f, .85f), 10);
                        }
                        else
                        {
                            var o = along * L * (ph * 2 - 1);
                            Seg(vh, c + o - across * s, c + o + across * s, s * .06f, new Color(1, 1, .7f, .8f));
                        }
                    }
                    break;
                default: // Venom bubbles
                    n = 9;
                    for (int k = 0; k < n; k++)
                    {
                        float ph = Mathf.Repeat(t * .9f + Hash(it.Seed + 5, k), 1);
                        var pos = c + along * L * (Hash(it.Seed + 6, k) * 2 - 1) * .8f + across * s * (Hash(it.Seed + 7, k) * 2 - 1) * .7f;
                        if (it.Circle && (pos - c).magnitude > s * .9f) continue;
                        Ring(vh, pos, s * .08f * ph, s * .02f, new Color(.7f, 1, .4f, (1 - ph) * .8f), 12);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ release + residue
        void DrawRelease(VertexHelper vh, Item it)
        {
            float a = it.Age; if (a > 1.4f) return;
            var c = it.Circle ? it.Center : Mid(it); float s = it.Circle ? it.Radius : ShortSide(it) * .5f;
            float L = it.Circle ? s : LongSide(it) * .5f; var along = it.Circle ? Vector2.right : Axis(it); var across = new Vector2(-along.y, along.x);
            // Flash inside the footprint.
            float flash = Mathf.Clamp01(1 - a / .18f);
            if (flash > 0)
            {
                var fc = Color.Lerp(Main, Color.white, .6f); fc.a = .75f * flash;
                if (it.Circle) Disc(vh, c, s, fc, new Color(Main.r, Main.g, Main.b, .5f * flash)); else Poly(vh, it, fc);
            }
            // Shockwave (Void implodes instead).
            float w = Mathf.Clamp01(a / .42f), fade = 1 - w;
            if (fade > 0)
            {
                var sc = Color.Lerp(Accent, Main, w); sc.a = .9f * fade;
                if (Style == Element.Void)
                {
                    if (it.Circle) Ring(vh, c, s * (1.15f - w), s * .1f * fade, sc, 64);
                    else QuadEdges(vh, Inset(it, -s * .3f * fade), s * .08f * fade, sc, new Color(0, 0, 0, 0));
                }
                else if (it.Circle) Ring(vh, c, s * (.6f + .6f * w), s * .12f * fade, sc, 64);
                else QuadEdges(vh, Inset(it, -s * .5f * w), s * .1f * fade, sc, new Color(Main.r, Main.g, Main.b, .3f * fade));
            }
            // Particle burst.
            if (a < .7f)
            {
                float life = 1 - a / .7f;
                for (int k = 0; k < 18; k++)
                {
                    Vector2 origin = it.Circle ? c : c + along * L * (Hash(it.Seed + 9, k) * 2 - 1);
                    float ang = Hash(it.Seed + 10, k) * Mathf.PI * 2; var dir = it.Circle ? Rot(ang) : (across * (k % 2 == 0 ? 1 : -1) + along * (Hash(it.Seed + 11, k) - .5f)).normalized;
                    float speed = s * (2.2f + 2.4f * Hash(it.Seed + 12, k));
                    Vector2 pos;
                    switch (Style)
                    {
                        case Element.Void: pos = origin + dir * s * 1.4f * life; break;                               // pulled in
                        case Element.Fire: pos = origin + dir * speed * a * (1 - a * .6f) + Vector2.up * s * a * 1.2f; break; // rising
                        case Element.Water: pos = origin + dir * speed * a - Vector2.up * s * a * a * 3f; break;          // arcs down
                        default: pos = origin + dir * speed * a * (1 - a * .6f); break;
                    }
                    var pc = Color.Lerp(Main, Accent, life); pc.a = life;
                    switch (Style)
                    {
                        case Element.Crystal: Shard(vh, pos, Rot(ang + a * 9), s * .16f * life, s * .06f, pc); break;
                        case Element.Tech: Square(vh, pos, s * .07f * life, pc); break;
                        case Element.Lightning: Seg(vh, pos, pos - dir * s * .35f * life, s * .03f, new Color(.9f, .95f, 1, life)); break;
                        case Element.Water: case Element.Venom: Diamond(vh, pos, s * .08f * life, pc); break;
                        default: Seg(vh, pos, pos - dir * s * .18f * life, s * .05f * life, pc); break;
                    }
                }
            }
            // Residue.
            float res = Mathf.Clamp01(1 - (a - .2f) / 1.2f); if (a < .12f || res <= 0) return;
            switch (Style)
            {
                case Element.Fire:
                    if (it.Circle) { Disc(vh, c, s * .95f, new Color(.1f, .02f, 0, .35f * res), new Color(Deep.r, Deep.g, Deep.b, 0)); Ring(vh, c, s * .95f, s * .05f, new Color(1, .4f, .05f, .7f * res), 64); }
                    else QuadEdges(vh, it, s * .05f, new Color(1, .4f, .05f, .7f * res), new Color(1, .2f, 0, .2f * res));
                    break;
                case Element.Water:
                    if (it.Circle) Ring(vh, c, s * (.9f + .1f * Mathf.Sin(a * 8)), s * .04f, new Color(.7f, .95f, 1, .5f * res), 64);
                    else Poly(vh, it, new Color(.4f, .8f, 1, .12f * res));
                    break;
                case Element.Lightning:
                    if (Hash(Mathf.Floor(Clock * 18), it.Seed) < .5f * res)
                        Bolt(vh, c - (it.Circle ? Rot(it.Seed) * s * .8f : along * L), c + (it.Circle ? Rot(it.Seed) * s * .8f : along * L), s * .12f, 5, Mathf.Floor(Clock * 18) + it.Seed, s * .02f, new Color(.8f, .85f, 1, .8f * res));
                    break;
                case Element.Crystal:
                    for (int k = 0; k < 6; k++)
                    {
                        var pos = it.Circle ? c + Rot(k * 1.047f + it.Seed) * s * .6f : c + along * L * (k / 5f * 2 - 1) * .8f;
                        Shard(vh, pos, Rot(k * 1.3f + it.Seed), s * .2f, s * .07f, new Color(Accent.r, Accent.g, Accent.b, .7f * res));
                    }
                    break;
                case Element.Void:
                    Disc(vh, c, s * .35f * res, new Color(0, 0, 0, .7f * res), new Color(.25f, 0, .4f, 0));
                    break;
                case Element.Tech:
                    for (int k = -2; k <= 2; k++)
                    {
                        var o = across * s * k * .4f; var o2 = along * (it.Circle ? s : L) * k * .4f;
                        Seg(vh, c + o - along * (it.Circle ? s : L) * .9f, c + o + along * (it.Circle ? s : L) * .9f, 2, new Color(1, .85f, .3f, .35f * res));
                        Seg(vh, c + o2 - across * s * .9f, c + o2 + across * s * .9f, 2, new Color(1, .85f, .3f, .35f * res));
                    }
                    break;
                default:
                    if (it.Circle) Disc(vh, c, s * .85f, new Color(.4f, .9f, .15f, .3f * res), new Color(.3f, .6f, .1f, .05f * res));
                    else Poly(vh, it, new Color(.4f, .9f, .15f, .2f * res));
                    break;
            }
        }

        // ------------------------------------------------------------------ wall
        void DrawWall(VertexHelper vh)
        {
            var r = rectTransform.rect; float h = r.height;
            foreach (var b in Bursts)
            {
                float a = Clock - b; if (a < 0 || a > .8f) continue;
                float f = 1 - a / .8f;
                FullRect(vh, new Color(Main.r, Main.g, Main.b, .16f * Mathf.Clamp01(1 - a / .2f)));
                // 2026-11-27: the element streaks drawn over the boss are gone; only the tint (and the Void dark beat) stays.
                if (Style == Element.Void) FullRect(vh, new Color(0, 0, 0, .25f * f));
            }
        }

        // ------------------------------------------------------------------ geometry helpers
        static Vector2 Mid(Item it) => (it.C0 + it.C1 + it.C2 + it.C3) * .25f;
        static float SideA(Item it) => (it.C1 - it.C0).magnitude;
        static float SideB(Item it) => (it.C3 - it.C0).magnitude;
        static float ShortSide(Item it) => Mathf.Min(SideA(it), SideB(it));
        static float LongSide(Item it) => Mathf.Max(SideA(it), SideB(it));
        static Vector2 Axis(Item it) => (SideA(it) >= SideB(it) ? it.C1 - it.C0 : it.C3 - it.C0).normalized;
        static Vector2 Rot(float a) => new(Mathf.Cos(a), Mathf.Sin(a));
        static float Hash(float a, float b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f + 1.7f) * 43758.5453f, 1f);
        static Item Inset(Item it, float d)
        {
            var m = Mid(it);
            Vector2 Push(Vector2 p) { var v = p - m; return p - v.normalized * d; }
            it.C0 = Push(it.C0); it.C1 = Push(it.C1); it.C2 = Push(it.C2); it.C3 = Push(it.C3); return it;
        }

        void FullRect(VertexHelper vh, Color c) { var r = rectTransform.rect; Quad(vh, new(r.xMin, r.yMin), new(r.xMin, r.yMax), new(r.xMax, r.yMax), new(r.xMax, r.yMin), c, c, c, c); }
        static void Poly(VertexHelper vh, Item it, Color c) => Quad(vh, it.C0, it.C1, it.C2, it.C3, c, c, c, c);
        static void QuadEdges(VertexHelper vh, Item it, float th, Color rim, Color glow)
        {
            var pts = new[] { it.C0, it.C1, it.C2, it.C3 };
            for (int i = 0; i < 4; i++) Glow(vh, pts[i], pts[(i + 1) % 4], th, th * 5, rim, glow);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero); vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        static void Seg(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
        {
            var d = b - a; if (d.sqrMagnitude < .0001f || w <= 0) return;
            var n = new Vector2(-d.y, d.x).normalized * w * .5f; Quad(vh, a - n, a + n, b + n, b - n, c, c, c, c);
        }
        static void Glow(VertexHelper vh, Vector2 a, Vector2 b, float core, float glow, Color cc, Color gc)
        {
            var d = b - a; if (d.sqrMagnitude < .0001f) return;
            var n = new Vector2(-d.y, d.x).normalized; float hc = Mathf.Max(0, core) * .5f, hg = Mathf.Max(hc, glow * .5f);
            var clear = new Color(gc.r, gc.g, gc.b, 0);
            if (hg > hc) { Quad(vh, a + n * hg, a + n * hc, b + n * hc, b + n * hg, clear, gc, gc, clear); Quad(vh, a - n * hc, a - n * hg, b - n * hg, b - n * hc, gc, clear, clear, gc); }
            if (hc > 0) Quad(vh, a - n * hc, a + n * hc, b + n * hc, b - n * hc, cc, cc, cc, cc);
        }
        static void Diamond(VertexHelper vh, Vector2 c, float s, Color col) { s *= .5f; Quad(vh, c + new Vector2(-s, 0), c + new Vector2(0, s), c + new Vector2(s, 0), c + new Vector2(0, -s), col, col, col, col); }
        static void Square(VertexHelper vh, Vector2 c, float s, Color col) { s *= .5f; Quad(vh, c + new Vector2(-s, -s), c + new Vector2(-s, s), c + new Vector2(s, s), c + new Vector2(s, -s), col, col, col, col); }
        static void Shard(VertexHelper vh, Vector2 root, Vector2 dir, float len, float w, Color c)
        {
            if (len <= 0) return; var n = new Vector2(-dir.y, dir.x) * w * .5f; var tip = root + dir * len; var bright = Color.Lerp(c, Color.white, .5f);
            Quad(vh, root - n, root + n, tip, tip, c, c, bright, bright);
        }
        static void Disc(VertexHelper vh, Vector2 c, float r, Color centre, Color edge, int seg = 40)
        {
            if (r <= 0) return; int i0 = vh.currentVertCount; vh.AddVert(c, centre, Vector2.zero);
            for (int k = 0; k <= seg; k++) { float a = k / (float)seg * Mathf.PI * 2; vh.AddVert(c + Rot(a) * r, edge, Vector2.zero); if (k > 0) vh.AddTriangle(i0, i0 + k, i0 + k + 1); }
        }
        static void Ring(VertexHelper vh, Vector2 c, float r, float th, Color col, int seg, bool soft = false) => Arc(vh, c, r, th, 0, Mathf.PI * 2, col, seg, soft);
        static void Arc(VertexHelper vh, Vector2 c, float r, float th, float from, float to, Color col, int seg, bool soft = false)
        {
            if (th <= 0 || r <= 0) return; float r0 = Mathf.Max(0, r - th * .5f), r1 = r + th * .5f; var outer = soft ? new Color(col.r, col.g, col.b, 0) : col;
            for (int k = 0; k < seg; k++)
            {
                var d0 = Rot(Mathf.Lerp(from, to, k / (float)seg)); var d1 = Rot(Mathf.Lerp(from, to, (k + 1) / (float)seg));
                Quad(vh, c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0, col, outer, outer, col);
            }
        }
        static void Bolt(VertexHelper vh, Vector2 a, Vector2 b, float jag, int segs, float seed, float w, Color c)
        {
            var d = b - a; var n = new Vector2(-d.y, d.x).normalized; var prev = a;
            for (int k = 1; k <= segs; k++)
            {
                var p = k == segs ? b : a + d * k / segs + n * (Hash(seed, k) - .5f) * jag * 2;
                Glow(vh, prev, p, w, w * 5, c, new Color(c.r, c.g, c.b, c.a * .3f)); prev = p;
            }
        }
    }
}
