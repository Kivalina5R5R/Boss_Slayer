Shader "Deepspace/Wraith Purple Mist"
{
 Properties { _Clock("Clock",Float)=0 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent"}
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float _Clock;
   CBUFFER_END
   struct A{float4 p:POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;};
   V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 Frag(V i):SV_Target
   {
    float2 p=i.uv*2-1;float r=length(p);
    float2 drift=float2(_Clock*.17,-_Clock*.23);
    float n=noise(p*3+drift)*.57+noise(p*6.2-drift*.8)*.28+noise(p*12.7+drift)*.15;
    float cloud=smoothstep(.22,.65,n)*(1-smoothstep(.35,1,r));
    float rim=pow(saturate(n+.2),3);
    half3 tint=lerp(half3(.045,.006,.075),half3(.40,.075,.62),rim);
    return half4(tint*i.c.rgb*1.7,cloud*i.c.a);
   }
   ENDHLSL
  }
 }
}
