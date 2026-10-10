using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Loads the original Godot art that <c>GodotAssetSync</c> copies into
    /// <c>Assets/Resources/Godot</c>, addressed by the Godot path (e.g. "meshes/SM_Fish_A.obj").
    /// Every loader returns null when an asset is missing so callers can fall back to primitives.
    /// </summary>
    public static class GodotAssets
    {
        const string Root = "Godot/";
        static readonly Dictionary<string, Object> Cache = new();
        static readonly HashSet<string> Warned = new();

        public static GameObject Model(string godotPath) => Load<GameObject>(StripExtension(godotPath));
        public static Material Material(string name) => Load<Material>("Materials/" + name);
        public static Texture2D Texture(string godotPath) => Load<Texture2D>(StripExtension(godotPath));
        public static Sprite Sprite(string godotPath) => Load<Sprite>(StripExtension(godotPath));
        public static AudioClip Audio(string godotPath) => Load<AudioClip>(StripExtension(godotPath));

        static T Load<T>(string path) where T : Object
        {
            string key = typeof(T).Name + ":" + path;
            if (Cache.TryGetValue(key, out var cached)) return cached as T;
            var asset = Resources.Load<T>(Root + path);
            if (asset == null && Warned.Add(key))
                Debug.LogWarning($"[GodotAssets] Missing {typeof(T).Name} '{path}'. Run 'Too Fishy/Sync Godot Assets'.");
            Cache[key] = asset;
            return asset;
        }

        static string StripExtension(string path)
        {
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            return dot > slash ? path.Substring(0, dot) : path;
        }

        /// <summary>
        /// Recreates a Godot MeshInstance3D: a node named <paramref name="name"/> carrying the Godot
        /// transform, with the imported model below it. Unity's importer mirrors X; the extra 180°
        /// turn on the model child undoes that together with the Z flip of <see cref="GodotSpace"/>.
        /// Returns the node carrying the Godot transform.
        /// </summary>
        public static Transform SpawnModel(Transform parent, string name, string modelPath, string material,
            params float[] godotTransform)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            if (godotTransform != null && godotTransform.Length == 12)
                GodotSpace.Apply(node, godotTransform);

            // Unity mirrors X on import; the 180° turn plus GodotSpace's Z flip undo that. The
            // import fix node keeps the prefab's own root transform intact.
            var fix = new GameObject("ImportFix").transform;
            fix.SetParent(node, false);
            fix.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // gestein_v001.obj (boss section rock) has 344k vertices, too heavy for phones; the
            // similar gestein_v003 rock stands in, fitted to v001's bounds.
            var prefab = Model(modelPath == "meshes/gestein_v001.obj" ? "meshes/gestein_v003.obj" : modelPath);
            GameObject model;
            if (prefab != null)
            {
                model = Object.Instantiate(prefab, fix, false);
                model.name = "Model";
                FitToGodotBounds(node, fix, modelPath);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = "Model (missing)";
                Object.Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(fix, false);
            }

            var mat = material != null ? Material(material) : null;
            if (mat != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
            return node;
        }

        /// <summary>
        /// Mesh-space bounds of each model as Godot imports it (centre in Godot coordinates, size),
        /// measured with Godot 4.5.1 from the same files. Unity's unit handling for OBJ/FBX can
        /// differ (FBX centimetres, file-scale options), so every model is scaled and centred to
        /// match these exactly.
        /// </summary>
        static readonly Dictionary<string, Bounds> GodotBounds = new()
        {
            { "meshes/SM_FishSubmarine_FINAL.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(11.624174f, 4.628540f, 5.250996f)) },
            { "meshes/SM_Fish_A.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(6.413802f, 3.913930f, 1.380738f)) },
            { "meshes/SM_Fish_B.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(6.413803f, 3.129376f, 1.380738f)) },
            { "meshes/SM_AnglerFish.obj", new Bounds(new Vector3(0.841642f, 0.790889f, 0.009557f), new Vector3(7.654031f, 5.892616f, 4.595830f)) },
            { "meshes/SM_Blobert.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(7.250636f, 2.636098f, 6.716822f)) },
            { "meshes/SM_Crate.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(3.426820f, 3.354746f, 3.426820f)) },
            { "meshes/SM_Pickaxe.obj", new Bounds(new Vector3(0f, 0f, 0f), new Vector3(0.628562f, 4.447320f, 3.619540f)) },
            { "meshes/gestein_v001.obj", new Bounds(new Vector3(0.476100f, 3.605497f, 1.262957f), new Vector3(6.683904f, 12.29678f, 16.38527f)) },
            { "meshes/gestein_v003.obj", new Bounds(new Vector3(2.619935f, -0.564794f, 0.149968f), new Vector3(2.609154f, 20.937698f, 16.794592f)) },
            { "meshes/vains_v003.obj", new Bounds(new Vector3(2.443382f, -0.877941f, 0.716296f), new Vector3(2.256048f, 20.011921f, 14.704123f)) },
            { "meshes/dock3_remesh.fbx", new Bounds(new Vector3(-0.001991f, 0.013149f, -0.003475f), new Vector3(2.003870f, 0.787000f, 1.126116f)) },
            { "meshes/short_submar_texture.fbx", new Bounds(new Vector3(-0.001113f, 0.005302f, -0.001198f), new Vector3(1.998814f, 0.840123f, 1.043501f)) },
            { "meshes/Lanceharpoon.fbx", new Bounds(new Vector3(0.004568f, -0.009638f, 0.000854f), new Vector3(0.330984f, 1.981163f, 0.123930f)) },
            { "meshes/Spiky_remesh.fbx", new Bounds(new Vector3(-0.027289f, -0.011557f, -0.006333f), new Vector3(1.394045f, 1.339248f, 1.724723f)) },
            { "meshes/ak47_texture.fbx", new Bounds(new Vector3(-0.001843f, -0.004309f, -0.019886f), new Vector3(2.000580f, 0.842408f, 0.234459f)) },
            { "meshes/ak47_0406195124_texture.fbx", new Bounds(new Vector3(-0.001790f, -0.003860f, -0.019902f), new Vector3(2.000520f, 0.841874f, 0.234525f)) },
        };

        static void FitToGodotBounds(Transform node, Transform fix, string modelPath)
        {
            if (!GodotBounds.TryGetValue(modelPath, out var godot)) return;
            if (!MeshBounds(node, fix, out var actual)) return;

            // Expected bounds in the node's (Godot MeshInstance3D's) space: Godot (x, y, z) -> (x, y, -z)
            var expectedCenter = new Vector3(godot.center.x, godot.center.y, -godot.center.z);
            float expectedMax = Mathf.Max(godot.size.x, Mathf.Max(godot.size.y, godot.size.z));
            float actualMax = Mathf.Max(actual.size.x, Mathf.Max(actual.size.y, actual.size.z));
            if (actualMax < 1e-6f) return;

            float k = expectedMax / actualMax;
            if (Mathf.Abs(k - 1f) > 0.001f) fix.localScale *= k;
            if (MeshBounds(node, fix, out actual))
                fix.localPosition += expectedCenter - actual.center;

            var a = actual.size / Mathf.Max(actual.size.x, Mathf.Max(actual.size.y, actual.size.z));
            var g = godot.size / expectedMax;
            if (Mathf.Abs(a.x - g.x) > 0.1f || Mathf.Abs(a.y - g.y) > 0.1f || Mathf.Abs(a.z - g.z) > 0.1f)
                Debug.LogWarning($"[GodotAssets] {modelPath}: proportions differ from Godot (Unity {a}, Godot {g}); check the import axes.");
        }

        /// <summary>Combined mesh bounds of everything under <paramref name="root"/>, in <paramref name="space"/>'s local space.</summary>
        static bool MeshBounds(Transform space, Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var toSpace = space.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var m = toSpace * mf.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        /// <summary>Adds a Godot <c>material_overlay</c>: the mesh is drawn a second time with it.</summary>
        public static void AddOverlay(Transform node, string material)
        {
            var mat = Material(material);
            if (mat == null) return;
            foreach (var r in node.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                var withOverlay = new Material[mats.Length + 1];
                mats.CopyTo(withOverlay, 0);
                withOverlay[mats.Length] = mat;
                r.sharedMaterials = withOverlay;
            }
        }

        public static void SetMaterial(Transform node, string material)
        {
            var mat = Material(material);
            if (mat == null) return;
            foreach (var r in node.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }
    }

    /// <summary>
    /// Godot is right-handed (camera looks down -Z), Unity is left-handed (camera looks down +Z).
    /// The port maps Godot (x, y, z) to Unity (x, y, -z): X and Y — the whole 2.5D playfield —
    /// keep their Godot values, and the camera sits at -Z looking +Z, so screen-right is +X as in Godot.
    /// </summary>
    public static class GodotSpace
    {
        public static Vector3 Pos(float x, float y, float z) => new(x, y, -z);
        public static Vector3 Pos(Vector3 godot) => new(godot.x, godot.y, -godot.z);

        /// <summary>Godot rotation about Z (degrees) — unchanged by the Z flip.</summary>
        public static Quaternion RotZ(float deg) => Quaternion.Euler(0f, 0f, deg);
        /// <summary>Godot rotation about Y (degrees) — mirrored by the Z flip.</summary>
        public static Quaternion RotY(float deg) => Quaternion.Euler(0f, -deg, 0f);

        /// <summary>
        /// Applies a Godot <c>Transform3D(xx, xy, xz, yx, yy, yz, zx, zy, zz, ox, oy, oz)</c> exactly as
        /// written in a .tscn (basis rows, then origin) as the local transform of <paramref name="t"/>.
        /// </summary>
        public static void Apply(Transform t, params float[] m)
        {
            // U = F * M * F with F = diag(1, 1, -1): negate entries that mix Z with X or Y.
            var c0 = new Vector3(m[0], m[3], -m[6]);
            var c1 = new Vector3(m[1], m[4], -m[7]);
            var c2 = new Vector3(-m[2], -m[5], m[8]);

            float sx = c0.magnitude, sy = c1.magnitude, sz = c2.magnitude;
            if (Vector3.Dot(Vector3.Cross(c0, c1), c2) < 0f) sx = -sx;

            t.localPosition = new Vector3(m[9], m[10], -m[11]);
            if (sy > 1e-6f && sz > 1e-6f)
                t.localRotation = Quaternion.LookRotation(c2 / sz, c1 / sy);
            t.localScale = new Vector3(sx, sy, sz);
        }
    }
}
