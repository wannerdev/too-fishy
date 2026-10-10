using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>Shared colours of the Godot HUD (hud.gd, inv_ui.gd).</summary>
    public static class HudColors
    {
        public static readonly Color Blue = new(0f, 0.36f, 0.83f, 0.8f);
        public static readonly Color Orange = new(0.918f, 0.525f, 0.212f, 0.82f);
        public static readonly Color Red = new(0.918f, 0.212f, 0.212f, 0.82f);
        public static readonly Color LabelGrey = new(0.780392f, 0.780392f, 0.780392f);
        public static readonly Color Teal = new(0.2f, 0.4f, 0.7f);

        public static readonly StyleBox Subway = new(new Color(0.0509804f, 0.2f, 0.301961f), 3, Teal, 8, new Color(0, 0, 0, 0.25f), 4);
        public static readonly StyleBox Digital = new(new Color(0.066f, 0.132f, 0.2f, 0.85f), 2, Teal, 6, new Color(0, 0, 0, 0.15f), 2);
        public static readonly StyleBox BarBg = new(new Color(0.05f, 0.05f, 0.05f, 0.7f), 1, new Color(0.105882f, 0.85098f, 0.917647f, 0.3f), 4);
        public static readonly StyleBox BarFill = new(Blue, 0, null, 4);

        /// <summary>hud.gd thresholds for health and pressure (percent).</summary>
        public static Color ForPercent(float percent) => percent <= 15f ? Red : percent <= 30f ? Orange : Blue;
    }

    /// <summary>
    /// Port of the HUD of scenes/main_scene.tscn + scripts/hud.gd: full-width bar with
    /// SUBMARINE INTEGRITY, CURRENT ZONE and PRESSURE SYSTEM, plus the depth markers.
    /// </summary>
    public class HudBar : MonoBehaviour
    {
        UiKit.ProgressBar _health, _pressure;
        Text _stageValue, _currentLabel, _maxLabel;
        RectTransform _currentMarker, _maxMarker, _markerContainer;
        float _currentPos, _maxPos;
        const float MaxPossibleDepth = 600f;

        public static HudBar Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "HUD", HudColors.Subway);
            UiKit.Place(panel.rectTransform, 0, 0, 1, 0, 0, 0, 0, 105);
            var hud = panel.gameObject.AddComponent<HudBar>();
            hud.Build(panel.rectTransform);
            return hud;
        }

        void Build(RectTransform root)
        {
            // PanelContainer border 3 + MarginContainer 15
            var content = UiKit.Fill(UiKit.Rect(root, "MarginContainer"), 18, 18, 18, 18);
            var row = UiKit.Fill(UiKit.Rect(content, "HUDContainer"));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            _health = InfoBar(row, "HealthInfo", "SUBMARINE INTEGRITY");

            var stage = UiKit.Panel(row, "StageInfo", HudColors.Digital);
            stage.gameObject.AddComponent<LayoutElement>().preferredWidth = 290;
            var stageLabel = UiKit.Label(stage.transform, "StageLabel", "CURRENT ZONE", 14, HudColors.LabelGrey, TextAnchor.MiddleCenter);
            UiKit.Place(stageLabel.rectTransform, 0, 0.5f, 1, 0.5f, 0, -24, 0, -5);
            _stageValue = UiKit.Label(stage.transform, "StageValue", "OCEAN", 24, HudColors.Teal, TextAnchor.MiddleCenter);
            UiKit.Place(_stageValue.rectTransform, 0, 0.5f, 1, 0.5f, 0, -5, 0, 28);

            _pressure = InfoBar(row, "PressureInfo", "PRESSURE SYSTEM");

            // DepthIndicator: markers below the bottom edge of the margin container
            _markerContainer = UiKit.Place(UiKit.Rect(content, "MarkerContainer"), 0, 1, 1, 1, 0, 0, 0, 0);
            _currentMarker = Marker(_markerContainer, "CurrentDepthMarker", new Color(0.7f, 0.6f, 0.15f, 0.8f), out _currentLabel);
            _maxMarker = Marker(_markerContainer, "MaxDepthMarker", new Color(0.95f, 0.8f, 0.2f, 1f), out _maxLabel);
        }

        static UiKit.ProgressBar InfoBar(RectTransform row, string name, string caption)
        {
            var panel = UiKit.Panel(row, name, HudColors.Digital);
            var le = panel.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 200;
            le.flexibleWidth = 1;
            // ProgressBar fills the panel (size_flags_vertical = fill), the label overlaps it
            var bar = new UiKit.ProgressBar(panel.transform, name.Replace("Info", "Bar"), HudColors.BarBg, HudColors.BarFill);
            UiKit.Fill(bar.Root, 2, 2, 2, 2);
            bar.Value = 1f;
            // Label with StyleBoxEmpty content_margin_top 10, font 20
            var label = UiKit.Label(panel.transform, "Label", caption, 20, HudColors.LabelGrey, TextAnchor.MiddleCenter);
            UiKit.Place(label.rectTransform, 0, 0, 1, 0, 2, 12, -2, 40);
            return bar;
        }

        static RectTransform Marker(RectTransform parent, string name, Color color, out Text label)
        {
            var marker = UiKit.ColorRect(parent, name, color).rectTransform;
            UiKit.TopLeft(marker, -1, 16, 4, 16);
            label = UiKit.Label(marker, "Label", "0m", 16, color, TextAnchor.UpperCenter);
            UiKit.Place(label.rectTransform, 0.5f, 1, 0.5f, 1, -25, -34, 25, -14);
            return marker;
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _stageValue.text = GameState.StageName(gs.PlayerInStage);

            _health.Value = gs.Health / 100f;
            _health.SetFillColor(HudColors.ForPercent(gs.Health));

            // hud.gd: headroom / (depth resistance + 1), i.e. percent of the safe depth left
            float headroom = (gs.GetUpgradeLevel(Upgrade.DepthResistance) + 1) * 100f - gs.Depth;
            float pressure = headroom / (gs.GetUpgradeLevel(Upgrade.DepthResistance) + 1);
            _pressure.Value = pressure / 100f;
            _pressure.SetFillColor(HudColors.ForPercent(pressure));

            // update_depth_indicator(): markers glide to depth / 600 of the width
            float dt = Time.unscaledDeltaTime;
            float width = _markerContainer.rect.width;
            float currentTarget = Mathf.Clamp01(gs.Depth / MaxPossibleDepth) * width - 1f;
            float maxTarget = Mathf.Clamp01(gs.MaxDepthReached / MaxPossibleDepth) * width - 1f;
            _currentPos = Mathf.Lerp(_currentPos, currentTarget, dt * 8f);
            _maxPos = Mathf.Lerp(_maxPos, maxTarget, dt * 8f);
            _currentMarker.anchoredPosition = new Vector2(_currentPos, _currentMarker.anchoredPosition.y);
            _maxMarker.anchoredPosition = new Vector2(_maxPos, _maxMarker.anchoredPosition.y);
            _currentLabel.text = $"{gs.Depth}m";
            _maxLabel.text = $"{gs.MaxDepthReached}m";
        }
    }

    /// <summary>
    /// Port of scenes/inventory/inv_ui.tscn + scripts/inventory/inv_ui.gd: bank account, fish
    /// count/value and cargo capacity below the HUD's left edge; E/Tab toggles it (with the
    /// inventory menu).
    /// </summary>
    public class InvUi : MonoBehaviour
    {
        static readonly StyleBox Bg = new(new Color(0.05f, 0.2f, 0.3f, 0.85f), 2, HudColors.Teal, 8, new Color(0, 0, 0, 0.25f), 4);
        static readonly StyleBox Header = new(new Color(0.066f, 0.132f, 0.2f, 0.95f), 2, HudColors.Teal, 6, new Color(0, 0, 0, 0.15f), 2);
        // Godot default theme ProgressBar background
        static readonly StyleBox WeightBg = new(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3);

        Text _money, _fish, _value, _weight;
        UiKit.ProgressBar _weightBar;

        public static InvUi Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "inv_ui", Bg);
            UiKit.TopLeft(panel.rectTransform, 1, 87, 197.38f, 135);
            var ui = panel.gameObject.AddComponent<InvUi>();
            ui.Build(panel.rectTransform);
            return ui;
        }

        void Build(RectTransform root)
        {
            const float rowH = 26f, x = 10f, w = 177.38f;
            float y = 2 + 30;

            var money = UiKit.Panel(root, "MoneyPanel", Header).rectTransform;
            UiKit.TopLeft(money, x, y, w, rowH);
            UiKit.Fill(UiKit.Label(money, "BankAccountLabel", " Bank Account: ", 16, HudColors.Teal).rectTransform, 2, 0, 2, 0);
            _money = UiKit.Label(money, "MoneyLabel", "$25", 16, HudColors.Teal, TextAnchor.MiddleRight);
            UiKit.Fill(_money.rectTransform, 2, 0, 4, 0);

            y += rowH + 6;
            var info = UiKit.Panel(root, "InfoPanel", Header).rectTransform;
            UiKit.TopLeft(info, x, y, w, rowH);
            _fish = UiKit.Label(info, "FishLabel", " Fish: 0 ", 16, HudColors.LabelGrey);
            UiKit.Fill(_fish.rectTransform, 2, 0, 2, 0);
            _value = UiKit.Label(info, "ValueLabel", "$0.00 ", 16, HudColors.LabelGrey, TextAnchor.MiddleRight);
            UiKit.Fill(_value.rectTransform, 2, 0, 2, 0);

            y += rowH + 6;
            var weight = UiKit.Panel(root, "WeightPanel", Header).rectTransform;
            UiKit.TopLeft(weight, x, y, w, rowH);
            _weightBar = new UiKit.ProgressBar(weight, "WeightBar", WeightBg, HudColors.BarFill);
            UiKit.Fill(_weightBar.Root, 2, 2, 2, 2);
            UiKit.Fill(UiKit.Label(weight, "WeightLabel", " Capacity:", 14, HudColors.LabelGrey).rectTransform, 2, 0, 2, 0);
            _weight = UiKit.Label(weight, "WeightValueLabel", "0.0 / 25.0 ", 14, HudColors.LabelGrey, TextAnchor.MiddleRight);
            UiKit.Fill(_weight.rectTransform, 2, 0, 2, 0);
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            var inv = gs.Inventory;
            _money.text = $"${gs.Money}";
            _fish.text = $" Fish: {inv.FishesCaught} ";
            _value.text = $"${inv.TotalValue:F2} ";
            float max = inv.GetMaxWeight();
            _weight.text = $"{inv.TotalWeight:F1} / {max:F1} ";
            float ratio = inv.TotalWeight / max;
            _weightBar.Value = ratio;
            _weightBar.SetFillColor(ratio > 0.7f ? HudColors.Red : ratio > 0.5f ? HudColors.Orange : HudColors.Blue);
        }
    }

    /// <summary>
    /// Port of scenes/ui/achievement_panel.tscn + achievement_ui.gd: one row per fish type with
    /// its icon (or "???"), star badge for shiny, bubble badge for surfaced, plus Drone Lift.
    /// </summary>
    public class AchievementPanel : MonoBehaviour
    {
        static readonly StyleBox ItemBg = new(new Color(0.066f, 0.132f, 0.2f, 0.75f), 1, new Color(0.2f, 0.4f, 0.7f, 0.5f), 4);
        RectTransform _list;
        bool _dirty = true;

        public static AchievementPanel Create(RectTransform canvas)
        {
            var root = UiKit.Rect(canvas, "AchievementUI");
            // main_scene.tscn: right column x -190..0, y 105..525; panel margins 8
            UiKit.Place(root, 1, 0, 1, 0, -190, 105, 0, 525);
            var panel = root.gameObject.AddComponent<AchievementPanel>();
            panel._list = UiKit.Fill(UiKit.Rect(root, "AchievementContainer"), 8, 8, 8, 8);
            var layout = panel._list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            Achievements.Updated += panel.MarkDirty;
            return panel;
        }

        void OnDestroy() => Achievements.Updated -= MarkDirty;
        void MarkDirty() => _dirty = true;

        void Update()
        {
            var gs = GameState.Instance;
            // Hidden during the intro mission (game_state.gd)
            bool show = gs == null || !gs.IsIntro();
            if (_list.gameObject.activeSelf != show) _list.gameObject.SetActive(show);
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);

            var question = GodotAssets.Sprite("textures/icons/questionmark.png");
            var star = GodotAssets.Sprite("textures/icons/pure_star.png");
            var bubble = GodotAssets.Sprite("textures/icons/air_bubble.png");

            foreach (var kv in Achievements.Fish)
            {
                var rec = kv.Value;
                var badges = new List<Sprite>();
                if (rec.Caught && rec.Shiny) badges.Add(star);
                if (rec.Caught && rec.Surface) badges.Add(bubble);
                Row(rec.Caught ? Achievements.FishName(kv.Key) : "???",
                    rec.Caught ? GodotAssets.Sprite(Achievements.IconPath(kv.Key)) : question, badges);
            }

            bool drone = Achievements.DroneLift;
            Row(drone ? "Drone Lift" : "Drone Lift (Locked)",
                drone ? GodotAssets.Sprite("textures/icons/boss_icon.png") : question,
                drone ? new List<Sprite> { star } : new List<Sprite>());
        }

        void Row(string name, Sprite icon, List<Sprite> badges)
        {
            var item = UiKit.Panel(_list, "AchievementItem", ItemBg);
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            UiKit.TopLeft(UiKit.Picture(item.transform, "IconTexture", icon).rectTransform, 4, 4, 32, 32);
            var label = UiKit.Label(item.transform, "FishNameLabel", name, 12, HudColors.LabelGrey);
            UiKit.Place(label.rectTransform, 0, 0, 1, 1, 44, 4, -4 - badges.Count * 28, -4);
            for (int i = 0; i < badges.Count; i++)
            {
                var b = UiKit.Picture(item.transform, "Badge", badges[i]).rectTransform;
                UiKit.Place(b, 1, 0.5f, 1, 0.5f, -4 - (badges.Count - i) * 28, -12, -4 - (badges.Count - i) * 28 + 24, 12);
            }
        }
    }

    /// <summary>scripts/fps_counter.gd: "FPS: N" at the top-left, refreshed every 0.5 s.</summary>
    public class FpsCounter : MonoBehaviour
    {
        Text _label;
        float _timer;
        int _frames;

        public static FpsCounter Create(RectTransform canvas)
        {
            var label = UiKit.Label(canvas, "FPS Counter", "FPS", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            UiKit.TopLeft(label.rectTransform, 8, 4, 197, 27);
            var c = label.gameObject.AddComponent<FpsCounter>();
            c._label = label;
            return c;
        }

        void Update()
        {
            _label.enabled = Settings.ShowFps;
            _frames++;
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.5f) return;
            _label.text = $"FPS: {Mathf.RoundToInt(_frames / _timer)}";
            _timer = 0f;
            _frames = 0;
        }
    }
}
