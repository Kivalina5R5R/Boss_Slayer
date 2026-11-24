Shader "Deepspace/Boss Model Focus"
{
    Properties { _Theme("Theme",Float)=0 _Charge("Charge",Float)=0 _Release("Release",Float)=-1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Theme, _Charge, _Release;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V vert(A i){ V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float t=_Time.y,r=length(p),d;
                if(_Theme<.5||(_Theme>2.5&&_Theme<3.5))
                {
                    float sway=sin(p.y*7-t*10)*.11+sin(p.y*13+t*6)*.05;
                    d=max(abs(p.x+sway)/max(.08,.5-p.y*.33),abs(p.y+.1)/.9);
                }
                else if(_Theme<1.5||(_Theme>4.5&&_Theme<5.5)||_Theme>7.5)
                {
                    float ring=abs(r-(.55+.04*sin(t*7)))*5;
                    float orb=length(p-float2(sin(t*3),cos(t*3))*.42)*4;
                    d=min(ring,orb);
                }
                else if(_Theme<2.5)
                {
                    float bolt=abs(p.x-sin(p.y*16+floor(t*15))*.14)*7;
                    float cross=abs(p.y-sin(p.x*12-floor(t*15))*.12)*8;
                    d=max(min(bolt,cross),r*1.1);
                }
                else if(_Theme<4.5) d=abs(p.x)*1.6+abs(p.y)*1.05;
                else if(_Theme<6.5) d=min(max(abs(p.x)*8,abs(p.y)*1.05),abs(r-.55)*10);
                else d=r*(1.35+.2*sin(p.x*12+t*3)*sin(p.y*9-t*2));
                float edge=1-smoothstep(.8,1.05,d);
                float inner=1-smoothstep(.05,.78,d);
                float pulse=1+smoothstep(.7,1,_Charge)*(.12+.12*sin(t*28));
                // A saturated dark border keeps the element readable against bright scenery.
                half3 rgb=i.color.rgb*lerp(.22,1.65,inner)*pulse;
                rgb+=inner*inner*.22;
                float fade=_Release<0?1:1-smoothstep(.25,1,_Release);
                return half4(rgb,edge*(.65+.2*_Charge)*fade);
            }
            ENDHLSL
        }
    }
}
