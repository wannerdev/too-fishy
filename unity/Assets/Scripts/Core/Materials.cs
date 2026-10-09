using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TooFishy
{
    /// <summary>
    /// Central material factory.
    ///
    /// Nothing in the scene references a material asset, so a bare
    /// <c>new Material(Shader.Find("Standard"))</c> works in the editor but the Standard shader is
    /// stripped from device builds (black screen / pink objects). The base materials below live in
    /// <c>Assets/Resources/Materials</c>, which forces the shader and its emissive and transparent
    /// variants into every build. Materials are cached by appearance so identical objects share one
    /// instance and can be batched.
    /// </summary>
    public static class Materials
    {
        static Material _opaqueBase, _emissiveBase, _transparentBase;
        static readonly Dictionary<string, Material> Cache = new();

        /// <summary>Shared opaque material for the given colour.</summary>
        public static Material Opaque(Color color, float metallic = 0f, float glossiness = 0.5f)
        {
            string key = $"o|{Quantize(color)}|{metallic:F2}|{glossiness:F2}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = new Material(OpaqueBase()) { name = key, color = color };
            SetFloatIfExists(mat, "_Metallic", metallic);
            SetFloatIfExists(mat, "_Glossiness", glossiness);
            Cache[key] = mat;
            return mat;
        }

        /// <summary>Shared emissive material (glowing fish, lava, boss).</summary>
        public static Material Emissive(Color color, Color emission)
        {
            string key = $"e|{Quantize(color)}|{Quantize(emission)}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = new Material(EmissiveBase()) { name = key, color = color };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission);
            Cache[key] = mat;
            return mat;
        }

        /// <summary>Shared alpha-blended material (surface water).</summary>
        public static Material Transparent(Color color, float glossiness = 0.5f)
        {
            string key = $"t|{Quantize(color)}|{glossiness:F2}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = new Material(TransparentBase()) { name = key, color = color };
            // Equivalent of picking "Transparent" in the Standard shader inspector.
            SetFloatIfExists(mat, "_Mode", 3f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            SetFloatIfExists(mat, "_Glossiness", glossiness);
            Cache[key] = mat;
            return mat;
        }

        static Material OpaqueBase() => _opaqueBase ??= Load("Materials/StandardBase");
        static Material EmissiveBase() => _emissiveBase ??= Load("Materials/StandardEmissive");
        static Material TransparentBase() => _transparentBase ??= Load("Materials/StandardTransparent");

        static Material Load(string path)
        {
            var asset = Resources.Load<Material>(path);
            if (asset != null) return asset;

            Debug.LogWarning($"[Materials] Resource '{path}' missing; falling back to Shader.Find.");
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");
            return new Material(shader);
        }

        static void SetFloatIfExists(Material mat, string property, float value)
        {
            if (mat.HasProperty(property)) mat.SetFloat(property, value);
        }

        static string Quantize(Color c) =>
            $"{Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)},{Mathf.RoundToInt(c.a * 255)}";
    }
}
