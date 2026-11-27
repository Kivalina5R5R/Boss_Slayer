Shader "Deepspace/Boss Gem Crystal"
{
    Properties
    {
        [HDR] _EmissionColor ("Gem color", Color) = (0.4,0.7,1,1)
        _EmissionIntensity ("Glow", Float) = 2.8
        _RimPower ("Rim", Float) = 2
        _Smoothness ("Polish", Range(0,1)) = 0.92
        _Metallic ("Reflection", Range(0,1)) = 0.16
        _Phase ("Shimmer offset", Float) = 0
        _GemCenter ("Mesh center", Vector) = (0,0,0,0)
        _GemExtents ("Mesh extents", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float _EmissionIntensity, _RimPower, _Smoothness, _Metallic, _Phase;
                float4 _GemCenter, _GemExtents;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; };
            V vert(A i)
            {
                V o;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.color=i.color;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                InputData data=(InputData)0;
                data.positionWS=i.positionWS;
                data.normalWS=NormalizeNormalPerPixel(i.normalWS);
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                data.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                data.bakedGI=SampleSH(data.normalWS);
                data.shadowMask=unity_ProbesOcclusion;
                data.vertexLighting=VertexLighting(i.positionWS,data.normalWS);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=i.color.rgb;
                surface.metallic=_Metallic;
                surface.smoothness=_Smoothness;
                surface.specular=half3(0.08,0.08,0.08);
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                float facing=saturate(dot(data.normalWS,data.viewDirectionWS));
                float rim=pow(1-facing,max(1.6,_RimPower));
                float glow=min(_EmissionIntensity,6);
                // A colored interior stays readable even on an unlit floor overlay camera.
                surface.emission=i.color.rgb*0.85+_EmissionColor.rgb*(0.12+rim*0.42)*glow;
                half4 lit=UniversalFragmentPBR(data,surface);

                // Moving studio reflections complement the scene's reflection probes.
                float3 reflected=TransformWorldToViewDir(reflect(-data.viewDirectionWS,data.normalWS),true);
                float t=_Time.y*0.8+_Phase;
                float3 softbox=normalize(float3(0.3*sin(t),0.25+0.28*cos(t*0.73),1));
                float spec=pow(saturate(dot(reflected,softbox)),64);
                float band=exp2(-abs(reflected.x*0.8+reflected.y*0.45-0.22*sin(t*1.1))*32);
                float sparkle=pow(saturate(dot(reflected,normalize(float3(-0.6,0.5,0.75)))),110);
                half3 reflectionTint=lerp(_EmissionColor.rgb,half3(1,1,1),0.78);
                float3 centerWS=TransformObjectToWorld(_GemCenter.xyz);
                float span=max(0.0001,length(mul((float3x3)unity_ObjectToWorld,_GemExtents.xyz)));
                float3 face=TransformWorldToViewDir(i.positionWS-centerWS,false)/span;
                float sweep=frac(t*0.32)*2.8-1.4;
                float ribbon=exp2(-abs(face.x+face.y*0.65-sweep)*75);
                float softRibbon=exp2(-abs(face.x+face.y*0.65-sweep-0.065)*20)*0.22;
                lit.rgb+=reflectionTint*(spec*0.85+band*0.25+sparkle*1.4+ribbon*2.2+softRibbon);
                return half4(lit.rgb,1);
            }
            ENDHLSL
        }
    }
}
