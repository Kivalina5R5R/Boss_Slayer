// 2026-11-25 "เอาพื้นทั้ง3อันไปทำ animation ขยับไม่ต้องเยอะ": the element floors (Fire, Water, Land) stay as painted; only
// their glowing lines breathe and a faint wave of light rolls out from the centre circle. Plain stone and dark areas
// are left still, so the floor never pulls the eye away from the wall screen.
Shader "Deepspace/BossFloorGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Floor art", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Strength ("Glow motion strength", Range(0,1)) = 1
        _Aspect ("Art aspect", Float) = 2.09
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Strength, _Aspect;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                // Glowing parts are the bright, saturated pixels; grey stone and shadow get no motion.
                float hi = max(col.r, max(col.g, col.b));
                float lo = min(col.r, min(col.g, col.b));
                float saturation = (hi - lo) / max(hi, 1e-4);
                float glow = smoothstep(.35, .7, saturation) * smoothstep(.3, .65, hi);

                float2 p = (i.uv - .5) * float2(_Aspect, 1);
                float t = _Time.y;
                // A slow breath (about 6 s) plus a soft ring of light travelling outwards (about 1 ring every 5 s).
                float breath = sin(t * 1.05) * .5 + .5;
                float wave = pow(sin(length(p) * 6.5 - t * 1.25) * .5 + .5, 6);
                float lift = (breath * .1 + wave * .16 - .05) * _Strength;
                col.rgb *= 1 + glow * lift;
                return col;
            }
            ENDCG
        }
    }
}
