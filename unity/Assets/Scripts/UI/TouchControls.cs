using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/touch_controls.gd (phones and touch browsers): a floating joystick that
    /// appears wherever a touch starts (grey base r 100, knob r 50) and the red 150 px shoot
    /// button with a white cross in the bottom-right corner.
    ///
    /// Godot reaches the inventory, buoy, drone, pickaxe and pause only through keys, which a
    /// phone does not have, so small buttons for those are added along the top edge (the
    /// upgrade-bound ones only once the upgrade is owned).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        const float JoystickRadius = 100f;

        RectTransform _root, _joyBase, _joyKnob;
        Image _shoot;
        Button _buoyBtn, _droneBtn, _pickaxeBtn;

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
            // Everything outside the buttons starts the joystick
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

            // Shoot button: 150×150, 20 px from the bottom-right corner
            _shoot = UiKit.Picture(_root, "ShootButton", UiKit.Circle, false);
            _shoot.raycastTarget = true;
            _shoot.color = new Color(1f, 0.3f, 0.3f, 0.7f);
            UiKit.Place(_shoot.rectTransform, 1, 1, 1, 1, -170, -170, -20, -20);
            var h = UiKit.ColorRect(_shoot.transform, "CrossH", Color.white).rectTransform;
            UiKit.Centered(h, 60, 5);
            var v = UiKit.ColorRect(_shoot.transform, "CrossV", Color.white).rectTransform;
            UiKit.Centered(v, 5, 60);
            _shoot.gameObject.AddComponent<ShootButton>().Owner = this;

            // Extra buttons (not in Godot, see class comment)
            float x = -20f;
            SmallButton("PauseBtn", "II", ref x, GameInput.PressPause);
            SmallButton("InventoryBtn", "INV", ref x, GameInput.PressInventory);
            _buoyBtn = SmallButton("BuoyBtn", "BUOY", ref x, GameInput.PressBuoy);
            _droneBtn = SmallButton("DroneBtn", "SELL", ref x, GameInput.PressDrone);
            _pickaxeBtn = SmallButton("PickaxeBtn", "DIG", ref x, GameInput.PressPickaxe);
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
        }

        Vector2 ToLocal(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, e.position, e.pressEventCamera, out var local);
            return local - _root.rect.min; // bottom-left origin
        }

        void JoystickDown(PointerEventData e)
        {
            var p = ToLocal(e);
            _joyBase.anchoredPosition = p;
            _joyKnob.anchoredPosition = p;
            _joyBase.gameObject.SetActive(true);
            _joyKnob.gameObject.SetActive(true);
            GameInput.VirtualMove = Vector2.zero;
        }

        void JoystickDrag(PointerEventData e)
        {
            Vector2 origin = _joyBase.anchoredPosition;
            Vector2 d = Vector2.ClampMagnitude(ToLocal(e) - origin, JoystickRadius);
            _joyKnob.anchoredPosition = origin + d;
            GameInput.VirtualMove = d / JoystickRadius;
        }

        void JoystickUp()
        {
            _joyBase.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);
            GameInput.VirtualMove = Vector2.zero;
        }

        class JoystickPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public TouchControls Owner;
            public void OnPointerDown(PointerEventData e) => Owner.JoystickDown(e);
            public void OnDrag(PointerEventData e) => Owner.JoystickDrag(e);
            public void OnPointerUp(PointerEventData e) => Owner.JoystickUp();
        }

        class ShootButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public TouchControls Owner;

            public void OnPointerDown(PointerEventData e)
            {
                Owner._shoot.color = new Color(1f, 0.5f, 0.5f, 0.8f);
                // Godot aims a rotatable harpoon at the touch position, i.e. at this button
                GameInput.PressFire(e.position);
            }

            public void OnPointerUp(PointerEventData e) => Owner._shoot.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        }
    }
}
