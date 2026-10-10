// Port of res://materials/shaders/lava_shader.gdshader: scrolling noise mapped to a
// red-to-yellow hue ramp with a rounded-box alpha mask, driven by world XY.
Shader "TooFishy/Lava"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Scale ("Scale", Float) = 0.2
        _Speed ("Speed", Float) = 1
        _Temperature ("Temperature", Float) = 2.2
        _Brightness ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            float _Scale, _Speed, _Temperature, _Brightness;

            struct v2f { float4 pos : SV_POSITION; float2 world : TEXCOORD0; float2 center : TEXCOORD1; float2 size : TEXCOORD2; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.center = float2(unity_ObjectToWorld[0][3], unity_ObjectToWorld[1][3]);
                o.size = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                return o;
            }

            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = 0.1 * _Time.y;
                float2 v0 = float2(0, t * 2.0) * _Speed;
                float2 v1 = float2(0, t * 0.1) * _Speed;
                float h0 = tex2D(_NoiseTex, i.world * _Scale + v0).r;
                float h1 = tex2D(_NoiseTex, (i.world * _Scale + v1 * 1.23) * 0.4).r;
                float h = pow(h0 * h1, 1.5);
                float hue = lerp(0.0, 0.18, min(h * _Temperature, 1.0));
                float3 c = hsv2rgb(float3(hue, 1, 1)) * _Brightness;
                float l = -sdRoundedBox(i.center - i.world, i.size * 0.5, min(1.0, 0.5 * min(i.size.x, i.size.y)));
                return fixed4(c, smoothstep(0.0, 0.1, l - h));
            }
            ENDCG
        }
    }
}
