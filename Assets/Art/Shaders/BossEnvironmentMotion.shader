Shader "Deepspace/BossEnvironmentMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Theme ("Sky / Water / Hell", Float) = 0
        _Floor ("Floor display", Float) = 0
        _Strength ("Atmosphere", Range(0,1)) = 0.85
        _Aspect ("Display aspect", Float) = 1
        _MotionTime ("Continuous time", Float) = 0
        _UsePreviewTime ("Use fixed preview time", Float) = 0
        _Preview ("Preview vertex color", Float) = 0
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use alpha clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _MainTex_ST, _MainTex_TexelSize, _Color, _ClipRect, _TextureSampleAdd;
            float _Theme, _Floor, _Strength, _Aspect, _MotionTime, _Preview, _UsePreviewTime;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.color=lerp(v.color,float4(1,1,1,1),_Preview)*_Color;
                return o;
            }
            float hash21(float2 p)
            {
                float3 q=frac(float3(p.x,p.y,p.x)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float cloud(float2 p)
            {
                float n=noise(p)*.57;
                p=mul(float2x2(.8,-.6,.6,.8),p)*2.07+8.3;
                n+=noise(p)*.28;
                p=mul(float2x2(.8,-.6,.6,.8),p)*2.03+2.7;
                return n+noise(p)*.15;
            }
            float motes(float2 p, float t, float density, float speed, float seed)
            {
                p=p*density+float2(t*speed*.28,-t*speed)+seed;
                p.x+=sin(p.y*.35+t*.12)*.18;
                float2 id=floor(p), q=frac(p)-.5;
                float h=hash21(id+seed);
                float2 center=float2(hash21(id+13),hash21(id+39))*.52-.26;
                float r=lerp(.035,.12,h);
                float d=length(q-center);
                float core=1-smoothstep(r*.35,r,d);
                float halo=exp(-d*d/(r*r*7))*.16;
                return (core+halo)*smoothstep(.66,.91,h)*(.72+.28*sin(t*.7+h*25));
            }
            float caustic(float2 p, float t)
            {
                t*=3.3;
                float bend=noise(p*.4+float2(t*.045,-t*.028))*2.2;
                float a=sin(p.x*1.4+bend+t*.24)+sin(p.y*1.6-bend-t*.19);
                float b=sin(p.x*.87-p.y*.5+t*.17)+sin(p.y*1.31+p.x*.43-t*.22);
                float ridge=pow(saturate(1-abs(a)*1.6),5);
                return ridge*(.42+.58*pow(saturate(1-abs(b)*.6),2));
            }
            float risingBubbles(float2 p, float t)
            {
                float2 grid=p*4.4+float2(sin(t*.24)*.24,-t*.65);
                float2 id=floor(grid), q=frac(grid)-.5;
                float h=hash21(id+81);
                q-=float2(hash21(id+12),hash21(id+32))*.34-.17;
                float radius=.075+h*.085;
                float rim=exp(-pow((length(q)-radius)*90,2));
                float2 glint=q-float2(-radius*.45,radius*.48);
                return (rim*.38+exp(-dot(glint,glint)*3800)*.80)*smoothstep(.66,.88,h);
            }
            float3 water(float2 uv, float2 p, float3 original, float t, float border)
            {
                float surface=smoothstep(.38,.94,uv.y);
                float openWater=smoothstep(.02,.13,original.b-original.r);
                float moving=lerp(surface*openWater,.48,_Floor);
                float2 flow=float2(noise(p*2.4+float2(t*.15,-t*.075)),noise(p*2.1+float2(-t*.11,t*.14)))-.5;
                float coral=smoothstep(.035,.17,original.r-original.g*.82);
                coral*=lerp((1-smoothstep(.30,.73,uv.y))*smoothstep(.1,.4,abs(uv.x-.5)),1,_Floor);
                float sway=sin(t*1.35+p.x*3+p.y*2)+sin(t*.81-p.x*2.1)*.32;
                float2 coralMotion=float2(sway*.007,sin(t*1.1+p.x*4)*.0018)*coral;
                float surfaceWave=sin(p.x*11-t*1.7+noise(p*3)*2)*surface*(1-_Floor);
                float2 refractUV=uv+(flow*float2(.017,.023)*moving+coralMotion+float2(0,surfaceWave*.007))*border*_Strength;
                float3 col=tex2D(_MainTex,refractUV).rgb;
                float lightPattern=smoothstep(.45,.82,max(original.g,original.b))*smoothstep(.025,.12,original.g-original.r);
                float phaseA=frac(t*.28), phaseB=frac(t*.28+.5);
                float2 current=float2(.016,-.028)*border;
                float3 lightA=tex2D(_MainTex,saturate(refractUV-current*phaseA)).rgb;
                float3 lightB=tex2D(_MainTex,saturate(refractUV-current*phaseB)).rgb;
                col=lerp(col,lerp(lightA,lightB,abs(phaseA*2-1)),lightPattern*.60);
                float rayX=(uv.x-.5)/max(.24,uv.y+.08);
                float beams=pow(noise(float2(rayX*21+t*.13,t*.16)),5);
                float broad=pow(noise(float2(rayX*8-t*.07,t*.12+8)),3);
                float depth=pow(saturate(uv.y),.65);
                float rays=(beams*.85+broad*.20)*depth*(1-_Floor);
                float haze=cloud(p*1.35+float2(t*.075,-t*.04));
                col=lerp(col,float3(.025,.30,.39),haze*.10*(1-_Floor)*(1-surface));
                col+=float3(.30,.88,1)*rays;
                float caustics=caustic(p*6,t)+caustic(p*9+9,-t*.63)*.35;
                float causticMask=lerp((1-smoothstep(.1,.6,uv.y))*.22,.72,_Floor);
                col+=float3(.27,.72,.78)*caustics*causticMask*.66;
                col*=1+(noise(p*3+float2(t*.11,t*.095))-.5)*.075;
                float particles=motes(p,t,13,.50,2)*.35+motes(p,t,7,.72,19)*.46+motes(p,t,3.8,.85,47)*.32;
                col+=float3(.32,.82,.94)*particles*lerp(1,.38,_Floor);
                col+=float3(.53,.91,1)*risingBubbles(p,t)*(1-_Floor);
                return col;
            }
            float4 driftingLeaves(float2 p, float t, float density, float seed)
            {
                // Cell-edge fading keeps each leaf continuous as the wind carries
                // it across the display. Coordinates use the display aspect ratio.
                float2 grid=p*density-float2(t*.48,-t*.19)+seed;
                float2 id=floor(grid), q=frac(grid)-.5;
                float h=hash21(id+seed);
                float visibility=smoothstep(.48,.82,h);
                q-=float2(sin(t*1.1+h*19),cos(t*.83+h*27))*.065;
                float angle=t*(.65+h*.65)+h*31;
                float s=sin(angle), c=cos(angle);
                float2 leaf=mul(float2x2(c,-s,s,c),q);
                float size=lerp(.075,.13,h);
                float tumble=.55+.45*abs(sin(t*1.5+h*17));
                float shape=pow(leaf.x/size,2)+abs(leaf.y)/(size*.40*tumble);
                float silhouette=1-smoothstep(.75,1,shape);
                float edge=min(min(frac(grid).x,1-frac(grid).x),min(frac(grid).y,1-frac(grid).y));
                float alpha=silhouette*visibility*smoothstep(0,.16,edge)*.78;
                float3 tint=lerp(float3(.23,.40,.055),float3(.65,.46,.10),hash21(id+seed+42));
                float vein=1-smoothstep(.003,.012,abs(leaf.y));
                tint*=.85+.25*tumble;
                tint+=float3(.20,.19,.055)*vein;
                return float4(tint,alpha);
            }
            float3 sky(float2 uv, float2 p, float3 original, float t, float border)
            {
                float luminance=dot(original,float3(.2126,.7152,.0722));
                float whiteness=1-saturate((max(original.r,max(original.g,original.b))-min(original.r,min(original.g,original.b)))*2);
                float cloudMask=smoothstep(.42,.78,luminance)*whiteness*smoothstep(.2,.65,uv.y)*(1-_Floor);
                float grassMask=smoothstep(.025,.15,original.g-max(original.r,original.b))*_Floor;
                float breeze=sin(t*.24)+sin(t*.11+2)*.35;
                float2 cloudDrift=float2(breeze*.024,sin(t*.16+p.x*.4)*.005)*cloudMask;
                float gust=.65+.35*noise(p*1.2-float2(t*.28,0));
                float2 grassDrift=float2(sin(p.x*7+p.y*5-t*1.9)*.011,cos(p.x*4-p.y*6+t*1.3)*.0035)*grassMask*gust;
                float3 col=tex2D(_MainTex,uv+(cloudDrift+grassDrift)*border*_Strength).rgb;
                // Translate the painted cloud bank continuously while keeping the
                // mountain band pinned. Blend two phases to hide texture resets.
                float cloudPhase=frac(t*.075), otherPhase=frac(t*.075+.5);
                float2 cloudTravel=float2(.16,.010)*border;
                float3 driftingA=tex2D(_MainTex,saturate(uv-cloudTravel*cloudPhase)).rgb;
                float3 driftingB=tex2D(_MainTex,saturate(uv-cloudTravel*otherPhase)).rgb;
                float skyBand=smoothstep(.23,.48,uv.y)*(1-_Floor);
                col=lerp(col,lerp(driftingA,driftingB,abs(cloudPhase*2-1)),skyBand);
                float shadow=cloud(p*1.5+float2(-t*.15,t*.022));
                col*=1-(shadow-.40)*lerp(.07,.42,_Floor);
                float cloudVeil=smoothstep(.49,.73,cloud(p*1.3+float2(-t*.065,7)));
                col=lerp(col,float3(.86,.93,1),cloudVeil*.065*smoothstep(.25,.8,uv.y)*(1-_Floor));
                float2 sun=uv-float2(.075,1.10);
                float shaftCoord=sun.x/max(.16,-sun.y);
                float shaft=pow(noise(float2(shaftCoord*12+t*.055,t*.10)),6);
                float sunFalloff=exp(-dot(sun*float2(.85,1),sun*float2(.85,1))*2.1);
                col+=float3(1,.87,.64)*(shaft*.24+sunFalloff*.035)*(1-_Floor);
                float horizon=exp(-pow((uv.y-.16)*12,2));
                col=lerp(col,float3(.59,.76,.84),horizon*shadow*.035*(1-_Floor));
                float particles=motes(p,t,8,.38,32)*.23+motes(p,t,4.4,.52,73)*.28;
                col+=float3(1,.91,.63)*particles*lerp(1,.32,_Floor);
                // Lift the shaded brickwork and sky without replacing the art
                // or adding a white veil over their existing details.
                // บาส 2026-10-02: much gentler lift (was pow .74/.80 x1.035 + .24 midtone boost) - it washed the deep
                // blue sky art into pale lavender on the wall. Keep a touch of brightness, keep the colour.
                float lift=lerp(.93,.94,_Floor);
                col=pow(max(col,float3(0,0,0)),float3(lift,lift,lift));
                // Brighten sky midtones a little while preserving cloud highlights.
                col+=saturate(col)*(1-saturate(col))*.06*(1-_Floor);
                if(_Floor>.5)
                {
                    float4 nearLeaves=driftingLeaves(p,t,7.5,24);
                    float4 farLeaves=driftingLeaves(p+float2(.19,.37),t*.72,12,67);
                    col=lerp(col,farLeaves.rgb,farLeaves.a*.65);
                    col=lerp(col,nearLeaves.rgb,nearLeaves.a);
                }
                return col;
            }
            float lavaRegion(float3 c)
            {
                return smoothstep(.16,.60,c.r)*smoothstep(.08,.38,c.r-c.g)*smoothstep(.006,.065,c.g);
            }
            float boilingLava(float2 p, float t, float density, float seed, out float sparks)
            {
                float2 grid=p*density;
                float2 id=floor(grid);
                float h=hash21(id+seed);
                float2 center=float2(hash21(id+seed+19),hash21(id+seed+53))*.42+.29;
                float2 centerUV=(id+center)/(density*float2(max(.25,_Aspect),1));
                float sourceMask=lavaRegion(tex2D(_MainTex,saturate(centerUV)).rgb);
                float age=frac(t/(1.5+h*1.9)+h*7);
                float2 q=frac(grid)-center;
                float d=length(q);
                float radius=.045+.21*smoothstep(0,.70,age);
                float alive=smoothstep(0,.15,age)*(1-smoothstep(.70,.83,age));
                float dome=saturate(1-d/max(radius,.001))*alive;
                float rim=exp(-pow((d-radius)*48,2))*alive;
                float pop=smoothstep(.69,.75,age)*(1-smoothstep(.81,.98,age));
                float expandingRadius=radius+saturate((age-.72)*3.8)*.23;
                float ring=exp(-pow((d-expandingRadius)*42,2))*pop;
                float2 eject=q-float2(sin(h*22)*.08,saturate((age-.72)*4)*.35);
                sparks=exp(-dot(eject,eject)*1600)*pop*sourceMask;
                // Darkening the raised dome and lighting its rim gives each boil
                // volume before it ruptures into a traveling molten ring.
                return (rim*.70+ring*.95-dome*.32)*sourceMask;
            }
            float3 hell(float2 uv, float2 p, float3 original, float t, float border)
            {
                float lava=lavaRegion(original);
                float heat=noise(p*3.8+float2(t*.13,-t*.22))-.5;
                float haze=smoothstep(.20,.82,uv.y)*(1-_Floor);
                float2 offset=float2(heat,sin(p.x*7+t*1.4)*.25)*.008*(haze*.7+lava)*border*_Strength;
                float3 col=tex2D(_MainTex,uv+offset).rgb;
                float2 e=float2(.0018,.0028);
                float left=lavaRegion(tex2D(_MainTex,saturate(uv-float2(e.x,0))).rgb);
                float right=lavaRegion(tex2D(_MainTex,saturate(uv+float2(e.x,0))).rgb);
                float below=lavaRegion(tex2D(_MainTex,saturate(uv-float2(0,e.y))).rgb);
                float above=lavaRegion(tex2D(_MainTex,saturate(uv+float2(0,e.y))).rgb);
                float2 gradient=float2(right-left,above-below);
                float2 channel=float2(-gradient.y,gradient.x);
                channel*=channel.y>0?-1:1;
                channel=normalize(channel+float2(.08,-.22));
                // Follow the painted river banks. Two advected phases crossfade
                // while invisible so flow never reverses or jumps at a wrap.
                float cycle=t*.27;
                float phaseA=frac(cycle), phaseB=frac(cycle+.5);
                float blend=abs(phaseA*2-1);
                float2 travel=channel*float2(.028,.041)*border;
                float3 carriedA=tex2D(_MainTex,saturate(uv-travel*phaseA)).rgb;
                float3 carriedB=tex2D(_MainTex,saturate(uv-travel*phaseB)).rgb;
                float3 carried=lerp(carriedA,carriedB,blend);
                float moltenDetail=cloud(p*float2(12,5)+float2(t*.36,-t*1.35));
                float hotCrests=pow(saturate(moltenDetail*1.45),3);
                float darkCrust=smoothstep(.27,.52,moltenDetail);
                float streamPulse=.70+.28*sin(dot(p,float2(4,11))+t*2.1)+.12*sin(t*3.3-p.x*8);
                float3 liquid=max(original*.48,carried*.75);
                liquid*=.48+darkCrust*.65;
                liquid+=float3(1,.24,.015)*(hotCrests*.68+.13)*streamPulse;
                col=lerp(col,liquid,lava*.92);
                float sparkA, sparkB;
                float boils=boilingLava(p,t,9,12,sparkA)+boilingLava(p+float2(.037,.021),t*.93,17,47,sparkB)*.55;
                col+=float3(1,.40,.035)*boils*lava*1.35;
                col+=float3(1,.56,.09)*(sparkA+sparkB)*1.20;
                float bankLight=max(max(left,right),max(below,above))*(1-lava);
                col+=float3(.65,.12,.012)*bankLight*(.18+hotCrests*.32)*streamPulse;
                float flow=cloud(p*3.2+float2(-t*.15,t*.06));
                float pulse=.60+.25*sin(t*.57+p.x*.6)+.15*sin(t*.31-p.y*1.7);
                col+=float3(1,.29,.035)*lava*(.09+flow*.22)*pulse;
                float smoke=cloud(p*1.9+float2(t*.14,-t*.23));
                float smokeMask=smoothstep(.38,.70,smoke)*haze*.42;
                col=lerp(col,float3(.12,.045,.045),smokeMask);
                float glow=exp(-pow((uv.y-.40)*4.4,2))*(.7+.3*flow)*(1-_Floor);
                col+=float3(.45,.095,.012)*glow*.065;
                float embers=motes(p,t,14,.90,9)*.52+motes(p,t,7,1.15,34)*.65+motes(p,t,3.5,1.02,61)*.38;
                col+=float3(1,.32,.065)*embers*lerp(1,.38,_Floor);
                return col;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float4 source=tex2D(_MainTex,uv)+_TextureSampleAdd;
                float2 p=uv*float2(max(.25,_Aspect),1);
                float border=smoothstep(0,.04,min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
                float3 atmosphere;
                // Unity updates this global for every draw, including cached UI
                // stencil materials. Per-material time can freeze under a Mask.
                float motionTime=lerp(_Time.y,_MotionTime,_UsePreviewTime);
                if(_Theme<.5) atmosphere=sky(uv,p,source.rgb,motionTime,border);
                else if(_Theme<1.5) atmosphere=water(uv,p,source.rgb,motionTime,border);
                else atmosphere=hell(uv,p,source.rgb,motionTime,border);
                // The source artwork remains the dominant layer, especially below
                // floor warnings. This never pans/crops the complete painting.
                float4 col=float4(lerp(source.rgb,atmosphere,_Strength),source.a)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a-.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
