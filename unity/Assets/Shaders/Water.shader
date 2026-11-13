// Approximation of res://Shaders/Water.gdshader for the surface plane: fresnel blend of the two
// albedo colours, two scrolling normal maps (world XZ), very low roughness, constant alpha.
// The Godot version also refracts the screen texture; that is omitted here for mobile.
Shader "TooFishy/Water"
{
    Properties
    {
        _Albedo ("Albedo", Color) = (0, 0.3216, 0.4314, 1)
        _Albedo2 ("Albedo 2", Color) = (0, 0.4745, 0.7647, 1)
        _ColorShallow ("Shallow", Color) = (0, 0.5529, 0.651, 1)
        _NormalMap ("Normal", 2D) = "bump" {}
        _WaveDir ("Wave Direction", Vector) = (0, 1, 0, 1)
        _TimeScale ("Time Scale", Float) = 0.025
        _Smoothness ("Smoothness", Range(0,1)) = 0.98
        _Transparency ("Transparency", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        fixed4 _Albedo, _Albedo2, _ColorShallow;
        sampler2D _NormalMap;
        float4 _WaveDir;
        float _TimeScale, _Smoothness, _Transparency;

        struct Input { float3 worldPos; float3 viewDir; };

        void vert(inout appdata_full v) { }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 t = _Time.y * _WaveDir.xy * _TimeScale;
            float2 uv = IN.worldPos.xz;
            float3 n1 = UnpackNormal(tex2D(_NormalMap, uv + t));
            float3 n2 = UnpackNormal(tex2D(_NormalMap, uv * 0.7 - t));
            o.Normal = normalize(lerp(n1, n2, 0.5));
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 5.0);
            o.Albedo = saturate(lerp(_Albedo.rgb, _Albedo2.rgb, fres) + _ColorShallow.rgb * 0.25);
            o.Metallic = 0;
            o.Smoothness = _Smoothness;
            o.Alpha = _Transparency;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
