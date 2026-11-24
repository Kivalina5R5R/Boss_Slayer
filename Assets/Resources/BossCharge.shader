Shader "Deepspace/Boss Charge"
{
    Properties
    {
        [HDR] _Color ("Element", Color) = (0.5,0.2,1,1)
        _Theme ("Theme", Float) = 0
        _Clock ("Cast time", Float) = 0
        _Progress ("Charge", Range(0,1)) = 0
        _Release ("Release", Range(0,1)) = 0
        _Fade ("Visibility", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
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
                float _Theme, _Clock, _Progress, _Release, _Fade;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float stroke(float distance, float width)
            {
                return 1-smoothstep(width, width+max(fwidth(distance),0.0015),abs(distance));
            }
            float ring(float r,float radius,float width) { return stroke(r-radius,width); }
            half4 frag(V i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float r=length(p), a=atan2(p.y,p.x);
                float t=_Clock, q=_Progress;
                float turn=t*(0.5+q*0.65);
                float core=0, mist=0, detail=0;
                float radial=smoothstep(0.27,0.48,r)*(1-smoothstep(0.86,0.98,r));

                if (_Theme < 0.5) // Demon: violet hellfire tongues and a six-point binding seal.
                {
                    float flame=sin(a*13+t*5-r*23)+0.45*sin(a*27-t*8+r*16);
                    float edge=0.63+0.065*flame+0.035*sin(a*6-turn);
                    core=ring(r,edge,0.009)*(0.55+0.45*sin(a*7+t*4));
                    mist=exp2(-abs(r-edge)*24)*0.35;
                    float star=0.68+0.075*cos(a*6-turn);
                    detail=ring(r,star,0.004)*0.8+ring(r,0.81,0.003)*pow(saturate(cos(a*36+turn)),8);
                }
                else if (_Theme < 1.5) // Giant fish: layered tidal crescents and rising bubble pearls.
                {
                    float swell=0.027*sin(a*5-turn*3)+0.018*sin(a*9+t);
                    core=ring(r,0.66+swell,0.011)*smoothstep(-0.4,0.8,sin(a*3-turn));
                    detail=ring(r,0.75-swell,0.004)+ring(r,0.55+swell,0.004)*0.6;
                    mist=exp2(-abs(r-0.67-swell)*22)*0.3;
                    float sector=atan2(sin(a*14+t),cos(a*14+t))/14;
                    float2 bubble=float2((r-0.84)*1.15,sector*0.84);
                    detail+=stroke(length(bubble)-0.018,0.003);
                }
                else if (_Theme < 2.5) // Jellyfish: pulsing bell membrane with branching electric tendrils.
                {
                    float pulse=sin(t*7-r*18);
                    float bolt=0.66+0.035*sin(a*19+t*9)+0.017*sin(a*47-t*13);
                    core=ring(r,bolt,0.006)*(0.7+0.3*pulse);
                    float spoke=sin(a*9+turn+sin(r*31-t*7)*0.18);
                    detail=stroke(spoke,0.019)*smoothstep(0.43,0.59,r)*(1-smoothstep(0.75,0.86,r));
                    detail+=ring(r,0.79+sin(a*9+turn)*0.022,0.003)*0.65;
                    mist=exp2(-abs(r-bolt)*27)*0.45;
                }
                else if (_Theme < 3.5) // Dragon: green flame claws curling into a breath vortex.
                {
                    float curl=a*3-r*12+turn*3;
                    float flames=pow(saturate(sin(curl+0.22*sin(a*17+t*6))),18);
                    core=flames*smoothstep(0.38,0.62,r)*(1-smoothstep(0.72,0.87,r));
                    detail=ring(r,0.7+0.035*sin(a*15+t*5-r*8),0.008)*0.7;
                    mist=pow(saturate(sin(curl)),4)*exp2(-abs(r-0.65)*12)*0.3;
                }
                else if (_Theme < 4.5) // Crystal golem: counter-rotating hexagonal facets and orbiting shards.
                {
                    float sector=atan2(sin((a-turn*0.45)*6),cos((a-turn*0.45)*6))/6;
                    float hex=r*cos(sector);
                    core=stroke(hex-0.62,0.007);
                    float other=atan2(sin((a+turn*0.3)*6),cos((a+turn*0.3)*6))/6;
                    detail=stroke(r*cos(other)-0.72,0.003);
                    float shard=abs(r-0.83)/0.055+abs(sector)/0.034;
                    detail+=stroke(shard-1,0.09)*1.2;
                    mist=exp2(-abs(hex-0.62)*30)*0.35;
                }
                else if (_Theme < 5.5) // Sea serpent: braided undertow spirals, foam and storm crests.
                {
                    float spiral=a*4+r*17-turn*4;
                    core=pow(saturate(sin(spiral)),28)*smoothstep(0.38,0.58,r)*(1-smoothstep(0.75,0.88,r));
                    detail=ring(r,0.77+0.035*sin(a*8+turn*3),0.005);
                    detail+=pow(saturate(cos(a*32-r*12+t*2)),20)*ring(r,0.83,0.02)*0.7;
                    mist=pow(saturate(sin(spiral-0.3)),4)*exp2(-abs(r-0.64)*14)*0.4;
                }
                else if (_Theme < 6.5) // Sentinel: segmented reactor, locking brackets and a charging dial.
                {
                    float sector=atan2(sin((a-turn*0.35)*8),cos((a-turn*0.35)*8))/8;
                    float segment=1-smoothstep(0.27,0.32,abs(sector));
                    core=ring(r,0.67,0.016)*segment;
                    detail=ring(r,0.78,0.004)*segment;
                    detail+=stroke(abs(sector)-0.27,0.006)*smoothstep(0.61,0.64,r)*(1-smoothstep(0.76,0.8,r));
                    float dial=step(frac((a+3.141593)/6.283185),q);
                    detail+=ring(r,0.84,0.009)*pow(saturate(cos(a*48)),10)*dial;
                    mist=exp2(-abs(r-0.67)*30)*0.28;
                }
                else // Void wraith: broken gravitational arcs and inward-folding dimensional threads.
                {
                    float warp=a*3+log(max(r,0.08))*9+turn*3;
                    core=pow(saturate(sin(warp)),26)*smoothstep(0.32,0.5,r)*(1-smoothstep(0.75,0.88,r));
                    detail=ring(r,0.75+sin(a*7-turn)*0.025,0.004)*smoothstep(-0.3,0.2,sin(a*5+t));
                    detail+=ring(r,0.52+sin(a*3+t)*0.018,0.005)*0.65;
                    mist=pow(saturate(sin(warp-0.35)),5)*exp2(-abs(r-0.59)*15)*0.35;
                }
                float gathering=ring(r,lerp(0.91,0.48,q),0.006)*0.55;
                gathering+=ring(r,lerp(0.98,0.57,q*q),0.003)*0.35;
                float release=ring(r,lerp(0.46,0.96,_Release),0.009)*step(0.001,_Release)*(1-_Release)*1.5;
                float crescendo=1+smoothstep(0.65,1,q)*0.3;
                float energy=((core+detail*0.7+gathering)*lerp(0.38,1.25,q)+mist*lerp(0.35,0.85,q))
                    *crescendo*exp2(-_Release*10);
                half3 rgb=_Color.rgb*(energy*1.7+release)+lerp(_Color.rgb,half3(1,1,1),0.65)*(core*0.28+release*0.35);
                return half4(rgb*radial*_Fade,0);
            }
            ENDHLSL
        }
    }
}
