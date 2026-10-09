using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// On-screen controls for phones and touch browsers, built at runtime like the rest of the UI:
    /// a floating joystick on the left half, an aim/fire pad on the right half (tap where you want
    /// the harpoon to go), and buttons for shop, buoy, drone, pickaxe and pause that only appear
    /// when the matching upgrade is owned.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        const float JoystickRadius = 110f;

        RectTransform _joyBase, _joyKnob;
        Button _shopBtn, _buoyBtn, _droneBtn, _pickaxeBtn, _pauseBtn;
        Text _pauseLabel;

        public static TouchControls Create(Transform canvasRoot)
        {
            var go = new GameObject("TouchControls", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tc = go.AddComponent<TouchControls>();
            tc.Build(rt);
            return tc;
        }

        void Build(RectTransform root)
        {
            // Left half: joystick pad
            var joyPad = MakePad(root, "JoystickPad", new Vector2(0f, 0f), new Vector2(0.5f, 0.8f));
            joyPad.gameObject.AddComponent<JoystickPad>().Owner = this;

            _joyBase = MakeCircle(root, "JoyBase", JoystickRadius * 2f, new Color(1f, 1f, 1f, 0.12f));
            _joyKnob = MakeCircle(root, "JoyKnob", JoystickRadius * 0.9f, new Color(1f, 1f, 1f, 0.35f));
            _joyBase.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);

            // Right half: aim / fire pad
            var firePad = MakePad(root, "FirePad", new Vector2(0.5f, 0f), new Vector2(1f, 0.8f));
            firePad.gameObject.AddComponent<FirePad>();

            // Action buttons along the bottom right
            _pauseBtn = MakeButton(root, "PauseBtn", "II", new Vector2(1f, 1f), new Vector2(-70f, -70f), () => GameInput.PressPause());
            _pauseLabel = _pauseBtn.GetComponentInChildren<Text>();
            _shopBtn = MakeButton(root, "ShopBtn", "SHOP", new Vector2(1f, 0f), new Vector2(-90f, 90f), () => GameInput.PressShop());
            _buoyBtn = MakeButton(root, "BuoyBtn", "BUOY", new Vector2(1f, 0f), new Vector2(-230f, 90f), () => GameInput.PressBuoy());
            _droneBtn = MakeButton(root, "DroneBtn", "SELL", new Vector2(1f, 0f), new Vector2(-370f, 90f), () => GameInput.PressDrone());
            _pickaxeBtn = MakeButton(root, "PickaxeBtn", "DIG", new Vector2(1f, 0f), new Vector2(-510f, 90f), () => GameInput.PressPickaxe());
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            _shopBtn.gameObject.SetActive(gs.IsDocked && !gs.DeathScreen);
            _buoyBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0 && !gs.DeathScreen);
            _droneBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0 && !gs.DeathScreen);
            _pickaxeBtn.gameObject.SetActive(gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0 && !gs.DeathScreen);
            _pauseBtn.gameObject.SetActive(!gs.DeathScreen);
            if (_pauseLabel != null) _pauseLabel.text = gs.Paused ? ">" : "II";
        }

        // ---- joystick callbacks ----

        internal void JoystickDown(Vector2 screenPos)
        {
            _joyBase.gameObject.SetActive(true);
            _joyKnob.gameObject.SetActive(true);
            _joyBase.position = screenPos;
            _joyKnob.position = screenPos;
            GameInput.VirtualMove = Vector2.zero;
        }

        internal void JoystickDrag(Vector2 screenPos)
        {
            Vector2 center = _joyBase.position;
            Vector2 delta = screenPos - center;
            float scale = _joyBase.lossyScale.x <= 0f ? 1f : _joyBase.lossyScale.x;
            float maxPx = JoystickRadius * scale;
            if (delta.magnitude > maxPx) delta = delta.normalized * maxPx;
            _joyKnob.position = center + delta;
            var move = delta / maxPx;
            // Small dead zone, then full range
            GameInput.VirtualMove = move.magnitude < 0.12f ? Vector2.zero : move;
        }

        internal void JoystickUp()
        {
            _joyBase.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);
            GameInput.VirtualMove = Vector2.zero;
        }

        // ---- UI construction helpers ----

        static RectTransform MakePad(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // invisible but raycastable
            img.raycastTarget = true;
            return rt;
        }

        static RectTransform MakeCircle(RectTransform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = CircleSprite();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Button MakeButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 120f);
            var img = go.GetComponent<Image>();
            img.sprite = CircleSprite();
            img.color = new Color(0.1f, 0.25f, 0.4f, 0.75f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        static Sprite _circle;
        static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }
    }

    /// <summary>Receives the drag on the left half of the screen and feeds the joystick.</summary>
    public class JoystickPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public TouchControls Owner;
        int _pointerId = int.MinValue;

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = e.pointerId;
            Owner.JoystickDown(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointerId) Owner.JoystickDrag(e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = int.MinValue;
            Owner.JoystickUp();
        }
    }

    /// <summary>Tap anywhere on the right half to fire the harpoon toward the tap.</summary>
    public class FirePad : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData e) => GameInput.PressFire(e.position);
    }
}
