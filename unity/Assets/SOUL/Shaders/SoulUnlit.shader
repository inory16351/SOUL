// Unlit colour x vertex colour x texture, configurable blending. Used for the sky dome, neon
// edge tubes, the LED face, telegraphs and every VFX (additive: _SrcBlend One, _DstBlend One).
// HDR colours (>1) feed the URP bloom. Colours are linear; set them with SetVector.
Shader "SOUL/Unlit"
{
    Properties
    {
        _BaseColor ("Color (linear HDR, a = opacity)", Vector) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _UseFog ("Use Fog", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float _UseVertexColor;
                float _UseFog;
                float _SrcBlend, _DstBlend, _Cull, _ZWrite, _ZTest;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = lerp(float4(1,1,1,1), v.color, _UseVertexColor);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(o.positionCS.z) ;
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 c = _BaseColor * i.color * t;
                // additive: fade by alpha in colour; alpha blend keeps alpha
                if (_DstBlend == 1) c.rgb *= c.a;
                if (_UseFog > .5) c.rgb = _DstBlend == 1 ? c.rgb * ComputeFogIntensity(i.fogFactor) : MixFog(c.rgb, i.fogFactor);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
