// 2026-11-26 "portal ที่หมุน หมุนตามเข็มนาฬิกา": the Final Boss wall's rune ring does not turn in its video loop, and
// turning the painted ring would drag the floating stairs in front of it along, so the ring turns with light instead:
// bright comets of violet light race clockwise round the rune band, over a faint band glow. Additive. The sector where the floating stairs pass in front of the ring (_HideFrom.._HideTo,
// degrees, 0 = right, counter-clockwise) is left dark, so the light goes behind them.
Shader "Deepspace/BossPortalOrbit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Light colour", Color) = (1, .55, 1, 1)
        _Inner ("Rune band inner edge (fraction of the radius)", Range(0, 1)) = .74
        _Outer ("Rune band outer edge (fraction of the radius)", Range(0, 1)) = .92
        _DegreesPerSecond ("Orbit speed (degrees per second, clockwise)", Float) = 40
        _Comets ("Comets round the ring", Float) = 4
        _Strength ("Brightness", Range(0, 2)) = .9
        _HideFrom ("Hidden sector start (degrees)", Float) = -88
        _HideTo ("Hidden sector end (degrees)", Float) = -4
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

            fixed4 _Color;
            float _Inner, _Outer, _DegreesPerSecond, _Comets, _Strength, _HideFrom, _HideTo;

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
                float2 local = i.uv - .5;
                float r = length(local) * 2;
                float angle = atan2(local.y, local.x);                 // radians, 0 = right, counter-clockwise
                float t = _Time.y;

                // Where the rune band is: a soft-edged ring, brightest in its middle.
                float mid = (_Inner + _Outer) * .5, halfWidth = (_Outer - _Inner) * .5;
                float band = saturate(1 - abs(r - mid) / halfWidth);
                band = band * band * (3 - 2 * band);

                // Comets: adding time to the angle moves the pattern towards smaller angles, i.e. clockwise on screen.
                // Each comet's head is its clockwise-most end; its tail trails counter-clockwise behind it.
                float turn = radians(_DegreesPerSecond) * t;
                float s = frac((angle + turn) / (2 * UNITY_PI) * _Comets);
                float comet = pow(1 - s, 7) + .35 * pow(1 - s, 2);

                // Fine flicker along the band so the light reads as running over runes.
                float runes = .75 + .25 * sin((angle + turn) * 48);

                // (2026-11-26: the aura ripples moved to BossShockwave.shader, which rolls them on across the whole wall.)
                float glow = band * (.18 + comet * runes);

                // Leave the light out where the floating stairs cross the ring (and fade off the quad's edge).
                float degreesAt = degrees(angle);
                float hidden = smoothstep(_HideFrom - 8, _HideFrom + 8, degreesAt) * (1 - smoothstep(_HideTo - 8, _HideTo + 8, degreesAt));
                glow *= 1 - hidden;
                glow *= 1 - smoothstep(.96, 1, r);

                fixed4 col = i.color;
                col.a = saturate(glow * _Strength) * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
