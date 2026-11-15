using UnityEngine;
using UnityEngine.EventSystems;

namespace TooFishy
{
    /// <summary>
    /// Single place every gameplay input is read from. Keyboard and mouse stay as they were;
    /// <see cref="TouchControls"/> feeds the virtual fields on phones and touch browsers.
    /// One-shot actions (fire, buoy, drone, pickaxe, shop, pause) are consumed on read so a
    /// single tap triggers exactly one action.
    /// </summary>
    public static class GameInput
    {
        /// <summary>
        /// True when on-screen controls should be shown: always on Android and iOS; in a browser only
        /// when the device's main pointer is a finger (coarse) or after the first real touch. Unity's
        /// Input.touchSupported is true in every WebGL build, so it cannot tell a phone from a PC.
        /// </summary>
        public static bool TouchMode => Application.isMobilePlatform || WebTouch.Detected || TestTouchUi;

        /// <summary>Dev tool: <c>-tftouchtest</c> builds and shows the touch UI in a desktop player (ScreenshotDirector).</summary>
        public static readonly bool TestTouchUi = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tftouchtest") >= 0;

        /// <summary>Whether the touch UI has to be built at all (it is created hidden in browsers).</summary>
        public static bool TouchUiPossible => Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer || TestTouchUi;

        /// <summary>
        /// Mouse clicks fire the harpoon on desktops, including desktop browsers and laptops with a
        /// touch screen (where <see cref="TouchMode"/> is true too). On phones every tap is also
        /// reported as a mouse click, so there only taps that TouchControls turns into fire count:
        /// the right half, and with the Rotatable Harpoon also quick taps on the left half.
        /// </summary>
        public static bool MouseFireAllowed => !Application.isMobilePlatform && Input.touchCount == 0;

        // Written by TouchControls
        public static Vector2 VirtualMove;
        static bool _virtualFire, _virtualBuoy, _virtualDrone, _virtualPickaxe, _virtualSave, _virtualInventory, _virtualPause;
        static Vector2 _virtualAim;
        static bool _hasVirtualAim;

        /// <summary>
        /// player.gd:102-104 / 270-293 check !GameState.paused at the moment of the press: a gameplay
        /// press made while paused, in a pausing dialog or on the death screen is dropped, not queued.
        /// </summary>
        static bool GameplayBlocked
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null && (gs.Paused || gs.DeathScreen);
            }
        }

        public static void PressFire(Vector2 screenPos)
        {
            // player.gd:347-350 is_mouse_over_ui(): an open inventory menu blocks every throw
            if (GameplayBlocked || InventoryMenu.AnyOpen) return;
            _virtualFire = true;
            _virtualAim = screenPos;
            _hasVirtualAim = true;
        }

        public static void PressBuoy() { if (!GameplayBlocked) _virtualBuoy = true; }
        public static void PressDrone() { if (!GameplayBlocked) _virtualDrone = true; }
        public static void PressPickaxe() { if (!GameplayBlocked) _virtualPickaxe = true; }
        /// <summary>The touch SAVE button: Godot "inventory_save" (V, player.gd:281-288).</summary>
        public static void PressSave() { if (!GameplayBlocked) _virtualSave = true; }
        // Not blocked: GameUI reads these while paused (the II button has to resume)
        public static void PressInventory() => _virtualInventory = true;
        public static void PressPause() => _virtualPause = true;

        /// <summary>
        /// GameUI calls this every frame the game is frozen: PlayerController does not run then, so a
        /// press from the frame the pause started would otherwise fire after resuming.
        /// </summary>
        public static void ClearGameplayPresses()
        {
            _virtualFire = _virtualBuoy = _virtualDrone = _virtualPickaxe = _virtualSave = false;
        }

        /// <summary>-1..1. Screen-right is positive.</summary>
        public static float Horizontal
        {
            get
            {
                float x = VirtualMove.x;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x = 1f;
                else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x = -1f;
                return Mathf.Clamp(x, -1f, 1f);
            }
        }

        /// <summary>-1..1. Up is positive.</summary>
        public static float Vertical
        {
            get
            {
                float y = VirtualMove.y;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y = 1f;
                else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y = -1f;
                return Mathf.Clamp(y, -1f, 1f);
            }
        }

        /// <summary>
        /// Fire request for this frame. In touch mode only the on-screen pad fires (TouchControls), so
        /// taps on buttons never launch a harpoon and joystick drags never do. With a mouse, clicks
        /// over UI are ignored.
        /// While the inventory menu is open nothing fires (player.gd:347-350 is_mouse_over_ui()); the
        /// request is still consumed so it does not fire once the menu closes.
        /// </summary>
        public static bool ConsumeFire(out Vector2 aimScreenPos)
        {
            bool inventoryOpen = InventoryMenu.AnyOpen;
            if (_virtualFire)
            {
                _virtualFire = false;
                if (!inventoryOpen)
                {
                    aimScreenPos = _virtualAim;
                    return true;
                }
            }
            if (!inventoryOpen && MouseFireAllowed && Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                aimScreenPos = Input.mousePosition;
                return true;
            }
            aimScreenPos = _hasVirtualAim && !MouseFireAllowed ? _virtualAim : (Vector2)Input.mousePosition;
            return false;
        }

        /// <summary>Godot "shoot" action: right mouse button held (AK47s).</summary>
        public static bool VirtualShootHeld;
        public static bool ShootHeld => VirtualShootHeld || (MouseFireAllowed && Input.GetMouseButton(1) && !IsPointerOverUI());

        /// <summary>Godot "inventory_save" action (V): quick save with Inventory Insurance.</summary>
        public static bool ConsumeQuickSave() => Consume(ref _virtualSave) || Input.GetKeyDown(KeyCode.V);

        /// <summary>Godot "cht_toggle" action (C): cheats panel.</summary>
        public static bool ConsumeCheats() => Input.GetKeyDown(KeyCode.C);

        public static bool ConsumeBuoy() => Consume(ref _virtualBuoy) || Input.GetKeyDown(KeyCode.B);
        public static bool ConsumeDrone() => Consume(ref _virtualDrone) || Input.GetKeyDown(KeyCode.Q);
        public static bool ConsumePickaxe() => Consume(ref _virtualPickaxe) || Input.GetKeyDown(KeyCode.Space);
        public static bool ConsumeInventory() => Consume(ref _virtualInventory) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab);
        public static bool ConsumePause() => Consume(ref _virtualPause) || Input.GetKeyDown(KeyCode.Escape);

        static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

        static bool IsPointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
