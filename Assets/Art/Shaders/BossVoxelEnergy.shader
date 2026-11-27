Shader "Deepspace/Boss Voxel Energy"
{
    Properties { _Alpha("Opacity",Range(0,1))=1 _SrcBlend("Source Blend",Float)=1 _DstBlend("Destination Blend",Float)=0 _ZWrite("Depth Write",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" "RenderType"="Opaque" }
        Pass
        {
            ZWrite [_ZWrite] Cull Back Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Alpha;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; half4 color:COLOR; };
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;return o;}
            half4 frag(V i):SV_Target
            {
                clip(i.color.a-.015);
                float3 n=normalize(i.normalWS);
                float light=saturate(dot(n,normalize(float3(-.45,.8,-.6))));
                // Contrasting cube faces retain volume even on an emissive effect.
                half3 color=i.color.rgb*(.34+light*1.15);
                return half4(color,_Alpha);
            }
            ENDHLSL
        }
    }
}
