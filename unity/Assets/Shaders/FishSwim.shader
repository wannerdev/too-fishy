// Port of res://Shaders/fish_animation.gdshader (pivot, wave and twist vertex animation).
// Unity's model importer mirrors X, so the Godot X coordinate is -v.x here.
Shader "TooFishy/FishSwim"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _EmissionEnergy ("Emission Energy", Float) = 1
        _AnimTime ("Animation Time (set per fish)", Float) = 0
        _Pivot ("Pivot", Float) = 1
        _Wave ("Wave", Float) = 1
        _Twist ("Twist", Float) = 1
        _AxisOffset ("Fish Axis Offset", Float) = 0.5
        _AxisLengthInv ("Fish Axis Length Inv", Float) = 0.5
        _MaskBlack ("Mask Black", Float) = 0.5
        _MaskWhite ("Mask White", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;
        float _EmissionEnergy, _AnimTime, _Pivot, _Wave, _Twist;
        float _AxisOffset, _AxisLengthInv, _MaskBlack, _MaskWhite;

        struct Input { float2 uv_MainTex; };

        void vert(inout appdata_full v)
        {
            float t = _AnimTime;
            float3 p = v.vertex.xyz;
            float gx = -p.x; // Godot-space X
            float body = saturate((gx + _AxisOffset) * _AxisLengthInv);
            float mask = smoothstep(_MaskBlack, _MaskWhite, 1.0 - body);

            float pa = cos(t) * 0.1 * _Pivot;
            float cp = cos(pa), sp = sin(pa);
            // Godot: VERTEX.xz = mat2(vec2(c,-s), vec2(s,c)) * VERTEX.xz (column-major)
            float nx = cp * gx + sp * p.z;
            float nz = -sp * gx + cp * p.z;
            gx = nx; p.z = nz;

            p.z += cos(t + body * 3.0) * _Wave * 0.2 * mask;

            float ta = cos(t + body * 2.5) * 0.3 * _Twist * mask;
            float ct = cos(ta), st = sin(ta);
            float2 yz = float2(p.y, p.z);
            float2 tw = float2(ct * yz.x + st * yz.y, -st * yz.x + ct * yz.y);
            yz = lerp(yz, tw, mask);
            p.y = yz.x; p.z = yz.y;

            p.x = -gx;
            v.vertex.xyz = p;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionEnergy;
            o.Metallic = 0;
            o.Smoothness = 0; // Godot default roughness 1.0
        }
        ENDCG
    }
    FallBack "Diffuse"
}
