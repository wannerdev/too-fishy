using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scenes/lava.tscn / scenes/lava_side.tscn + scripts/lava_area.gd: a lava quad
    /// (lava_shader) with a damage area. While the submarine is inside, player.gd's
    /// process_lava_damage() runs (PlayerController checks LavaZone.Contains every frame).
    /// </summary>
    public class LavaZone : MonoBehaviour
    {

        /// <summary>Builds a lava scene node; <paramref name="side"/> selects lava_side.tscn (box area).</summary>
        public static Transform Create(Transform parent, string name, bool side, params float[] godotTransform)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            GodotSpace.Apply(node, godotTransform);

            // MeshInstance3D: QuadMesh × 2
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MeshInstance3D";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(node, false);
            quad.transform.localScale = Vector3.one * 2f;
            var mat = GodotAssets.Material("lava");
            // Before the translucent rock overlays (3001), like Godot's render_priority 0 vs 1 (see Lava.shader)
            if (mat != null) mat.renderQueue = 3000;
            quad.GetComponent<Renderer>().sharedMaterial = mat != null ? mat
                : Materials.Emissive(new Color(1f, 0.25f, 0.05f), new Color(2f, 0.4f, 0.05f));

            // LavaDamageArea
            var area = new GameObject("LavaDamageArea");
            area.transform.SetParent(node, false);
            area.transform.localScale = Vector3.one * 2f;
            if (side)
            {
                var box = area.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1f, 1f, 0.5f);
            }
            else
            {
                area.transform.localPosition = GodotSpace.Pos(0f, 0f, 0.70497f);
                // lava.tscn:21-24 CylinderShape3D (h 2, r 0.5) turned so its axis points along Z. Godot
                // carries the lava node's non-uniform scale into the shape (an elliptic cylinder: Lava5
                // is a 2.9 × 14 m strip, the boss pools 2 m tall bands). A CapsuleCollider would stay
                // round and grow to the largest axis, so the cylinder is a convex mesh on the unrotated
                // area node, whose MeshCollider bakes the node's scale per axis.
                var mc = area.AddComponent<MeshCollider>();
                mc.convex = true;
                mc.sharedMesh = CylinderMesh();
                mc.isTrigger = true;
                area.AddComponent<LavaZone>();
                return node;
            }
            area.AddComponent<LavaZone>();
            return node;
        }

        static Mesh s_cylinder;

        /// <summary>
        /// The CylinderShape3D in LavaDamageArea space: radius 0.5, height 2 along Godot Y, turned by
        /// the CollisionShape3D basis (axis (0, 0.0521, -0.9986) in Godot, Z flipped for Unity).
        /// </summary>
        static Mesh CylinderMesh()
        {
            if (s_cylinder != null) return s_cylinder;
            const int segments = 24;
            var axis = new Vector3(0f, 0.0521095f, 0.998641f);
            var u = Vector3.right;
            var v = new Vector3(0f, 0.998641f, -0.0521095f);
            var verts = new Vector3[segments * 2];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var ring = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * 0.5f;
                verts[i] = ring + axis;
                verts[segments + i] = ring - axis;
            }
            // Side quads plus fan caps (a convex MeshCollider only needs the points, but keep it a valid mesh)
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                tris.AddRange(new[] { i, j, segments + i, j, segments + j, segments + i });
            }
            for (int i = 1; i < segments - 1; i++)
            {
                tris.AddRange(new[] { 0, i + 1, i });
                tris.AddRange(new[] { segments, segments + i, segments + i + 1 });
            }
            s_cylinder = new Mesh { name = "LavaDamageCylinder", vertices = verts, triangles = tris.ToArray() };
            s_cylinder.RecalculateBounds();
            return s_cylinder;
        }

        // room for the player's own shapes, nearby fish and walls besides the lava
        static readonly Collider[] Hits = new Collider[32];
        static CharacterController s_hullOwner;
        static CapsuleCollider s_hull;

        /// <summary>
        /// True when the player's hull overlaps any lava damage area. Godot tests the submarine's real
        /// body (player.tscn: a horizontal capsule, r 0.5, length 2.8), which is the "Hull" capsule
        /// on the player; the upright CharacterController is only used when there is no hull.
        /// </summary>
        public static bool Contains(CharacterController cc)
        {
            if (cc != s_hullOwner)
            {
                s_hullOwner = cc;
                var hullT = cc.transform.Find("Hull");
                s_hull = hullT != null ? hullT.GetComponent<CapsuleCollider>() : null;
            }

            Vector3 a, b;
            float radius;
            if (s_hull != null)
            {
                var t = s_hull.transform;
                var s = t.lossyScale;
                Vector3 axis;
                float axisScale, radiusScale;
                switch (s_hull.direction)
                {
                    case 0: axis = t.right; axisScale = Mathf.Abs(s.x); radiusScale = Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)); break;
                    case 1: axis = t.up; axisScale = Mathf.Abs(s.y); radiusScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)); break;
                    default: axis = t.forward; axisScale = Mathf.Abs(s.z); radiusScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)); break;
                }
                radius = s_hull.radius * radiusScale;
                float half = Mathf.Max(0f, s_hull.height * axisScale / 2f - radius);
                var center = t.TransformPoint(s_hull.center);
                a = center + axis * half;
                b = center - axis * half;
            }
            else
            {
                var t = cc.transform;
                var center = t.TransformPoint(cc.center);
                float half = Mathf.Max(0f, cc.height / 2f - cc.radius);
                a = center + Vector3.up * half;
                b = center - Vector3.up * half;
                radius = cc.radius;
            }
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (Hits[i] != null && Hits[i].GetComponent<LavaZone>() != null) return true;
            return false;
        }
    }
}
