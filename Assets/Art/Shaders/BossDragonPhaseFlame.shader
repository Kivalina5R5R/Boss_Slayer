Shader "Deepspace/Dragon Phase Flame"
{
    Properties { _Clock("Clock",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Clock;
            CBUFFER_END
            struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float y=saturate(i.uv.y);
                float x=i.uv.x-.5+sin(y*9-_Clock*9)*y*.12;
                float width=max(.005,(1-y)*.45);
                float mask=(1-smoothstep(width*.25,width,abs(x)))*smoothstep(0,.12,y)*(1-smoothstep(.75,1,y));
                half3 color=lerp(half3(2,.65,.025),half3(1,.02,.001),y);
                return half4(color,mask*i.c.a);
            }
            ENDHLSL
        }
    }
}
