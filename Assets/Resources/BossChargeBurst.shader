Shader "Deepspace/Boss Charge Burst"
{
    Properties
    {
        [HDR] _Color ("Element", Color) = (0.5,0.2,1,1)
        _Theme ("Theme", Float) = 0
        _Clock ("Cast time", Float) = 0
        _Progress ("Charge", Range(0,1)) = 0
        _Release ("Release", Range(0,1)) = 0
        _Fade ("Visibility", Range(0,1)) = 0
        _Extent ("World aspect", Vector) = (3,1.2,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-11" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Extent;
                float _Theme, _Clock, _Progress, _Release, _Fade;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float filament(float d,float width) { return exp2(-abs(d)/max(width,0.0015)); }
            half4 frag(V i) : SV_Target
            {
                float2 uv=i.uv*2-1;
                float2 p=uv*_Extent.xy;
                float r=length(p), a=atan2(p.y,p.x);
                float q=_Progress, t=_Clock, burst=_Release;
                float released=step(0.0001,burst);
                float twist=0.55, spokes=12, jagged=0;
                if (_Theme < 0.5) { twist=0.85; spokes=10; jagged=sin(r*19-t*8)*0.045; }
                else if (_Theme < 1.5) { twist=0.32; spokes=8; jagged=sin(r*8-t*4)*0.07; }
                else if (_Theme < 2.5) { twist=0.1; spokes=14; jagged=(abs(frac(r*4-t*2)*2-1)-0.5)*0.19; }
                else if (_Theme < 3.5) { twist=1.2; spokes=8; jagged=sin(r*14-t*6)*0.075; }
                else if (_Theme < 4.5) { twist=0; spokes=6; }
                else if (_Theme < 5.5) { twist=-0.85; spokes=10; jagged=sin(r*7+t*5)*0.08; }
                else if (_Theme < 6.5) { twist=0; spokes=12; }
                else { twist=-1.5; spokes=9; jagged=sin(r*11+t*4)*0.035; }

                // Wide energy filaments accelerate towards the boss, with bright moving heads.
                float winding=a+twist*r-t*0.2+jagged;
                float lane=atan2(sin(winding*spokes),cos(winding*spokes))/spokes;
                float strand=filament(lane*r,0.011+q*0.009);
                float section=floor((winding+3.141593)*spokes/6.283185);
                float phase=frac(r*0.63+t*(0.5+q*0.7)+sin(section*17.3)*0.5);
                float head=pow(saturate(1-phase),12);
                float trail=pow(saturate(1-phase),3);
                float feed=strand*(head*2.2+trail*0.4)*smoothstep(0.6,0.92,r)*(1-smoothstep(2.2,3.2,r));
                float haze=filament(lane*r,0.075)*trail*0.065;

                // Suspended sparks form two broken orbital ellipses around the charge.
                float ellipse=length(float2(p.x*0.57,p.y*1.4));
                float orbit=filament(ellipse-(0.84+0.05*sin(a*3+t*2)),0.009)
                    *pow(saturate(sin(a*7-t*3)),4);
                float crowns=filament(ellipse-1.02,0.018)*pow(saturate(cos(a*22+t*2)),28);
                float charge=(feed+haze+orbit*0.65+crowns*0.45)*lerp(0.16,1.3,q*q)*(1-released);

                // Three expanding fronts carry the release far beyond the standing halo.
                float shockDistance=length(float2(p.x*0.60,p.y*1.15));
                if (_Theme > 3.5 && _Theme < 4.5)
                {
                    float sector=atan2(sin(a*6),cos(a*6))/6;
                    shockDistance=length(float2(p.x*0.60,p.y))*cos(sector);
                }
                if (_Theme > 5.5 && _Theme < 6.5) shockDistance=max(abs(p.x)*0.58,abs(p.y)*1.1);
                float wave0=filament(shockDistance-lerp(0.25,2.4,sqrt(burst)),0.026)*(1-burst);
                float wave1=filament(shockDistance-lerp(0.22,1.9,saturate((burst-0.09)*1.3)),0.014)
                    *smoothstep(0.09,0.15,burst)*(1-burst);
                float wave2=filament(shockDistance-lerp(0.2,1.5,saturate((burst-0.22)*1.5)),0.018)
                    *smoothstep(0.22,0.29,burst)*(1-burst);
                float fracture=pow(saturate(cos((a+jagged+twist*r*(1-burst))*spokes*1.5)),28);
                float front=lerp(0.6,3.4,burst);
                float shards=fracture*filament(r-front,0.18+burst*0.3)*(1-burst);
                float horizontal=filament(p.y+sin(p.x*8+t*5)*0.006,0.014)
                    *exp2(-abs(p.x)*0.6)*exp2(-burst*12);
                float flash=exp2(-abs(ellipse-0.8)*6)*exp2(-burst*18)*0.45;
                float impact=(wave0*2.2+wave1*1.3+wave2+shards*1.8+horizontal*1.2+flash)*released;
                float edge=(1-smoothstep(0.85,1,abs(uv.x)))*(1-smoothstep(0.83,1,abs(uv.y)));
                float clearBody=smoothstep(0.48,0.77,r);
                half3 color=_Color.rgb*(charge*1.5+impact*1.8)
                    +lerp(_Color.rgb,half3(1,1,1),0.55)*(wave0*0.45+horizontal*0.5)*released;
                return half4(color*edge*clearBody*_Fade,0);
            }
            ENDHLSL
        }
    }
}
