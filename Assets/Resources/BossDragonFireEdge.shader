Shader "Deepspace/Dragon Inferno Edge"
{
    Properties { _Heat("Heat",Float)=0 _Width("Width",Float)=.01 _Clock("Clock",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Heat,_Width,_Clock;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float pulse:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                float wave=sin(dot(i.p.xyz,float3(7.1,4.3,8.7))-_Clock*7);
                float flicker=.55+.45*sin(dot(i.p.xyz,float3(14.3,9.7,3.2))+_Clock*11);
                float extrusion=_Width*(.55+flicker*.9+wave*.2)*saturate(_Heat);
                o.p=TransformObjectToHClip(i.p.xyz+i.n*extrusion);
                o.pulse=flicker;
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                half3 color=lerp(half3(1,.035,.002),half3(2,.65,.03),i.pulse);
                return half4(color,saturate(_Heat)*.8);
            }
            ENDHLSL
        }
    }
}
