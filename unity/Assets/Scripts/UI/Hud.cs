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
        /// <summary>Teal text on the dark panels is hard to read on a small phone screen; compact layouts use this lighter tone.</summary>
        public static readonly Color TealReadable = new(0.45f, 0.68f, 1f);
        public static Color TealText => UiScale.Compact ? TealReadable : Teal;

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
    /// On a phone-sized canvas (<see cref="UiScale.Compact"/>) the bar is 40 units tall: the three
    /// panels share one 21-unit row and the depth markers run in a strip along its bottom edge,
    /// each label beside its tick.
    /// </summary>
    public class HudBar : MonoBehaviour
    {
        class InfoParts
        {
            public LayoutElement Le;
            public Text Label;
        }

        UiKit.ProgressBar _health, _pressure;
        Text _stageLabel, _stageValue, _currentLabel, _maxLabel;
        RectTransform _root, _content, _currentMarker, _maxMarker, _markerContainer;
        HorizontalLayoutGroup _layout;
        LayoutElement _stageLe;
        readonly List<InfoParts> _infos = new();
        float _currentPos, _maxPos;
        bool _compact, _laidOut, _currentLeft, _maxLeft;
        const float MaxPossibleDepth = 600f;
        /// <summary>Compact: width of a depth label beside its tick.</summary>
        const float CompactLabelW = 60f;

        public static HudBar Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "HUD", HudColors.Subway);
            var hud = panel.gameObject.AddComponent<HudBar>();
            hud._root = panel.rectTransform;
            hud.Build(panel.rectTransform);
            hud.Layout();
            UiScale.Changed += hud.Layout;
            return hud;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Build(RectTransform root)
        {
            // PanelContainer border 3 + MarginContainer 15
            _content = UiKit.Rect(root, "MarginContainer");
            var row = UiKit.Fill(UiKit.Rect(_content, "HUDContainer"));
            _layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            _layout.childAlignment = TextAnchor.MiddleCenter;
            _layout.childControlWidth = _layout.childControlHeight = true;
            _layout.childForceExpandWidth = false;
            _layout.childForceExpandHeight = true;

            _health = InfoBar(row, "HealthInfo", "SUBMARINE INTEGRITY");

            var stage = UiKit.Panel(row, "StageInfo", HudColors.Digital);
            _stageLe = stage.gameObject.AddComponent<LayoutElement>();
            _stageLabel = UiKit.Label(stage.transform, "StageLabel", "CURRENT ZONE", 14, HudColors.LabelGrey, TextAnchor.MiddleCenter);
            _stageValue = UiKit.Label(stage.transform, "StageValue", "OCEAN", 24, HudColors.Teal, TextAnchor.MiddleCenter);

            _pressure = InfoBar(row, "PressureInfo", "PRESSURE SYSTEM");

            // DepthIndicator: markers below the bottom edge of the margin container
            _markerContainer = UiKit.Place(UiKit.Rect(_content, "MarkerContainer"), 0, 1, 1, 1, 0, 0, 0, 0);
            _currentMarker = Marker(_markerContainer, "CurrentDepthMarker", new Color(0.7f, 0.6f, 0.15f, 0.8f), out _currentLabel);
            _maxMarker = Marker(_markerContainer, "MaxDepthMarker", new Color(0.95f, 0.8f, 0.2f, 1f), out _maxLabel);
        }

        UiKit.ProgressBar InfoBar(RectTransform row, string name, string caption)
        {
            var panel = UiKit.Panel(row, name, HudColors.Digital);
            var le = panel.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            // ProgressBar fills the panel (size_flags_vertical = fill), the label overlaps it
            var bar = new UiKit.ProgressBar(panel.transform, name.Replace("Info", "Bar"), HudColors.BarBg, HudColors.BarFill);
            UiKit.Fill(bar.Root, 2, 2, 2, 2);
            bar.Value = 1f;
            var label = UiKit.Label(panel.transform, "Label", caption, 20, HudColors.LabelGrey, TextAnchor.MiddleCenter);
            _infos.Add(new InfoParts { Le = le, Label = label });
            return bar;
        }

        static RectTransform Marker(RectTransform parent, string name, Color color, out Text label)
        {
            var marker = UiKit.ColorRect(parent, name, color).rectTransform;
            label = UiKit.Label(marker, "Label", "0m", 16, color, TextAnchor.UpperCenter);
            return marker;
        }

        /// <summary>Sizes for the current canvas; runs at creation and on <see cref="UiScale.Changed"/>.</summary>
        void Layout()
        {
            _compact = UiScale.Compact;
            if (!_compact)
            {
                // Godot main_scene.tscn: HUD 105 tall, margins 18, label font 20, zone 14/24
                UiKit.Place(_root, 0, 0, 1, 0, 0, 0, 0, 105);
                UiKit.Fill(_content, 18, 18, 18, 18);
                _layout.spacing = 20;
                _stageLe.preferredWidth = 290;
                _stageLabel.gameObject.SetActive(true);
                UiKit.Place(_stageLabel.rectTransform, 0, 0.5f, 1, 0.5f, 0, -24, 0, -5);
                _stageValue.fontSize = 24;
                _stageValue.color = HudColors.Teal;
                UiKit.Place(_stageValue.rectTransform, 0, 0.5f, 1, 0.5f, 0, -5, 0, 28);
                foreach (var info in _infos)
                {
                    info.Le.minWidth = 200;
                    // Label with StyleBoxEmpty content_margin_top 10, font 20
                    info.Label.fontSize = 20;
                    UiKit.Place(info.Label.rectTransform, 0, 0, 1, 0, 2, 12, -2, 40);
                }
                foreach (var (marker, label) in new[] { (_currentMarker, _currentLabel), (_maxMarker, _maxLabel) })
                {
                    UiKit.TopLeft(marker, -1, 16, 4, 16);
                    label.fontSize = 16;
                    label.alignment = TextAnchor.UpperCenter;
                    label.enabled = true;
                    UiKit.Place(label.rectTransform, 0.5f, 1, 0.5f, 1, -25, -34, 25, -14);
                }
            }
            else
            {
                // Phone: bar y 0..40; panels row y 4..25, depth strip y 25..37 above the bottom border
                UiKit.Place(_root, 0, 0, 1, 0, 0, 0, 0, 40);
                UiKit.Fill(_content, 6, 4, 6, 15);
                _layout.spacing = 8;
                _stageLe.preferredWidth = 170;
                _stageLabel.gameObject.SetActive(false); // no room for the caption; the zone name says it
                _stageValue.fontSize = 16;
                UiKit.Fill(_stageValue.rectTransform, 2, 0, 2, 0);
                _stageValue.color = HudColors.TealReadable;
                foreach (var info in _infos)
                {
                    info.Le.minWidth = 110;
                    info.Label.fontSize = 14;
                    UiKit.Fill(info.Label.rectTransform, 2, 0, 2, 0);
                }
                foreach (var (marker, label) in new[] { (_currentMarker, _currentLabel), (_maxMarker, _maxLabel) })
                {
                    UiKit.TopLeft(marker, -1, 3, 4, 9);
                    label.fontSize = 12;
                }
                // Current depth to the left of its tick, max depth to the right, so the two never
                // cover each other while you are at your max depth (UpdateCompactLabels flips them)
                SideLabel(_currentLabel, _currentLeft = true);
                SideLabel(_maxLabel, _maxLeft = false);
            }
            // TopLeft reset the x; Update glides on from the current position. Not on the first
            // layout, so the markers start exactly where TopLeft put them (as before)
            if (_laidOut)
            {
                _currentMarker.anchoredPosition = new Vector2(_currentPos, _currentMarker.anchoredPosition.y);
                _maxMarker.anchoredPosition = new Vector2(_maxPos, _maxMarker.anchoredPosition.y);
            }
            _laidOut = true;
        }

        /// <summary>Compact: depth label on one side of its tick (it is 4 wide, label 3 away).</summary>
        static void SideLabel(Text label, bool left)
        {
            label.alignment = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            if (left) UiKit.Place(label.rectTransform, 0, 0.5f, 0, 0.5f, -3 - CompactLabelW, -9, -3, 9);
            else UiKit.Place(label.rectTransform, 1, 0.5f, 1, 0.5f, 3, -9, 3 + CompactLabelW, 9);
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

            if (_compact) UpdateCompactLabels(width);
        }

        /// <summary>
        /// Compact: current depth left of its tick (right of it near 0 m), max depth right of its tick
        /// (left of it near 600 m), so both stay inside the strip. Where they would still overlap
        /// (both depths shallow, or both near 600 m) only the max depth label is shown.
        /// </summary>
        void UpdateCompactLabels(float width)
        {
            const float side = 2f + 3f; // half the tick + gap
            float cw = Mathf.Min(_currentLabel.preferredWidth, CompactLabelW);
            float mw = Mathf.Min(_maxLabel.preferredWidth, CompactLabelW);
            bool currentLeft = _currentPos - side - cw >= 0f;
            bool maxLeft = _maxPos + side + mw > width;
            if (currentLeft != _currentLeft) SideLabel(_currentLabel, _currentLeft = currentLeft);
            if (maxLeft != _maxLeft) SideLabel(_maxLabel, _maxLeft = maxLeft);

            float c0 = currentLeft ? _currentPos - side - cw : _currentPos + side;
            float m0 = maxLeft ? _maxPos - side - mw : _maxPos + side;
            bool overlap = c0 < m0 + mw + 3f && m0 < c0 + cw + 3f;
            if (_currentLabel.enabled == overlap) _currentLabel.enabled = !overlap;
        }
    }

    /// <summary>
    /// Port of scenes/inventory/inv_ui.tscn + scripts/inventory/inv_ui.gd: bank account, fish
    /// count/value and cargo capacity below the HUD's left edge; E/Tab toggles it (with the
    /// inventory menu). Compact: a 174x70 panel at (6, 44) with three 20-unit rows in 14 sp.
    /// </summary>
    public class InvUi : MonoBehaviour
    {
        static readonly StyleBox Bg = new(new Color(0.05f, 0.2f, 0.3f, 0.85f), 2, HudColors.Teal, 8, new Color(0, 0, 0, 0.25f), 4);
        static readonly StyleBox Header = new(new Color(0.066f, 0.132f, 0.2f, 0.95f), 2, HudColors.Teal, 6, new Color(0, 0, 0, 0.15f), 2);
        // Godot default theme ProgressBar background
        static readonly StyleBox WeightBg = new(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3);

        Text _bankLabel, _money, _fish, _value, _capacityLabel, _weight;
        RectTransform _root, _moneyRow, _infoRow, _weightRow;
        UiKit.ProgressBar _weightBar;

        public static InvUi Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "inv_ui", Bg);
            var ui = panel.gameObject.AddComponent<InvUi>();
            ui._root = panel.rectTransform;
            ui.Build(panel.rectTransform);
            ui.Layout();
            UiScale.Changed += ui.Layout;
            return ui;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        void Build(RectTransform root)
        {
            _moneyRow = UiKit.Panel(root, "MoneyPanel", Header).rectTransform;
            _bankLabel = UiKit.Label(_moneyRow, "BankAccountLabel", " Bank Account: ", 16, HudColors.Teal);
            UiKit.Fill(_bankLabel.rectTransform, 2, 0, 2, 0);
            _money = UiKit.Label(_moneyRow, "MoneyLabel", "$25", 16, HudColors.Teal, TextAnchor.MiddleRight);
            UiKit.Fill(_money.rectTransform, 2, 0, 4, 0);

            _infoRow = UiKit.Panel(root, "InfoPanel", Header).rectTransform;
            _fish = UiKit.Label(_infoRow, "FishLabel", " Fish: 0 ", 16, HudColors.LabelGrey);
            UiKit.Fill(_fish.rectTransform, 2, 0, 2, 0);
            _value = UiKit.Label(_infoRow, "ValueLabel", "$0.00 ", 16, HudColors.LabelGrey, TextAnchor.MiddleRight);
            UiKit.Fill(_value.rectTransform, 2, 0, 2, 0);

            _weightRow = UiKit.Panel(root, "WeightPanel", Header).rectTransform;
            _weightBar = new UiKit.ProgressBar(_weightRow, "WeightBar", WeightBg, HudColors.BarFill);
            UiKit.Fill(_weightBar.Root, 2, 2, 2, 2);
            _capacityLabel = UiKit.Label(_weightRow, "WeightLabel", " Capacity:", 14, HudColors.LabelGrey);
            UiKit.Fill(_capacityLabel.rectTransform, 2, 0, 2, 0);
            _weight = UiKit.Label(_weightRow, "WeightValueLabel", "0.0 / 25.0 ", 14, HudColors.LabelGrey, TextAnchor.MiddleRight);
            UiKit.Fill(_weight.rectTransform, 2, 0, 2, 0);
        }

        /// <summary>Sizes for the current canvas; runs at creation and on <see cref="UiScale.Changed"/>.</summary>
        void Layout()
        {
            bool compact = UiScale.Compact;
            // Godot: panel (1, 87) 197.38x135 under the HUD, rows 177.38x26 at x 10 from y 32, 6 apart
            float x = compact ? 5f : 10f, w = compact ? 164f : 177.38f;
            float rowH = compact ? 20f : 26f, gap = compact ? 2f : 6f;
            float y = compact ? 3f : 2 + 30;
            if (compact) UiKit.TopLeft(_root, 6, 44, 174, 70);
            else UiKit.TopLeft(_root, 1, 87, 197.38f, 135);

            UiKit.TopLeft(_moneyRow, x, y, w, rowH);
            y += rowH + gap;
            UiKit.TopLeft(_infoRow, x, y, w, rowH);
            y += rowH + gap;
            UiKit.TopLeft(_weightRow, x, y, w, rowH);

            int body = compact ? 14 : 16;
            _bankLabel.fontSize = _money.fontSize = _fish.fontSize = _value.fontSize = body;
            _bankLabel.color = _money.color = HudColors.TealText;
            _capacityLabel.fontSize = _weight.fontSize = 14;
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
    /// Compact: a 170-unit column at the top right (y 44 to at most 260) with rows sized to fit.
    /// </summary>
    public class AchievementPanel : MonoBehaviour
    {
        static readonly StyleBox ItemBg = new(new Color(0.066f, 0.132f, 0.2f, 0.75f), 1, new Color(0.2f, 0.4f, 0.7f, 0.5f), 4);
        RectTransform _root, _list;
        VerticalLayoutGroup _layout;
        bool _dirty = true;
        // Row metrics of the current layout (Godot: 40 tall, icon 32 at 4, label from 44, badges 24 every 28)
        float _rowH = 40, _iconInset = 4, _iconSize = 32, _labelLeft = 44, _badgeSize = 24, _badgeStep = 28;

        public static AchievementPanel Create(RectTransform canvas)
        {
            var root = UiKit.Rect(canvas, "AchievementUI");
            var panel = root.gameObject.AddComponent<AchievementPanel>();
            panel._root = root;
            panel._list = UiKit.Rect(root, "AchievementContainer");
            var layout = panel._layout = panel._list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            panel.Layout();
            Achievements.Updated += panel.MarkDirty;
            UiScale.Changed += panel.Layout;
            return panel;
        }

        void OnDestroy()
        {
            Achievements.Updated -= MarkDirty;
            UiScale.Changed -= Layout;
        }

        void MarkDirty() => _dirty = true;

        /// <summary>Sizes for the current canvas; runs at creation and on <see cref="UiScale.Changed"/>.</summary>
        void Layout()
        {
            if (!UiScale.Compact)
            {
                // main_scene.tscn: right column x -190..0, y 105..525; panel margins 8
                UiKit.Place(_root, 1, 0, 1, 0, -190, 105, 0, 525);
                UiKit.Fill(_list, 8, 8, 8, 8);
                _layout.spacing = 8;
                _rowH = 40; _iconInset = 4; _iconSize = 32; _labelLeft = 44; _badgeSize = 24; _badgeStep = 28;
            }
            else
            {
                const float top = 44f, pad = 4f, spacing = 3f;
                int rows = Achievements.Fish.Count + 1; // + Drone Lift
                float bottom = Mathf.Min(260f, UiScale.CanvasSize.y - 8f);
                float rowH = Mathf.Clamp(Mathf.Floor((bottom - top - 2 * pad - (rows - 1) * spacing) / rows), 20f, 30f);
                float height = 2 * pad + rows * rowH + (rows - 1) * spacing;
                UiKit.Place(_root, 1, 0, 1, 0, -176, top, -6, top + height);
                UiKit.Fill(_list, pad, pad, pad, pad);
                _layout.spacing = spacing;
                _rowH = rowH;
                _iconInset = 3;
                _iconSize = rowH - 6;
                _labelLeft = rowH + 2;
                _badgeSize = rowH - 8;
                _badgeStep = _badgeSize + 3;
            }
            _dirty = true;
        }

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
            // Deactivate first: Destroy only happens at the end of the frame, and the layout group
            // would otherwise stack the old and the new rows for one frame
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var old = _list.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }

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
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = _rowH;
            UiKit.TopLeft(UiKit.Picture(item.transform, "IconTexture", icon).rectTransform, _iconInset, _iconInset, _iconSize, _iconSize);
            var label = UiKit.Label(item.transform, "FishNameLabel", name, 12, HudColors.LabelGrey);
            UiKit.Place(label.rectTransform, 0, 0, 1, 1, _labelLeft, 4, -4 - badges.Count * _badgeStep, -4);
            for (int i = 0; i < badges.Count; i++)
            {
                var b = UiKit.Picture(item.transform, "Badge", badges[i]).rectTransform;
                float left = -4 - (badges.Count - i) * _badgeStep;
                UiKit.Place(b, 1, 0.5f, 1, 0.5f, left, -_badgeSize / 2, left + _badgeSize, _badgeSize / 2);
            }
        }
    }

    /// <summary>
    /// scripts/fps_counter.gd: "FPS: N" at the top-left, refreshed every 0.5 s. Compact: right of
    /// the inventory panel, below the boss bar slot (x 186, y 66), where it covers no HUD text.
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        Text _label;
        float _timer;
        int _frames;

        public static FpsCounter Create(RectTransform canvas)
        {
            var label = UiKit.Label(canvas, "FPS Counter", "FPS", 16, UiKit.DefaultText, TextAnchor.UpperLeft);
            var c = label.gameObject.AddComponent<FpsCounter>();
            c._label = label;
            c.Layout();
            UiScale.Changed += c.Layout;
            return c;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        /// <summary>Sizes for the current canvas; runs at creation and on <see cref="UiScale.Changed"/>.</summary>
        void Layout()
        {
            if (UiScale.Compact)
            {
                _label.fontSize = 14;
                UiKit.TopLeft(_label.rectTransform, 186, 66, 120, 20);
            }
            else
            {
                _label.fontSize = 16;
                UiKit.TopLeft(_label.rectTransform, 8, 4, 197, 27);
            }
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
