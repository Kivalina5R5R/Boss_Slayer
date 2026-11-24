// Spreads a boss's new colour theme outward from one point, with a glowing edge (README_Unity.md section 5).
Shader "Deepspace/BossSkinThemeWipe"
{
    Properties
    {
        _BaseMap ("Base A", 2D) = "white" {}
        _BaseMapB ("Base B", 2D) = "white" {}
        _EmissionMap ("Emission A", 2D) = "black" {}
        _EmissionMapB ("Emission B", 2D) = "black" {}
        _MetallicGlossMap ("Metallic A", 2D) = "black" {}
        _MetallicGlossMapB ("Metallic B", 2D) = "black" {}
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _WipeCenter ("Center", Vector) = (0,0,0,0)
        _WipeRadius ("Radius", Float) = 0
        _WipeEdge ("Edge", Float) = .2
        [HDR] _EdgeColor ("Edge Color", Color) = (2,1.2,.4,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BaseMapB);
            TEXTURE2D(_EmissionMap);
            TEXTURE2D(_EmissionMapB);
            TEXTURE2D(_MetallicGlossMap);
            TEXTURE2D(_MetallicGlossMapB);
            TEXTURE2D(_OcclusionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _WipeCenter;
                float _WipeRadius;
                float _WipeEdge;
                float4 _EdgeColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float distanceFromCore = distance(input.positionWS, _WipeCenter.xyz);
                float reached = 1 - smoothstep(_WipeRadius - _WipeEdge, _WipeRadius, distanceFromCore);
                float edge = reached * (1 - reached) * 4;

                float4 metallic = lerp(SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_BaseMap, input.uv), SAMPLE_TEXTURE2D(_MetallicGlossMapB, sampler_BaseMap, input.uv), reached);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb, SAMPLE_TEXTURE2D(_BaseMapB, sampler_BaseMap, input.uv).rgb, reached);
                surface.metallic = metallic.r;
                surface.smoothness = metallic.a;
                surface.occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv).g;
                surface.emission = lerp(SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb, SAMPLE_TEXTURE2D(_EmissionMapB, sampler_BaseMap, input.uv).rgb, reached) + _EdgeColor.rgb * edge;
                surface.alpha = 1;
                surface.normalTS = float3(0, 0, 1);

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normalize(input.normalWS);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                return UniversalFragmentPBR(data, surface);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
