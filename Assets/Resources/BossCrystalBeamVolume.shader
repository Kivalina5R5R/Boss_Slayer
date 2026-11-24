Shader "Deepspace/Crystal Beam Volume"
{
 Properties { _CoreTint("Core Tint",Color)=(2.2,1.3,1.15,1) _Tint("Tint",Color)=(1,.025,.06,1) _Clock("Clock",Float)=0 _Power("Power",Float)=1 _Layer("Layer",Float)=0 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha One ZWrite Off Cull Back
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Tint,_CoreTint;float _Clock,_Power,_Layer;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float2 uv:TEXCOORD2;half4 c:COLOR;};
   V Vert(A v){V o;o.w=TransformObjectToWorld(v.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(v.n);o.uv=v.uv;o.c=v.c;return o;}
   half4 Frag(V v):SV_Target
   {
    float facing=saturate(abs(dot(normalize(v.n),normalize(GetWorldSpaceViewDir(v.w)))));
    float endFade=smoothstep(0,.025,v.uv.y)*(1-smoothstep(.96,1,v.uv.y));
    float flow=.5+.5*sin(v.uv.y*85-_Clock*27+sin(v.uv.x*37+_Clock*4)*2.5);
    float threads=pow(saturate(.5+.5*sin(v.uv.x*55+v.uv.y*31-_Clock*17)),7);
    half3 color;float alpha;
    if(_Layer<.5){color=lerp(_Tint.rgb*2,_CoreTint.rgb,pow(facing,2));alpha=(.6+.3*flow)*endFade;}
    else if(_Layer<1.5){color=_Tint.rgb*(1.4+threads*2)+half3(1,.25,.1)*threads;color*=.65+.35*facing;alpha=(.18+.4*threads+.16*flow)*endFade;}
    else if(_Layer<2.5){color=_Tint.rgb*1.2;alpha=pow(facing,2)*(.06+.09*flow)*endFade;}
    else {color=lerp(_Tint.rgb,half3(1.5,.6,.3),facing)*v.c.rgb;alpha=v.c.a*(.65+.35*facing);}
    return half4(color,alpha*_Power);
   }
   ENDHLSL
  }
 }
}
