Shader "Deepspace/Boss Divine Halo"
{
    Properties
    {
        [HDR] _Color ("Boss Color", Color) = (0.3,0.8,1,1)
        _Intensity ("Radiance", Range(0,5)) = 1.6
        _AnimationTime ("Animation Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _AnimationTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float t = _AnimationTime;
                float a = atan2(p.y, p.x) - t * 0.45;
                float wave = 0.021 * sin(a * 9 + t * 2.7) + 0.012 * sin(a * 17 - t * 3.1);
                float distance = abs(r - (0.72 + wave));
                float core = exp2(-distance * 230);
                float aura = exp2(-distance * 42) * 0.32;
                float filament = exp2(-abs(r - (0.752 - wave * 0.6)) * 290);
                float inner = exp2(-abs(r - 0.624) * 340)
                    * smoothstep(-0.15,0.2,sin(a * 6 - t * 2.5));

                // Repeating diamond sigils and counter-rotating radial ticks.
                float sigilAngle = atan2(sin(a * 12 + t * 10), cos(a * 12 + t * 10)) / 12;
                float diamond = abs(r - 0.842) / 0.028 + abs(sigilAngle) / 0.022;
                float sigils = exp2(-abs(diamond - 1) * 9) * 0.9;
                float ticks = pow(saturate(cos(a * 48 - t * 4)), 32)
                    * (1 - smoothstep(0.008,0.024,abs(r - 0.793))) * 0.7;
                float rays = pow(saturate(cos(a * 8 + t)), 100)
                    * exp2(-abs(r - 0.875) * 42) * 0.7;
                float pulse = 0.94 + 0.06 * sin(t * 1.4);
                float orbit = pow(saturate(cos(a * 3 - t * 1.8)), 42);
                float light = (core * (1.35 + orbit * 2.2) + aura * (1 + orbit * 2)
                    + filament * 0.65 + inner * 0.6 + sigils + ticks + rays) * pulse;
                float edgeFade = 1 - smoothstep(0.93,0.995,r);
                half3 color = (_Color.rgb * light + half3(0.3,0.32,0.36) * core) * _Intensity * edgeFade;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
