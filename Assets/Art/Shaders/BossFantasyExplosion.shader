Shader "Deepspace/Boss Fantasy Explosion"
{
    Properties
    {
        _Color ("Element", Color) = (0.5,0.2,1,1)
        _Theme ("Theme", Float) = 0
        _Progress ("Lifetime", Range(0,1)) = 0
        _Seed ("Variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Theme, _Progress, _Seed;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float fill(float d,float aa) { return 1-smoothstep(-aa,aa,d); }
            half4 frag(V i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float t=_Progress, r=length(p), a=atan2(p.y,p.x)+_Seed;
                float aa=max(fwidth(r),.002);
                float pop=1-pow(1-saturate(t*4),3);
                float fade=1-smoothstep(.30,1,t);
                float3 dark=_Color.rgb*.26+float3(.035,.016,.07);
                float3 light=lerp(_Color.rgb,float3(1,.97,.78),.78);
                float3 rgb=0; float alpha=0;
                // A broken expanding shock ring, painted with an ink-like outer edge.
                float ringRadius=.16+.74*(1-pow(1-t,2));
                float rd=abs(r-ringRadius)-.019*(1-t);
                float ring=fill(rd,aa)*(1-smoothstep(.4,.85,t))*step(-.45,sin(a*3+t*4));
                rgb=lerp(_Color.rgb,light,.55); alpha=ring*.85;
                // Individual puffs separate as they travel, reopening the boss silhouette.
                float d=r-.28*pop*(1-smoothstep(.08,.35,t));
                float highlight=0;
                [unroll] for(int puff=0;puff<10;puff++)
                {
                    float angle=puff*6.283185/10+_Seed;
                    float variation=frac(puff*.618+.17);
                    float travel=(.20+.35*t)*pop*(.9+variation*.16);
                    float2 center=float2(cos(angle),sin(angle))*travel;
                    float2 q=p-center;
                    float puffRadius=(.12+variation*.035)*pop*(1-smoothstep(.18,1,t)*.7);
                    float dist=length(q);
                    if (abs(_Theme-4)<.1 || abs(_Theme-6)<.1)
                    {
                        float angleQ=atan2(q.y,q.x);
                        dist*=cos(atan2(sin(angleQ*6),cos(angleQ*6))/6);
                    }
                    d=min(d,dist-puffRadius);
                    highlight=max(highlight,fill(length(q-float2(-.018,.027))-puffRadius*.56,aa));
                }
                float cloud=fill(d,aa)*fade;
                float inner=fill(d+.014,aa);
                float3 cloudColor=lerp(_Color.rgb*.8,lerp(_Color.rgb,light,.65),highlight);
                cloudColor=lerp(dark,cloudColor,inner);
                rgb=lerp(rgb,cloudColor,cloud);alpha=max(alpha,cloud);
                // Cream star core snaps open, then leaves a colored puff and separate stars.
                float spikes=(_Theme==4?6:8);
                float starRadius=(.12+.26*pow(abs(cos(a*spikes*.5)),10))*pop*(1-smoothstep(.10,.45,t));
                float star=fill(r-starRadius,aa)*fade;
                float core=fill(r-starRadius+.018,aa);
                rgb=lerp(rgb,lerp(_Color.rgb*.55,light*1.6,core),star);alpha=max(alpha,star);
                // Twelve moving four-point stars / diamond shards, with dark outlines.
                [unroll] for(int j=0;j<12;j++)
                {
                    float angle=j*2.399963+_Seed;
                    float speed=.55+frac(j*.618)*.35;
                    float2 center=float2(cos(angle),sin(angle))*(.22+t*speed);
                    center.y-=t*t*.14;
                    float2 q=abs(p-center);
                    float size=(.022+frac(j*.371)*.021)*sin(saturate(t*3)*1.5708)*(1-t*.65);
                    float shape=(q.x+q.y)*.66+min(q.x,q.y)*1.7;
                    float shard=fill(shape-size,aa)*fade;
                    float inside=fill(shape-size+.009,aa);
                    float3 tint=lerp(_Color.rgb,light,frac(j*.41));
                    rgb=lerp(rgb,lerp(dark,tint*1.25,inside),shard);alpha=max(alpha,shard);
                }
                alpha*=smoothstep(0,.018,t);
                return half4(rgb,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
