using System;
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
    }

    /// <summary>Port of scenes/ui/pause_menu.tscn + pause_menu.gd (Esc, ignored while docked).</summary>
    public class PauseMenu : MonoBehaviour
    {
        RectTransform _panel;
        SettingsMenu _settings;
        SaveMenu _save;

        public bool IsPaused { get; private set; }

        public static PauseMenu Create(RectTransform canvas)
        {
            var root = UiKit.Fill(UiKit.Rect(canvas, "PauseMenu"));
            var m = root.gameObject.AddComponent<PauseMenu>();

            var panel = UiKit.Panel(root, "Panel", MenuStyles.Panel);
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 400, 400);
            m._panel = panel.rectTransform;
            var c = UiKit.Fill(UiKit.Rect(panel.transform, "MarginContainer"), 30, 30, 30, 30);
            UiKit.Place(UiKit.Label(c, "Title", "PAUSED", 32, Color.white, TextAnchor.UpperCenter).rectTransform, 0, 0, 1, 0, 0, 0, 0, 44);
            UiKit.Place(UiKit.Label(c, "Subtitle", "Game is currently paused", 16, MenuStyles.Subtitle, TextAnchor.UpperCenter).rectTransform, 0, 0, 1, 0, 0, 44, 0, 66);
            UiKit.Place(UiKit.Separator(c).rectTransform, 0, 0, 1, 0, 0, 87, 0, 88);

            // ButtonsContainer: 4 × 50 px, separation 15, centred in the remaining space
            string[] labels = { "Resume Game", "Save / Load Game", "Settings", "Quit Game" };
            UnityAction[] actions = { m.Resume, m.OpenSave, m.OpenSettings, Quit };
            float top = 88f + 20f + 10f + 20f;
            float available = 340f - top;
            float block = 4 * 50f + 3 * 15f;
            float y = top + (available - block) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var b = UiKit.Button(c, labels[i].Replace(" ", ""), labels[i], 18, UiKit.MenuButton, actions[i]);
                UiKit.Place(b.GetComponent<RectTransform>(), 0, 0, 1, 0, 0, y, 0, y + 50);
                y += 65f;
            }

            m._save = SaveMenu.Create(root, m);
            m._settings = SettingsMenu.Create(root, m);
            root.gameObject.SetActive(false);
            return m;
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            IsPaused = true;
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

        void OpenSave()
        {
            ShowPauseElements(false);
            _save.Show();
        }

        void OpenSettings()
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
        RectTransform[] _tabs;
        Button[] _tabButtons;
        Text _status;
        float _statusTimer;

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
            UiKit.Centered(panel.rectTransform, 500, 400);
            var m = panel.gameObject.AddComponent<SettingsMenu>();
            m._owner = owner;
            m.Build(panel.rectTransform);
            panel.gameObject.SetActive(false);
            return m;
        }

        void Build(RectTransform panel)
        {
            var c = UiKit.Fill(UiKit.Rect(panel, "MarginContainer"), 30, 30, 30, 30);
            UiKit.Place(UiKit.Label(c, "Title", "SETTINGS", 28, Color.white, TextAnchor.UpperLeft).rectTransform, 0, 0, 1, 0, 0, 0, 0, 38);
            UiKit.Place(UiKit.Label(c, "Subtitle", "Customize your game experience", 16, MenuStyles.Subtitle, TextAnchor.UpperLeft).rectTransform, 0, 0, 1, 0, 0, 38, 0, 60);
            UiKit.Place(UiKit.Separator(c).rectTransform, 0, 0, 1, 0, 0, 75, 0, 76);

            // TabContainer (tabs centred)
            string[] names = { "Audio", "Display", "Controls" };
            _tabs = new RectTransform[3];
            _tabButtons = new Button[3];
            float tabW = 80f, startX = -tabW * 1.5f;
            var tabContent = UiKit.Panel(c, "SettingsContainer", new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3));
            UiKit.Place(tabContent.rectTransform, 0, 0, 1, 1, 0, 122, 0, -55);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = UiKit.Button(c, names[i] + "Tab", names[i], 16, TabUnselected, () => SelectTab(index));
                UiKit.Place(b.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, startX + i * tabW, 91, startX + (i + 1) * tabW, 122);
                _tabButtons[i] = b;
                _tabs[i] = UiKit.Fill(UiKit.Rect(tabContent.transform, names[i]), 8, 8, 8, 8);
            }

            // Audio
            float y = 0;
            Slider(_tabs[0], "Master Volume", ref y, Settings.MasterVolume, v => { Settings.SetMasterVolume(v); });
            Slider(_tabs[0], "Music Volume", ref y, Settings.MusicVolume, v => { Settings.SetMusicVolume(v); });
            Slider(_tabs[0], "SFX Volume", ref y, Settings.SfxVolume, v => { Settings.SetSfxVolume(v); });
            Toggle(_tabs[0], "Mute Audio", ref y, Settings.Muted, v => { Settings.SetMuted(v); ShowStatus(v ? "Audio muted" : "Audio unmuted"); });

            // Display
            y = 0;
            Toggle(_tabs[1], "Show Particles", ref y, Settings.ShowParticles, v => { Settings.SetShowParticles(v); ShowStatus("Settings saved!"); });
            Toggle(_tabs[1], "Show FPS Counter", ref y, Settings.ShowFps, v => { Settings.SetShowFps(v); ShowStatus("Settings saved!"); });

            // Controls
            var info = UiKit.Label(_tabs[2], "ControlsInfoLabel", "Game Controls:", 16, UiKit.DefaultText, TextAnchor.UpperCenter);
            UiKit.Place(info.rectTransform, 0, 0, 1, 0, 0, 0, 0, 22);
            string[,] controls =
            {
                { "Movement", "WASD / Arrow Keys" }, { "Throw Harpoon", "Left Mouse Button" }, { "Swing Pickaxe", "Space" },
                { "Toggle Inventory", "E / Tab" }, { "Pause Game", "Escape" }, { "Surface Buoy", "B" },
                { "Sell with Drone", "Q" }, { "Quick Save", "V" }
            };
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                float ry = 37 + i * 22;
                UiKit.Place(UiKit.Label(_tabs[2], "Label", controls[i, 0], 16, UiKit.DefaultText).rectTransform, 0, 0, 0.5f, 0, 0, ry, 0, ry + 22);
                UiKit.Place(UiKit.Label(_tabs[2], "Value", controls[i, 1], 16, UiKit.DefaultText).rectTransform, 0.5f, 0, 1, 0, 0, ry, 0, ry + 22);
            }

            var back = UiKit.Button(c, "BackButton", "Back", 16, UiKit.MenuButton, Back);
            UiKit.Place(back.GetComponent<RectTransform>(), 0, 1, 1, 1, 0, -40, 0, 0);

            _status = UiKit.Label(panel, "StatusLabel", "", 16, MenuStyles.Status, TextAnchor.MiddleCenter);
            UiKit.Place(_status.rectTransform, 0.5f, 0, 0.5f, 0, -150, -103 + 200, 150, -77 + 200);
            SelectTab(0);
        }

        void SelectTab(int index)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].gameObject.SetActive(i == index);
                UiKit.SetButtonStyles(_tabButtons[i], i == index ? TabSelected : TabUnselected);
            }
        }

        static void Slider(RectTransform parent, string label, ref float y, float value, Action<float> onChange)
        {
            UiKit.Place(UiKit.Label(parent, label, label, 16, UiKit.DefaultText).rectTransform, 0, 0, 1, 0, 0, y, 0, y + 22);
            var root = UiKit.Rect(parent, label + "Slider");
            UiKit.Place(root, 0, 0, 1, 0, 0, y + 24, 0, y + 40);
            var track = UiKit.Panel(root, "Track", new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.8f), 0, null, 2));
            UiKit.Place(track.rectTransform, 0, 0.5f, 1, 0.5f, 0, -2, 0, 2);
            var fillArea = UiKit.Place(UiKit.Rect(root, "Fill Area"), 0, 0.5f, 1, 0.5f, 0, -2, 0, 2);
            var fill = UiKit.Panel(fillArea, "Fill", new StyleBox(new Color(0.7f, 0.7f, 0.7f, 1f), 0, null, 2));
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UiKit.Place(UiKit.Rect(root, "Handle Slide Area"), 0, 0, 1, 1, 8, 0, -8, 0);
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
            y += 55;
        }

        static void Toggle(RectTransform parent, string label, ref float y, bool value, Action<bool> onChange)
        {
            UiKit.Place(UiKit.Label(parent, label, label, 16, UiKit.DefaultText).rectTransform, 0, 0, 1, 0, 0, y, -50, y + 26);
            // Godot CheckButton: a small switch on the right
            var bg = UiKit.Panel(parent, label + "Toggle", new StyleBox(new Color(0.3f, 0.3f, 0.3f, 1f), 0, null, 9));
            UiKit.Place(bg.rectTransform, 1, 0, 1, 0, -40, y + 4, 0, y + 22);
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
            y += 41;
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

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }

    /// <summary>Port of scenes/ui/save_menu.tscn + save_menu.gd.</summary>
    public class SaveMenu : MonoBehaviour
    {
        PauseMenu _owner;
        Text _info, _status;
        Button _load, _delete;
        float _statusTimer;

        public static SaveMenu Create(RectTransform parent, PauseMenu owner)
        {
            var panel = UiKit.Panel(parent, "SaveMenu", MenuStyles.Panel);
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 500, 400);
            var m = panel.gameObject.AddComponent<SaveMenu>();
            m._owner = owner;
            m.Build(panel.rectTransform);
            panel.gameObject.SetActive(false);
            return m;
        }

        void Build(RectTransform panel)
        {
            var c = UiKit.Fill(UiKit.Rect(panel, "MarginContainer"), 30, 30, 30, 30);
            UiKit.Place(UiKit.Label(c, "Title", "SAVE/LOAD GAME", 28, Color.white, TextAnchor.UpperLeft).rectTransform, 0, 0, 1, 0, 0, 0, 0, 38);
            UiKit.Place(UiKit.Label(c, "Subtitle", "Manage your game progress", 16, MenuStyles.Subtitle, TextAnchor.UpperLeft).rectTransform, 0, 0, 1, 0, 0, 38, 0, 60);
            UiKit.Place(UiKit.Separator(c).rectTransform, 0, 0, 1, 0, 0, 75, 0, 76);

            var infoBox = UiKit.Panel(c, "SaveInfoContainer", new StyleBox(new Color(0.05f, 0.15f, 0.3f, 0.7f), 2, new Color(0.3f, 0.5f, 0.7f), 8));
            UiKit.Place(infoBox.rectTransform, 0, 0, 1, 0, 0, 91, 0, 196);
            _info = UiKit.Label(infoBox.transform, "SaveInfoLabel", "No save file found", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            UiKit.Fill(_info.rectTransform, 12, 10, 12, 10);

            float x = 0;
            var save = UiKit.Button(c, "SaveButton", "Save Game", 16, UiKit.MenuButton, OnSave);
            UiKit.Place(save.GetComponent<RectTransform>(), 0, 0, 0, 0, x, 211, x + 120, 251);
            x += 130;
            _load = UiKit.Button(c, "LoadButton", "Load Game", 16, UiKit.MenuButton, OnLoad);
            UiKit.Place(_load.GetComponent<RectTransform>(), 0, 0, 0, 0, x, 211, x + 120, 251);
            x += 130;
            _delete = UiKit.Button(c, "DeleteButton", "Delete Save", 16, UiKit.MenuButton, OnDelete);
            UiKit.Place(_delete.GetComponent<RectTransform>(), 0, 0, 0, 0, x, 211, x + 120, 251);

            var back = UiKit.Button(c, "BackButton", "Back", 16, UiKit.MenuButton, Back);
            UiKit.Place(back.GetComponent<RectTransform>(), 0, 1, 1, 1, 0, -40, 0, 0);

            _status = UiKit.Label(panel, "StatusLabel", "", 16, MenuStyles.Status, TextAnchor.MiddleCenter);
            UiKit.Place(_status.rectTransform, 0.5f, 1, 0.5f, 1, -150, 8, 150, 34);
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
            ShowStatus(SaveSystem.SaveGame() ? "Game saved successfully!" : "Failed to save game!");
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
        RectTransform _panel;
        Image _portrait;
        Text _name, _message;

        public static DialogPanel Create(RectTransform canvas)
        {
            var panel = UiKit.ThemePanel(canvas, "BossDialog");
            UiKit.BlockRaycasts(panel);
            var d = canvas.gameObject.AddComponent<DialogPanel>();
            d._panel = panel.rectTransform;
            // min size (min(0.7·w, 700), min(0.4·h, 200)); content grows with the text
            UiKit.Centered(d._panel, 700, 420);
            var c = UiKit.Fill(UiKit.Rect(d._panel, "VBoxContainer"), 25, 25, 25, 25);
            d._portrait = UiKit.Picture(c, "ProfilePicture", null);
            UiKit.TopLeft(d._portrait.rectTransform, 0, 0, 110, 110);
            d._name = UiKit.Label(c, "NameLabel", "John", 16, UiKit.DefaultText, TextAnchor.MiddleLeft);
            UiKit.TopLeft(d._name.rectTransform, 114, 0, 400, 110);
            d._message = UiKit.Label(c, "MessageLabel", "", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            d._message.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(d._message.rectTransform, 0, 0, 1, 0, 0, 120, 0, 260);
            var cont = UiKit.Button(c, "ContinueButton", "Continue", 16, UiKit.DefaultButton, Dialogs.Continue);
            d._continue = cont.GetComponent<RectTransform>();
            panel.gameObject.SetActive(false);
            return d;
        }

        RectTransform _continue;

        void Update()
        {
            bool show = Dialogs.Displayed;
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;

            string from = Dialogs.CurrentFrom;
            _name.text = from;
            Sprite portrait = from == "John" ? GodotAssets.Sprite("textures/characters/john.png")
                : from is "Blobfish" or "???" ? GodotAssets.Sprite("textures/icons/boss_icon.png") : null;
            _portrait.sprite = portrait;
            _portrait.enabled = portrait != null;
            _name.rectTransform.offsetMin = new Vector2(portrait != null ? 114 : 0, _name.rectTransform.offsetMin.y);
            _message.text = Dialogs.CurrentText;

            // Fit the panel to the message (RichTextLabel fit_content)
            float textH = Mathf.Max(140f, _message.preferredHeight);
            UiKit.Place(_message.rectTransform, 0, 0, 1, 0, 0, 120, 0, 120 + textH);
            UiKit.Place(_continue, 0, 0, 1, 0, 0, 130 + textH, 0, 130 + textH + 31);
            float h = Mathf.Max(200f, 130 + textH + 31 + 50);
            UiKit.Centered(_panel, 700, h);
        }
    }

    /// <summary>Port of the DeathScreen of main_scene.tscn + death_screen.gd.</summary>
    public class DeathScreen : MonoBehaviour
    {
        RectTransform _panel;

        public static DeathScreen Create(RectTransform canvas)
        {
            var panel = UiKit.ThemePanel(canvas, "DeathScreen");
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 470, 142);
            var d = canvas.gameObject.AddComponent<DeathScreen>();
            d._panel = panel.rectTransform;
            var c = UiKit.Fill(UiKit.Rect(panel.transform, "VBoxContainer"), 25, 25, 25, 25);
            UiKit.Place(UiKit.Label(c, "Label", "Oh nooo, you died!\nAnyways... Lets keep going - we don't have all day!", 16, UiKit.DefaultText, TextAnchor.UpperLeft).rectTransform,
                0, 0, 1, 0, 0, 0, 0, 44);
            var b = UiKit.Button(c, "Button", "Respawn\n", 16, UiKit.DefaultButton, () => GameState.Instance?.Respawn());
            UiKit.Place(b.GetComponent<RectTransform>(), 0, 0, 1, 0, 0, 48, 0, 92);
            panel.gameObject.SetActive(false);
            return d;
        }

        void Update()
        {
            bool show = GameState.Instance != null && GameState.Instance.DeathScreen;
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
        }
    }
}
