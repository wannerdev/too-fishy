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
        /// reported as a mouse click, so there only taps on the right half of the screen count (TouchControls).
        /// </summary>
        public static bool MouseFireAllowed => !Application.isMobilePlatform && Input.touchCount == 0;

        // Written by TouchControls
        public static Vector2 VirtualMove;
        static bool _virtualFire, _virtualBuoy, _virtualDrone, _virtualPickaxe, _virtualInventory, _virtualPause;
        static Vector2 _virtualAim;
        static bool _hasVirtualAim;

        public static void PressFire(Vector2 screenPos)
        {
            _virtualFire = true;
            _virtualAim = screenPos;
            _hasVirtualAim = true;
        }

        public static void PressBuoy() => _virtualBuoy = true;
        public static void PressDrone() => _virtualDrone = true;
        public static void PressPickaxe() => _virtualPickaxe = true;
        public static void PressInventory() => _virtualInventory = true;
        public static void PressPause() => _virtualPause = true;

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
        /// Fire request for this frame. In touch mode only the on-screen aim pad fires, so taps on
        /// the joystick or on buttons never launch a harpoon. With a mouse, clicks over UI are ignored.
        /// </summary>
        public static bool ConsumeFire(out Vector2 aimScreenPos)
        {
            if (_virtualFire)
            {
                _virtualFire = false;
                aimScreenPos = _virtualAim;
                return true;
            }
            if (MouseFireAllowed && Input.GetMouseButtonDown(0) && !IsPointerOverUI())
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
        public static bool ConsumeQuickSave() => Input.GetKeyDown(KeyCode.V);

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
