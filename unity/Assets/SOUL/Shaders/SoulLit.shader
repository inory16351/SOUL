// Lit, vertex-coloured PBR for the imported procedural models (three.js MeshStandardMaterial with
// vertexColors + flatShading: flat normals are baked into the mesh by the importer).
// Colours are set with Material.SetVector (linear values, may exceed 1), never SetColor.
Shader "SOUL/Lit"
{
    Properties
    {
        _BaseColor ("Base Color (linear)", Vector) = (1,1,1,1)
        _EmissionColor ("Emission (linear HDR)", Vector) = (0,0,0,0)
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _UseVertexColor ("Use Vertex Color", Float) = 1
        _Cutaway ("Top-down cutaway (world props)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EmissionColor;
            float _Metallic;
            float _Smoothness;
            float _UseVertexColor;
            float _Cull;
            float _Cutaway;
        CBUFFER_END
        // top-down cutaway: geometry above the player's head near the player is dithered out
        float4 _SoulCutCenter;   // xyz = player feet (world), w = radius
        float4 _SoulCutParams;   // x = height above feet where cutting starts, y = fade width
        // fog clears around the player (RegionAtmosphere sets it): the play area stays crisp, distance stays hazy
        float4 _SoulFogClear;    // xyz = player (world), w = clear radius (0 = off)
        half3 SoulMixFog(half3 c, float fogFactor, float3 positionWS)
        {
            half k = ComputeFogIntensity(fogFactor);   // 1 = no fog
            if (_SoulFogClear.w > 0) k = lerp(1, k, smoothstep(_SoulFogClear.w * .45, _SoulFogClear.w, length(positionWS.xz - _SoulFogClear.xz)));
            return lerp(unity_FogColor.rgb, c, k);
        }
        // purification wave (boss kill, SoulGame BossPurify): the zone is already rebuilt healed; outside the expanding
        // ring it still reads as ashen grey, and the ring front glows
        float4 _SoulHeal;        // xz = wave centre (world), y = radius, w = 1 while the wave runs
        half3 SoulHealWave(half3 c, float3 positionWS)
        {
            if (_SoulHeal.w <= 0) return c;
            float d = length(positionWS.xz - _SoulHeal.xz);
            half outside = smoothstep(_SoulHeal.y - 5, _SoulHeal.y + 3, d);
            half g = dot(c, half3(.3, .55, .15));
            c = lerp(c, g * half3(.86, .83, .8) * .82, outside);
            half band = saturate(1 - abs(d - _SoulHeal.y) / 14);
            half inner = saturate(1 - (_SoulHeal.y - d) / 60) * (1 - outside);   // fresh green just behind the front
            return c * (1 + inner * half3(-.05, .25, -.05)) + half3(.6, 1.1, .7) * band * band * 2.4;
        }
        void SoulCutaway(float3 positionWS, float4 positionCS)
        {
            if (_Cutaway < .5 || _SoulCutCenter.w <= 0) return;
            float above = positionWS.y - (_SoulCutCenter.y + _SoulCutParams.x);
            if (above <= 0) return;
            float d = length(positionWS.xz - _SoulCutCenter.xz);
            float fade = saturate((_SoulCutCenter.w - d) / max(.001, _SoulCutParams.y)) * saturate(above / .35);
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            uint2 px = (uint2)positionCS.xy % 4;
            clip((bayer[px.y * 4 + px.x] + .5) / 16 - fade);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 color : COLOR; float fogFactor : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = lerp(float4(1,1,1,1), v.color, _UseVertexColor);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                SoulCutaway(i.positionWS, i.positionCS);
                float3 n = normalize(i.normalWS); n = front ? n : -n;
                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.normalWS = n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor; d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1,1,1,1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = _BaseColor.rgb * i.color.rgb; s.metallic = _Metallic; s.smoothness = _Smoothness;
                s.occlusion = 1; s.alpha = 1; s.emission = _EmissionColor.rgb;
                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = SoulHealWave(c.rgb, i.positionWS);
                c.rgb = SoulMixFog(c.rgb, i.fogFactor, i.positionWS);
                return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert (Attributes v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - p);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, ld));
            #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
            #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return c;
            }
            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V2 { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            V2 vert (float4 positionOS : POSITION) { V2 o; o.positionWS = TransformObjectToWorld(positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }
            half frag (V2 i) : SV_Target { SoulCutaway(i.positionWS, i.positionCS); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag (Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
