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
    /// - Left half of the screen: a floating joystick (grey base r 100, knob r 50) that starts where
    ///   a finger lands and follows that finger until it lifts.
    /// - Right half: a tap fires the harpoon at the tapped point, like a mouse click on desktop.
    /// - Every finger is tracked by its pointer id, so the left thumb can steer while the right
    ///   hand taps.
    /// - The buttons along the top edge (II, INV, BUOY, SELL, DIG, GUN) sit above the full-screen
    ///   pad, so the EventSystem hands a touch on them to the button and the pad never sees it:
    ///   such a touch neither fires nor starts the joystick. Godot reaches the inventory, buoy,
    ///   drone, pickaxe and pause only through keys, which a phone does not have (the upgrade-bound
    ///   ones appear once the upgrade is owned).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        const float JoystickRadius = 100f;
        const int NoPointer = int.MinValue;

        RectTransform _root, _joyBase, _joyKnob;
        int _joyPointer = NoPointer;
        Button _buoyBtn, _droneBtn, _pickaxeBtn, _gunBtn;

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
            _joyBase.sizeDelta = Vector2.one * JoystickRadius * 2f;
            _joyKnob = UiKit.Picture(_root, "JoyKnob", UiKit.Circle, false).rectTransform;
            _joyKnob.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            _joyKnob.sizeDelta = Vector2.one * 100f;
            foreach (var rt in new[] { _joyBase, _joyKnob })
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.gameObject.SetActive(false);
            }

            // Top-edge buttons (not in Godot, see class comment)
            float x = -20f;
            SmallButton("PauseBtn", "II", ref x, GameInput.PressPause);
            SmallButton("InventoryBtn", "INV", ref x, GameInput.PressInventory);
            _buoyBtn = SmallButton("BuoyBtn", "BUOY", ref x, GameInput.PressBuoy);
            _droneBtn = SmallButton("DroneBtn", "SELL", ref x, GameInput.PressDrone);
            _pickaxeBtn = SmallButton("PickaxeBtn", "DIG", ref x, GameInput.PressPickaxe);
            // Hold to fire the AK47s (Godot: hold the right mouse button)
            _gunBtn = SmallButton("GunBtn", "GUN", ref x, null);
            _gunBtn.gameObject.AddComponent<HoldButton>();
        }

        Button SmallButton(string name, string label, ref float right, UnityEngine.Events.UnityAction onClick)
        {
            var b = UiKit.Button(_root, name, label, 16, UiKit.MenuButton, onClick);
            UiKit.Place(b.GetComponent<RectTransform>(), 1, 0, 1, 0, right - 80, 540, right, 590);
            right -= 90f;
            return b;
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            _buoyBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0);
            _droneBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0);
            _pickaxeBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0);
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

        /// <summary>
        /// A finger landed on the pad (nothing else was under it). Left half: it becomes the joystick
        /// finger unless another finger already is. Right half: fire at that point.
        /// </summary>
        public void PadDown(int pointerId, Vector2 screenPos, Camera cam)
        {
            if (IsLeftHalf(screenPos))
            {
                if (_joyPointer != NoPointer) return;
                _joyPointer = pointerId;
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
            Vector2 d = Vector2.ClampMagnitude(ToLocal(screenPos, cam) - origin, JoystickRadius);
            _joyKnob.anchoredPosition = origin + d;
            GameInput.VirtualMove = d / JoystickRadius;
        }

        public void PadUp(int pointerId)
        {
            if (pointerId != _joyPointer) return;
            ReleaseJoystick();
        }

        void ReleaseJoystick()
        {
            _joyPointer = NoPointer;
            _joyBase.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);
            GameInput.VirtualMove = Vector2.zero;
        }

        void OnDisable()
        {
            if (_joyPointer != NoPointer && _joyBase != null) ReleaseJoystick();
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
