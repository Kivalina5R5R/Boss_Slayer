Shader "Deepspace/Boss Gem Aura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _Tint ("Gem color", Color) = (0.4,0.7,1,1)
        _Phase ("Shimmer offset", Float) = 0
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
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
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
            float4 _Tint, _ClipRect;
            float _Phase;
            V vert(A i) { V o; o.vertex=UnityObjectToClipPos(i.vertex); o.local=i.vertex; o.uv=i.uv; o.color=i.color; return o; }
            float star(float2 p,float size)
            {
                p/=size;
                float cross=min(abs(p.x)+abs(p.y)*4,abs(p.y)+abs(p.x)*4);
                return exp2(-cross*2.3)*saturate(1-length(p));
            }
            half4 frag(V i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float r=length(p),t=_Time.y+_Phase;
                float breathing=0.85+0.15*sin(t*2.2);
                float aura=exp2(-r*r*5.5)*0.9*breathing;
                float rim=exp2(-abs(r-0.46)*28)*0.32;
                float a=t*0.42;
                float2 orbit=float2(cos(a),sin(a))*0.62;
                float sparkle=star(p-orbit,0.32)*(0.55+0.45*pow(saturate(sin(t*2.4)),2));
                sparkle+=star(p+orbit*0.9,0.25)*(0.45+0.55*pow(saturate(cos(t*2.1)),2));
                sparkle+=star(p-float2(0.24,-0.55),0.11)*pow(saturate(sin(t*3+1.3)),6);
                float fade=1-smoothstep(0.76,1,r);
                float alpha=(aura+rim+sparkle)*fade*i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                half3 tint=lerp(_Tint.rgb,half3(1,1,1),saturate(sparkle)*0.8);
                return half4(tint*(1.8+sparkle*3.5),alpha);
            }
            ENDCG
        }
    }
}
