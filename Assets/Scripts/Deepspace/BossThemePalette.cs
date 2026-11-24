using UnityEngine;
namespace Deepspace
{
    // sRGB art palettes; baked vertex colors are stored in linear space.
    public static class BossThemePalette
    {
        public static Color Hex(int value) => new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,1);
        public static Color Accent(string name)
        {
            switch(BossPassiveState.Identify(name))
            {
                case BossPassiveState.Kind.Demon:return Hex(0xFF442B);
                case BossPassiveState.Kind.Fish:return Hex(0x27C5ED);
                case BossPassiveState.Kind.Dragon:return Hex(0xFF742B);
                case BossPassiveState.Kind.Jellyfish:return Hex(0x66CFFF);
                case BossPassiveState.Kind.Golem:return Hex(0x59E5F2);
                case BossPassiveState.Kind.Wraith:return Hex(0xAD6CFF);
                case BossPassiveState.Kind.Sentinel:return Hex(0xF4E7C8);
                case BossPassiveState.Kind.Serpent:return Hex(0x31DFC0);
                case BossPassiveState.Kind.Hydra:return Hex(0x83DE46);
                case BossPassiveState.Kind.AbyssalKing:return Hex(0xB65CFF);
                default:return Color.white;
            }
        }

        // บาส 2026-09-29: per-boss UI theme (chrome only: frames, plates, glows, titles). Semantic colours
        // (gems, stun/ultimate fill, HP fills, damage numbers) never come from here. Hues are lifted from the
        // vertex palettes below; the four cyan-ish bosses are split by secondary: Fish=pearl, Jelly=violet,
        // Golem=granite, Serpent=amber. Sentinel uses its gold trim, not the near-white accent.
        public struct BossUiTheme
        {
            public Color primary,secondary,glow,panelDark,textLight;public string epithet;
            public BossUiTheme(int p,int s,int g,int panel,int text,string title)
            {primary=Hex(p);secondary=Hex(s);glow=Hex(g);panelDark=Hex(panel);textLight=Hex(text);epithet=title;}
            public static BossUiTheme Lerp(BossUiTheme a,BossUiTheme b,float t)
            {
                a.primary=Color.Lerp(a.primary,b.primary,t);a.secondary=Color.Lerp(a.secondary,b.secondary,t);a.glow=Color.Lerp(a.glow,b.glow,t);
                a.panelDark=Color.Lerp(a.panelDark,b.panelDark,t);a.textLight=Color.Lerp(a.textLight,b.textLight,t);if(t>=.5f)a.epithet=b.epithet;return a;
            }
        }
        public static BossUiTheme UiTheme(string name)=>UiTheme(BossPassiveState.Identify(name));
        public static BossUiTheme UiTheme(BossPassiveState.Kind kind,bool phase2=false)
        {
            switch(kind)
            {
                case BossPassiveState.Kind.Demon:return phase2?new BossUiTheme(0xF0283C,0xF1C35A,0xFF3B2F,0x2A0810,0xFFE3C8,"YAKSHA KING"):new BossUiTheme(0xFF442B,0xC7A24E,0xFF6A3D,0x27121E,0xFFDCD0,"RED BOY");
                case BossPassiveState.Kind.Fish:return new BossUiTheme(0x2C9BE0,0xE3F8FF,0x27C5ED,0x071727,0xD9F2FF,"LORD OF THE TIDE");
                case BossPassiveState.Kind.Dragon:return phase2?new BossUiTheme(0xFFB020,0xFF4A1A,0xFFC845,0x3A1206,0xFFF0C8,"INFERNO"):new BossUiTheme(0xFF742B,0xC7AC86,0xFF812D,0x283039,0xFFE2CC,"SCORCHED SKY");
                case BossPassiveState.Kind.Jellyfish:return new BossUiTheme(0x66CFFF,0xC58BFF,0x9A8CFF,0x0B1232,0xE2F5FF,"PULSE OF THE ABYSS");
                case BossPassiveState.Kind.Golem:return phase2?new BossUiTheme(0xFF3B5C,0xFFB0C0,0xFF2A4A,0x2A0A14,0xFFE0E6,"CRIMSON CRYSTAL"):new BossUiTheme(0x59E5F2,0xA8B8D0,0xC4F8FF,0x162232,0xDDFCFF,"CRYSTAL COLOSSUS");
                case BossPassiveState.Kind.Wraith:return new BossUiTheme(0xAD6CFF,0xFF7AD9,0xB585EB,0x160E24,0xEEDFFF,"HERALD OF THE VOID");
                case BossPassiveState.Kind.Sentinel:return new BossUiTheme(0xFFC04A,0xDCE1E5,0xFFE098,0x1E232B,0xFFF6DC,"ADAPTIVE GUARDIAN");
                case BossPassiveState.Kind.Serpent:return new BossUiTheme(0x31DFC0,0xFFBF48,0x54BCAA,0x0E2A30,0xD9F8EE,"STORM TIDE");
                case BossPassiveState.Kind.Hydra:return new BossUiTheme(0x83DE46,0xD9F27A,0x5CFF6A,0x0E2412,0xE6FFD6,"THE SEVENFOLD");
                case BossPassiveState.Kind.AbyssalKing:return phase2?new BossUiTheme(0xFF4FD2,0xFFC24A,0xFF6BE6,0x22051C,0xFFE6F8,"THE CROWN IGNITES"):new BossUiTheme(0x9B4CFF,0xF2B94A,0xC77BFF,0x10061C,0xF0E2FF,"SOVEREIGN OF THE ABYSS");
                default:return new BossUiTheme(0x1F80C7,0x5AC8FF,0x59DEFF,0x04060F,0xE6F4FF,"");
            }
        }
        public static bool HasPhase2Theme(BossPassiveState.Kind kind)=>kind==BossPassiveState.Kind.Demon||kind==BossPassiveState.Kind.Dragon||kind==BossPassiveState.Kind.Golem||kind==BossPassiveState.Kind.AbyssalKing;
        // Shared soft sprites, generated once: halo = solid centre fading out at the edge; edge = bright rim fading inward (use fillCenter=false).
        static Sprite halo,edge;
        public static Sprite SoftHalo=>halo!=null?halo:halo=SoftSprite(false);
        public static Sprite SoftEdge=>edge!=null?edge:edge=SoftSprite(true);
        static Sprite SoftSprite(bool rim)
        {
            const int size=64,border=31;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name=rim?"UI Theme Edge":"UI Theme Halo"};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=Mathf.Min(Mathf.Min(x,size-1-x),Mathf.Min(y,size-1-y))/(float)border;d=Mathf.Clamp01(d);
                float a=rim?Mathf.Pow(1-d,2.2f):Mathf.SmoothStep(0,1,d);
                pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
        }

        static readonly int[] DemonSource={0xb7463f,0x882f36,0xd86653,0xbd5555,0x2b151f,0xc3b6a0,0xd8d5ce,0xb39a58};
        static readonly int[] DemonTarget={0xc73c37,0x8e2132,0xf16445,0xd4454e,0x27121e,0xd7c8b1,0xe9e1da,0xc7a24e};
        static readonly int[] FishSource={0x237eaf,0x163f73,0x8fdae5,0x388fb6,0x53c7de,0xd0eff5,0xb8f6ff,0x071728,0x02080f,0xe2f8ff,0x247ba7};
        static readonly int[] FishTarget={0x197db8,0x143b73,0x99dbe7,0x289cb9,0x4ed9e5,0xd9edf0,0x92f4ff,0x071727,0x020811,0xe3f8ff,0x247fad};
        static readonly int[] DragonSource={0x11171c,0x273038,0x302923,0x211720,0x91836b,0xe96621};
        static readonly int[] DragonTarget={0x283039,0x4b4144,0x59423b,0x652b32,0xc7ac86,0xff812d};
        static readonly int[] GolemSource={0x23585e,0x397d80,0x62dfdc,0xc2ffff};
        static readonly int[] GolemTarget={0x263d56,0x42657d,0x55dce9,0xc4f8ff};
        static readonly int[] WraithSource={0x45266f,0x24113d,0x7553a6,0x10091f,0xa078d8,0xd3baff,0x39304f};
        static readonly int[] WraithTarget={0x4d237d,0x271338,0x8758b9,0x160e24,0xb585eb,0xe4ceff,0x493956};
        static readonly int[] SentinelSource={0x526f83,0x7998aa,0x202735,0x514458,0x94939e,0xffb530,0xffef97};
        static readonly int[] SentinelTarget={0xd4a43b,0xffe098,0x535c66,0xb8bcc0,0xdce1e5,0xe8d3a0,0xfffae7};
        static readonly int[] SerpentSource={0x756039,0x575733,0x994c29,0xc9c3a0,0x9f8b5c,0x302c24,0xe3d7ad,0xffa429,0x8d8c6b};
        static readonly int[] SerpentTarget={0x176b72,0x16474f,0x269e9c,0xa1d3c5,0x54bcaa,0x112f39,0xd9e8cd,0xffbf48,0x568f88};

        public static Color Recolor(BossPassiveState.Kind kind,Color source)
        {
            if(kind==BossPassiveState.Kind.Jellyfish)
            {
                var srgb=source.gamma;Color.RGBToHSV(srgb,out float h,out float s,out float v);
                if(v<.08f)return source;
                if(s>.25f && (h>.65f || h<.07f))
                {
                    var tint=Color.HSVToRGB(h<.07f?.53f:Mathf.Lerp(.61f,.73f,Mathf.InverseLerp(.70f,.88f,h)),s*.87f,Mathf.Min(1,v*1.03f)).linear;
                    tint.a=source.a;return tint;
                }
                return source;
            }
            int[] from=null,to=null;
            switch(kind)
            {
                case BossPassiveState.Kind.Demon:from=DemonSource;to=DemonTarget;break;
                case BossPassiveState.Kind.Fish:from=FishSource;to=FishTarget;break;
                case BossPassiveState.Kind.Dragon:from=DragonSource;to=DragonTarget;break;
                case BossPassiveState.Kind.Golem:from=GolemSource;to=GolemTarget;break;
                case BossPassiveState.Kind.Wraith:from=WraithSource;to=WraithTarget;break;
                case BossPassiveState.Kind.Sentinel:from=SentinelSource;to=SentinelTarget;break;
                case BossPassiveState.Kind.Serpent:from=SerpentSource;to=SerpentTarget;break;
            }
            if(from==null)return source;
            var rgb=new Vector3(source.r,source.g,source.b);float best=float.MaxValue,bestShade=1;int match=-1;
            for(int i=0;i<from.Length;i++)
            {
                var c=Hex(from[i]).linear;var target=new Vector3(c.r,c.g,c.b);
                float shade=Mathf.Clamp(Vector3.Dot(rgb,target)/Mathf.Max(.000001f,target.sqrMagnitude),.85f,1.18f);
                float error=(rgb-target*shade).sqrMagnitude/Mathf.Max(.000001f,rgb.sqrMagnitude);
                if(error<best){best=error;match=i;bestShade=shade;}
            }
            if(best>.018f)return source; // Preserve unmapped anatomy/detail colors.
            var result=Hex(to[match]).linear*bestShade;result.a=source.a;return result;
        }
    }
}
