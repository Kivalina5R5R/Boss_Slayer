// 2026-11-25 "ทำไมตัว portal ไม่หมุน หมุนแบบเดียวกับน้ำและไฟ": the Land wall's video barely turns its vortex, so this
// turns it: a disc of the still wall picture (the portal's inside) drawn over the video, rotating clockwise forever
// (no loop seam) with a soft round edge. _UvRect is the RawImage's uvRect (the disc's square in the wall texture).
Shader "Deepspace/BossPortalSpin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Wall art", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UvRect ("Disc square in the texture (x, y, w, h)", Vector) = (0,0,1,1)
        _DegreesPerSecond ("Spin (degrees per second, + = clockwise)", Float) = 20
        _Edge ("Soft edge (fraction of the radius)", Range(0, .5)) = .18
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
            float4 _UvRect;
            float _DegreesPerSecond, _Edge;

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
                // The disc's own 0..1 square, centred, so it can turn about its middle.
                float2 local = (i.uv - _UvRect.xy) / _UvRect.zw - .5;
                float r = length(local) * 2;
                float a = radians(_DegreesPerSecond) * _Time.y;
                float s = sin(a), c = cos(a);
                // Sampling turned the other way makes the picture turn clockwise on screen (uv y points up).
                float2 turned = float2(c * local.x - s * local.y, s * local.x + c * local.y);
                float2 uv = _UvRect.xy + (turned + .5) * _UvRect.zw;
                fixed4 col = tex2D(_MainTex, uv) * i.color;
                col.a *= 1 - smoothstep(1 - _Edge, 1, r);
                return col;
            }
            ENDCG
        }
    }
}
