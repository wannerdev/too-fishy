// Port of res://Shaders/mind_control_effect.gdshader: a twisting, tearing purple rift sphere.
Shader "TooFishy/MindControl"
{
    Properties
    {
        _PullStrength ("Pull Strength", Float) = 1
        _ColorTint ("Color Tint", Color) = (0.8, 0.3, 0.9, 0.6)
        _TimeSpeed ("Time Speed", Float) = 1
        _CustomTime ("Custom Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _PullStrength, _TimeSpeed, _CustomTime;
            fixed4 _ColorTint;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                float t = _CustomTime * _TimeSpeed;
                float3 p = v.vertex.xyz;
                float d = length(p);
                float tear = sin(t * 3.0 + d * 8.0) * 0.5 + 0.5;
                float riftAngle = atan2(p.z, p.x) + t * 0.5;
                float rift = sin(riftAngle * 6.0 + t * 2.0) * _PullStrength * 0.1;
                float twist = d * _PullStrength * 0.2;
                float c = cos(twist + t), s = sin(twist + t);
                float3 q = p;
                q.x = p.x * c - p.z * s;
                q.z = p.x * s + p.z * c;
                q += v.normal * rift * tear;
                v2f o;
                o.pos = UnityObjectToClipPos(float4(q, 1));
                o.uv = v.texcoord.xy;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }
            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.0; a *= 0.5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _CustomTime * _TimeSpeed;
                float2 r = i.uv - 0.5;
                float dist = length(r);
                float angle = atan2(r.y, r.x);
                float tears = 0;
                for (float k = 0; k < 6; k++)
                {
                    float ta = (k / 6.0) * 6.28318 + t * 0.3;
                    float diff = abs(angle - ta);
                    diff = min(diff, 6.28318 - diff);
                    tears += 1.0 / (1.0 + diff * 20.0) * (0.5 + 0.5 * sin(t * 4.0 + k));
                }
                float2 nuv = i.uv * 8.0 + t * 0.5;
                float chaos = fbm(nuv) * fbm(nuv + float2(t * 0.7, t * 0.3));
                float voidI = (1.0 - smoothstep(0.0, 0.3, dist)) * (0.7 + 0.3 * sin(t * 6.0));
                float rings = (sin(dist * 25.0 - t * 8.0) * 0.5 + 0.5) * (sin(dist * 40.0 + t * 5.0) * 0.5 + 0.5);
                rings = rings * rings;
                float lightning = 0;
                float2 luv = i.uv * 15.0;
                for (float m = 0; m < 3; m++)
                {
                    float2 off = float2(sin(t * 8.0 + m * 2.0), cos(t * 6.0 + m * 1.5)) * 0.1;
                    float bolt = abs(sin(luv.x + off.x + t * 10.0)) * abs(sin(luv.y + off.y + t * 8.0));
                    lightning += pow(bolt, 8.0) * (0.5 + 0.5 * sin(t * 12.0 + m));
                }
                float3 col = _ColorTint.rgb;
                col = lerp(col, float3(0.9, 0.2, 0.7), tears * 0.6);
                col = lerp(col, float3(0.2, 0.8, 0.9), chaos * 0.4);
                col = lerp(col, float3(0.8, 0.9, 0.2), lightning * 0.8);
                col = lerp(col, float3(0.1, 0.05, 0.2), voidI * 0.7);
                float total = (tears + chaos * 0.5 + rings * 0.3 + lightning + voidI) * _PullStrength;
                float edge = 1.0 - smoothstep(0.6, 1.0, dist);
                float pulse = 0.8 + 0.2 * sin(t * 4.0);
                float alpha = saturate(_ColorTint.a * total * edge * pulse);
                // unshaded: albedo plus emission
                return fixed4(col * (1.5 + lightning * 0.5), alpha);
            }
            ENDCG
        }
    }
}
