using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Touch controls for phones and touch browsers. A deliberate redesign, not a port of
    /// scripts/touch_controls.gd: Godot's red shoot button aims at the button itself, so the harpoon
    /// could only ever fly towards the bottom-right corner.
    ///
    /// - Left half of the screen: a floating joystick (grey base r 100, knob r 50 on the desktop
    ///   canvas; about 9 mm of travel on phones and tablets) that starts where
    ///   a finger lands and follows that finger until it lifts. With the Rotatable Harpoon a quick
    ///   tap there (short, almost no movement) fires at the tapped point, and so does a second
    ///   finger while the joystick is held: Godot turns every touch into a click that aims at the
    ///   touch point (player.gd:389-399), so the upgrade aims all 360 degrees.
    /// - Right half: a tap fires the harpoon at the tapped point, like a mouse click on desktop.
    /// - Every finger is tracked by its pointer id, so the left thumb can steer while the right
    ///   hand taps.
    /// - The buttons (II, INV in a column under the achievement panel, or at the left edge under the
    ///   bank/fish/capacity box on phones; DIG, GUN, BUOY, SELL, SAVE along the bottom-right edge,
    ///   see Layout) sit above the full-screen pad, so the EventSystem hands a
    ///   touch on them to the button and the pad never sees it: such a touch neither fires nor
    ///   starts the joystick. Godot reaches the inventory, buoy, drone, pickaxe, quick save and
    ///   pause only through keys, which a phone does not have (the upgrade-bound ones appear once
    ///   the upgrade is owned). They are sized to about 9 mm on the real screen.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        const int NoPointer = int.MinValue;
        // A left-half touch shorter and stiller than this is a tap (fires with the Rotatable Harpoon)
        const float TapMaxSeconds = 0.25f;

        RectTransform _root, _joyBase, _joyKnob;
        int _joyPointer = NoPointer;
        Vector2 _joyDownScreen;
        float _joyDownTime;
        bool _joyMoved;
        Button _pauseBtn, _invBtn, _buoyBtn, _droneBtn, _pickaxeBtn, _gunBtn, _saveBtn;
        Rect _layoutSafe;
        // Joystick travel and tap drift in canvas units, set by Layout() (100 / 25 on desktop)
        float _joyRadius = 100f, _tapDrift = 25f;

        public static TouchControls Create(Transform canvasRoot)
        {
            var root = UiKit.Fill(UiKit.Rect(canvasRoot, "TouchControls"));
            var tc = root.gameObject.AddComponent<TouchControls>();
            tc._root = root;
            tc.Build();
            return tc;
        }

        void Build()
        {
            // Full-screen pad below the buttons: left half = joystick, right half = aim and fire
            var pad = UiKit.ColorRect(_root, "JoystickPad", new Color(0, 0, 0, 0));
            pad.raycastTarget = true;
            UiKit.Fill(pad.rectTransform);
            pad.gameObject.AddComponent<JoystickPad>().Owner = this;

            _joyBase = UiKit.Picture(_root, "JoyBase", UiKit.Circle, false).rectTransform;
            _joyBase.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _joyKnob = UiKit.Picture(_root, "JoyKnob", UiKit.Circle, false).rectTransform;
            _joyKnob.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            foreach (var rt in new[] { _joyBase, _joyKnob })
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.gameObject.SetActive(false);
            }

            // On-screen buttons (not in Godot, see class comment); placed by Layout()
            _pauseBtn = SmallButton("PauseBtn", "II", GameInput.PressPause);
            _invBtn = SmallButton("InventoryBtn", "INV", GameInput.PressInventory);
            _pickaxeBtn = SmallButton("PickaxeBtn", "DIG", GameInput.PressPickaxe);
            // Hold to fire the AK47s (Godot: hold the right mouse button)
            _gunBtn = SmallButton("GunBtn", "GUN", null);
            _gunBtn.gameObject.AddComponent<HoldButton>();
            _buoyBtn = SmallButton("BuoyBtn", "BUOY", GameInput.PressBuoy);
            _droneBtn = SmallButton("DroneBtn", "SELL", GameInput.PressDrone);
            _saveBtn = SmallButton("SaveBtn", "SAVE", GameInput.PressSave);
            Layout();
            // Subscribed here, not in OnEnable: the canvas stays hidden until a touch and must
            // already have the right layout when it appears
            UiScale.Changed += Layout;
        }

        Button SmallButton(string name, string label, UnityEngine.Events.UnityAction onClick) =>
            UiKit.Button(_root, name, label, 16, UiKit.MenuButton, onClick);

        /// <summary>
        /// Button edge in canvas units for about 9 mm on the real screen (Android's 48 dp touch
        /// target): 9 mm / <see cref="UiScale.MmPerUnit"/>, about 57 units on phones and tablets,
        /// where one unit is one dp. The 1920x1080 desktop canvas (Factor 1) keeps its minimum of
        /// 80 units, so it is sized exactly as before. With no known screen size (a desktop browser)
        /// the short screen side counts as a phone's (about 70 mm), as before.
        /// </summary>
        static float ButtonSize()
        {
            float min = UiScale.Factor <= 1.0001f ? 80f : 52f;
            if (UiScale.MmPerUnit > 0f) return Mathf.Clamp(9f / UiScale.MmPerUnit, min, 150f);
            float pxPerMm = Mathf.Max(0.01f, Mathf.Min(Screen.width, Screen.height) / 70f);
            float unitPx = Mathf.Max(0.01f, Mathf.Min(Screen.width / UiScale.RefWidth, Screen.height / UiScale.RefHeight));
            return Mathf.Clamp(9f * pxPerMm / unitPx * UiScale.Factor, min, 150f);
        }

        /// <summary>Joystick travel (base radius): about 9 mm on phones and tablets, Godot's 100 units on desktop.</summary>
        static float JoystickRadiusFor()
        {
            if (UiScale.Factor <= 1.0001f || UiScale.MmPerUnit <= 0f) return 100f;
            return Mathf.Clamp(9f / UiScale.MmPerUnit, 40f, 100f);
        }

        /// <summary>Screen cut-outs (notch, rounded corners, gesture bar) in canvas units.</summary>
        static void SafeInsets(Vector2 canvas, out float left, out float right, out float bottom)
        {
            left = right = bottom = 0f;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect sa = Screen.safeArea;
            float kx = canvas.x / Screen.width, ky = canvas.y / Screen.height;
            left = Mathf.Max(0f, sa.xMin) * kx;
            right = Mathf.Max(0f, Screen.width - sa.xMax) * kx;
            bottom = Mathf.Max(0f, sa.yMin) * ky;
        }

        /// <summary>
        /// Runs at creation, on <see cref="UiScale.Changed"/> and when the safe area moves.
        ///
        /// Desktop and tablet canvases (not <see cref="UiScale.Compact"/>): II and INV in a column at
        /// the right edge below the achievement panel (main_scene.tscn x -190..0, y 105..525); the
        /// action buttons in a row along the bottom-right edge, clear of the HUD, the boss bar and the
        /// centred menus. Everything is anchored to the right edge, so it fits any canvas width; on the
        /// 1920x1080 canvas it is exactly the old layout.
        ///
        /// Phones (Compact, shared phone screen map): II and INV stacked at the left edge below InvUi
        /// (x 6..62, y 120..), the action row (DIG, GUN, BUOY, SELL, SAVE) along the bottom-right edge
        /// with an 8-unit margin; both moved inwards by the screen's safe-area insets.
        ///
        /// Every slot stays put when a button is hidden.
        /// </summary>
        void Layout()
        {
            _layoutSafe = Screen.safeArea;
            Vector2 canvas = UiScale.CanvasSize;
            float s = ButtonSize();

            _joyRadius = JoystickRadiusFor();
            _tapDrift = _joyRadius * 0.25f; // 25 units for the desktop radius of 100
            _joyBase.sizeDelta = Vector2.one * _joyRadius * 2f;
            _joyKnob.sizeDelta = Vector2.one * _joyRadius;

            if (UiScale.Compact)
            {
                const float gap = 8f, edge = 6f, margin = 8f, columnTop = 120f;
                SafeInsets(canvas, out float insetL, out float insetR, out float insetB);
                // The column must end above the bottom margin (only matters on very small canvases)
                s = Mathf.Max(44f, Mathf.Min(s, (canvas.y - margin - insetB - columnTop - gap) * 0.5f));
                int font = Mathf.Max(14, Mathf.RoundToInt(s * 0.24f));

                float x0 = edge + insetL;
                Place(_pauseBtn, 0, 0, x0, columnTop, x0 + s, columnTop + s, font);
                Place(_invBtn, 0, 0, x0, columnTop + s + gap, x0 + s, columnTop + 2 * s + gap, font);

                float right = -margin - insetR, bottom = -margin - insetB;
                foreach (var b in new[] { _pickaxeBtn, _gunBtn, _buoyBtn, _droneBtn, _saveBtn })
                {
                    Place(b, 1, 1, right - s, bottom - s, right, bottom, font);
                    right -= s + gap;
                }
            }
            else
            {
                float w = s * 1.1f;
                const float gap = 14f, margin = 20f;
                // Below the achievement panel; moves up only if the column would reach the row (never at 1080)
                float columnTop = Mathf.Min(540f, canvas.y - margin - s - gap - (2 * s + gap) - gap);
                int font = Mathf.Max(14, Mathf.RoundToInt(s * 0.24f));

                Place(_pauseBtn, 1, 0, -margin - w, columnTop, -margin, columnTop + s, font);
                Place(_invBtn, 1, 0, -margin - w, columnTop + s + gap, -margin, columnTop + 2 * s + gap, font);

                float right = -margin;
                foreach (var b in new[] { _pickaxeBtn, _gunBtn, _buoyBtn, _droneBtn, _saveBtn })
                {
                    Place(b, 1, 1, right - w, -margin - s, right, -margin, font);
                    right -= w + gap;
                }
            }
        }

        static void Place(Button b, float anchorX, float anchorY, float left, float top, float right, float bottom, int font)
        {
            UiKit.Place(b.GetComponent<RectTransform>(), anchorX, anchorY, anchorX, anchorY, left, top, right, bottom);
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.fontSize = font;
        }

        void Update()
        {
            if (Screen.safeArea != _layoutSafe) Layout(); // a landscape flip moves the notch to the other side
            var gs = GameState.Instance;
            if (gs == null) return;
            _buoyBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0);
            _droneBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0);
            _pickaxeBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0);
            _saveBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.InventorySave) > 0);
            bool gun = gs.GetUpgradeLevel(Upgrade.Ak47) > 0;
            if (!gun) GameInput.VirtualShootHeld = false;
            _gunBtn.gameObject.SetActive(gun);
        }

        Vector2 ToLocal(Vector2 screenPos, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPos, cam, out var local);
            return local - _root.rect.min; // bottom-left origin
        }

        /// <summary>True for a touch that starts on the left half of the screen.</summary>
        public static bool IsLeftHalf(Vector2 screenPos) => screenPos.x < Screen.width * 0.5f;

        /// <summary>The Rotatable Harpoon aims at the touch point (player.gd:389-399); without it the aim is ignored.</summary>
        static bool RotationOwned()
        {
            var gs = GameState.Instance;
            return gs != null && gs.GetUpgradeLevel(Upgrade.HarpoonRotation) > 0;
        }

        /// <summary>
        /// A finger landed on the pad (nothing else was under it). Left half: it becomes the joystick
        /// finger unless another finger already is (that one fires with the Rotatable Harpoon).
        /// Right half: fire at that point.
        /// </summary>
        public void PadDown(int pointerId, Vector2 screenPos, Camera cam)
        {
            if (IsLeftHalf(screenPos))
            {
                if (_joyPointer != NoPointer)
                {
                    if (RotationOwned()) GameInput.PressFire(screenPos);
                    return;
                }
                _joyPointer = pointerId;
                _joyDownScreen = screenPos;
                _joyDownTime = Time.unscaledTime;
                _joyMoved = false;
                var p = ToLocal(screenPos, cam);
                _joyBase.anchoredPosition = p;
                _joyKnob.anchoredPosition = p;
                _joyBase.gameObject.SetActive(true);
                _joyKnob.gameObject.SetActive(true);
                GameInput.VirtualMove = Vector2.zero;
            }
            else
            {
                GameInput.PressFire(screenPos);
            }
        }

        public void PadDrag(int pointerId, Vector2 screenPos, Camera cam)
        {
            if (pointerId != _joyPointer) return;
            Vector2 origin = _joyBase.anchoredPosition;
            Vector2 offset = ToLocal(screenPos, cam) - origin;
            if (offset.sqrMagnitude > _tapDrift * _tapDrift) _joyMoved = true;
            Vector2 d = Vector2.ClampMagnitude(offset, _joyRadius);
            _joyKnob.anchoredPosition = origin + d;
            // Dead zone = the tap drift: a quick tap that fires must not also nudge the sub
            GameInput.VirtualMove = _joyMoved ? d / _joyRadius : Vector2.zero;
        }

        public void PadUp(int pointerId)
        {
            if (pointerId != _joyPointer) return;
            bool tap = !_joyMoved && Time.unscaledTime - _joyDownTime <= TapMaxSeconds;
            ReleaseJoystick();
            // A quick left-half tap is a shot at the tapped point, not a joystick (Godot: every touch is a click)
            if (tap && RotationOwned()) GameInput.PressFire(_joyDownScreen);
        }

        void ReleaseJoystick()
        {
            _joyPointer = NoPointer;
            _joyBase.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);
            GameInput.VirtualMove = Vector2.zero;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void OnDisable()
        {
            if (_joyPointer != NoPointer && _joyBase != null) ReleaseJoystick();
        }

        // A call, the notification shade or the app switcher can end a touch without a pointer-up:
        // drop the joystick and the GUN hold so the sub does not drift and the guns stop.
        void OnApplicationPause(bool paused)
        {
            if (paused) ReleaseAllHeld();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseAllHeld();
        }

        void ReleaseAllHeld()
        {
            if (_joyPointer != NoPointer && _joyBase != null) ReleaseJoystick();
            GameInput.VirtualShootHeld = false;
        }

        class JoystickPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICanvasRaycastFilter
        {
            public TouchControls Owner;

            /// <summary>
            /// The pad covers the whole screen. It only catches real touches, so a mouse on a desktop
            /// browser (or a laptop with a touch screen) is never blocked by it and keeps shooting.
            /// Keyboard input never goes through the UI and is read in <see cref="GameInput"/>.
            /// </summary>
            public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
                GameInput.TestTouchUi || Application.isMobilePlatform || Input.touchCount > 0;

            public void OnPointerDown(PointerEventData e) => Owner.PadDown(e.pointerId, e.position, e.pressEventCamera);
            public void OnDrag(PointerEventData e) => Owner.PadDrag(e.pointerId, e.position, e.pressEventCamera);
            public void OnPointerUp(PointerEventData e) => Owner.PadUp(e.pointerId);
        }

        class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public void OnPointerDown(PointerEventData e) => GameInput.VirtualShootHeld = true;
            public void OnPointerUp(PointerEventData e) => GameInput.VirtualShootHeld = false;
            void OnDisable() => GameInput.VirtualShootHeld = false;
        }
    }
}
