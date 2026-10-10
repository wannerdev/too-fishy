using UnityEngine;

namespace TooFishy
{
    /// <summary>Godot primitive meshes Unity has no built-in equivalent for.</summary>
    public static class ProceduralMeshes
    {
        static Mesh _prism;

        /// <summary>Godot PrismMesh with default size (1, 1, 1) and the apex centred.</summary>
        public static Mesh Prism => _prism ??= BuildPrism();

        static Mesh BuildPrism()
        {
            var l = new Vector3(-0.5f, -0.5f, 0f);
            var r = new Vector3(0.5f, -0.5f, 0f);
            var t = new Vector3(0f, 0.5f, 0f);
            var f = new Vector3(0f, 0f, -0.5f); // front (toward the camera)
            var b = new Vector3(0f, 0f, 0.5f);

            var verts = new[]
            {
                // front and back triangles
                l + f, t + f, r + f,
                l + b, r + b, t + b,
                // left slope
                l + f, l + b, t + b, t + f,
                // right slope
                r + f, t + f, t + b, r + b,
                // bottom
                l + f, r + f, r + b, l + b,
            };
            var tris = new[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8, 6, 8, 9,
                10, 11, 12, 10, 12, 13,
                14, 15, 16, 14, 16, 17,
            };
            var mesh = new Mesh { name = "Prism", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
