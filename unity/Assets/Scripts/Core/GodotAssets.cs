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

            var prefab = Model(modelPath);
            GameObject model;
            if (prefab != null)
            {
                model = Object.Instantiate(prefab);
                model.name = "Model";
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = "Model (missing)";
                Object.Destroy(model.GetComponent<Collider>());
            }
            model.transform.SetParent(node, false);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

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
