using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
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
        bool _dockOnly;
        bool _touchTest;
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
                d._dockOnly = System.Array.IndexOf(args, "-tfdock") >= 0;
                d._touchTest = GameInput.TestTouchUi;
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

        void DumpRocks()
        {
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var r = mf.GetComponent<Renderer>();
                if (r == null || mf.sharedMesh == null) continue;
                string path = mf.name; var t = mf.transform.parent;
                while (t != null) { path = t.name + "/" + path; t = t.parent; }
                if (!path.Contains("Gestein") && !path.Contains("Veins") && !path.Contains("Rock")) continue;
                var b = r.bounds;
                if (b.center.y < -70f) continue;
                File.AppendAllText(Path.Combine(_dir, "director.log"),
                    $"ROCK {path} mats={r.sharedMaterials.Length} center=({b.center.x:0.00},{b.center.y:0.00},{-b.center.z:0.00}) size=({b.size.x:0.00},{b.size.y:0.00},{b.size.z:0.00})\n");
            }
        }
        // ---- -tftouchtest: drives TouchControls through the real EventSystem raycast with synthetic
        // pointers, one pointer id per finger (a desktop player has no multi-touch hardware).
        readonly Dictionary<int, PointerEventData> _sim = new Dictionary<int, PointerEventData>();

        GameObject SimDown(int id, Vector2 pos)
        {
            var es = EventSystem.current;
            var e = new PointerEventData(es) { pointerId = id, position = pos, pressPosition = pos, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            es.RaycastAll(e, hits);
            var hit = hits.Count > 0 ? hits[0] : default;
            e.pointerCurrentRaycast = e.pointerPressRaycast = hit;
            GameObject go = hit.gameObject;
            if (go != null)
            {
                e.pointerPress = ExecuteEvents.ExecuteHierarchy(go, e, ExecuteEvents.pointerDownHandler);
                e.rawPointerPress = go;
                e.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(go);
            }
            _sim[id] = e;
            return go;
        }

        void SimMove(int id, Vector2 pos)
        {
            var e = _sim[id];
            e.delta = pos - e.position;
            e.position = pos;
            if (e.pointerDrag != null)
            {
                if (!e.dragging) { e.dragging = true; ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.beginDragHandler); }
                ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.dragHandler);
            }
        }

        void SimUp(int id)
        {
            var e = _sim[id];
            if (e.pointerPress != null)
            {
                ExecuteEvents.Execute(e.pointerPress, e, ExecuteEvents.pointerUpHandler);
                if (!e.dragging && ExecuteEvents.GetEventHandler<IPointerClickHandler>(e.rawPointerPress) == e.pointerPress)
                    ExecuteEvents.Execute(e.pointerPress, e, ExecuteEvents.pointerClickHandler);
            }
            if (e.dragging && e.pointerDrag != null) ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.endDragHandler);
            _sim.Remove(id);
        }

        static bool JoystickVisible()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == "JoyBase" && t.gameObject.scene.IsValid()) return t.gameObject.activeInHierarchy;
            return false;
        }

        void T(string m) => Log("TOUCHTEST " + m);

        IEnumerator TouchTest(GameState gs)
        {
            if (gs.IsIntro()) gs.Level.SkipIntro();
            yield return Wait(1.5f);
            yield return SkipDialogs();
            gs.GodMode = true;
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade))) gs.Upgrades[u] = GameState.MaxUpgrades[u];
            Teleport(-8f, -12f);
            yield return Wait(3f);
            float W = Screen.width, H = Screen.height;
            var cam = Camera.main;
            Vector2 playerScreen = cam.WorldToScreenPoint(gs.Player.transform.position);
            T($"screen {W}x{H}, player at screen {playerScreen}, red ShootButton exists: {GameObject.Find("ShootButton") != null}");
            yield return Shot("touch_ui_idle");

            // 1. Taps on the right half fire at the tapped point
            foreach (var (fx, fy, label) in new[] { (0.80f, 0.62f, "tap_right_upper"), (0.62f, 0.18f, "tap_right_lower") })
            {
                var tap = new Vector2(W * fx, H * fy);
                int before = FindObjectsByType<Harpoon>(FindObjectsSortMode.None).Length;
                var hit = SimDown(1, tap);
                SimUp(1);
                yield return Wait(0.2f);
                var hs = FindObjectsByType<Harpoon>(FindObjectsSortMode.None);
                float z = hs.Length > before ? hs[0].transform.eulerAngles.z : float.NaN;
                Vector2 d = tap - (Vector2)cam.WorldToScreenPoint(gs.Player.transform.position);
                float want = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; if (want < 0) want += 360f;
                T($"{label}: tap {tap} hit={(hit != null ? hit.name : "none")} harpoons {before}->{hs.Length} harpoonAngle={z:0.0} expectedAngle~{want:0.0} joystickVisible={JoystickVisible()}");
                yield return Wait(0.1f);
                yield return Shot(label);
                yield return Wait(1.2f);
            }

            // 2. Multitouch: left finger steers while the right hand taps
            {
                var start = new Vector2(W * 0.18f, H * 0.4f);
                int before = FindObjectsByType<Harpoon>(FindObjectsSortMode.None).Length;
                var h1 = SimDown(10, start);
                SimMove(10, start + new Vector2(70f, 55f));
                T($"left finger down hit={(h1 != null ? h1.name : "none")} joystickVisible={JoystickVisible()} move={GameInput.VirtualMove}");
                var tap = new Vector2(W * 0.86f, H * 0.3f);
                var h2 = SimDown(11, tap);
                SimUp(11);
                yield return Wait(0.2f);
                int after = FindObjectsByType<Harpoon>(FindObjectsSortMode.None).Length;
                T($"second finger tap right hit={(h2 != null ? h2.name : "none")} harpoons {before}->{after} move still {GameInput.VirtualMove} joystickVisible={JoystickVisible()}");
                // A second finger on the left half must not steal or restart the joystick
                var before2 = GameInput.VirtualMove;
                SimDown(12, new Vector2(W * 0.3f, H * 0.2f));
                SimMove(12, new Vector2(W * 0.3f - 90f, H * 0.2f));
                T($"third finger on left half ignored: move {before2} -> {GameInput.VirtualMove}");
                SimUp(12);
                yield return Wait(0.6f);
                yield return Shot("multitouch_move_and_tap");
                SimUp(10);
                T($"left finger up: move={GameInput.VirtualMove} joystickVisible={JoystickVisible()}");
                yield return Wait(1f);
            }

            // 3. Top buttons: the touch goes to the button, never to the pad
            var canvasRoot = GameObject.Find("TouchControls");
            foreach (var name in new[] { "PauseBtn", "InventoryBtn", "BuoyBtn", "DroneBtn", "PickaxeBtn", "GunBtn" })
            {
                var t = canvasRoot.transform.Find(name);
                if (t == null || !t.gameObject.activeInHierarchy) { T($"{name}: not active"); continue; }
                var corners = new Vector3[4];
                ((RectTransform)t).GetWorldCorners(corners);
                Vector2 c = (corners[0] + corners[2]) * 0.5f;
                int before = FindObjectsByType<Harpoon>(FindObjectsSortMode.None).Length;
                var hit = SimDown(20, c);
                bool held = GameInput.VirtualShootHeld;
                yield return Wait(0.1f);
                if (name == "InventoryBtn") yield return Shot("top_button_inventory_tap");
                SimUp(20);
                yield return Wait(0.3f);
                int after = FindObjectsByType<Harpoon>(FindObjectsSortMode.None).Length;
                T($"{name}: centre {c} hit={(hit != null ? hit.name : "none")} harpoons {before}->{after} joystickVisible={JoystickVisible()} move={GameInput.VirtualMove} gunHeldWhileDown={held} afterUp={GameInput.VirtualShootHeld} paused={gs.Paused}");
                if (name == "PauseBtn") { yield return Shot("top_button_pause_tap"); GameInput.PressPause(); yield return Wait(0.5f); }
                if (name == "InventoryBtn") { GameInput.PressInventory(); yield return Wait(0.5f); }
            }
            T("done");
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
            if (_touchTest)
            {
                yield return TouchTest(gs);
                Application.Quit(0);
                yield break;
            }
            if (_dockOnly)
            {
                // Surface / dock views only (compared with godot-ref shot_director.gd "dock" mode)
                if (gs.IsIntro()) gs.Level.SkipIntro();
                yield return Wait(1.5f);
                yield return SkipDialogs();
                gs.GodMode = true;
                DumpRocks();
                foreach (var (x, y, label) in new[] { (-3f, 0.5f, "dock"), (-8f, 0.5f, "dock_left"), (-14f, 0.5f, "dock_far_left"), (-3f, -3f, "dock_below") })
                {
                    Teleport(x, y);
                    yield return Wait(3f);
                    var menu0 = FindFirstObjectByType<UpgradeMenu>();
                    if (menu0 != null && label != "dock") menu0.HandleEscape();
                    yield return Shot(label);
                }
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