// 2026-11-26 "ของน้ำให้ขอบๆ มีลายแสงกระทบน้ำตรงขอบๆที่ไม่ได้สร้าง VDO": the Water wall's centre video has light
// dancing over it as if sunlight hit the water above, but the painted sides beside it stayed still. This draws that
// light on the sides: a moving net of bright caustic lines (the edges of drifting cells), brighter on the lit parts of
// the painting (_MainTex is the wall picture) and towards the top, fading out where the centre video takes over
// (_BandLeft.._BandRight with the video's own edge feather). Additive.
Shader "Deepspace/BossWaterCaustics"
{
    Properties
    {
        [PerRendererData] _MainTex ("Wall art", 2D) = "white" {}
        _Color ("Light colour", Color) = (.5, .95, 1, 1)
        // 2026-11-26 "เยอะไป แล้วก็เห็นชัด เอาแค่พอให้รู้ว่ามา ลดค่า alpha ให้เหลือซัก20": the light is at most 20% opaque
        // (was up to about 55%+) and the net is coarser (9 cells across the height, was 14).
        // 2026-11-26 "ใส่ทั้งหน้าจอเลย แต่เหลือ10 พอ": over the whole wall now (_SidesOnly 0), at most 10% opaque.
        _Strength ("Most opaque the light gets (0..1)", Range(0, 1)) = .1
        [Toggle] _SidesOnly ("Only beside the centre video", Float) = 0
        // 2026-11-26 "ลดความเยอะของมันลงอีก ช่องๆมันเยอะเกินไป": 5 cells across the height (was 9), one layer only.
        _Scale ("Cells across the wall height", Float) = 5
        _Speed ("Speed", Float) = .55
        _Aspect ("Wall width / height", Float) = 3.7926
        _BandLeft ("Video band left (fraction of the width)", Float) = .1924
        _BandRight ("Video band right (fraction of the width)", Float) = .8076
        _BandFeather ("Video edge feather (fraction of the band)", Float) = .12
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
        Blend SrcAlpha One
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Strength, _Scale, _Speed, _Aspect, _BandLeft, _BandRight, _BandFeather, _SidesOnly;

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

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Distance between the nearest and second nearest drifting cell centre: small along the cell edges.
            float CellEdge(float2 p, float t)
            {
                float2 cell = floor(p), inside = frac(p);
                float nearest = 8, second = 8;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 centre = .5 + .4 * sin(t + 6.2831 * Hash2(cell + offset));
                        float d = length(offset + centre - inside);
                        if (d < nearest) { second = nearest; nearest = d; }
                        else if (d < second) second = d;
                    }
                return second - nearest;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = float2(i.uv.x * _Aspect, i.uv.y) * _Scale;
                p += .18 * sin(p.yx * 1.7 + t * 1.3);                          // the water surface wobbles the net

                float lines = 1 - smoothstep(0, .22, CellEdge(p, t));
                float caustic = pow(saturate(lines), 2.2);

                // Brighter on the lit parts of the painting and nearer the top (closer to the surface).
                fixed3 art = tex2D(_MainTex, i.uv).rgb;
                float lum = dot(art, float3(.3, .59, .11));
                caustic *= lerp(.35, 1.6, saturate(lum * 2.5)) * lerp(.55, 1.15, i.uv.y);

                // With _SidesOnly on, only beside the centre video (the inverse of its edge feather); off, the whole wall.
                float u = (i.uv.x - _BandLeft) / (_BandRight - _BandLeft);
                float video = smoothstep(0, _BandFeather, u) * smoothstep(0, _BandFeather, 1 - u);
                caustic *= 1 - video * _SidesOnly;

                fixed4 col = i.color;
                col.a = saturate(caustic) * _Strength * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
