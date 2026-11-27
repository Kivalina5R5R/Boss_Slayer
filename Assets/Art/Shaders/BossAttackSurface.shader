Shader "Deepspace/Boss Attack Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorA ("Element", Color) = (1,.3,.1,1)
        _ColorB ("Accent", Color) = (1,.8,.3,1)
        _Theme ("Theme", Float) = 0
        _Circle ("Circle", Float) = 0
        _Phase ("Phase", Float) = 0
        _Progress ("Charge", Float) = 0
        _Fade ("Fade", Float) = 1
        _Scorch ("Lingering fire", Float) = 0
        _Size ("Aspect", Vector) = (1,1,0,0)
        _Form ("Impact Beam Sweep Wave Barrage Pool", Float) = 0
        _Propagation ("None Radial Forward", Float) = 0
        _Spread ("Fire front", Float) = 1
        _Age ("Seconds since release", Float) = 0
        _WaveAxis ("Wave travel axis", Float) = 0
        _WaveDirection ("Wave direction", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; half4 color:COLOR; };
            float4 _ColorA,_ColorB,_Size,_ClipRect;
            float _Theme,_Circle,_Phase,_Progress,_Fade,_Scorch,_Form,_Propagation,_Spread,_Age,_WaveAxis,_WaveDirection;
            v2f vert(appdata v){v2f o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 id=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(id),hash(id+float2(1,0)),f.x),lerp(hash(id+float2(0,1)),hash(id+1),f.x),f.y);}
            float fbm(float2 p){return noise(p)*.57+noise(p*2.07)*.28+noise(p*4.13)*.15;}
            half4 frag(v2f i):SV_Target
            {
                float2 q=i.uv*2-1,p=q*_Size.xy;
                float r=length(q), angle=atan2(q.y,q.x);
                if(_Circle>.5 && r>1)discard;
                float live=step(.5,_Phase),t=_Time.y;
                float2 ex=(1-abs(q))*_Size.xy;
                float edgeDistance=_Circle>.5?1-r:min(ex.x,ex.y);
                float edge=1-smoothstep(.018,.052,edgeDistance);
                float travel=(_WaveAxis<.5?i.uv.y:i.uv.x)*_WaveDirection;
                float radial=_Circle>.5?r:travel;
                float n=fbm(p*4+float2(0,-t*1.8));
                float motif=0,detail=0;
                half3 element=_ColorA.rgb;
                half3 color;
                if(_Theme<.5) // Demon: lava veins, molten craters, infernal sweep.
                {
                    float cracks=abs(sin(p.x*7+sin(p.y*9)*1.3+n*4));
                    motif=pow(1-cracks,14);
                    detail=0;
                    color=lerp(half3(.12,.007,.005),half3(1.5,.13,.008),motif);
                    color+=half3(1,.65,.04)*(pow(motif,3)*.8+detail*.6);
                }
                else if(_Theme<1.5) // Giant Fish: rolling water, foam and bubbles.
                {
                    float crest=sin(radial*18+n*2-t*5);
                    motif=smoothstep(.45,.96,crest);
                    float2 cell=frac(p*5+float2(t*.2,t*.5))-.5;
                    float bubble=exp2(-abs(length(cell)-.25)*85);
                    detail=bubble*.6+motif*smoothstep(.55,.9,noise(p*24+t));
                    color=lerp(half3(.005,.09,.28),half3(.02,.58,1.2),.3+motif*.7)+detail*half3(.65,1,1.2);
                }
                else if(_Theme<2.5) // Jellyfish: forked bolts strike, then arc across the floor.
                {
                    float axis=_Circle>.5?angle*1.6:p.x*1.3;
                    float bolt=abs(frac(axis+sin(p.y*16+t*18)*.08+noise(p*9+t*4)*.25)-.5);
                    motif=exp2(-bolt*110);
                    detail=exp2(-abs(sin(p.y*7+sin(p.x*19+t*11)*.35))*34);
                    float flicker=.65+.35*step(.35,noise(float2(floor(t*16),3)));
                    color=half3(.065,.012,.17)+half3(.35,.28,1.2)*motif+half3(.8,.95,1.3)*(motif*.7+detail*.5)*flicker;
                }
                else if(_Theme<3.5) // Dragon: hot orange flames are applied below, including live propagation.
                {motif=n;color=half3(.35,.025,.003);}
                else if(_Theme<4.5) // Golem: faceted crystal shards and spreading fracture rings.
                {
                    float2 tile=frac(float2(p.x+p.y,p.y-p.x)*2.5)-.5;
                    float diamond=abs(tile.x)*1.2+abs(tile.y);
                    motif=1-smoothstep(.25,.39,diamond);
                    detail=exp2(-abs(diamond-.33)*95);
                    float facet=step(0,tile.x)*.35+step(0,tile.y)*.2;
                    float stone=noise(p*13)*.13; color=half3(.07+stone,.075+stone,.09+stone)+motif*lerp(element*.25,lerp(element,half3(1,1,1),.25)*1.2,facet)+detail*element*.8;
                }
                else if(_Theme<5.5) // Sea Serpent: curling storm currents and white-water crests.
                {
                    float spiral=_Circle>.5?angle*4+length(p)*11:travel*19+sin(p.x*4+t)*2;
                    motif=pow(saturate(sin(spiral-t*5)),4);
                    detail=pow(saturate(cos(spiral*1.7-t*3+n*2)),16);
                    color=half3(.005,.075,.09)+motif*half3(.02,.7,.57)+detail*half3(.55,1.1,1.2);
                }
                else if(_Theme<6.5) // Sentinel: rail core, targeting grid and pulsed impact rings.
                {
                    float2 grid=abs(frac(p*5)-.5);
                    motif=exp2(-min(grid.x,grid.y)*65)*.4;
                    float core=_Form>.5&&_Form<1.5?exp2(-abs(q.x)*14):0;
                    detail=core+pow(saturate(sin(p.y*9-t*14)),22)*.35;
                    color=half3(.085,.025,.004)+motif*half3(.85,.27,.02)+detail*half3(1.5,1.05,.32);
                }
                else if(_Theme<7.5) // Wraith: dark void mouths, torn dimensional rims and inward streaks.
                {
                    float2 drift=p*2.3+float2(sin(t*.7)*.6,-t*.65);
                    float smoke=fbm(drift+float2(n*1.4,noise(p*3-t)*1.5));
                    float wisps=fbm(drift*1.8+float2(t*.3,-t*.4));
                    motif=smoothstep(.25,.75,smoke); detail=pow(saturate(wisps),3);
                    color=lerp(half3(.025,.004,.05),half3(.42,.13,.65),motif)+detail*half3(.3,.13,.45);
                    color*=.65+.35*smoothstep(.05,.5,r+n*.2);
                }
                else // Hydra: venom puddles, rolling acid bubbles and toxic foam.
                {
                    float2 cell=frac(p*4+float2(sin(t)*.15,-t*.3))-.5;
                    float bubbleRadius=.13+.12*noise(floor(p*4)+floor(t*2));
                    float bubble=length(cell);
                    motif=1-smoothstep(bubbleRadius-.035,bubbleRadius,bubble);
                    detail=exp2(-abs(bubble-bubbleRadius)*85);
                    color=half3(.025,.08,.003)+half3(.15,.55,.008)*n+motif*half3(.1,.35,.01)+detail*half3(.6,1.1,.025);
                }
                // Shape changes the motion language as well as the footprint.
                // Live attacks show no rings; only the charge countdown line draws a contour.
                float shock=0;
                if(_Form<.5)color+=shock*_ColorB.rgb*.7;
                else if(_Form<1.5)color+=exp2(-abs(q.x)*18)*(.2+.2*sin(t*12+p.y*8))*element*live;
                else if(_Form<2.5)color+=exp2(-abs(i.uv.y-frac(_Age*1.2))*30)*element*.6*live;
                else if(_Form<3.5)color+=pow(saturate(sin(travel*15-t*6)),8)*_ColorB.rgb*.35*live;
                else if(_Form<4.5)color+=shock*_ColorB.rgb+exp2(-r*7)*exp2(-_Age*5)*live;
                // Flames use multiple moving noise scales: tongues, hot cores and glowing embers.
                if(_Theme<.5||(_Theme>2.5&&_Theme<3.5)||_Propagation>.5)
                {
                    float2 flameUV=p*float2(4,2.2)+float2(0,-t*3.8);
                    float hot=fbm(flameUV+float2(sin(p.y*4-t*3)*.4,0));
                    float tongues=saturate((hot-.27)*2.3);
                    float core=pow(saturate(tongues*1.3),3);
                    float embers=pow(saturate(noise(p*21+float2(0,-t*8))),16);
                    if(_Theme<.5) {
                        color=lerp(half3(.045,.001,.008),half3(1.15,.008,.035),tongues);
                        color+=half3(.7,.055,.09)*core+embers*half3(.85,.04,.1);
                    } else {
                        color=lerp(half3(.085,.009,.002),half3(1.6,.17,.004),tongues);
                        color+=half3(1.3,.95,.13)*core+embers*half3(1.2,.6,.1);
                    }
                    if(_Form<.5 || (_Form>3.5 && _Form<4.5)) color+=element*shock*.6; if(_Form>.5 && _Form<1.5) color+=element*exp2(-abs(q.x)*12)*(.2+.2*sin(p.y*8-t*12))*live; motif=tongues;detail=core;
                }
                float alpha=.72+motif*.18+detail*.1;
                // Warning shows the whole future footprint without looking like live fire.
                if(live<.5)
                {
                    float charge=exp2(-abs((_Circle>.5?r:edgeDistance)-(1-_Progress))*65);
                    color=color*.32+element*.09;
                    color=lerp(color,half3(1,.08,.12),saturate(max(edge,charge)));
                    alpha=.54+max(edge,charge)*.45;
                }
                else
                {
                    color=lerp(color,element*1.15,edge*.75);
                    if(_Propagation>.5)
                    {
                        float coordinate=_Propagation<1.5?r:1-i.uv.y;
                        float lit=step(coordinate,_Spread);
                        float front=exp2(-abs(coordinate-_Spread)*70)*step(coordinate,_Spread);
                        half3 frontColor=_Theme<.5?half3(1,.045,.09):half3(1.5,.85,.08); color=lerp(half3(.06,.009,.006),color+front*frontColor,lit);
                        // Unreached territory keeps a dim dashed warning; it is not yet damaging.
                        float dash=step(.5,frac((q.x+q.y)*13));
                        color+=edge*(1-lit)*dash*half3(.65,.17,.015);
                        alpha=lerp(.25,.93,lit);
                    }
                    if(_Scorch>1.5){color=half3(.07,.035,.015);alpha=.3;}
                }
                alpha=saturate(alpha)*_Fade*i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return half4(color,alpha);
            }
            ENDCG
        }
    }
}
