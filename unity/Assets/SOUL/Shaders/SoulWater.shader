// Stylized water for the terrain basins (늪 · 빗물 · 기름 · 독성 슬러지). No depth texture needed: the streamer
// writes the true water depth per vertex (uv0.x, metres) and the kind (uv0.y), so colour, opacity and the shore
// line come from the terrain data itself. Reads as water from the top-down camera through:
//   depth colour      shallow tint -> deep body colour (Beer-Lambert style absorption)
//   shore line        bright broken foam / scum band where the depth runs out, slowly breathing
//   moving surface    two scrolling noise normal layers -> sky reflection (fresnel) + sharp sun glints
//   kind              mire: murky green-brown, algae flecks · clean: grey-blue, clearer · toxic: glowing green
//                     with bubbles · oil: black with iridescent sheen
// Colours are linear. Fog matches SOUL/Lit.
Shader "SOUL/Water"
{
    Properties
    {
        _SkyColor ("Sky reflection (linear)", Vector) = (.55,.6,.62,1)
        _Glint ("Sun glint strength", Float) = 2.2
        _Speed ("Flow speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _SoulFogClear;   // same as SOUL/Lit: fog clears around the player
            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColor; float _Glint; float _Speed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; float fogFactor : TEXCOORD2; float tox : TEXCOORD3; };

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            // height field of the surface ripples (two layers drifting in different directions)
            float waves(float2 p, float t)
            {
                return vnoise(p * 1.7 + float2(t * .35, t * .21)) * .6 + vnoise(p * 3.9 - float2(t * .27, -t * .41)) * .4;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pi.positionCS; o.posWS = pi.positionWS;
                o.color = v.color; o.uv = v.uv; o.tox = v.uv1.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                int kind = (int)round(i.uv.y);
                float2 p0 = i.posWS.xz;
                // organic outline: the raw depth comes from a 1 m grid (straight triangle edges), so the shore
                // contour is pushed around by two noise octaves before it drives opacity and foam
                float depth = max(i.uv.x + (vnoise(p0 * .9) - .5) * .06 + (vnoise(p0 * 3.7) - .5) * .025, 0);
                float tw = saturate(i.tox);   // toxic sludge weight inside the mire (continuous, no cell seams)
                float t = _Time.y * _Speed; float2 p = i.posWS.xz;
                float flow = kind == 2 ? .35 : kind == 3 ? .5 : kind == 4 ? .25 : 1;   // mire / toxic / oil move slowly
                float tt = t * flow;
                // normal from the ripple height field
                float e = .06, h = waves(p, tt);
                float2 g = float2(waves(p + float2(e, 0), tt) - h, waves(p + float2(0, e), tt) - h) / e;
                float rough = kind == 1 ? .09 : kind == 4 ? .05 : .13;
                float3 n = normalize(float3(-g.x * rough, 1, -g.y * rough));

                // body colour: vertex colour = deep colour (linear), shallow = bottom showing through
                float3 deep = lerp(i.color.rgb, float3(.02, .1, .005), tw);
                float3 shallow = kind == 1 ? deep * 1.5 + .02 : kind == 3 ? deep * 1.2 : deep * 1.35 + .01;
                float absorb = 1 - exp(-depth * (kind == 1 ? 6 : 10));
                float3 col = lerp(shallow, deep, absorb);
                float alpha = lerp(.6, i.color.a, absorb);

                // reflection first: the surface mirrors the hazy sky, which is what separates water from the
                // equally dark mud around it (wet banks are darkened in GroundColor, water gets lighter)
                float3 V = normalize(GetWorldSpaceViewDir(i.posWS));
                float3 sky = lerp(_SkyColor.rgb, unity_FogColor.rgb, .35);
                float fres = pow(1 - saturate(dot(n, V)), 3);
                float refl = kind == 2 ? lerp(.6, .3, tw) : kind == 3 ? .3 : kind == 4 ? .55 : .55;
                float sheet = .55 + .45 * smoothstep(.25, .75, vnoise(p * .09 + tt * .02));   // broad sky gradients drifting
                col = lerp(col, sky, saturate((.35 + fres) * refl * sheet));

                // moving highlight streaks: thin contour lines of the ripple field (stylized glitter)
                float hw = waves(p * .8 + float2(tt * .12, tt * .05), tt);
                float lineW = fwidth(hw) * 1.5 + .004;
                float streak = smoothstep(.5 - lineW * 3, .5 - lineW, abs(frac(hw * 3.0) - .5));   // 1 on the contour lines only
                streak *= smoothstep(.62, .88, vnoise(p * .35 - tt * .03)) * smoothstep(.5, .8, vnoise(p * .11 + 7.3));   // sparse, in drifting patches
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(n, H)), 120);
                float glint = (streak * .3 + spec) * (kind == 2 ? .55 : kind == 3 ? .3 : kind == 4 ? .8 : 1);
                col += (sky * .6 + L.color * .5) * glint * _Glint * .45;
                alpha = saturate(alpha + glint * .5);

                // murk + floating algae / duckweed rafts (mire), bubbles (toxic), iridescent oil sheen
                if (kind == 2)
                {
                    float m = vnoise(p * .45 + tt * .03) * .65 + vnoise(p * 1.9 - tt * .05) * .35;
                    float raft = smoothstep(.62, .68, m) * smoothstep(.12, .3, depth) * (1 - tw);
                    col = lerp(col, float3(.10, .14, .035), raft * .9);
                    float fleck = smoothstep(.8, .86, vnoise(p * 7.0 + float2(tt * .05, 0)));
                    col = lerp(col, float3(.13, .17, .05), fleck * .55);
                    alpha = max(alpha, raft * .95);
                }
                if (kind == 3 || tw > .01)
                {
                    float w = kind == 3 ? 1 : tw;
                    col = lerp(col, col * .55, smoothstep(.4, .7, vnoise(p * .3 + tt * .04)) * w);   // murky depth variation
                    float2 cell = floor(p * 1.1); float2 fc = frac(p * 1.1) - .5;
                    float life = frac(hash(cell) * 7 + t * (.2 + hash(cell + 3) * .35));
                    float r = .04 + .12 * life;
                    float ring = 1 - smoothstep(.0, .025, abs(length(fc - (hash(cell + 9) - .5) * .5) - r));
                    float bub = ring * step(.5, hash(cell + 17)) * (1 - life);
                    col += float3(.25, .9, .15) * bub * w;
                }
                if (kind == 4)
                {
                    float s = vnoise(p * .35 + tt * .03) * 6.2831 + h * 3;
                    col += (.5 + .5 * cos(s + float3(0, 2.1, 4.2))) * .06 * smoothstep(.3, .7, vnoise(p * .2));
                }

                // shore: a thin broken foam / scum line right at the water's edge (constant pixel width) and a
                // second faint line a little further in that breathes in and out like a lapping ripple
                float dw = fwidth(depth) + 1e-4;
                float edge = 1 - smoothstep(dw * 1.2, dw * 3.5, abs(depth - .03));
                float breath = .5 + .5 * sin(t * 1.1 + vnoise(p * .3) * 6);
                float lap = (1 - smoothstep(dw, dw * 2.5, abs(depth - (.06 + .04 * breath)))) * .45 * (1 - breath * .5);
                float broken = smoothstep(.3, .6, vnoise(p * 4.5 + float2(t * .1, 0)));
                float3 foam = kind == 1 ? float3(.62, .65, .64) : kind == 2 ? lerp(float3(.36, .38, .26), float3(.35, .8, .2), tw) : kind == 3 ? float3(.35, .8, .2) : float3(.1, .095, .09);
                float f = saturate(edge * broken + lap * broken);
                col = lerp(col, foam, f * .8);
                alpha = max(alpha, f * .85);
                alpha *= smoothstep(.0, .02, depth);

                half fk = ComputeFogIntensity(i.fogFactor);
                if (_SoulFogClear.w > 0) fk = lerp(1, fk, smoothstep(_SoulFogClear.w * .45, _SoulFogClear.w, length(i.posWS.xz - _SoulFogClear.xz)));
                col = lerp(unity_FogColor.rgb, col, fk);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
