Shader "Deepspace/Boss Theme Surface"
{
    Properties
    {
        _EmissionColor("Theme rim",Color)=(0,0,0,1)
        _EmissionIntensity("Rim intensity",Float)=0
        _RimPower("Rim falloff",Range(1,8))=5
        _Metallic("Metal highlight",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.5
        _SpecularTint("Specular tint",Color)=(.8,.9,1,1)
        _Alpha("Opacity",Range(0,1))=1
        _SrcBlend("Source blend",Float)=1
        _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor, _SpecularTint;
            float _EmissionIntensity, _RimPower, _Metallic, _Smoothness, _Alpha;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; half4 color:COLOR; };
            V vert(A i)
            {
                V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.color=i.color; return o;
            }
            half4 frag(V i):SV_Target
            {
                clip(i.color.a-.015);
                float3 n=normalize(i.normalWS), view=normalize(_WorldSpaceCameraPos.xyz-i.positionWS);
                // Neutral presentation light keeps each boss palette consistent across environments.
                float3 key=normalize(float3(-.45,.8,-.6));
                float diffuse=.52+.64*saturate(dot(n,key));
                float rim=pow(1-saturate(dot(n,view)),max(1,_RimPower));
                float spec=pow(saturate(dot(n,normalize(key+view))),lerp(18,96,_Smoothness));
                half3 baseColor=i.color.rgb*diffuse;
                half3 edge=_EmissionColor.rgb*min(_EmissionIntensity,.42)*rim*.25;
                half3 highlight=_SpecularTint.rgb*spec*_Metallic*.16;
                return half4(baseColor+edge+highlight,i.color.a*_Alpha);
            }
            ENDHLSL
        }
    }
}
