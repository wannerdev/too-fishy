// Lava for the Unity port, written from scratch (not a port of the Godot lava_shader).
// Fully procedural, so it needs no texture and works the same on desktop, phones and WebGL:
//   * a dark cooled crust that drifts slowly upwards (domain-warped fractal noise),
//   * glowing magma seams where the noise crosses its mid value (ridged noise),
//   * large hot regions that breathe, with white-yellow cores where seams and heat meet,
//   * a noisy, softly glowing edge instead of a hard quad border.
// The pattern lives in world XY so neighbouring lava quads line up. Alpha comes from a rounded
// box in the quad's own space, so rotated and stretched quads fade out evenly.
// _NoiseTex, _Temperature and _Brightness are kept so the material that the Godot asset sync
// generated keeps working: _Scale and _Speed still drive the pattern, _Temperature (2.2 by
// default) biases how much of the surface is hot and _Brightness scales the final colour.
Shader "TooFishy/Lava"
{
    Properties
    {
        _NoiseTex ("Unused (kept for old materials)", 2D) = "gray" {}
        _CrustColor ("Crust", Color) = (0.32, 0.045, 0.008, 1)
        _MagmaColor ("Magma", Color) = (1.0, 0.36, 0.03, 1)
        _CoreColor ("Hot core", Color) = (1.0, 0.90, 0.42, 1)
        _Scale ("Pattern scale", Float) = 0.2
        _Speed ("Flow speed", Float) = 1
        _Temperature ("Heat", Float) = 2.2
        _Brightness ("Brightness", Float) = 1
        _EdgeSoftness ("Edge softness (m)", Float) = 0.8
    }
    SubShader
    {
        // Queue 3000, before the translucent rock overlays (3001), as in Godot: lava_shader_material has
        // render_priority 0 and the section.tscn overlay render_priority 1, so the overlay covers lava
        // quads that lie behind a rock face from the camera's view (the shallow quads at 20 m and 40 m,
        // which Godot shows only as a faint red tint). Unlit and no fog pass: open lava stays bright
        // however deep the camera is.
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _CrustColor, _MagmaColor, _CoreColor;
            float _Scale, _Speed, _Temperature, _Brightness, _EdgeSoftness;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 world : TEXCOORD0;   // world XY, the pattern coordinate
                float2 local : TEXCOORD1;   // metres from the quad centre, in the quad's own axes
                float2 half2 : TEXCOORD2;   // half extent of the quad in metres
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                float2 size = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                o.local = v.vertex.xy * size;
                o.half2 = size * 0.5;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                v += a * vnoise(p); p = p * 2.03 + float2(17.1, 9.2); a *= 0.5;
                v += a * vnoise(p); p = p * 2.03 + float2(17.1, 9.2); a *= 0.5;
                v += a * vnoise(p); p = p * 2.03 + float2(17.1, 9.2); a *= 0.5;
                v += a * vnoise(p);
                return v / 0.9375;
            }

            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.world * (_Scale * 4.5);

                // slow upwelling plus a domain warp, so the seams crawl and merge like real flow
                float2 q = p + float2(0.0, -t * 0.10);
                float2 warp = float2(fbm(q + float2(3.1, 1.7) + t * 0.06),
                                     fbm(q + float2(8.3, 2.8) - t * 0.05)) - 0.5;
                float2 r = q + warp * 1.7;
                float n = fbm(r * 1.3);

                float ridge = 1.0 - abs(n * 2.0 - 1.0);
                float seams = pow(saturate(ridge), 5.0);
                float heat = fbm(r * 0.55 + float2(0.0, t * 0.04));
                float breathe = 0.85 + 0.15 * sin(t * 1.7 + heat * 8.0);
                float hotBias = saturate((_Temperature - 1.0) * 0.18);
                float hot = saturate(heat * 1.5 + hotBias - 0.32);

                // the crust is dark orange, never black: seams and hot regions burn through it
                float glow = saturate(seams * 1.5 + hot * 0.5);
                float3 col = lerp(_CrustColor.rgb, _MagmaColor.rgb, glow);
                col += _MagmaColor.rgb * seams * (0.6 + hot) * breathe * 1.1;
                col = lerp(col, _CoreColor.rgb, saturate(seams * (0.35 + hot) * 1.6));
                col *= _Brightness * 1.2;
                // edge: noisy outline with a glowing rim of fresh magma
                float d = -sdRoundedBox(i.local, i.half2, min(1.0, 0.5 * min(i.half2.x, i.half2.y) * 2.0));
                float soft = max(0.05, min(_EdgeSoftness, 0.35 * min(i.half2.x, i.half2.y)));
                float edge = d + (n - 0.5) * soft * 1.4;
                float alpha = smoothstep(0.0, soft, edge); // 1 everywhere except the ragged rim
                float rim = 1.0 - smoothstep(0.0, soft * 2.0, edge);
                col = lerp(col, _MagmaColor.rgb * 1.6 + _CoreColor.rgb * 0.2, rim * 0.55);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
