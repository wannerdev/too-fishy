#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TooFishy.EditorTools
{
    /// <summary>
    /// Brings the original Godot art and audio into the Unity project.
    ///
    /// The Unity project lives inside the Godot repository, so instead of committing a second copy
    /// of ~150 MB of meshes and textures, the files are copied from the repository root into
    /// <c>Assets/Resources/Godot</c> (git-ignored) and the materials from <c>materials/*.tres</c>
    /// are re-created as Unity materials there. Runtime code loads everything through
    /// <see cref="TooFishy.GodotAssets"/> by the original Godot path.
    ///
    /// Runs automatically when the editor loads (if the output is missing or outdated), from the
    /// "Too Fishy/Sync Godot Assets" menu, and at the start of every command-line build.
    /// </summary>
    [InitializeOnLoad]
    public static class GodotAssetSync
    {
        public const string OutRoot = "Assets/Resources/Godot";
        const string MaterialDir = OutRoot + "/Materials";
        const string GeneratedDir = OutRoot + "/Generated";
        const string StampFile = OutRoot + "/.synced";
        // Bump when the file list or a material definition changes.
        const string Version = "3";

        /// <summary>Paths relative to the repository root, copied verbatim under <see cref="OutRoot"/>.</summary>
        static readonly string[] Files =
        {
            // Models
            "meshes/SM_FishSubmarine_FINAL.obj",
            "meshes/SM_Fish_A.obj",
            "meshes/SM_Fish_B.obj",
            "meshes/SM_AnglerFish.obj",
            "meshes/SM_Blobert.obj",
            "meshes/SM_Crate.obj",
            "meshes/SM_Pickaxe.obj",
            "meshes/gestein_v003.obj",
            "meshes/vains_v003.obj",
            "meshes/Lanceharpoon.fbx",
            "meshes/Lanceharpoon_0.png",
            "meshes/Spiky_remesh.fbx",
            "meshes/Spiky_remesh_0.png",
            "meshes/dock3_remesh.fbx",
            "meshes/dock3_remesh_0.png",
            "meshes/short_submar_texture.fbx",
            "meshes/short_submar_texture_0.png",
            "meshes/ak47_0406195124_texture.fbx",
            "meshes/ak47_0406195124_texture_0.png",
            "meshes/ak47_texture.fbx",
            "meshes/ak47_texture_0.png",

            // Material textures
            "textures/sub/SM_FishSubmarine_initialShadingGroup_BaseColor.png",
            "textures/sub/SM_FishSubmarine_initialShadingGroup_Normal.png",
            "textures/fishes/fishies painter_initialShadingGroup_BaseColor.png",
            "textures/fishes/fishies painter_initialShadingGroup_Emissive.png",
            "textures/fishes/Fishies.png",
            "textures/misc/SM_Assets_initialShadingGroup_BaseColor.png",
            "textures/BASE_color_Stein_v001.png",
            "textures/NORMAL_Stein_v001.png",
            "textures/perlin_noise_smudged.jpg",
            "Shaders/AllSkyFree_Sky_EpicBlueSunset_Equirect.png",

            // Section backgrounds
            "textures/backgrounds/First.png",
            "textures/backgrounds/underfirst.png",
            "textures/backgrounds/Loop_for_Deep_v002.png",
            "textures/backgrounds/trans_to_Lava.png",
            "textures/backgrounds/voiD_v002.png",
            "textures/backgrounds/Lava.png",

            // UI
            "textures/UI_element_background.png",
            "textures/effects/screen_crack.png",
            "textures/characters/john.png",
            "textures/characters/john_insub.png",
            "textures/icons/air_bubble.png",
            "textures/icons/angler_fish.png",
            "textures/icons/boss_icon.png",
            "textures/icons/dummy_fish.png",
            "textures/icons/fish_a.png",
            "textures/icons/fish_b.png",
            "textures/icons/pure_star.png",
            "textures/icons/questionmark.png",
            "textures/icons/spikey_fish.png",

            // Audio
            "music/surface.mp3",
            "music/deep.mp3",
            "music/hotzone.mp3",
            "music/bossfight.mp3",
            "sounds/Ouugh.wav",
            "sounds/bup.wav",
            "sounds/bupp.wav",
            "sounds/coins.wav",
            "sounds/harp.wav",
            "sounds/harp2.wav",
            "sounds/harp3.wav",
            "sounds/ughhh.wav",
            "sounds/urrgh.wav",
        };

        static GodotAssetSync()
        {
            if (Application.isBatchMode) return; // builds call EnsureSynced() explicitly
            EditorApplication.delayCall += () =>
            {
                if (!IsUpToDate()) Sync();
            };
        }

        [MenuItem("Too Fishy/Sync Godot Assets")]
        public static void SyncFromMenu() => Sync();

        public static void EnsureSynced()
        {
            if (!IsUpToDate()) Sync();
        }

        static bool IsUpToDate() =>
            File.Exists(Abs(StampFile)) && File.ReadAllText(Abs(StampFile)).Trim() == Version;

        public static void Sync()
        {
            string repoRoot = RepoRoot();
            if (!File.Exists(Path.Combine(repoRoot, "project.godot")))
            {
                Debug.LogError($"[GodotAssetSync] Godot project not found at {repoRoot}; the Unity project must stay inside the too-fishy repository.");
                return;
            }

            int copied = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string rel in Files)
                {
                    string src = Path.Combine(repoRoot, rel);
                    string dst = Abs($"{OutRoot}/{rel}");
                    if (!File.Exists(src))
                    {
                        Debug.LogWarning($"[GodotAssetSync] Missing source file {rel}");
                        continue;
                    }
                    if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length &&
                        File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src))
                        continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    copied++;
                }
                Directory.CreateDirectory(Abs(MaterialDir));
                Directory.CreateDirectory(Abs(GeneratedDir));
                WriteGeneratedTextures(repoRoot);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildMaterials();
            AssetDatabase.SaveAssets();

            File.WriteAllText(Abs(StampFile), Version);
            Debug.Log($"[GodotAssetSync] Synced Godot assets ({copied} files copied).");
        }

        // ------------------------------------------------------------------ generated textures

        /// <summary>
        /// Godot uses separate metallic and roughness maps; Unity's Standard shader wants metallic
        /// in R and smoothness in A of one texture. Godot multiplies the maps by the material's
        /// metallic / roughness values, so those scales are baked in here.
        /// </summary>
        static void WriteGeneratedTextures(string repoRoot)
        {
            WriteMetallicSmoothness(repoRoot, "submarine",
                null, 0f, "textures/sub/SM_FishSubmarine_initialShadingGroup_Roughness.png", 1f);
            WriteMetallicSmoothness(repoRoot, "fishes",
                null, 0f, "textures/fishes/fishies painter_initialShadingGroup_Roughness.png", 1f);
            WriteMetallicSmoothness(repoRoot, "boss_mini",
                null, 0f, "textures/fishes/fishies painter_initialShadingGroup_Roughness.png", 0.62f);
            WriteMetallicSmoothness(repoRoot, "assets",
                "textures/misc/SM_Assets_initialShadingGroup_Metallic.png", 1f,
                "textures/misc/SM_Assets_initialShadingGroup_Roughness.png", 1f);
        }

        static void WriteMetallicSmoothness(string repoRoot, string name, string metallicRel, float metallicScale,
            string roughnessRel, float roughnessScale)
        {
            string dst = Abs($"{GeneratedDir}/{name}_MetallicSmoothness.png");
            if (File.Exists(dst)) return;

            var rough = LoadSource(repoRoot, roughnessRel);
            var metal = metallicRel != null ? LoadSource(repoRoot, metallicRel) : null;
            if (rough == null) return;

            int w = rough.width, h = rough.height;
            var r = rough.GetPixels32();
            Color32[] m = null;
            if (metal != null)
            {
                if (metal.width != w || metal.height != h) metal = Resize(metal, w, h);
                m = metal.GetPixels32();
            }

            var outPixels = new Color32[r.Length];
            for (int i = 0; i < r.Length; i++)
            {
                byte metallic = (byte)(m != null ? Mathf.Clamp(m[i].r * metallicScale, 0, 255) : 0);
                byte smooth = (byte)(255 - Mathf.Clamp(r[i].g * roughnessScale, 0, 255));
                outPixels[i] = new Color32(metallic, 0, 0, smooth);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(outPixels);
            File.WriteAllBytes(dst, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rough);
            if (metal != null) UnityEngine.Object.DestroyImmediate(metal);
        }

        static Texture2D LoadSource(string repoRoot, string rel)
        {
            string path = Path.Combine(repoRoot, rel);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GodotAssetSync] Missing source file {rel}");
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        static Texture2D Resize(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(src);
            return dst;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Unity equivalents of the Godot materials, one per <c>.tres</c> (same names).</summary>
        static void BuildMaterials()
        {
            // materials/submarine.tres
            Standard("submarine",
                albedo: Tex("textures/sub/SM_FishSubmarine_initialShadingGroup_BaseColor.png"),
                normal: Tex("textures/sub/SM_FishSubmarine_initialShadingGroup_Normal.png"),
                metallicSmoothness: Generated("submarine"));

            // materials/fishes.tres (angler fish, boss)
            Standard("fishes",
                albedo: Tex("textures/fishes/fishies painter_initialShadingGroup_BaseColor.png"),
                normal: Tex("textures/fishes/Fishies.png"),
                metallicSmoothness: Generated("fishes"),
                emissionMap: Tex("textures/fishes/fishies painter_initialShadingGroup_Emissive.png"),
                emission: Color.white);

            // materials/mobs/boss_mini_material.tres. Godot adds the emission colour to the
            // emission texture; Unity multiplies, so the colour is folded into a brighter tint.
            Standard("boss_mini",
                color: new Color(0.99f, 0.84f, 0.90f),
                albedo: Tex("textures/fishes/fishies painter_initialShadingGroup_BaseColor.png"),
                normal: Tex("textures/fishes/Fishies.png"),
                metallicSmoothness: Generated("boss_mini"),
                emissionMap: Tex("textures/fishes/fishies painter_initialShadingGroup_Emissive.png"),
                emission: new Color(1f, 0.75f, 0.9f) * 1.45f);

            // materials/fish_a_animated_material.tres
            var fishShader = Shader.Find("TooFishy/FishSwim");
            var fishA = GetOrCreate("fish_a_animated", fishShader);
            fishA.SetTexture("_MainTex", Tex("textures/fishes/fishies painter_initialShadingGroup_BaseColor.png"));
            fishA.SetTexture("_EmissionMap", Tex("textures/fishes/fishies painter_initialShadingGroup_Emissive.png"));
            fishA.SetFloat("_EmissionEnergy", 1f);
            fishA.enableInstancing = true;

            // materials/assets.tres (crates)
            Standard("assets",
                albedo: Tex("textures/misc/SM_Assets_initialShadingGroup_BaseColor.png"),
                metallicSmoothness: Generated("assets"));

            // materials/walls/walls.tres + the translucent overlay from scenes/section.tscn
            Standard("walls",
                albedo: Tex("textures/BASE_color_Stein_v001.png"),
                normal: Tex("textures/NORMAL_Stein_v001.png"),
                smoothness: 1f - 0.24f);
            var overlay = Standard("walls_overlay_section", color: new Color(0.0627451f, 0.243137f, 0.368627f, 0.862745f), smoothness: 0f);
            MakeFade(overlay);
            overlay.renderQueue = (int)RenderQueue.Transparent + 1;

            // materials/walls/veins.tres / veins_lava.tres
            Standard("veins", color: new Color(0f, 0.286275f, 0.247059f), smoothness: 0f);
            Standard("veins_lava", color: new Color(0.634834f, 0.108211f, 0f), smoothness: 0f,
                emission: new Color(0.609048f, 0.10211f, 0f));

            // FBX models: Godot uses the embedded texture (extracted as <name>_0.png).
            Standard("Lanceharpoon", albedo: Tex("meshes/Lanceharpoon_0.png"), smoothness: 0.3f);
            Standard("Spiky_remesh", albedo: Tex("meshes/Spiky_remesh_0.png"), smoothness: 0.3f);
            Standard("dock3_remesh", albedo: Tex("meshes/dock3_remesh_0.png"), smoothness: 0.3f);
            Standard("short_submar_texture", albedo: Tex("meshes/short_submar_texture_0.png"), smoothness: 0.3f);
            Standard("ak47", albedo: Tex("meshes/ak47_0406195124_texture_0.png"), smoothness: 0.3f);
            Standard("ak47_second", albedo: Tex("meshes/ak47_texture_0.png"), smoothness: 0.3f);

            // materials/backgrounds/*.tres through background_fade.gdshader
            var bgShader = Shader.Find("TooFishy/BackgroundFade");
            foreach (var (name, tex) in new[]
                     {
                         ("bg_first", "textures/backgrounds/First.png"),
                         ("bg_afterfirst", "textures/backgrounds/underfirst.png"),
                         ("bg_loop", "textures/backgrounds/Loop_for_Deep_v002.png"),
                         ("bg_deep_to_lava", "textures/backgrounds/trans_to_Lava.png"),
                         ("bg_lava_to_void", "textures/backgrounds/voiD_v002.png"),
                         ("bg_lava", "textures/backgrounds/Lava.png"),
                     })
            {
                var bg = GetOrCreate(name, bgShader);
                bg.SetTexture("_MainTex", Tex(tex));
            }
            // materials/backgrounds/bg_void.tres is plain black
            var bgVoid = GetOrCreate("bg_void", bgShader);
            bgVoid.SetColor("_Color", Color.black);

            // Shaders/Water.tres
            var water = GetOrCreate("water", Shader.Find("TooFishy/Water"));
            water.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/WaterNormal.png"));

            // materials/shaders/lava_shader_material.tres
            var lava = GetOrCreate("lava", Shader.Find("TooFishy/Lava"));
            lava.SetTexture("_NoiseTex", Tex("textures/perlin_noise_smudged.jpg"));
            lava.SetFloat("_Scale", 0.2f);
            lava.SetFloat("_Speed", 1f);
            lava.SetFloat("_Temperature", 2.2f);
            lava.SetFloat("_Brightness", 1f);

            // Player environment sky (PanoramaSkyMaterial in scenes/player.tscn)
            var sky = GetOrCreate("sky", Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", Tex("Shaders/AllSkyFree_Sky_EpicBlueSunset_Equirect.png"));
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1f);
        }

        static Material Standard(string name, Color? color = null, Texture2D albedo = null, Texture2D normal = null,
            Texture2D metallicSmoothness = null, float smoothness = 0f, Texture2D emissionMap = null, Color? emission = null)
        {
            var mat = GetOrCreate(name, Shader.Find("Standard"));
            mat.color = color ?? Color.white;
            mat.SetTexture("_MainTex", albedo);

            mat.SetTexture("_BumpMap", normal);
            SetKeyword(mat, "_NORMALMAP", normal != null);

            mat.SetTexture("_MetallicGlossMap", metallicSmoothness);
            SetKeyword(mat, "_METALLICGLOSSMAP", metallicSmoothness != null);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_GlossMapScale", 1f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", smoothness);

            bool emissive = emission.HasValue;
            mat.SetTexture("_EmissionMap", emissionMap);
            mat.SetColor("_EmissionColor", emissive ? emission.Value : Color.black);
            SetKeyword(mat, "_EMISSION", emissive);
            mat.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.enableInstancing = true;
            return mat;
        }

        static void MakeFade(Material mat)
        {
            mat.SetFloat("_Mode", 2f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            if (shader == null) throw new InvalidOperationException($"[GodotAssetSync] Shader for material {name} not found");
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
                mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        static Texture2D Tex(string rel)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OutRoot}/{rel}");
            if (tex == null) Debug.LogWarning($"[GodotAssetSync] Texture {rel} not imported");
            return tex;
        }

        static Texture2D Generated(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedDir}/{name}_MetallicSmoothness.png");

        // ------------------------------------------------------------------ helpers

        static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoRoot() => Path.GetFullPath(Path.Combine(ProjectRoot(), ".."));
        static string Abs(string assetPath) => Path.Combine(ProjectRoot(), assetPath);
    }

    /// <summary>Import settings for the synced Godot files.</summary>
    public class GodotAssetImportSettings : AssetPostprocessor
    {
        static bool IsGodot(string path) => path.StartsWith(GodotAssetSync.OutRoot + "/", StringComparison.Ordinal);

        void OnPreprocessModel()
        {
            if (!IsGodot(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true; // Y-up geometry without a corrective root rotation
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            // Keep Godot's smoothing: OBJ normals are imported as authored.
            importer.importNormals = ModelImporterNormals.Import;
        }

        void OnPreprocessTexture()
        {
            if (!IsGodot(assetPath) && assetPath != "Assets/Textures/WaterNormal.png") return;
            var importer = (TextureImporter)assetImporter;
            string file = Path.GetFileName(assetPath);

            bool normal = file.Contains("_Normal") || file.StartsWith("NORMAL_") || file == "Fishies.png" || file == "WaterNormal.png";
            bool data = assetPath.Contains("/Generated/");
            bool ui = assetPath.Contains("/textures/icons/") || assetPath.Contains("/textures/characters/") ||
                      assetPath.Contains("/textures/effects/") || file == "UI_element_background.png";

            if (normal)
                importer.textureType = TextureImporterType.NormalMap;
            else if (ui)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !data;
            }

            if (assetPath.Contains("/backgrounds/") || file.StartsWith("AllSkyFree"))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = !file.StartsWith("AllSkyFree");
            }
            if (file == "UI_element_background.png")
                importer.spriteBorder = new Vector4(25, 25, 25, 25); // main_scene.tres texture_margin 25

            importer.maxTextureSize = 2048;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = ui ? 1024 : 2048;
            android.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(android);
        }

        void OnPreprocessAudio()
        {
            if (!IsGodot(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            bool music = assetPath.Contains("/music/");
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.8f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
        }
    }
}
#endif
