using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TooFishy
{
    static class MenuStyles
    {
        public static readonly StyleBox Panel = new(new Color(0.05f, 0.2f, 0.3f, 0.95f), 3, new Color(0.2f, 0.4f, 0.7f), 12);
        public static readonly Color Subtitle = new(0.8f, 0.8f, 0.9f);
        public static readonly Color Status = new(0.8f, 0.9f, 0.5f);

        /// <summary>Distance every menu keeps from the canvas edge (canvas units).</summary>
        public const float Edge = 8f;
        /// <summary>Finger-sized touch target (44 dp).</summary>
        public const float Touch = 44f;

        /// <summary>
        /// The old stopgap that drew the menus 1.5x on touch screens. <see cref="UiScale"/> now sizes
        /// the whole canvas in dp, so menus are never scaled on top of that.
        /// </summary>
        public static float TouchScale => 1f;

        /// <summary>
        /// Finger-sized menus: phone canvases and touch screens. A desktop with a mouse keeps
        /// Godot's exact layout. <see cref="UiScale.Changed"/> fires when either changes.
        /// </summary>
        public static bool FingerLayout => UiScale.Compact || GameInput.TouchMode;

        /// <summary>Height a text needs when wrapped at <paramref name="width"/> (Text.preferredHeight at a given width).</summary>
        public static float PreferredHeight(Text t, float width)
        {
            var settings = t.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / Mathf.Max(0.0001f, t.pixelsPerUnit);
        }
    }

    /// <summary>
    /// A vertical, touch-draggable ScrollRect whose clipping and dragging are switched on only
    /// where a layout needs it, so a layout that fits renders exactly as without it.
    /// </summary>
    sealed class MenuScroll
    {
        public RectTransform Viewport;
        public RectTransform Content;
        ScrollRect _scroll;
        RectMask2D _mask;
        Image _hit;

        public static MenuScroll Create(Transform parent, string name)
        {
            var s = new MenuScroll { Viewport = UiKit.Rect(parent, name) };
            // Transparent drag surface between the rows (labels take no raycasts)
            s._hit = s.Viewport.gameObject.AddComponent<Image>();
            s._hit.color = new Color(0, 0, 0, 0);
            s._hit.raycastTarget = true;
            s._hit.enabled = false;
            s._mask = s.Viewport.gameObject.AddComponent<RectMask2D>();
            s._mask.enabled = false;
            s._scroll = s.Viewport.gameObject.AddComponent<ScrollRect>();
            s._scroll.viewport = s.Viewport;
            s._scroll.horizontal = false;
            s._scroll.vertical = true;
            s._scroll.movementType = ScrollRect.MovementType.Clamped;
            s._scroll.inertia = true;
            s._scroll.scrollSensitivity = 30f;
            s._scroll.enabled = false;
            return s;
        }

        /// <summary>Sets the scrolled rect (created under <see cref="Viewport"/>); it hangs from the top.</summary>
        public void SetContent(RectTransform content)
        {
            Content = content;
            content.pivot = new Vector2(0.5f, 1f);
            _scroll.content = content;
        }

        public bool Scrolling => _scroll.enabled;

        public void SetScrolling(bool on)
        {
            if (!on) _scroll.StopMovement();
            _scroll.enabled = on;
            _mask.enabled = on;
            _hit.enabled = on;
        }

        public void StopMovement() => _scroll.StopMovement();
    }

    /// <summary>Port of scenes/ui/pause_menu.tscn + pause_menu.gd (Esc, ignored while docked).</summary>
    public class PauseMenu : MonoBehaviour
    {
        RectTransform _panel, _content, _title, _subtitle, _separator;
        RectTransform[] _buttons;
        SettingsMenu _settings;
        SaveMenu _save;

        public bool IsPaused { get; private set; }

        public static PauseMenu Create(RectTransform canvas)
        {
            var root = UiKit.Fill(UiKit.Rect(canvas, "PauseMenu"));
            var m = root.gameObject.AddComponent<PauseMenu>();

            var panel = UiKit.Panel(root, "Panel", MenuStyles.Panel);
            UiKit.BlockRaycasts(panel);
            m._panel = panel.rectTransform;
            m._content = UiKit.Rect(panel.transform, "MarginContainer");
            m._title = UiKit.Label(m._content, "Title", "PAUSED", 32, Color.white, TextAnchor.UpperCenter).rectTransform;
            m._subtitle = UiKit.Label(m._content, "Subtitle", "Game is currently paused", 16, MenuStyles.Subtitle, TextAnchor.UpperCenter).rectTransform;
            m._separator = UiKit.Separator(m._content).rectTransform;

            string[] labels = { "Resume Game", "Save / Load Game", "Settings", "Quit Game" };
            UnityAction[] actions = { m.Resume, m.OpenSave, m.OpenSettings, Quit };
            m._buttons = new RectTransform[4];
            for (int i = 0; i < 4; i++)
                m._buttons[i] = UiKit.Button(m._content, labels[i].Replace(" ", ""), labels[i], 18, UiKit.MenuButton, actions[i]).GetComponent<RectTransform>();

            m._save = SaveMenu.Create(root, m);
            m._settings = SettingsMenu.Create(root, m);
            m.Layout();
            UiScale.Changed += m.Layout;
            root.gameObject.SetActive(false);
            return m;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Layout()
        {
            if (!this) { UiScale.Changed -= Layout; return; }
            if (!UiScale.Compact)
            {
                // Godot: 400x400, MarginContainer 30, ButtonsContainer 4 x 50 px, separation 15,
                // centred in the remaining space (50 px buttons are finger-sized already)
                UiKit.Centered(_panel, 400, 400);
                UiKit.Fill(_content, 30, 30, 30, 30);
                UiKit.Place(_title, 0, 0, 1, 0, 0, 0, 0, 44);
                UiKit.Place(_subtitle, 0, 0, 1, 0, 0, 44, 0, 66);
                UiKit.Place(_separator, 0, 0, 1, 0, 0, 87, 0, 88);
                float top = 88f + 20f + 10f + 20f;
                float available = 340f - top;
                float block = 4 * 50f + 3 * 15f;
                float y = top + (available - block) / 2f;
                for (int i = 0; i < 4; i++)
                {
                    UiKit.Place(_buttons[i], 0, 0, 1, 0, 0, y, 0, y + 50);
                    y += 65f;
                }
                return;
            }

            // Phone: same stack with less air, 48-unit buttons (351 units tall)
            var canvas = UiScale.CanvasSize;
            const float margin = 20f, buttonH = 48f, gap = 10f, firstButton = 89f;
            float h = margin + firstButton + 4 * buttonH + 3 * gap + margin;
            UiKit.Centered(_panel, Mathf.Min(400f, canvas.x - 2 * MenuStyles.Edge), Mathf.Min(h, canvas.y - 2 * MenuStyles.Edge));
            UiKit.Fill(_content, margin, margin, margin, margin);
            UiKit.Place(_title, 0, 0, 1, 0, 0, 0, 0, 44);
            UiKit.Place(_subtitle, 0, 0, 1, 0, 0, 44, 0, 66);
            UiKit.Place(_separator, 0, 0, 1, 0, 0, 76, 0, 77);
            float by = firstButton;
            for (int i = 0; i < 4; i++)
            {
                UiKit.Place(_buttons[i], 0, 0, 1, 0, 0, by, 0, by + buttonH);
                by += buttonH + gap;
            }
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            transform.localScale = Vector3.one;
            Layout();
            gameObject.SetActive(true);
            ShowPauseElements(true);
        }

        public void Resume()
        {
            IsPaused = false;
            _save.Hide();
            _settings.Hide();
            gameObject.SetActive(false);
        }

        public void ShowPauseElements(bool show) => _panel.gameObject.SetActive(show);

        public void OpenSave()
        {
            ShowPauseElements(false);
            _save.Show();
        }

        public void OpenSettings()
        {
            ShowPauseElements(false);
            _settings.Show();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>Port of scenes/ui/settings_menu.tscn + settings_menu.gd (Audio / Display / Controls tabs).</summary>
    public class SettingsMenu : MonoBehaviour
    {
        PauseMenu _owner;
        RectTransform _panel, _content, _separator, _tabContent, _back, _info;
        Text _title, _subtitle;
        MenuScroll[] _tabs;
        Button[] _tabButtons;
        Text _status;
        float _statusTimer;
        ControlsGridRows _keyboardControls, _touchControls;
        readonly List<SettingRow>[] _rows = { new(), new(), new() };

        sealed class SettingRow
        {
            public bool IsToggle;
            public RectTransform Label, Control, Switch;
        }

        sealed class ControlsGridRows
        {
            public RectTransform Root;
            public RectTransform[] Labels, Values;
            public float DesktopRowH;
            public bool Active => Root.gameObject.activeSelf;
        }

        static readonly UiKit.ButtonStyles TabSelected = new()
        {
            Normal = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3),
            Hover = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3),
            Pressed = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3),
            Font = new Color(0.95f, 0.95f, 0.95f)
        };
        static readonly UiKit.ButtonStyles TabUnselected = new()
        {
            Normal = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.3f), 0, null, 3),
            Hover = new StyleBox(new Color(0.15f, 0.15f, 0.15f, 0.45f), 0, null, 3),
            Pressed = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3),
            Font = new Color(0.7f, 0.7f, 0.7f)
        };

        public static SettingsMenu Create(RectTransform parent, PauseMenu owner)
        {
            var panel = UiKit.Panel(parent, "SettingsMenu", MenuStyles.Panel);
            UiKit.BlockRaycasts(panel);
            var m = panel.gameObject.AddComponent<SettingsMenu>();
            m._owner = owner;
            m.Build(panel.rectTransform);
            m.Layout();
            UiScale.Changed += m.Layout;
            panel.gameObject.SetActive(false);
            return m;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Build(RectTransform panel)
        {
            _panel = panel;
            _content = UiKit.Rect(panel, "MarginContainer");
            _title = UiKit.Label(_content, "Title", "SETTINGS", 28, Color.white, TextAnchor.UpperLeft);
            _subtitle = UiKit.Label(_content, "Subtitle", "Customize your game experience", 16, MenuStyles.Subtitle, TextAnchor.UpperLeft);
            _separator = UiKit.Separator(_content).rectTransform;

            // TabContainer (tabs centred); every tab page scrolls where it does not fit
            string[] names = { "Audio", "Display", "Controls" };
            _tabs = new MenuScroll[3];
            _tabButtons = new Button[3];
            var tabContent = UiKit.Panel(_content, "SettingsContainer", new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3));
            _tabContent = tabContent.rectTransform;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _tabButtons[i] = UiKit.Button(_content, names[i] + "Tab", names[i], 16, TabUnselected, () => SelectTab(index));
                _tabs[i] = MenuScroll.Create(tabContent.transform, names[i]);
                _tabs[i].SetContent(UiKit.Rect(_tabs[i].Viewport, "Content"));
            }

            // Audio
            var audio = _tabs[0].Content;
            Slider(audio, 0, "Master Volume", Settings.MasterVolume, v => { Settings.SetMasterVolume(v); });
            Slider(audio, 0, "Music Volume", Settings.MusicVolume, v => { Settings.SetMusicVolume(v); });
            Slider(audio, 0, "SFX Volume", Settings.SfxVolume, v => { Settings.SetSfxVolume(v); });
            Toggle(audio, 0, "Mute Audio", Settings.Muted, v => { Settings.SetMuted(v); ShowStatus(v ? "Audio muted" : "Audio unmuted"); });

            // Display
            var display = _tabs[1].Content;
            Toggle(display, 1, "Show Particles", Settings.ShowParticles, v => { Settings.SetShowParticles(v); ShowStatus("Settings saved!"); });
            Toggle(display, 1, "Show FPS Counter", Settings.ShowFps, v => { Settings.SetShowFps(v); ShowStatus("Settings saved!"); });

            // Controls
            var controls = _tabs[2].Content;
            _info = UiKit.Label(controls, "ControlsInfoLabel", "Game Controls:", 16, UiKit.DefaultText, TextAnchor.UpperCenter).rectTransform;
            // settings_menu.tscn ControlsGrid (keyboard)
            _keyboardControls = ControlsGrid(controls, "KeyboardControlsGrid", new[,]
            {
                { "Movement", "WASD / Arrow Keys" }, { "Throw Harpoon", "Left Mouse Button" }, { "Swing Pickaxe", "Space" },
                { "Toggle Inventory", "E / Tab" }, { "Pause Game", "Escape" }, { "Surface Buoy", "B" },
                { "Sell with Drone", "Q" }, { "Quick Save", "V" }
            }, 22f);
            // Touch devices have no keys: the TouchControls gestures and buttons instead
            _touchControls = ControlsGrid(controls, "TouchControlsGrid", new[,]
            {
                { "Movement", "Hold + drag left half" }, { "Throw Harpoon", "Tap right half" }, { "Fire AK47", "Hold GUN" },
                { "Swing Pickaxe", "DIG button" }, { "Toggle Inventory", "INV button" }, { "Pause Game", "II button" },
                { "Surface Buoy", "BUOY button" }, { "Sell with Drone", "SELL button" }, { "Quick Save", "SAVE button" }
            }, 20f);
            ShowControlsFor(GameInput.TouchMode);

            _back = UiKit.Button(_content, "BackButton", "Back", 16, UiKit.MenuButton, Back).GetComponent<RectTransform>();

            _status = UiKit.Label(panel, "StatusLabel", "", 16, MenuStyles.Status, TextAnchor.MiddleCenter);
            SelectTab(0);
        }

        static ControlsGridRows ControlsGrid(RectTransform parent, string name, string[,] controls, float rowH)
        {
            var grid = new ControlsGridRows
            {
                Root = UiKit.Fill(UiKit.Rect(parent, name)),
                Labels = new RectTransform[controls.GetLength(0)],
                Values = new RectTransform[controls.GetLength(0)],
                DesktopRowH = rowH
            };
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                grid.Labels[i] = UiKit.Label(grid.Root, "Label", controls[i, 0], 16, UiKit.DefaultText).rectTransform;
                grid.Values[i] = UiKit.Label(grid.Root, "Value", controls[i, 1], 16, UiKit.DefaultText).rectTransform;
            }
            return grid;
        }

        /// <summary>Lays out one controls grid; returns the y below its last row.</summary>
        static float LayoutGrid(ControlsGridRows grid, bool finger)
        {
            UiKit.Fill(grid.Root);
            float top = finger ? 30f : 37f, rowH = finger ? 26f : grid.DesktopRowH;
            for (int i = 0; i < grid.Labels.Length; i++)
            {
                float ry = top + i * rowH;
                UiKit.Place(grid.Labels[i], 0, 0, 0.5f, 0, 0, ry, 0, ry + rowH);
                UiKit.Place(grid.Values[i], 0.5f, 0, 1, 0, 0, ry, 0, ry + rowH);
            }
            return top + grid.Labels.Length * rowH;
        }

        void ShowControlsFor(bool touch)
        {
            _keyboardControls.Root.gameObject.SetActive(!touch);
            _touchControls.Root.gameObject.SetActive(touch);
        }

        void SelectTab(int index)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].Viewport.gameObject.SetActive(i == index);
                UiKit.SetButtonStyles(_tabButtons[i], i == index ? TabSelected : TabUnselected);
            }
        }

        void Layout()
        {
            if (!this) { UiScale.Changed -= Layout; return; }
            // UiScale.Changed also fires when a browser turns out to be a touch device
            ShowControlsFor(GameInput.TouchMode);
            bool finger = MenuStyles.FingerLayout;
            var canvas = UiScale.CanvasSize;
            if (!finger)
            {
                // Godot: 500x400, MarginContainer 30, 80-px tabs
                UiKit.Centered(_panel, 500, 400);
                UiKit.Fill(_content, 30, 30, 30, 30);
                _title.alignment = TextAnchor.UpperLeft;
                _subtitle.alignment = TextAnchor.UpperLeft;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, 0, 38);
                UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 38, 0, 60);
                UiKit.Place(_separator, 0, 0, 1, 0, 0, 75, 0, 76);
                const float tabW = 80f, startX = -tabW * 1.5f;
                for (int i = 0; i < 3; i++)
                    UiKit.Place(_tabButtons[i].GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, startX + i * tabW, 91, startX + (i + 1) * tabW, 122);
                UiKit.Place(_tabContent, 0, 0, 1, 1, 0, 122, 0, -55);
                UiKit.Place(_back, 0, 1, 1, 1, 0, -40, 0, 0);
                UiKit.Place(_status.rectTransform, 0.5f, 0, 0.5f, 0, -150, -103 + 200, 150, -77 + 200);
            }
            else
            {
                // Touch / phone: up to 640x480; title and subtitle share a row, 44-unit tabs and
                // Back, the status message beside Back, the tab page takes the rest and scrolls
                // (480 lets the 270-unit touch controls list fit unscrolled on a tablet)
                const float margin = 16f, tabW = 110f, startX = -tabW * 1.5f;
                float w = Mathf.Min(640f, canvas.x - 2 * MenuStyles.Edge);
                float h = Mathf.Min(480f, canvas.y - 2 * MenuStyles.Edge);
                UiKit.Centered(_panel, w, h);
                UiKit.Fill(_content, margin, margin, margin, margin);
                _title.alignment = TextAnchor.MiddleLeft;
                _subtitle.alignment = TextAnchor.MiddleRight;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, 0, 36);
                UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 0, 0, 36);
                UiKit.Place(_separator, 0, 0, 1, 0, 0, 42, 0, 43);
                for (int i = 0; i < 3; i++)
                    UiKit.Place(_tabButtons[i].GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, startX + i * tabW, 50, startX + (i + 1) * tabW, 50 + MenuStyles.Touch);
                UiKit.Place(_tabContent, 0, 0, 1, 1, 0, 50 + MenuStyles.Touch, 0, -(MenuStyles.Touch + 8));
                UiKit.Place(_back, 0, 1, 0, 1, 0, -MenuStyles.Touch, 180, 0);
                UiKit.Place(_status.rectTransform, 0, 1, 1, 1, margin + 188, -margin - MenuStyles.Touch, -margin, -margin);
            }

            // Tab pages: 8 px inside the tab panel (Godot TabContainer content margin)
            float[] heights =
            {
                LayoutRows(_rows[0], finger),
                LayoutRows(_rows[1], finger),
                0f
            };
            UiKit.Place(_info, 0, 0, 1, 0, 0, 0, 0, 22);
            float kb = LayoutGrid(_keyboardControls, finger), touch = LayoutGrid(_touchControls, finger);
            heights[2] = (_touchControls.Active ? touch : kb) + 6f;

            for (int i = 0; i < _tabs.Length; i++)
            {
                var tab = _tabs[i];
                UiKit.Fill(tab.Viewport, 8, 8, 8, 8);
                if (finger)
                    UiKit.Place(tab.Content, 0, 0, 1, 0, 0, 0, 0, heights[i]); // from the top, scrolled back up
                else
                    UiKit.Fill(tab.Content); // Godot layout: no clipping, exactly as before
                tab.StopMovement();
                tab.SetScrolling(finger);
            }
        }

        /// <summary>Stacks the slider and toggle rows of one tab; returns the height they need.</summary>
        static float LayoutRows(List<SettingRow> rows, bool finger)
        {
            float y = 0;
            foreach (var r in rows)
            {
                if (!r.IsToggle)
                {
                    // Label, then a 54 px invisible hit area (almost the whole 55 px row; labels take
                    // no raycasts): a finger can grab the bar anywhere and it jumps there, like
                    // Godot's HSlider on click
                    UiKit.Place(r.Label, 0, 0, 1, 0, 0, y, 0, y + 22);
                    UiKit.Place(r.Control, 0, 0, 1, 0, 0, y + 5, 0, y + 59);
                    y += 55;
                }
                else if (!finger)
                {
                    UiKit.Place(r.Control, 0, 0, 1, 0, 0, y - 4, 0, y + 34);
                    UiKit.Place(r.Label, 0, 0, 1, 0, 0, y, -50, y + 26);
                    UiKit.Place(r.Switch, 1, 0, 1, 0, -40, y + 4, 0, y + 22);
                    y += 41;
                }
                else
                {
                    // 44-unit row, label and switch centred in it
                    UiKit.Place(r.Control, 0, 0, 1, 0, 0, y, 0, y + MenuStyles.Touch);
                    UiKit.Place(r.Label, 0, 0, 1, 0, 0, y, -50, y + MenuStyles.Touch);
                    UiKit.Place(r.Switch, 1, 0, 1, 0, -40, y + 13, 0, y + 31);
                    y += MenuStyles.Touch + 2;
                }
            }
            return y;
        }

        void Slider(RectTransform parent, int tab, string label, float value, Action<float> onChange)
        {
            var text = UiKit.Label(parent, label, label, 16, UiKit.DefaultText);
            var root = UiKit.Rect(parent, label + "Slider");
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var track = UiKit.Panel(root, "Track", new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.8f), 0, null, 2));
            UiKit.Place(track.rectTransform, 0, 0.5f, 1, 0.5f, 0, -2, 0, 2);
            var fillArea = UiKit.Place(UiKit.Rect(root, "Fill Area"), 0, 0.5f, 1, 0.5f, 0, -2, 0, 2);
            var fill = UiKit.Panel(fillArea, "Fill", new StyleBox(new Color(0.7f, 0.7f, 0.7f, 1f), 0, null, 2));
            fill.rectTransform.sizeDelta = Vector2.zero;
            // Slider drives the handle's anchors to 0..1 of this area on the cross axis, so the area
            // keeps the old 16 px height: the taller hit rect must not stretch the handle
            var handleArea = UiKit.Place(UiKit.Rect(root, "Handle Slide Area"), 0, 0.5f, 1, 0.5f, 8, -8, -8, 8);
            var handle = UiKit.Picture(handleArea, "Handle", UiKit.Circle, false);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(16, 16);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange(Mathf.Round(v * 100f) / 100f));
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            _rows[tab].Add(new SettingRow { Label = text.rectTransform, Control = root });
        }

        void Toggle(RectTransform parent, int tab, string label, bool value, Action<bool> onChange)
        {
            // Invisible row behind label and switch: the whole row toggles (finger-sized CheckButton)
            var row = UiKit.ColorRect(parent, label + "Row", new Color(0, 0, 0, 0));
            row.raycastTarget = true;
            var text = UiKit.Label(parent, label, label, 16, UiKit.DefaultText);
            // Godot CheckButton: a small switch on the right
            var bg = UiKit.Panel(parent, label + "Toggle", new StyleBox(new Color(0.3f, 0.3f, 0.3f, 1f), 0, null, 9));
            var knob = UiKit.Picture(bg.transform, "Knob", UiKit.Circle, false);
            knob.rectTransform.sizeDelta = new Vector2(14, 14);
            bg.raycastTarget = true;
            var toggle = bg.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = bg;
            toggle.isOn = value;
            void Visual(bool on)
            {
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? 0.75f : 0.25f, 0.5f);
                knob.rectTransform.anchoredPosition = Vector2.zero;
                UiKit.ApplyStyle(bg, new StyleBox(on ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.3f, 0.3f, 0.3f), 0, null, 9));
            }
            Visual(value);
            toggle.onValueChanged.AddListener(v => { Visual(v); onChange(v); });
            var nav = toggle.navigation;
            nav.mode = Navigation.Mode.None;
            toggle.navigation = nav;
            var rowButton = row.gameObject.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            rowButton.navigation = nav;
            rowButton.onClick.AddListener(() => toggle.isOn = !toggle.isOn);
            _rows[tab].Add(new SettingRow { IsToggle = true, Label = text.rectTransform, Control = row.rectTransform, Switch = bg.rectTransform });
        }

        void ShowStatus(string message, float duration = 2f)
        {
            _status.text = message;
            _statusTimer = duration;
        }

        void Update()
        {
            if (_statusTimer <= 0f) return;
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _status.text = "";
        }

        void Back()
        {
            Hide();
            _owner.ShowPauseElements(true);
        }

        public void Show()
        {
            ShowControlsFor(GameInput.TouchMode); // a browser can turn out to be a touch device later
            Layout();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }

    /// <summary>Port of scenes/ui/save_menu.tscn + save_menu.gd.</summary>
    public class SaveMenu : MonoBehaviour
    {
        PauseMenu _owner;
        RectTransform _panel, _content, _separator, _infoBox, _saveButton, _back;
        Text _title, _subtitle, _info, _status;
        Button _load, _delete;
        float _statusTimer;

        public static SaveMenu Create(RectTransform parent, PauseMenu owner)
        {
            var panel = UiKit.Panel(parent, "SaveMenu", MenuStyles.Panel);
            UiKit.BlockRaycasts(panel);
            var m = panel.gameObject.AddComponent<SaveMenu>();
            m._owner = owner;
            m.Build(panel.rectTransform);
            m.Layout();
            UiScale.Changed += m.Layout;
            panel.gameObject.SetActive(false);
            return m;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Build(RectTransform panel)
        {
            _panel = panel;
            _content = UiKit.Rect(panel, "MarginContainer");
            _title = UiKit.Label(_content, "Title", "SAVE/LOAD GAME", 28, Color.white, TextAnchor.UpperLeft);
            _subtitle = UiKit.Label(_content, "Subtitle", "Manage your game progress", 16, MenuStyles.Subtitle, TextAnchor.UpperLeft);
            _separator = UiKit.Separator(_content).rectTransform;

            var infoBox = UiKit.Panel(_content, "SaveInfoContainer", new StyleBox(new Color(0.05f, 0.15f, 0.3f, 0.7f), 2, new Color(0.3f, 0.5f, 0.7f), 8));
            _infoBox = infoBox.rectTransform;
            _info = UiKit.Label(infoBox.transform, "SaveInfoLabel", "No save file found", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            UiKit.Fill(_info.rectTransform, 12, 10, 12, 10);

            _saveButton = UiKit.Button(_content, "SaveButton", "Save Game", 16, UiKit.MenuButton, OnSave).GetComponent<RectTransform>();
            _load = UiKit.Button(_content, "LoadButton", "Load Game", 16, UiKit.MenuButton, OnLoad);
            _delete = UiKit.Button(_content, "DeleteButton", "Delete Save", 16, UiKit.MenuButton, OnDelete);
            _back = UiKit.Button(_content, "BackButton", "Back", 16, UiKit.MenuButton, Back).GetComponent<RectTransform>();

            _status = UiKit.Label(panel, "StatusLabel", "", 16, MenuStyles.Status, TextAnchor.MiddleCenter);
        }

        void Layout()
        {
            if (!this) { UiScale.Changed -= Layout; return; }
            var buttons = new[] { _saveButton, _load.GetComponent<RectTransform>(), _delete.GetComponent<RectTransform>() };
            if (!MenuStyles.FingerLayout)
            {
                // Godot: 500x400, MarginContainer 30, three 120-px buttons
                UiKit.Centered(_panel, 500, 400);
                UiKit.Fill(_content, 30, 30, 30, 30);
                _title.alignment = TextAnchor.UpperLeft;
                _subtitle.alignment = TextAnchor.UpperLeft;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, 0, 38);
                UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 38, 0, 60);
                UiKit.Place(_separator, 0, 0, 1, 0, 0, 75, 0, 76);
                UiKit.Place(_infoBox, 0, 0, 1, 0, 0, 91, 0, 196);
                float x = 0;
                foreach (var b in buttons)
                {
                    UiKit.Place(b, 0, 0, 0, 0, x, 211, x + 120, 251);
                    x += 130;
                }
                UiKit.Place(_back, 0, 1, 1, 1, 0, -40, 0, 0);
                UiKit.Place(_status.rectTransform, 0.5f, 1, 0.5f, 1, -150, 8, 150, 34);
                return;
            }

            // Touch / phone (304 units tall): title and subtitle share a row, the three buttons
            // split the width at 48 units, Back 44; the status line stays just below the panel
            var canvas = UiScale.CanvasSize;
            const float margin = 16f, buttonsTop = 170f, buttonH = 48f, gap = 8f;
            float backTop = buttonsTop + buttonH + 10f;
            float contentH = backTop + MenuStyles.Touch;
            float w = Mathf.Min(560f, canvas.x - 2 * MenuStyles.Edge);
            float h = Mathf.Min(contentH + 2 * margin, canvas.y - 2 * MenuStyles.Edge);
            UiKit.Centered(_panel, w, h);
            UiKit.Fill(_content, margin, margin, margin, margin);
            _title.alignment = TextAnchor.MiddleLeft;
            _subtitle.alignment = TextAnchor.MiddleRight;
            UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, 0, 36);
            UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 0, 0, 36);
            UiKit.Place(_separator, 0, 0, 1, 0, 0, 42, 0, 43);
            UiKit.Place(_infoBox, 0, 0, 1, 0, 0, 50, 0, 160); // four 16-unit lines + padding
            float bw = (w - 2 * margin - 2 * gap) / 3f;
            for (int i = 0; i < buttons.Length; i++)
            {
                float x = i * (bw + gap);
                UiKit.Place(buttons[i], 0, 0, 0, 0, x, buttonsTop, x + bw, buttonsTop + buttonH);
            }
            UiKit.Place(_back, 0, 0, 1, 0, 0, backTop, 0, backTop + MenuStyles.Touch);
            // Below the panel when there is room, else over the bottom of the info box
            float below = (canvas.y - h) / 2f;
            if (below >= 34f + MenuStyles.Edge)
                UiKit.Place(_status.rectTransform, 0.5f, 1, 0.5f, 1, -w / 2f, 8, w / 2f, 34);
            else
                UiKit.Place(_status.rectTransform, 0, 0, 1, 0, margin + 12, margin + 160 - 30, -margin - 12, margin + 160 - 4);
        }

        void UpdateUi()
        {
            bool has = SaveSystem.HasSaveFile();
            _load.interactable = has;
            _delete.interactable = has;
            var info = SaveSystem.GetSaveInfo();
            _info.text = info.HasValue
                ? $"Save file: {info.Value.Timestamp:yyyy-MM-dd HH:mm}\nDepth: {info.Value.Depth} m\nMax Depth: {info.Value.MaxDepth} m\nMoney: ${info.Value.Money}"
                : "No save file found";
        }

        void ShowStatus(string message, float duration = 2f)
        {
            _status.text = message;
            _statusTimer = duration;
        }

        void Update()
        {
            if (_statusTimer <= 0f) return;
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _status.text = "";
        }

        void OnSave()
        {
            var gs = GameState.Instance;
            bool intro = gs != null && gs.IsIntro();
            ShowStatus(SaveSystem.SaveGame() ? "Game saved successfully!" : intro ? "Can't save during the intro!" : "Failed to save game!");
            UpdateUi();
        }

        void OnLoad()
        {
            if (SaveSystem.LoadGame())
            {
                ShowStatus("Game loaded successfully!");
                _owner.Resume();
            }
            else
                ShowStatus("Failed to load save file!");
            UpdateUi();
        }

        void OnDelete()
        {
            ShowStatus(SaveSystem.DeleteSave() ? "Save file deleted!" : "Failed to delete save file!");
            UpdateUi();
        }

        void Back()
        {
            Hide();
            _owner.ShowPauseElements(true);
        }

        public void Show()
        {
            Layout();
            gameObject.SetActive(true);
            UpdateUi();
        }

        public void Hide() => gameObject.SetActive(false);
    }

    /// <summary>
    /// Port of the BossDialog of main_scene.tscn + boss_dialog_ui.gd: theme panel with a portrait
    /// (John or the blobfish), the speaker's name, the rich text line and Continue.
    /// </summary>
    public class DialogPanel : MonoBehaviour
    {
        RectTransform _panel, _content, _continue;
        Image _portrait;
        Text _name, _message;
        MenuScroll _messageScroll;
        string _shownText;

        public static DialogPanel Create(RectTransform canvas)
        {
            var panel = UiKit.ThemePanel(canvas, "BossDialog");
            UiKit.BlockRaycasts(panel);
            var d = canvas.gameObject.AddComponent<DialogPanel>();
            d._panel = panel.rectTransform;
            UiKit.Centered(d._panel, 700, 420);
            d._content = UiKit.Rect(d._panel, "VBoxContainer");
            d._portrait = UiKit.Picture(d._content, "ProfilePicture", null);
            d._name = UiKit.Label(d._content, "NameLabel", "John", 16, UiKit.DefaultText, TextAnchor.MiddleLeft);
            // The message wraps; on a small screen a very long line scrolls instead of leaving the panel
            d._messageScroll = MenuScroll.Create(d._content, "MessageViewport");
            d._message = UiKit.Label(d._messageScroll.Viewport, "MessageLabel", "", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            d._message.horizontalOverflow = HorizontalWrapMode.Wrap;
            d._messageScroll.SetContent(d._message.rectTransform);
            var cont = UiKit.Button(d._content, "ContinueButton", "Continue", 16, UiKit.DefaultButton, Dialogs.Continue);
            d._continue = cont.GetComponent<RectTransform>();
            d.Layout();
            UiScale.Changed += d.Layout;
            panel.gameObject.SetActive(false);
            return d;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Update()
        {
            bool show = Dialogs.Displayed;
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;

            // Godot only has the Continue button; with the game paused behind a dialog a keyboard
            // player would otherwise be stuck, so Enter (and Space while it pauses) continues too.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                (Dialogs.PausesGame && Input.GetKeyDown(KeyCode.Space)))
            {
                Dialogs.Continue();
                return;
            }

            string from = Dialogs.CurrentFrom;
            _name.text = from;
            Sprite portrait = from == "John" ? GodotAssets.Sprite("textures/characters/john.png")
                : from is "Blobfish" or "???" ? GodotAssets.Sprite("textures/icons/boss_icon.png") : null;
            _portrait.sprite = portrait;
            _portrait.enabled = portrait != null;
            _message.text = Dialogs.CurrentText;

            Layout();
        }

        /// <summary>Fits the panel to the message (RichTextLabel fit_content), within the canvas.</summary>
        void Layout()
        {
            if (!this) { UiScale.Changed -= Layout; return; }
            bool compact = UiScale.Compact;
            var canvas = UiScale.CanvasSize;
            bool hasPortrait = _portrait.enabled;

            // Godot: 700 wide, margin 25, 110-px portrait, text from y 120 (at least 140 high).
            // Phone: as wide as fits (up to 820, fewer wrapped lines), 56-unit portrait beside the name.
            float width = Mathf.Min(compact ? 820f : 700f, canvas.x - 2 * MenuStyles.Edge);
            float margin = compact ? 20f : 25f;
            float pic = compact ? 56f : 110f;
            float msgTop = compact ? pic + 8f : 120f;
            float minText = compact ? 44f : 140f;
            float minPanel = compact ? 0f : 200f;
            // Godot's Continue is 31 px high; on touch screens and phones it is finger-sized
            float contH = compact || GameInput.TouchMode ? 48f : 31f;

            float msgW = width - 2 * margin;
            float pref = Mathf.Abs(_message.rectTransform.rect.width - msgW) < 0.5f
                ? _message.preferredHeight
                : MenuStyles.PreferredHeight(_message, msgW);
            float textH = Mathf.Max(minText, pref);
            float viewH = textH;
            float h = Mathf.Max(minPanel, margin + msgTop + viewH + 10f + contH + margin);
            float maxH = canvas.y - 2 * MenuStyles.Edge;
            bool scroll = false;
            if (h > maxH)
            {
                viewH = Mathf.Max(minText, viewH - (h - maxH));
                h = Mathf.Max(minPanel, margin + msgTop + viewH + 10f + contH + margin);
                scroll = true;
            }

            UiKit.Centered(_panel, width, h);
            UiKit.Fill(_content, margin, margin, margin, margin);
            UiKit.TopLeft(_portrait.rectTransform, 0, 0, pic, pic);
            if (compact)
                UiKit.Place(_name.rectTransform, 0, 0, 1, 0, hasPortrait ? pic + 10f : 0f, 0, 0, pic);
            else
                UiKit.Place(_name.rectTransform, 0, 0, 0, 0, hasPortrait ? 114f : 0f, 0, 514, 110);

            UiKit.Place(_messageScroll.Viewport, 0, 0, 1, 0, 0, msgTop, 0, msgTop + viewH);
            var msg = _message.rectTransform;
            msg.anchorMin = new Vector2(0f, 1f);
            msg.anchorMax = new Vector2(1f, 1f);
            msg.pivot = new Vector2(0.5f, 1f);
            var sd = new Vector2(0f, textH);
            if (msg.sizeDelta != sd) msg.sizeDelta = sd;
            // A new line starts at its top; a line that fits never moves
            bool newText = _shownText != _message.text;
            _shownText = _message.text;
            if (!scroll || newText)
            {
                _messageScroll.StopMovement();
                if (msg.anchoredPosition != Vector2.zero) msg.anchoredPosition = Vector2.zero;
            }
            if (_messageScroll.Scrolling != scroll) _messageScroll.SetScrolling(scroll);

            float contTop = msgTop + viewH + 10f;
            UiKit.Place(_continue, 0, 0, 1, 0, 0, contTop, 0, contTop + contH);
        }
    }

    /// <summary>Port of the DeathScreen of main_scene.tscn + death_screen.gd.</summary>
    public class DeathScreen : MonoBehaviour
    {
        RectTransform _panel, _content, _button;
        Text _label;

        public static DeathScreen Create(RectTransform canvas)
        {
            var panel = UiKit.ThemePanel(canvas, "DeathScreen");
            UiKit.BlockRaycasts(panel);
            var d = canvas.gameObject.AddComponent<DeathScreen>();
            d._panel = panel.rectTransform;
            d._content = UiKit.Rect(panel.transform, "VBoxContainer");
            d._label = UiKit.Label(d._content, "Label", "Oh nooo, you died!\nAnyways... Lets keep going - we don't have all day!", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            d._button = UiKit.Button(d._content, "Button", "Respawn\n", 16, UiKit.DefaultButton, () => GameState.Instance?.Respawn()).GetComponent<RectTransform>();
            d.Layout();
            UiScale.Changed += d.Layout;
            panel.gameObject.SetActive(false);
            return d;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Update()
        {
            bool show = GameState.Instance != null && GameState.Instance.DeathScreen;
            if (_panel.gameObject.activeSelf == show) return;
            if (show) Layout();
            _panel.gameObject.SetActive(show);
        }

        void Layout()
        {
            if (!this) { UiScale.Changed -= Layout; return; }
            if (!MenuStyles.FingerLayout)
            {
                // Godot: 470x142, margin 25, Respawn 44 px
                UiKit.Centered(_panel, 470, 142);
                UiKit.Fill(_content, 25, 25, 25, 25);
                _label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place(_label.rectTransform, 0, 0, 1, 0, 0, 0, 0, 44);
                UiKit.Place(_button, 0, 0, 1, 0, 0, 48, 0, 92);
                return;
            }

            // Touch / phone: the text wraps if it must, Respawn is 48 units high
            var canvas = UiScale.CanvasSize;
            const float margin = 22f, buttonH = 48f;
            float w = Mathf.Min(520f, canvas.x - 2 * MenuStyles.Edge);
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            float labelH = Mathf.Max(44f, MenuStyles.PreferredHeight(_label, w - 2 * margin));
            float h = Mathf.Min(margin + labelH + 8f + buttonH + margin, canvas.y - 2 * MenuStyles.Edge);
            UiKit.Centered(_panel, w, h);
            UiKit.Fill(_content, margin, margin, margin, margin);
            UiKit.Place(_label.rectTransform, 0, 0, 1, 0, 0, 0, 0, labelH);
            UiKit.Place(_button, 0, 0, 1, 0, 0, labelH + 8f, 0, labelH + 8f + buttonH);
        }
    }
}
