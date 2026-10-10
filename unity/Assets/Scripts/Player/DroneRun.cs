using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// The selling drone of player.gd activate_selling_drone(): scenes/mobs/drone.tscn at half
    /// scale, 0.6 above the submarine, swims to the dock (-5, -1.2) at ~3 u/s (at least 0.6 s)
    /// while the released catch is dragged along. Money, coins.wav and the popup arrive with it.
    /// </summary>
    public class DroneRun : MonoBehaviour
    {
        static readonly Vector2 Dock = new(-5f, -1.2f);

        Vector3 _start, _target;
        float _duration, _t;
        int _sold;
        PlayerController _player;
        readonly List<(Transform fish, Vector3 from, Vector3 to, float duration)> _fish = new();

        public static void Launch(PlayerController player, GameState gs)
        {
            var items = new List<InventoryItem>(gs.Inventory.Items);
            int sold = 0;
            foreach (var item in items) sold += item.Price;

            var root = new GameObject("Drone");
            root.transform.position = player.transform.position + Vector3.up * 0.6f;
            root.transform.localScale = Vector3.one * 0.5f;
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = GodotSpace.Pos(0.118526f, 0f, 0f);
            // short_submar_texture.tscn re-orients the FBX's Mesh1 node; this is that rotation
            // relative to the imported orientation.
            var orient = new GameObject("short_submar_texture").transform;
            orient.SetParent(pivot, false);
            GodotSpace.Apply(orient, -0.024351f, 0.046747f, -0.99861f, -0.853104f, 0.519785f, 0.045135f, 0.521172f, 0.853017f, 0.027223f, 0f, 0f, 0f);
            GodotAssets.SpawnModel(orient, "Mesh1", "meshes/short_submar_texture.fbx", "short_submar_texture", null);

            // dronefart: 5 small rings drifting up, tinted gold
            Effects.SpawnBurst(root.transform.position, new Effects.Burst
            {
                Amount = 5, Lifetime = 1.5f, SpeedMin = 1f, SpeedMax = 3f, Spread = 30f, Size = 0.6f,
                Color = new Color(0.8f, 0.8f, 0.2f)
            }, root.transform);

            var run = root.AddComponent<DroneRun>();
            run._player = player;
            run._sold = sold;
            run._start = root.transform.position;
            run._target = new Vector3(Dock.x, Dock.y, root.transform.position.z);
            run._duration = Mathf.Max(0.6f, Vector3.Distance(run._start, run._target) / 3f);

            // The catch is released into the world and pulled to the dock with the drone
            gs.Inventory.Clear();
            foreach (var item in items)
            {
                var pos = player.transform.position + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1f, 1f), 0f);
                var fish = FishBehaviour.SpawnReleased(item, pos, player.transform.parent, scatter: false);
                fish.enabled = false;
                foreach (var c in fish.GetComponentsInChildren<Collider>()) c.enabled = false;
                var to = run._target + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.2f, 0.2f), 0f);
                to.z = fish.transform.position.z;
                run._fish.Add((fish.transform, fish.transform.position, to, run._duration * Random.Range(0.85f, 1.1f)));
            }

            if (sold > 0) Achievements.RecordDroneLift();
        }

        void Update()
        {
            _t += Time.deltaTime;
            transform.position = Vector3.Lerp(_start, _target, Mathf.Clamp01(_t / _duration));
            foreach (var f in _fish)
            {
                if (f.fish == null) continue;
                f.fish.position = Vector3.Lerp(f.from, f.to, Mathf.Clamp01(_t / f.duration));
                if (_t >= f.duration) Destroy(f.fish.gameObject);
            }

            if (_t < _duration) return;
            var gs = GameState.Instance;
            if (gs != null) gs.Money += _sold;
            if (_sold > 0)
            {
                SoundPlayer.Play("coins");
                var popup = _player != null && _player.PopupSpawn != null ? _player.PopupSpawn.position : transform.position;
                PopupText.Show("Drone sold all fish for $" + _sold, popup, Color.green);
            }
            foreach (var f in _fish)
                if (f.fish != null) Destroy(f.fish.gameObject);
            Destroy(gameObject);
        }
    }
}
