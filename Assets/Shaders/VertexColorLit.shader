// Reads the mesh's per-vertex color (COLOR) directly as the surface color, with a
// simple half-lambert light so the model isn't flat-shaded. URP's own Lit/Unlit
// shaders don't sample vertex color at all, and Blender's glTF->FBX round trip
// can't carry a node-graph material (only a flat color), so the AI-generated
// voxel models (Fortal3DS) need this to show their color in-engine at all.
Shader "Fortal/VertexColorLit"
{
    // Emission defaults to black/0 - every existing material on this shader
    // (glass tile frame, farm animals, coins, cars...) renders exactly as
    // before unless something explicitly sets these via a MaterialPropertyBlock
    // (see BossSlayerGameManager.ApplyBossGlow) - this shader itself doesn't
    // pick which objects glow, only makes glowing possible.
    //
    // บาส 2026-09-03: added _Smoothness/_Metallic/_SpecularTint (a simple
    // Blinn-Phong highlight, for "shiny like metal" - CrystalGolem/Sentinel
    // material tuning below) and _Alpha/_SrcBlend/_DstBlend/_ZWrite (for
    // "transparent like glass"). Every new property defaults to a no-op
    // (_Metallic 0 = no specular at all; _SrcBlend One/_DstBlend Zero = the
    // same fully-opaque overwrite as no blending; _ZWrite On = unchanged) so
    // every EXISTING material on this shared shader (all ~40+ of them) keeps
    // rendering pixel-identical unless a specific material's own Inspector
    // values are changed - this file only makes the new looks POSSIBLE, same
    // reasoning the emission properties above already use.
    Properties
    {
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionIntensity ("Emission Intensity", Float) = 0
        // Fresnel rim strength - 1 hugs only the silhouette edge tightly, lower
        // values spread the glow further across each face toward the center.
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        // 0 = no specular highlight at all (default, matches old behavior
        // exactly). Raise both for a "metal" look: Metallic scales how bright
        // the highlight is, Smoothness narrows it into a tight glint instead
        // of a soft sheen.
        _Metallic ("Metallic (specular highlight strength)", Range(0, 1)) = 0
        _Smoothness ("Smoothness (specular highlight tightness)", Range(0, 1)) = 0.5
        _SpecularTint ("Specular Tint", Color) = (1, 1, 1, 1)
        // Multiplies the mesh's own vertex alpha - 1 = fully opaque (default,
        // matches old behavior). Only visually does anything once _SrcBlend/
        // _DstBlend below are ALSO switched to actual alpha blending on a
        // given material - see the "glass" recipe in this file's own comment
        // above Pass{}.
        _Alpha ("Alpha Multiplier (for glass)", Range(0, 1)) = 1
        // One/Zero = fully opaque overwrite, the exact same result as no
        // blending existed at all (every current material's default). Set a
        // specific MATERIAL's own values to SrcAlpha/OneMinusSrcAlpha (and
        // its Render Queue to Transparent, and _ZWrite below to Off) to make
        // just that one material glass-transparent without touching this
        // shader again or any other object on it.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            // Both driven by per-MATERIAL property values (see _SrcBlend/
            // _DstBlend/_ZWrite above) - the SHADER stays generic, only an
            // individual material's own Inspector values decide whether it
            // renders opaque (the default) or glass-transparent.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _RimPower;
                half _Metallic;
                half _Smoothness;
                half4 _SpecularTint;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 normalWS = normalize(IN.normalWS);
                // 0.7/0.3 half-lambert mix so the unlit side of the model still
                // reads as its vertex color instead of going pure black.
                float ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.7 + 0.3;
                // BossFight currently has no directional Sun assigned at runtime; in that case
                // URP's main light is black, so keep the model readable from vertex color alone.
                float3 lighting = max(mainLight.color * ndotl, 0.55);
                float3 lit = IN.color.rgb * lighting;

                // Fresnel rim: ~0 on faces square to the camera (center of the
                // silhouette), ~1 on faces near-perpendicular to view (the edge) -
                // so emission only lights up the outline, not the whole body.
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                float3 emission = _EmissionColor.rgb * _EmissionIntensity * rim;

                // Blinn-Phong specular highlight ("shiny like metal") -
                // _Metallic defaults to 0 on every existing material, which
                // makes this term exactly 0 regardless of the other math, so
                // nothing already using this shader changes unless its own
                // _Metallic is raised above 0. Uses `lighting` (the already-
                // floored-to-0.55 value above), not raw mainLight.color -
                // same reason the diffuse term needed that floor: this scene
                // has no directional Sun assigned at runtime, so
                // mainLight.color is black and a highlight multiplied by it
                // directly would never actually show up.
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                float specAngle = saturate(dot(normalWS, halfDirWS));
                float specPower = lerp(8.0, 128.0, _Smoothness);
                float specular = pow(specAngle, specPower) * _Metallic;
                float3 specularColor = lighting * specular * _SpecularTint.rgb;

                // _Alpha defaults to 1 (fully opaque, unchanged) - only reads
                // as transparency once a material's own _SrcBlend/_DstBlend
                // are ALSO switched to real alpha blending (see Properties
                // block comment above).
                return half4(lit + emission + specularColor, IN.color.a * _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
