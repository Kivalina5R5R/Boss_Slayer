// 2026-11-26 "ส่วนของ Finalboss ทำคลื่นกระแทกให้ต่อกัน": shockwaves roll out from the Final Boss wall's eye over the
// whole wall. Distances are in wall pixels (_WallSize), so the rings stay round on the 8192x2160 wall. Additive.
// 2026-11-26 "คลื่นกระเพื่อมของ Final boss ให้มันต่อจากอันที่เล่น เป็น VDO มันไม่ต้องบ่อยขนาดนั้น ลดความชัดมันด้วย อยากได้แค่
// บางๆไม่บังตัวของบอส": no more endless evenly spaced rings. A ring is born only when the centre video's eye pulses
// (_Pulses: the three pulse starts in its loop, measured on BGFinalBossScene_Centre.mp4, w = the loop's length) and
// follows the video's clock (_VideoTime, set by BossShockwaveSync), so each one carries the video's own ring on past
// the video's edges. Nothing is drawn over the video band itself (_MaskInner.._MaskOuter from the eye, horizontally),
// so the boss in the middle is never covered; the rings are thin and faint.
Shader "Deepspace/BossShockwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Wave colour", Color) = (1, .45, 1, 1)
        _Strength ("Brightness", Range(0, 2)) = .3
        _Centre ("Eye centre (fraction of the wall, y up)", Vector) = (.5103, .5833, 0, 0)
        _WallSize ("Wall size in pixels", Vector) = (8192, 2160, 0, 0)
        _Pulses ("Pulse starts in the video loop (s), w = loop length", Vector) = (2.04, 5.04, 7.12, 8.04)
        _VideoTime ("Video clock (s)", Float) = 0
        _Born ("Ring radius at the pulse start (pixels)", Float) = 1300
        _Speed ("Ring speed (pixels per second)", Float) = 1250
        _Width ("Ring glow width (pixels)", Float) = 260
        _Life ("Ring life (seconds)", Float) = 3.2
        _MaskInner ("Hidden up to (pixels from the eye, horizontally)", Float) = 1900
        _MaskOuter ("Fully shown from (pixels from the eye, horizontally)", Float) = 2500
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
            float4 _Centre, _WallSize, _Pulses;
            float _Strength, _VideoTime, _Born, _Speed, _Width, _Life, _MaskInner, _MaskOuter;

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

            // One ring, pulseStart seconds into the loop: a crisp front with a soft glow trailing back to the eye.
            float Ring(float d, float pulseStart)
            {
                float loopLength = max(.01, _Pulses.w);
                float age = _VideoTime - pulseStart;
                age = age < 0 ? age + loopLength : age;              // the last loop's pulse still rolling on
                float radius = _Born + _Speed * age;
                float x = (radius - d) / _Width;                     // 0 at the front, 1 at the end of its glow
                float shape = smoothstep(-.05, 0, x) * pow(saturate(1 - x), 3);
                float fade = saturate(1 - age / _Life);
                return shape * fade * fade;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 offset = (i.uv - _Centre.xy) * _WallSize.xy;
                float d = length(offset);
                float wave = Ring(d, _Pulses.x) + Ring(d, _Pulses.y) + Ring(d, _Pulses.z);
                wave *= smoothstep(_MaskInner, _MaskOuter, abs(offset.x));   // only past the video's edges

                fixed4 col = i.color;
                col.a = saturate(wave * _Strength) * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
