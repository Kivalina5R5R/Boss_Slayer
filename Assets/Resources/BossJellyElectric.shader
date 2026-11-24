Shader "Deepspace/Jelly Electric" {
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
Pass { Cull Off ZWrite Off Blend SrcAlpha One
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 p:POSITION;half4 c:COLOR;};
struct V {float4 p:SV_POSITION;half4 c:COLOR;};
V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;return o;}
half4 Frag(V i):SV_Target{return half4(i.c.rgb*1.8,i.c.a);}
ENDHLSL
}}}

