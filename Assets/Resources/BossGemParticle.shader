Shader "Deepspace/Boss Gem Particle"
{
    Properties { [HDR] _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Tint;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float diamond = abs(p.x) + abs(p.y);
                float body = 1 - smoothstep(0.52,0.7,diamond);
                float glow = exp2(-length(p) * 6) * 0.55;
                float facet = p.x > 0 ? 0.55 : 1;
                half3 color = input.color.rgb * (body * facet * 2.5 + glow)
                    + half3(0.7,0.8,1) * pow(saturate(1 - diamond), 5);
                return half4(color, input.color.a * saturate(body + glow));
            }
            ENDHLSL
        }
    }
}
