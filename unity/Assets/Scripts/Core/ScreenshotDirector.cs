using System.Collections;
using System.IO;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Dev tool for the PR screenshots. Only does anything when the player is started with
    /// <c>-tfshots &lt;output dir&gt;</c>: it plays through dock, diving, catching, HUD, upgrades,
    /// deeper stages and the boss, saves a JPG per step and quits.
    /// </summary>
    public class ScreenshotDirector : MonoBehaviour
    {
        string _dir;
        bool _intro;
        int _n;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-tfshots") continue;
                var go = new GameObject("ScreenshotDirector");
                DontDestroyOnLoad(go);
                var d = go.AddComponent<ScreenshotDirector>();
                d._dir = args[i + 1];
                d._intro = System.Array.IndexOf(args, "-tfintro") >= 0;
                if (!d._intro && GameState.Instance != null) GameState.Instance.EnableIntroMission = false;
                Directory.CreateDirectory(d._dir);
                d.StartCoroutine(d.Run());
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, $"{++_n:00}_{name}.jpg"), tex.EncodeToJPG(88));
            Destroy(tex);
            Log($"shot {name} {tex.width}x{tex.height}");
        }

        void Log(string m)
        {
            var gs = GameState.Instance;
            var p = gs != null && gs.Player != null ? gs.Player.transform.position : Vector3.zero;
            File.AppendAllText(Path.Combine(_dir, "director.log"),
                $"{Time.realtimeSinceStartup:0.0}s depth={(gs != null ? gs.Depth : 0)} pos={p} hp={(gs != null ? gs.Health : 0)} docked={(gs != null && gs.IsDocked)} {m}\n");
        }

        IEnumerator Wait(float s)
        {
            float t = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < t) yield return null;
        }

        IEnumerator Run()
        {
            Log("start");
            while (GameState.Instance == null || GameState.Instance.Player == null || GameState.Instance.Level == null) yield return null;
            var gs = GameState.Instance;
            yield return Wait(2.5f);
            Log("loaded");
            if (_intro)
            {
                yield return Shot("intro_first_dialog");
                yield return SkipDialogs();
                yield return Wait(2f);
                yield return Shot("intro_friend_sub_450m");
                yield return Wait(5f);
                yield return Shot("intro_friend_sub_later");
                Application.Quit(0);
                yield break;
            }
            if (gs.IsIntro()) gs.Level.SkipIntro();
            yield return Wait(1.5f);
            yield return SkipDialogs();
            gs.GodMode = true;
            gs.Money = 2000;
            Teleport(-3f, 0.5f);
            yield return Wait(3f);
            Log("after skip");
            yield return Shot("dock_shop_opens");
            var menu = FindFirstObjectByType<UpgradeMenu>();
            if (menu != null) menu.HandleEscape();
            yield return Wait(1f);
            yield return Shot("dock");
            GameInput.PressInventory();
            yield return Wait(1f);
            yield return Shot("upgrades_or_inventory");
            GameInput.PressInventory();
            if (menu != null) menu.HandleEscape();
            yield return Wait(0.5f);

            // Max the upgrades for the rest of the run.
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade))) gs.Upgrades[u] = GameState.MaxUpgrades[u];
            yield return Wait(1f);
            yield return Shot("dock_with_upgrades");

            // Diving
            Teleport(-8f, -12f);
            yield return Wait(3f);
            yield return Shot("diving_shallow");
            Teleport(-8f, -45f);
            yield return Wait(4f);
            yield return Shot("diving_deep");

            // Catching: fire the harpoon at the closest fish.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var fish = Nearest(gs.Player.transform.position, 14f);
                if (fish == null) { yield return Wait(2f); continue; }
                int before = gs.Inventory.Items.Count;
                var cam = Camera.main;
                Vector3 sp = cam.WorldToScreenPoint(fish.transform.position);
                GameInput.PressFire(new Vector2(sp.x, sp.y));
                yield return Wait(0.25f);
                if (attempt == 0) yield return Shot("catching_harpoon_fired");
                yield return Wait(0.6f);
                if (gs.Inventory.Items.Count > before)
                {
                    yield return Shot("catching_catch_popup");
                    break;
                }
                yield return Wait(1.1f);
            }
            yield return Wait(1f);
            yield return Shot("hud_cargo");

            // Pickaxe on a stage barrier (row of crates at -150 m)
            Teleport(-8f, -146.5f);
            yield return Wait(4f);
            yield return Shot("barrier_crates");
            GameInput.PressPickaxe();
            yield return Wait(0.35f);
            yield return Shot("pickaxe_swing");

            // Lava (new procedural shader): visit the nearest lava quads in the shallow main scene
            {
                var lavaRenderers = new System.Collections.Generic.List<Renderer>();
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "TooFishy/Lava" && r.bounds.center.y > -80f)
                        lavaRenderers.Add(r);
                lavaRenderers.Sort((a, b) => b.bounds.center.y.CompareTo(a.bounds.center.y));
                Log($"lava renderers in main scene: {lavaRenderers.Count}");
                int li = 0;
                foreach (var r in lavaRenderers)
                {
                    if (li >= 3) break;
                    var c = r.bounds.center;
                    Log($"lava {li} center={c} size={r.bounds.size}");
                    Teleport(c.x < -15f ? c.x + 4f : c.x - 6f, c.y); // stand in open water next to the lava, not inside the rock
                    yield return Wait(3.5f);
                    yield return Shot($"lava_{li++}");
                }
            }
            // Section seams and the left/right edge of the backgrounds (the old build showed the skybox here)
            foreach (var (x, y, label) in new[] { (-8f, -37.5f, "seam_x8_y37"), (-26f, -62.5f, "seam_x26_y62"), (-3f, -87.5f, "seam_x3_y87") })
            {
                Teleport(x, y);
                yield return Wait(3.5f);
                yield return Shot(label);
            }
            // Deeper stages (clear of the crate rows at -150/-250/-350/-450 m)
            foreach (var (y, label) in new[] { (-135f, "stage_deep_135m"), (-235f, "stage_deeper_235m"), (-335f, "stage_superdeep_335m"), (-435f, "stage_hot_435m"), (-485f, "stage_lava_485m") })
            {
                Teleport(-8f, y);
                yield return Wait(4.5f);
                yield return Shot(label);
            }

            // Boss
            Teleport(-8f, -520f);
            yield return Wait(5f);
            yield return Shot("boss_dialog");
            yield return SkipDialogs();
            yield return Wait(3f);
            var boss = BossController.Current;
            Log(boss != null ? $"boss at {boss.transform.position}" : "no boss");
            if (boss != null) { Teleport(-8f, boss.transform.position.y - 5f); yield return Wait(1.5f); }
            yield return Shot("boss_approach");
            for (int k = 0; k < 8; k++)
            {
                boss = BossController.Current;
                if (boss != null && Camera.main != null)
                {
                    Vector3 sp = Camera.main.WorldToScreenPoint(boss.transform.position);
                    GameInput.PressFire(new Vector2(sp.x, sp.y));
                }
                yield return Wait(1.2f);
                yield return Shot($"boss_fight_{k + 1}");
                yield return SkipDialogs();
            }
            Log("done");
            Application.Quit(0);
        }

        IEnumerator SkipDialogs()
        {
            for (int i = 0; i < 12; i++) { Dialogs.Skip(); yield return Wait(0.2f); }
        }
        FishBehaviour Nearest(Vector3 from, float max)
        {
            FishBehaviour best = null;
            float bd = max;
            foreach (var f in FindObjectsByType<FishBehaviour>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(f.transform.position, from);
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        static void Teleport(float x, float y)
        {
            var p = GameState.Instance.Player;
            p.Teleport(new Vector3(x, y, p.transform.position.z));
        }
    }
}