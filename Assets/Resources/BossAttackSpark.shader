Shader "Deepspace/Boss Attack Spark"
{
    Properties { _Theme ("Theme", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Theme;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p),body;
                if(_Theme>7.5)body=exp2(-abs(r-.6)*22)+exp2(-length(p-float2(-.2,.25))*12)*.5;
                else if(_Theme>6.5)body=exp2(-r*r*4)*saturate(1-r)*(.65+.35*sin(p.x*8+_Time.y*2)*sin(p.y*7-_Time.y));
                else if(_Theme>5.5)body=exp2(-abs(p.x)*18)*saturate(1-abs(p.y));
                else if(_Theme>4.5||(_Theme>.5&&_Theme<1.5))body=exp2(-abs(r-.65)*24)+exp2(-r*7)*.2;
                else if(_Theme>3.5){float d=abs(p.x)*1.8+abs(p.y);body=saturate(1-d)*(.5+step(0,p.x)*.5);}
                else if(_Theme>2.5||_Theme<.5)
                {float width=.12+(.9-i.uv.y)*.55;float flame=abs(p.x+sin(p.y*6+_Time.y*8)*.12)/max(.08,width);body=saturate(1-flame)*saturate(1-abs(p.y));}
                else {float bolt=abs(p.x-sin(p.y*12)*.15);body=exp2(-bolt*34)*saturate(1-abs(p.y));}
                return half4(i.color.rgb*1.8+body*.25,saturate(body)*i.color.a);
            }
            ENDHLSL
        }
    }
}
