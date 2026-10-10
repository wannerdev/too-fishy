using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of the Friend of scenes/boss_section.tscn + scripts/friend.gd: the yellow friend sub
    /// waits in the boss section. Touching it starts the FRIEND_RESCUED dialog; while the player
    /// stays close it follows (velocity = 2 × offset). Once it is back above -5 m the boss leaves
    /// and the WIN dialog plays.
    /// </summary>
    public class Friend : MonoBehaviour
    {
        const float AreaRadius = 3.62441f;
        static readonly Vector3 AreaCenterGodot = new(0.691881f, 1.91581f, 0f);
        bool _following;

        public static Friend Create(Transform parent, Vector3 godotLocalPosition)
        {
            var go = new GameObject("Friend");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = GodotSpace.Pos(godotLocalPosition);
            go.tag = "Untagged";
            var mesh = GodotAssets.SpawnModel(go.transform, "SmFishSubmarine", "meshes/SM_FishSubmarine_FINAL.obj", "submarine",
                0.17f, 0f, 0f, 0f, 0.17f, 0f, 0f, 0f, 0.17f, 0.328859f, 1.72861f, 0f);
            TintFriend(mesh);
            return go.AddComponent<Friend>();
        }

        /// <summary>The friend's submarine material: the normal one tinted (1, 1, 0.152941).</summary>
        public static void TintFriend(Transform mesh)
        {
            var baseMat = GodotAssets.Material("submarine");
            if (baseMat == null) return;
            var mat = new Material(baseMat) { name = "submarine_friend", color = new Color(1f, 1f, 0.152941f) };
            foreach (var r in mesh.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        void Update()
        {
            var player = GameState.Instance?.PlayerTransform;
            if (player == null) return;

            var center = transform.TransformPoint(GodotSpace.Pos(AreaCenterGodot));
            var d = player.position - center;
            d.z = 0f;
            bool inside = d.magnitude <= AreaRadius + 0.5f;
            if (inside && !_following && Dialogs.Section != DialogSection.FriendRescued)
                Dialogs.SetStage(DialogSection.FriendRescued);
            _following = inside;
            if (!_following) return;

            var dir = player.position - transform.position;
            dir.z = 0f;
            transform.position += dir * 2f * Time.deltaTime;

            if (transform.position.y >= -5f)
            {
                BossController.Remove();
                if (Dialogs.Section != DialogSection.Win)
                    Dialogs.SetStage(DialogSection.Win);
            }
        }
    }
}
