using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // Code-native vector badges stay crisp on both the wall and LED-floor canvases.
    // บาส 2026-11-11: redrawn from the Design team's new voxel models (Boss_Slayer_SEND previews, the theme each fight
    // starts in), each boss's SYMBOL in its own colours; the ring takes the boss's main colour. Source of the shapes and
    // a preview sheet: scratchpad emblems/emblems.py (generates the methods below).
    // Drawn as PIXEL ART to match the voxel bosses: the shapes below are painted onto a GridSize x GridSize grid
    // (painter's order, last shape wins per pixel) and every filled pixel becomes one square.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossEmblemGraphic : MaskableGraphic
    {
        public BossPassiveState.Kind Kind;
        VertexHelper mesh;
        Vector2 center; float unit;
        readonly Color ink = new Color(.035f,.055f,.095f,1);
        static Color C(float r, float g, float b) => new Color(r, g, b, 1);

        const int GridSize = 34;
        struct Tri { public Vector2 A, B, C; public Color Color; }
        readonly System.Collections.Generic.List<Tri> tris = new();
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color) => tris.Add(new Tri { A = a, B = b, C = c, Color = color });

        static bool Inside(Vector2 p, in Tri t)
        {
            float d1 = (p.x - t.B.x) * (t.A.y - t.B.y) - (t.A.x - t.B.x) * (p.y - t.B.y);
            float d2 = (p.x - t.C.x) * (t.B.y - t.C.y) - (t.B.x - t.C.x) * (p.y - t.C.y);
            float d3 = (p.x - t.A.x) * (t.C.y - t.A.y) - (t.C.x - t.A.x) * (p.y - t.A.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // Paint the recorded shapes onto the pixel grid, then one quad per filled pixel.
        void Rasterize()
        {
            float cell = 2f / GridSize;
            for (int gy = 0; gy < GridSize; gy++)
            for (int gx = 0; gx < GridSize; gx++)
            {
                var p = new Vector2(-1 + (gx + .5f) * cell, -1 + (gy + .5f) * cell);
                bool hit = false; Color color = default;
                for (int i = tris.Count - 1; i >= 0; i--)
                {
                    var t = tris[i];
                    if (p.x < Mathf.Min(t.A.x, Mathf.Min(t.B.x, t.C.x)) || p.x > Mathf.Max(t.A.x, Mathf.Max(t.B.x, t.C.x))) continue;
                    if (p.y < Mathf.Min(t.A.y, Mathf.Min(t.B.y, t.C.y)) || p.y > Mathf.Max(t.A.y, Mathf.Max(t.B.y, t.C.y))) continue;
                    if (!Inside(p, t)) continue;
                    hit = true; color = t.Color; break;
                }
                if (!hit) continue;
                var min = center + new Vector2(-1 + gx * cell, -1 + gy * cell) * unit; float size = cell * unit;
                int start = mesh.currentVertCount;
                mesh.AddVert(min, color, Vector2.zero); mesh.AddVert(min + new Vector2(0, size), color, Vector2.zero);
                mesh.AddVert(min + new Vector2(size, size), color, Vector2.zero); mesh.AddVert(min + new Vector2(size, 0), color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
        void Disc(float x,float y,float rx,float ry,Color color) => Arc(x,y,rx,ry,0,Mathf.PI*2,color);
        // Filled ellipse sector from angle `from` to `to` (radians); 0..PI = top half.
        void Arc(float x,float y,float rx,float ry,float from,float to,Color color)
        {
            const int steps=40;
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)steps),b=Mathf.Lerp(from,to,(i+1)/(float)steps);
                Triangle(new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),new Vector2(x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry),color);
            }
        }
        void Bar(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Triangle(a-side,a+side,b+side,color); Triangle(a-side,b+side,b-side,color);
        }
        // Convex polygon, fanned from the first point.
        void Poly(Color color, params Vector2[] points)
        {
            for (int i = 1; i < points.Length - 1; i++) Triangle(points[0], points[i], points[i + 1], color);
        }
        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Vector2 M(Vector2 p) => new Vector2(-p.x, p.y); // mirror across the centre line

        static Color RingColor(BossPassiveState.Kind kind)
        {
            switch (kind)
            {
                case BossPassiveState.Kind.Demon: return C(1f,0.42f,0.15f);
                case BossPassiveState.Kind.Fish: return C(0.3f,0.85f,1f);
                case BossPassiveState.Kind.Dragon: return C(0.25f,0.8f,0.45f);
                case BossPassiveState.Kind.Jellyfish: return C(0.95f,0.35f,0.72f);
                case BossPassiveState.Kind.Golem: return C(0.35f,0.8f,1f);
                case BossPassiveState.Kind.Wraith: return C(0.7f,0.45f,1f);
                case BossPassiveState.Kind.Sentinel: return C(1f,0.72f,0.2f);
                case BossPassiveState.Kind.Serpent: return C(0.25f,0.55f,1f);
                case BossPassiveState.Kind.Hydra: return C(0.5f,0.86f,0.4f);
                case BossPassiveState.Kind.AbyssalKing: return C(0.95f,0.74f,0.3f);
                default: return Color.white;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh=vh; tris.Clear(); var rect=rectTransform.rect; center=rect.center; unit=Mathf.Min(rect.width,rect.height)*.49f;
            Color ring=RingColor(Kind);
            Disc(0,0,1,1,ring); Disc(0,0,.89f,.89f,ink); // ring ~2 pixels thick on the grid
            switch(Kind)
            {
                case BossPassiveState.Kind.Demon: Demon(); break;
                case BossPassiveState.Kind.Fish: Fish(); break;
                case BossPassiveState.Kind.Dragon: Dragon(); break;
                case BossPassiveState.Kind.Jellyfish: Jellyfish(); break;
                case BossPassiveState.Kind.Golem: Golem(); break;
                case BossPassiveState.Kind.Wraith: Wraith(); break;
                case BossPassiveState.Kind.Sentinel: Sentinel(); break;
                case BossPassiveState.Kind.Serpent: Serpent(); break;
                case BossPassiveState.Kind.Hydra: Hydra(); break;
                case BossPassiveState.Kind.AbyssalKing: AbyssalCrown(); break;
                default: Disc(0,0,.35f,.35f,ring); break;
            }
            Rasterize();
        }

        // Red Boy: square red face, flame hair, one bone horn, angry yellow eyes, fangs, gold collar.
        void Demon()
        {
            Color red=C(0.86f,0.2f,0.13f), redDark=C(0.58f,0.1f,0.08f), flame=C(1f,0.48f,0.12f), flameHi=C(1f,0.78f,0.28f), horn=C(0.95f,0.93f,0.86f), hornGrey=C(0.62f,0.62f,0.66f), eye=C(1f,0.84f,0.22f), black=C(0.06f,0.03f,0.04f), gold=C(0.88f,0.64f,0.2f), fang=C(0.97f,0.96f,0.9f);
            Triangle(V(-.4f,.26f),V(-.34f,.62f),V(-.18f,.32f),flame);
            Triangle(V(-.18f,.3f),V(-.04f,.78f),V(.1f,.32f),flameHi);
            Triangle(V(.06f,.3f),V(.2f,.66f),V(.34f,.28f),flame);
            Triangle(V(-.34f,.22f),V(-.56f,.46f),V(-.28f,.36f),hornGrey);
            Poly(horn,V(.3f,.16f),V(.54f,.22f),V(.6f,.46f),V(.42f,.36f));
            Poly(red,V(-.38f,.34f),V(.38f,.34f),V(.4f,-.3f),V(-.4f,-.3f));
            Poly(redDark,V(-.4f,-.18f),V(.4f,-.18f),V(.4f,-.3f),V(-.4f,-.3f));
            Bar(V(-.32f,.2f),V(-.08f,.1f),.08f,black);
            Bar(M(V(-.32f,.2f)),M(V(-.08f,.1f)),.08f,black);
            Poly(eye,V(-.3f,.08f),V(-.1f,.03f),V(-.12f,-.08f),V(-.3f,-.06f));
            Poly(eye,M(V(-.3f,.08f)),M(V(-.3f,-.06f)),M(V(-.12f,-.08f)),M(V(-.1f,.03f)));
            Bar(V(-.18f,-.2f),V(.18f,-.2f),.08f,black);
            Triangle(V(-.14f,-.16f),V(-.06f,-.16f),V(-.1f,-.27f),fang);
            Triangle(V(.06f,-.16f),V(.14f,-.16f),V(.1f,-.27f),fang);
            Bar(V(-.42f,-.4f),V(.42f,-.4f),.12f,gold);
        }

        // Giant Fish: teal shark head, a mouth full of square teeth, yellow eyes, cyan side fins.
        void Fish()
        {
            Color teal=C(0.15f,0.4f,0.44f), tealDark=C(0.08f,0.24f,0.28f), fin=C(0.35f,0.86f,1f), tooth=C(0.96f,0.95f,0.88f), mouth=C(0.07f,0.04f,0.07f), eye=C(1f,0.74f,0.15f);
            Triangle(V(-.44f,-.02f),V(-.82f,.16f),V(-.54f,-.34f),fin);
            Triangle(M(V(-.44f,-.02f)),M(V(-.82f,.16f)),M(V(-.54f,-.34f)),fin);
            Triangle(V(-.14f,.4f),V(.04f,.74f),V(.18f,.4f),tealDark);
            Arc(0,.26f,.5f,.28f,0,Mathf.PI,teal);
            Poly(teal,V(-.5f,.27f),V(.5f,.27f),V(.48f,-.42f),V(-.48f,-.42f));
            Poly(mouth,V(-.4f,.06f),V(.4f,.06f),V(.32f,-.34f),V(-.32f,-.34f));
            Poly(tooth,V(-.38f,.06f),V(-.26f,.06f),V(-.27f,-.07f),V(-.37f,-.07f));
            Poly(tooth,V(-.21f,.06f),V(-.09f,.06f),V(-.1f,-.09f),V(-.2f,-.09f));
            Poly(tooth,V(-.04f,.06f),V(.08f,.06f),V(.07f,-.09f),V(-.03f,-.09f));
            Poly(tooth,V(.13f,.06f),V(.25f,.06f),V(.24f,-.09f),V(.14f,-.09f));
            Poly(tooth,V(.3f,.06f),V(.4f,.06f),V(.38f,-.07f),V(.3f,-.07f));
            Poly(tooth,V(-.28f,-.34f),V(-.17f,-.34f),V(-.18f,-.22f),V(-.27f,-.22f));
            Poly(tooth,V(-.1f,-.34f),V(.01f,-.34f),V(0,-.2f),V(-.09f,-.2f));
            Poly(tooth,V(.08f,-.34f),V(.19f,-.34f),V(.18f,-.22f),V(.09f,-.22f));
            Disc(-.32f,.27f,.08f,.07f,eye);
            Disc(.32f,.27f,.08f,.07f,eye);
            Disc(-.32f,.26f,.035f,.045f,mouth);
            Disc(.32f,.26f,.035f,.045f,mouth);
        }

        // Jade dragon: green head with bone horns on spread jade wings, cream chest bands.
        void Dragon()
        {
            Color jade=C(0.16f,0.56f,0.33f), jadeLight=C(0.36f,0.84f,0.5f), membrane=C(0.09f,0.34f,0.2f), black=C(0.1f,0.22f,0.14f), bone=C(0.9f,0.86f,0.7f), eye=C(0.45f,1f,0.55f);
            Triangle(V(-.12f,.14f),V(-.84f,.48f),V(-.6f,-.32f),jade);
            Triangle(M(V(-.12f,.14f)),M(V(-.84f,.48f)),M(V(-.6f,-.32f)),jade);
            Triangle(V(-.12f,.06f),V(-.6f,-.32f),V(-.34f,-.24f),membrane);
            Triangle(M(V(-.12f,.06f)),M(V(-.6f,-.32f)),M(V(-.34f,-.24f)),membrane);
            Bar(V(-.12f,.14f),V(-.84f,.48f),.05f,black);
            Bar(M(V(-.12f,.14f)),M(V(-.84f,.48f)),.05f,black);
            Triangle(V(-.84f,.48f),V(-.7f,.34f),V(-.76f,.62f),jadeLight);
            Triangle(M(V(-.84f,.48f)),M(V(-.7f,.34f)),M(V(-.76f,.62f)),jadeLight);
            Bar(V(0,.2f),V(0,-.5f),.28f,black);
            Bar(V(-.1f,-.12f),V(.1f,-.12f),.05f,bone);
            Bar(V(-.1f,-.23f),V(.1f,-.23f),.05f,bone);
            Bar(V(-.09f,-.34f),V(.09f,-.34f),.05f,bone);
            Triangle(V(-.08f,-.46f),V(.08f,-.46f),V(.02f,-.78f),black);
            Triangle(V(-.1f,.44f),V(-.28f,.74f),V(-.02f,.5f),bone);
            Triangle(M(V(-.1f,.44f)),M(V(-.28f,.74f)),M(V(-.02f,.5f)),bone);
            Disc(0,.34f,.2f,.2f,jadeLight);
            Poly(jadeLight,V(-.12f,.28f),V(.12f,.28f),V(.09f,.1f),V(-.09f,.1f));
            Bar(V(-.06f,.16f),V(.06f,.16f),.04f,black);
            Disc(-.08f,.37f,.045f,.035f,ink);
            Disc(.08f,.37f,.045f,.035f,ink);
            Disc(-.08f,.38f,.03f,.025f,eye);
            Disc(.08f,.38f,.03f,.025f,eye);
        }

        // Magenta dome with pink spots, purple tentacles ending in cyan tips.
        void Jellyfish()
        {
            Color dome=C(0.78f,0.22f,0.55f), domeLight=C(0.96f,0.48f,0.76f), rim=C(0.6f,0.15f,0.44f), arms=C(0.56f,0.26f,0.62f), tip=C(0.35f,0.82f,1f);
            Bar(V(-0.36f,.02f),V(-0.31f,-.3f),.11f,arms);
            Bar(V(-0.31f,-.3f),V(-0.38f,-.52f),.1f,arms);
            Bar(V(-0.38f,-.52f),V(-0.34f,-.7f),.1f,tip);
            Bar(V(-0.12f,.02f),V(-0.07f,-.3f),.11f,arms);
            Bar(V(-0.07f,-.3f),V(-0.14f,-.52f),.1f,arms);
            Bar(V(-0.14f,-.52f),V(-0.10f,-.7f),.1f,tip);
            Bar(V(0.12f,.02f),V(0.17f,-.3f),.11f,arms);
            Bar(V(0.17f,-.3f),V(0.10f,-.52f),.1f,arms);
            Bar(V(0.10f,-.52f),V(0.14f,-.7f),.1f,tip);
            Bar(V(0.36f,.02f),V(0.41f,-.3f),.11f,arms);
            Bar(V(0.41f,-.3f),V(0.34f,-.52f),.1f,arms);
            Bar(V(0.34f,-.52f),V(0.38f,-.7f),.1f,tip);
            Arc(0,.02f,.64f,.62f,0,Mathf.PI,dome);
            Bar(V(-.62f,.04f),V(.62f,.04f),.09f,rim);
            Disc(-.24f,.38f,.09f,.07f,domeLight);
            Disc(.08f,.5f,.08f,.06f,domeLight);
            Disc(.32f,.28f,.08f,.07f,domeLight);
            Disc(-.44f,.18f,.06f,.05f,domeLight);
            Disc(.04f,.24f,.06f,.05f,domeLight);
        }

        // Crystal Golem (Land): stone block with a moss top, blue crystal spikes, glowing eyes and core.
        void Golem()
        {
            Color stone=C(0.44f,0.4f,0.34f), stoneDark=C(0.27f,0.24f,0.21f), moss=C(0.34f,0.56f,0.22f), crystal=C(0.3f,0.76f,1f), glow=C(0.7f,0.96f,1f);
            Triangle(V(-.46f,.36f),V(-.38f,.72f),V(-.26f,.38f),crystal);
            Triangle(V(-.1f,.4f),V(0,.8f),V(.1f,.4f),crystal);
            Triangle(V(.26f,.38f),V(.38f,.72f),V(.46f,.36f),crystal);
            Poly(stone,V(-.5f,.42f),V(.5f,.42f),V(.58f,.3f),V(.58f,-.44f),V(.48f,-.56f),V(-.48f,-.56f),V(-.58f,-.44f),V(-.58f,.3f));
            Bar(V(-.52f,.38f),V(.52f,.38f),.11f,moss);
            Bar(V(-.36f,.0f),V(-.24f,-.22f),.06f,stoneDark);
            Bar(V(.4f,-.18f),V(.28f,-.4f),.06f,stoneDark);
            Bar(V(-.44f,-.36f),V(-.3f,-.46f),.05f,stoneDark);
            Poly(crystal,V(-.32f,.22f),V(-.1f,.22f),V(-.1f,.08f),V(-.32f,.08f));
            Poly(crystal,M(V(-.32f,.22f)),M(V(-.32f,.08f)),M(V(-.1f,.08f)),M(V(-.1f,.22f)));
            Poly(glow,V(-.26f,.18f),V(-.16f,.18f),V(-.16f,.12f),V(-.26f,.12f));
            Poly(glow,M(V(-.26f,.18f)),M(V(-.26f,.12f)),M(V(-.16f,.12f)),M(V(-.16f,.18f)));
            Poly(crystal,V(0,-.04f),V(.14f,-.21f),V(0,-.4f),V(-.14f,-.21f));
            Poly(glow,V(0,-.12f),V(.06f,-.21f),V(0,-.3f),V(-.06f,-.21f));
        }

        // Void Wraith: bent dark hood, one white eye, glowing violet ribs, grey claws.
        void Wraith()
        {
            Color hood=C(0.21f,0.2f,0.27f), hoodDark=C(0.12f,0.11f,0.16f), violet=C(0.64f,0.32f,1f), white=C(0.96f,0.96f,1f), claw=C(0.58f,0.58f,0.72f), hole=C(0.05f,0.04f,0.08f);
            Bar(V(-.36f,.14f),V(-.62f,-.2f),.12f,hood);
            Bar(M(V(-.36f,.14f)),M(V(-.62f,-.2f)),.12f,hood);
            Bar(V(-.62f,-.2f),V(-.74f,-.5f),.045f,claw);
            Bar(V(-.62f,-.2f),V(-.64f,-.54f),.045f,claw);
            Bar(V(-.62f,-.2f),V(-.54f,-.5f),.045f,claw);
            Bar(M(V(-.62f,-.2f)),M(V(-.74f,-.5f)),.045f,claw);
            Bar(M(V(-.62f,-.2f)),M(V(-.64f,-.54f)),.045f,claw);
            Bar(M(V(-.62f,-.2f)),M(V(-.54f,-.5f)),.045f,claw);
            Triangle(V(-.3f,-.46f),V(.3f,-.46f),V(.06f,-.8f),hoodDark);
            Poly(hood,V(-.4f,.22f),V(.4f,.22f),V(.3f,-.5f),V(-.3f,-.5f));
            Poly(hood,V(-.34f,.18f),V(-.2f,.56f),V(.04f,.82f),V(.24f,.54f),V(.34f,.18f));
            Disc(0,.3f,.17f,.16f,hole);
            Disc(.04f,.32f,.07f,.08f,white);
            Disc(.04f,.31f,.03f,.035f,hole);
            Bar(V(0,.08f),V(0,-.34f),.07f,violet);
            Bar(V(-.2f,.03f),V(.2f,.03f),.065f,violet);
            Bar(V(-.17f,-.09f),V(.17f,-.09f),.065f,violet);
            Bar(V(-.13f,-.21f),V(.13f,-.21f),.065f,violet);
            Bar(V(-.32f,.1f),V(-.44f,-.02f),.06f,violet);
            Bar(M(V(-.32f,.1f)),M(V(-.44f,-.02f)),.06f,violet);
        }

        // Silver robot: gold shoulder pods with orange lights, black chest plate, orange sun core.
        void Sentinel()
        {
            Color silver=C(0.74f,0.76f,0.79f), steel=C(0.36f,0.38f,0.42f), black=C(0.1f,0.1f,0.12f), gold=C(1f,0.7f,0.18f), core=C(1f,0.58f,0.14f), coreHi=C(1f,0.86f,0.42f), light=C(1f,0.62f,0.12f);
            Poly(gold,V(-.84f,.48f),V(-.44f,.48f),V(-.44f,.06f),V(-.84f,.06f));
            Poly(gold,M(V(-.84f,.48f)),M(V(-.84f,.06f)),M(V(-.44f,.06f)),M(V(-.44f,.48f)));
            Poly(black,V(-.78f,.42f),V(-.5f,.42f),V(-.5f,.12f),V(-.78f,.12f));
            Poly(black,M(V(-.78f,.42f)),M(V(-.78f,.12f)),M(V(-.5f,.12f)),M(V(-.5f,.42f)));
            Disc(-.71f,.34f,.05f,.05f,light);
            Disc(-.57f,.34f,.05f,.05f,light);
            Disc(-.71f,.2f,.05f,.05f,light);
            Disc(-.57f,.2f,.05f,.05f,light);
            Disc(.71f,.34f,.05f,.05f,light);
            Disc(.57f,.34f,.05f,.05f,light);
            Disc(.71f,.2f,.05f,.05f,light);
            Disc(.57f,.2f,.05f,.05f,light);
            Poly(silver,V(-.46f,.42f),V(.46f,.42f),V(.46f,-.52f),V(-.46f,-.52f));
            Poly(steel,V(-.17f,.64f),V(.17f,.64f),V(.17f,.42f),V(-.17f,.42f));
            Bar(V(-.12f,.53f),V(.12f,.53f),.07f,light);
            Bar(V(0,.64f),V(0,.78f),.07f,gold);
            Poly(black,V(-.32f,.32f),V(.32f,.32f),V(.32f,-.22f),V(-.32f,-.22f));
            Bar(V(-.46f,-.32f),V(.46f,-.32f),.09f,gold);
            Disc(0,.05f,.17f,.17f,core);
            Disc(0,.05f,.08f,.08f,coreHi);
        }

        // Sea Serpent: deep-blue fanged head with cyan fins over a blue coil with teal spots.
        void Serpent()
        {
            Color blue=C(0.13f,0.23f,0.64f), blueDark=C(0.07f,0.12f,0.38f), teal=C(0.3f,0.86f,0.8f), fin=C(0.36f,0.9f,0.95f), tooth=C(0.97f,0.96f,0.9f), eye=C(1f,0.78f,0.2f);
            Disc(.05f,-.4f,.5f,.28f,blue);
            Disc(.05f,-.38f,.26f,.12f,blueDark);
            Disc(-.3f,-.44f,.07f,.05f,teal);
            Disc(.38f,-.36f,.07f,.05f,teal);
            Disc(.08f,-.62f,.07f,.04f,teal);
            Disc(-.12f,-.24f,.05f,.04f,teal);
            Bar(V(-.12f,-.24f),V(-.04f,.2f),.3f,blue);
            Triangle(V(-.34f,.4f),V(-.68f,.68f),V(-.42f,.16f),fin);
            Triangle(V(.28f,.42f),V(.62f,.7f),V(.34f,.18f),fin);
            Triangle(V(-.12f,.5f),V(0,.78f),V(.12f,.5f),teal);
            Poly(blue,V(-.36f,.52f),V(.3f,.52f),V(.38f,.12f),V(-.38f,.1f));
            Poly(blueDark,V(-.38f,.24f),V(.38f,.24f),V(.38f,.12f),V(-.38f,.1f));
            Bar(V(-.32f,.18f),V(.3f,.18f),.07f,ink);
            Triangle(V(-.24f,.21f),V(-.14f,.21f),V(-.19f,.08f),tooth);
            Triangle(V(.08f,.21f),V(.18f,.21f),V(.13f,.08f),tooth);
            Disc(.16f,.38f,.07f,.06f,eye);
            Disc(-.18f,.38f,.07f,.06f,eye);
            Disc(.17f,.37f,.03f,.035f,ink);
            Disc(-.17f,.37f,.03f,.035f,ink);
        }

        // Three purple heads with green spikes on a purple body.
        void Hydra()
        {
            Color purple=C(0.36f,0.18f,0.62f), purpleLight=C(0.54f,0.32f,0.86f), black=C(0.2f,0.09f,0.34f), spike=C(0.32f,0.82f,0.36f), eye=C(0.78f,1f,0.4f);
            Poly(purple,V(-.5f,-.12f),V(.5f,-.12f),V(.42f,-.66f),V(-.42f,-.66f));
            Bar(V(-.2f,-.2f),V(-.24f,-.6f),.06f,black);
            Bar(V(.12f,-.2f),V(.16f,-.6f),.06f,black);
            Bar(V(-.36f,-.4f),V(.36f,-.4f),.05f,purpleLight);
            Bar(V(-0.16f,-.16f),V(-0.42f,0.12f),.15f,black);
            Triangle(V(-0.56f,0.26f),V(-0.52f,0.48f),V(-0.44f,0.30f),spike);
            Triangle(V(-0.40f,0.30f),V(-0.32f,0.48f),V(-0.28f,0.26f),spike);
            Disc(-0.42f,0.20f,.18f,.14f,purple);
            Poly(purpleLight,V(-0.52f,0.18f),V(-0.32f,0.18f),V(-0.34f,0.06f),V(-0.50f,0.06f));
            Disc(-0.49f,0.24f,.035f,.03f,eye);
            Disc(-0.35f,0.24f,.035f,.03f,eye);
            Bar(V(0.00f,-.16f),V(0.00f,0.34f),.15f,black);
            Triangle(V(-0.14f,0.48f),V(-0.10f,0.70f),V(-0.02f,0.52f),spike);
            Triangle(V(0.02f,0.52f),V(0.10f,0.70f),V(0.14f,0.48f),spike);
            Disc(0.00f,0.42f,.18f,.14f,purple);
            Poly(purpleLight,V(-0.10f,0.40f),V(0.10f,0.40f),V(0.08f,0.28f),V(-0.08f,0.28f));
            Disc(-0.07f,0.46f,.035f,.03f,eye);
            Disc(0.07f,0.46f,.035f,.03f,eye);
            Bar(V(0.16f,-.16f),V(0.42f,0.12f),.15f,black);
            Triangle(V(0.28f,0.26f),V(0.32f,0.48f),V(0.40f,0.30f),spike);
            Triangle(V(0.44f,0.30f),V(0.52f,0.48f),V(0.56f,0.26f),spike);
            Disc(0.42f,0.20f,.18f,.14f,purple);
            Poly(purpleLight,V(0.32f,0.18f),V(0.52f,0.18f),V(0.50f,0.06f),V(0.34f,0.06f));
            Disc(0.35f,0.24f,.035f,.03f,eye);
            Disc(0.49f,0.24f,.035f,.03f,eye);
        }

        // Abyssal King: white skull with green eyes under a gold crown and halo rays, blue robe.
        void AbyssalCrown()
        {
            Color gold=C(0.94f,0.74f,0.28f), halo=C(0.62f,0.46f,0.2f), blue=C(0.2f,0.26f,0.66f), skull=C(0.96f,0.96f,0.92f), socket=C(0.08f,0.07f,0.14f), eye=C(0.3f,1f,0.5f);
            Bar(V(0.44f,0.24f),V(0.77f,0.33f),.07f,halo);
            Bar(V(0.35f,0.42f),V(0.61f,0.63f),.07f,halo);
            Bar(V(0.19f,0.54f),V(0.34f,0.85f),.07f,halo);
            Bar(V(0.00f,0.58f),V(0.00f,0.92f),.07f,halo);
            Bar(V(-0.19f,0.54f),V(-0.34f,0.85f),.07f,halo);
            Bar(V(-0.35f,0.42f),V(-0.61f,0.63f),.07f,halo);
            Bar(V(-0.44f,0.24f),V(-0.77f,0.33f),.07f,halo);
            Poly(blue,V(-.54f,-.18f),V(.54f,-.18f),V(.42f,-.62f),V(-.42f,-.62f));
            Poly(socket,V(-.12f,-.18f),V(.12f,-.18f),V(0,-.5f));
            Poly(skull,V(-.27f,.26f),V(.27f,.26f),V(.27f,-.1f),V(-.27f,-.1f));
            Poly(skull,V(-.19f,-.1f),V(.19f,-.1f),V(.19f,-.26f),V(-.19f,-.26f));
            Poly(socket,V(-.21f,.12f),V(-.05f,.12f),V(-.05f,-.02f),V(-.21f,-.02f));
            Poly(socket,M(V(-.21f,.12f)),M(V(-.21f,-.02f)),M(V(-.05f,-.02f)),M(V(-.05f,.12f)));
            Disc(-.13f,.05f,.04f,.04f,eye);
            Disc(.13f,.05f,.04f,.04f,eye);
            Triangle(V(-.04f,-.04f),V(.04f,-.04f),V(0,-.1f),socket);
            Bar(V(-.1f,-.15f),V(-.1f,-.25f),.03f,socket);
            Bar(V(0,-.15f),V(0,-.25f),.03f,socket);
            Bar(V(.1f,-.15f),V(.1f,-.25f),.03f,socket);
            Triangle(V(-0.35f,.3f),V(-0.21f,.3f),V(-0.28f,0.54f),gold);
            Triangle(V(-0.21f,.3f),V(-0.07f,.3f),V(-0.14f,0.46f),gold);
            Triangle(V(-0.07f,.3f),V(0.07f,.3f),V(0.00f,0.66f),gold);
            Triangle(V(0.07f,.3f),V(0.21f,.3f),V(0.14f,0.46f),gold);
            Triangle(V(0.21f,.3f),V(0.35f,.3f),V(0.28f,0.54f),gold);
            Bar(V(-.32f,.3f),V(.32f,.3f),.1f,gold);
        }
    }
}
