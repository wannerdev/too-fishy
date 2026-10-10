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
                // CylinderShape3D (h 2, r 0.5) turned so its axis points along Z
                var shape = new GameObject("CollisionShape3D");
                shape.transform.SetParent(area.transform, false);
                GodotSpace.Apply(shape.transform, 1f, 0f, 0f, 0f, 0.0521095f, 0.998641f, 0f, -0.998641f, 0.0521095f, 0f, 0f, 0f);
                var cap = shape.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.direction = 1;
                cap.radius = 0.5f;
                cap.height = 2f;
                shape.AddComponent<LavaZone>();
                return node;
            }
            area.AddComponent<LavaZone>();
            return node;
        }

        static readonly Collider[] Hits = new Collider[8];

        /// <summary>True when the player's hull overlaps any lava damage area.</summary>
        public static bool Contains(CharacterController cc)
        {
            var t = cc.transform;
            var center = t.TransformPoint(cc.center);
            float half = Mathf.Max(0f, cc.height / 2f - cc.radius);
            int n = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half, center - Vector3.up * half, cc.radius,
                Hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (Hits[i] != null && Hits[i].GetComponent<LavaZone>() != null) return true;
            return false;
        }
    }
}
