Shader "Deepspace/Boss Safe Zone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Aspect ("Aspect", Vector) = (1,1,0,0)
        _Fade ("Fade", Float) = 1
        _Active ("Active", Float) = 0
        _Offset ("Animation offset", Float) = 0
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
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; half4 color : COLOR; };
            float4 _Aspect, _ClipRect;
            float _Fade, _Active, _Offset;
            V vert(A i)
            {
                V o;
                o.local=i.vertex;
                o.vertex=UnityObjectToClipPos(i.vertex);
                o.uv=i.uv;
                o.color=i.color;
                return o;
            }
            float segment(float2 p,float2 a,float2 b)
            {
                float2 v=b-a;
                return length(p-a-v*saturate(dot(p-a,v)/dot(v,v)));
            }
            half4 frag(V i) : SV_Target
            {
                float2 q=i.uv*2-1;
                float2 p=q*_Aspect.xy;
                float2 inset=(1-abs(q))*_Aspect.xy;
                float edge=min(inset.x,inset.y);
                float t=_Time.y+_Offset;
                float pulse=0.88+0.12*sin(t*2.4);
                float rim=exp2(-abs(edge-0.035)*115);
                float glow=exp2(-edge*17)*0.5;
                float inner=exp2(-abs(edge-0.14)*150)*0.6;
                float angle=atan2(p.y,p.x);
                float orbit=pow(saturate(cos(angle-t*0.65)),14);
                float2 corner=inset-float2(0.17,0.17);
                float diamond=exp2(-abs(abs(corner.x)+abs(corner.y)-0.08)*140);
                float cornerArms=exp2(-abs(inset.x-0.10)*130)*step(inset.y,0.32)
                    +exp2(-abs(inset.y-0.10)*130)*step(inset.x,0.32);

                // The protective crest remains upright while its surrounding runes drift.
                float shield=segment(p,float2(-0.29,0.22),float2(0,0.34));
                shield=min(shield,segment(p,float2(0,0.34),float2(0.29,0.22)));
                shield=min(shield,segment(p,float2(0.29,0.22),float2(0.24,-0.10)));
                shield=min(shield,segment(p,float2(0.24,-0.10),float2(0,-0.34)));
                shield=min(shield,segment(p,float2(0,-0.34),float2(-0.24,-0.10)));
                shield=min(shield,segment(p,float2(-0.24,-0.10),float2(-0.29,0.22)));
                float crest=exp2(-shield*100)+exp2(-shield*26)*0.3;
                float check=min(segment(p,float2(-0.13,0.025),float2(-0.025,-0.09)),
                    segment(p,float2(-0.025,-0.09),float2(0.145,0.13)));
                crest+=exp2(-check*85);
                float r=length(p);
                float halo=exp2(-abs(r-0.48)*150)*0.33*pow(saturate(cos(angle*4-t*0.7)),3);
                float runeAngle=atan2(sin(angle*8+t*0.7),cos(angle*8+t*0.7))/8;
                float rune=exp2(-abs(abs(r-0.62)+abs(runeAngle)*0.4-0.035)*160);
                rune*=step(0.5,r)*step(r,0.73);
                float2 cell=floor((p+2)*5);
                float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float2 star=frac((p+2)*5)-0.5;
                float spark=exp2(-min(abs(star.x)*5+abs(star.y),abs(star.y)*5+abs(star.x))*32);
                spark*=step(0.8,seed)*pow(saturate(sin(t*1.7+seed*12)),8);

                float light=(rim*(1+orbit)+glow+inner+halo)*pulse;
                // Neutral backing and a white shield distinguish safety from every boss element.
                half3 color=half3(0.035,0.04,0.055)+half3(0.94,0.96,1)*light;
                color+=half3(1,0.76,0.28)*(diamond+cornerArms*0.9+rune);
                color+=half3(1,1,1)*(crest*1.3+spark);
                float alpha=saturate(0.84+light*0.3+crest*0.7+diamond+cornerArms+rune*0.7+spark);
                alpha*=_Fade*lerp(0.94,1,_Active)*i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return half4(color,alpha);
            }
            ENDCG
        }
    }
}
