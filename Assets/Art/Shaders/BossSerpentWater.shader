Shader "Deepspace/Serpent Water"
{
 Properties { _Clock("Clock",Float)=0 _Power("Power",Float)=1 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent"}
 Pass {
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _Clock,_Power;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float2 uv:TEXCOORD2;};
 V Vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 half4 Frag(V i):SV_Target {
 float2 uv=i.uv;float3 n=normalize(i.n),view=normalize(GetWorldSpaceViewDir(i.w));
 float fresnel=pow(1-saturate(abs(dot(n,view))),3);
 float ripples=sin(uv.x*85+uv.y*13-_Clock*7)*sin(uv.x*39-uv.y*22+_Clock*4);
 float grain=noise(float2(uv.x*60-_Clock*1.8,uv.y*12+_Clock*2));
 float foam=smoothstep(.36,.77,uv.y)*(1-smoothstep(.90,1,uv.y))*smoothstep(.36,.64,grain+ripples*.12);
 float threads=pow(saturate(.5+.5*sin(uv.x*96+uv.y*12-_Clock*10)),12)*smoothstep(.25,.8,uv.y);
 half3 color=lerp(half3(.008,.12,.23),half3(.025,.57,.67),saturate(uv.y+fresnel*.6));
 color+=half3(.12,.5,.58)*fresnel+half3(.08,.17,.19)*ripples;
 color=lerp(color,half3(.92,1.1,1.12),saturate(foam+threads*.45));
 float spec=pow(saturate(dot(reflect(-normalize(float3(.3,.7,-.4)),n),view)),42);
 color+=spec*half3(.7,1,1);
 float edge=smoothstep(0,.055,uv.x)*(1-smoothstep(.945,1,uv.x));
 float alpha=(.65+foam*.3+fresnel*.2)*edge*min(1,_Power);
 return half4(color*(.85+_Power*.15),alpha);
 }
 ENDHLSL
 }
 }
}