using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Port of scenes/ui/inventory_menu.tscn + inventory_menu.gd + fish_item.gd: centred 800×600
    /// panel with totals and a 5-column grid of stacked fish cards (icon, weight, value,
    /// Release). Close and Sell Drone buttons at the bottom.
    /// </summary>
    public class InventoryMenu : MonoBehaviour
    {
        static readonly StyleBox PanelStyle = new(new Color(0.05f, 0.2f, 0.3f, 0.95f), 3, new Color(0.2f, 0.4f, 0.7f), 12);
        static readonly StyleBox ScrollStyle = new(new Color(0.1f, 0.25f, 0.4f, 0.7f), 2, new Color(0.3f, 0.5f, 0.7f, 0.5f), 8);
        static readonly StyleBox CardStyle = new(new Color(0.1f, 0.25f, 0.4f, 0.7f), 2, new Color(0.3f, 0.5f, 0.7f), 8);
        static readonly StyleBox SlotStyle = new(new Color(0.05f, 0.15f, 0.25f, 0.8f), 2, new Color(0.3f, 0.5f, 0.7f, 0.5f), 6);
        static readonly StyleBox BadgeStyle = new(new Color(0.15f, 0.3f, 0.5f, 0.9f), 1, new Color(0.3f, 0.5f, 0.7f), 12);

        RectTransform _panel, _grid, _content, _scrollBg, _buttons;
        RectTransform _closeRt, _sellRt;
        Text _title, _subtitle, _closeLabel, _sellLabel;
        Image _separator;
        GridLayoutGroup _gridLayout;
        HorizontalLayoutGroup _buttonRow;
        ScrollRect _scroll;
        Text _count, _weight, _value;
        Button _sellDrone;
        bool _dirty = true;
        bool _droneReadyShown;
        /// <summary>Phone layout (<see cref="UiScale.Compact"/>): bigger text and touch-sized buttons on a small canvas.</summary>
        bool _compact;
        static InventoryMenu _current;

        public bool IsOpen => _panel.gameObject.activeSelf;

        /// <summary>Godot inventory_menu.visible, read by player.gd is_mouse_over_ui() to block throws.</summary>
        public static bool AnyOpen => _current != null && _current._panel != null && _current.IsOpen;

        public static InventoryMenu Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "InventoryMenu", PanelStyle);
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 800, 600);
            var menu = canvas.gameObject.AddComponent<InventoryMenu>();
            _current = menu;
            menu._panel = panel.rectTransform;
            menu.Build();
            menu.ApplyLayout();
            UiScale.Changed += menu.ApplyLayout;
            panel.gameObject.SetActive(false);
            return menu;
        }

        void Build()
        {
            var c = _content = UiKit.Fill(UiKit.Rect(_panel, "MarginContainer"), 33, 33, 33, 33);
            _title = UiKit.Label(c, "Title", "INVENTORY", 32, Color.white, TextAnchor.UpperCenter);
            _subtitle = UiKit.Label(c, "Subtitle", "Use release button to free fish", 16, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperCenter);
            _separator = UiKit.Separator(c);

            _count = UiKit.Label(c, "FishCount", "Fish Count: 0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);
            _weight = UiKit.Label(c, "TotalWeight", "Total Weight: 0 / 0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);
            _value = UiKit.Label(c, "TotalValue", "Total Value: $0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);

            // ScrollContainer with its panel style, 10 px margin, GridContainer 5 columns, 15 px gaps
            var scrollBg = UiKit.Panel(c, "ScrollContainer", ScrollStyle);
            _scrollBg = scrollBg.rectTransform;
            var viewport = UiKit.Fill(UiKit.Rect(scrollBg.transform, "Viewport"), 2, 2, 2, 2);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            _grid = UiKit.Rect(viewport, "FishGrid");
            _grid.anchorMin = new Vector2(0, 1);
            _grid.anchorMax = new Vector2(1, 1);
            _grid.pivot = new Vector2(0.5f, 1);
            _grid.offsetMin = Vector2.zero;
            _grid.offsetMax = Vector2.zero;
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = _scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // ButtonsContainer: 150×50, separation 20, centred
            _buttons = UiKit.Rect(c, "ButtonsContainer");
            _buttonRow = _buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            _buttonRow.childAlignment = TextAnchor.MiddleCenter;
            _buttonRow.childControlWidth = _buttonRow.childControlHeight = false;
            _buttonRow.childForceExpandWidth = _buttonRow.childForceExpandHeight = false;
            var close = UiKit.Button(_buttons, "CloseButton", "Close", 18, UiKit.BlueButton, Close);
            _closeRt = close.GetComponent<RectTransform>();
            _closeLabel = close.GetComponentInChildren<Text>(true);
            _sellDrone = UiKit.Button(_buttons, "SellDroneButton", "Sell Drone", 18, UiKit.BlueButton, OnSellDrone);
            _sellRt = _sellDrone.GetComponent<RectTransform>();
            _sellLabel = _sellDrone.GetComponentInChildren<Text>(true);
        }

        /// <summary>
        /// Sizes and positions for the current canvas. Desktop (not Compact) keeps the Godot 800×600
        /// layout exactly; it only shrinks the panel (and the column count) if the canvas is narrower.
        /// Compact (phones, canvas about 761..952 × 428): a canvas-sized panel with a one-line header,
        /// 16 sp totals, a scrollable grid of touch-sized cards and 48-unit buttons.
        /// Runs at creation and on <see cref="UiScale.Changed"/>.
        /// </summary>
        void ApplyLayout()
        {
            if (_panel == null) return;
            var canvas = UiScale.CanvasSize;
            _compact = UiScale.Compact;

            float panelW = Mathf.Min(800f, canvas.x - 16f);
            float panelH = Mathf.Min(600f, canvas.y - 16f);
            UiKit.Centered(_panel, panelW, panelH);

            float margin = _compact ? 12f : 33f;
            UiKit.Fill(_content, margin, margin, margin, margin);
            float viewportW = panelW - 2f * margin - 4f; // ScrollContainer fills the margin box, viewport inset 2

            float cellW, cellH, spacing, buttonW, buttonH;
            int maxCols, fontStats, fontButton;
            if (_compact)
            {
                // Row 1 (0..32): title left, hint right. Separator at 36. Row 2 (40..64): the three totals.
                _title.fontSize = 24;
                _title.alignment = TextAnchor.MiddleLeft;
                UiKit.Place(_title.rectTransform, 0, 0, 0.4f, 0, 0, 0, 0, 32);
                _subtitle.fontSize = 14;
                _subtitle.alignment = TextAnchor.MiddleRight;
                UiKit.Place(_subtitle.rectTransform, 0.4f, 0, 1, 0, 0, 0, 0, 32);
                UiKit.Place(_separator.rectTransform, 0, 0, 1, 0, 0, 36, 0, 37);
                UiKit.Place(_count.rectTransform, 0, 0, 0.22f, 0, 0, 40, 0, 64);
                UiKit.Place(_weight.rectTransform, 0.22f, 0, 0.62f, 0, 0, 40, 0, 64);
                UiKit.Place(_value.rectTransform, 0.62f, 0, 1, 0, 0, 40, 0, 64);
                fontStats = 16;
                UiKit.Place(_scrollBg, 0, 0, 1, 1, 0, 70, 0, -56);
                UiKit.Place(_buttons, 0, 1, 1, 1, 0, -48, 0, 0);
                _buttonRow.spacing = 16;
                buttonW = 150; buttonH = 48; fontButton = 16;
                cellW = 150; cellH = 170; spacing = 12; maxCols = 6;
            }
            else
            {
                _title.fontSize = 32;
                _title.alignment = TextAnchor.UpperCenter;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, 0, 44);
                _subtitle.fontSize = 16;
                _subtitle.alignment = TextAnchor.UpperCenter;
                UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 44, 0, 66);
                UiKit.Place(_separator.rectTransform, 0, 0, 1, 0, 0, 87, 0, 88);
                UiKit.Place(_count.rectTransform, 0, 0, 1, 0, 0, 110, 0, 135);
                UiKit.Place(_weight.rectTransform, 0, 0, 1, 0, 0, 135, 0, 160);
                UiKit.Place(_value.rectTransform, 0, 0, 1, 0, 0, 160, 0, 185);
                fontStats = 18;
                UiKit.Place(_scrollBg, 0, 0, 1, 1, 0, 205, 0, -70);
                UiKit.Place(_buttons, 0, 1, 1, 1, 0, -50, 0, 0);
                _buttonRow.spacing = 20;
                buttonW = 150; buttonH = 50; fontButton = 18;
                cellW = 130; cellH = 175; spacing = 15; maxCols = 5;
            }
            _count.fontSize = _weight.fontSize = _value.fontSize = fontStats;
            _closeRt.sizeDelta = _sellRt.sizeDelta = new Vector2(buttonW, buttonH);
            _closeLabel.fontSize = _sellLabel.fontSize = fontButton;

            // Grid: as many columns as fit (5 on the 800-wide desktop panel); on phones the cards
            // stretch to share the row evenly.
            const int pad = 10;
            float avail = viewportW - 2 * pad;
            int cols = Mathf.Clamp(Mathf.FloorToInt((avail + spacing + 0.5f) / (cellW + spacing)), 1, maxCols);
            if (_compact) cellW = Mathf.Floor((avail - (cols - 1) * spacing) / cols);
            _gridLayout.padding = new RectOffset(pad, pad, pad, pad);
            _gridLayout.cellSize = new Vector2(cellW, cellH);
            _gridLayout.spacing = new Vector2(spacing, spacing);
            _gridLayout.constraintCount = cols;
            _scroll.StopMovement(); // no leftover fling velocity after the reset
            _grid.anchoredPosition = Vector2.zero; // scroll back to the top

            _dirty = true; // rebuild the cards for the new layout
        }

        void OnEnable()
        {
            if (GameState.Instance != null) GameState.Instance.OnInventoryUpdated += MarkDirty;
        }

        void Start()
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.OnInventoryUpdated -= MarkDirty;
                GameState.Instance.OnInventoryUpdated += MarkDirty;
            }
        }

        void OnDisable()
        {
            if (GameState.Instance != null) GameState.Instance.OnInventoryUpdated -= MarkDirty;
        }

        void OnDestroy()
        {
            UiScale.Changed -= ApplyLayout;
            if (_current == this) _current = null;
        }

        void MarkDirty() => _dirty = true;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            _panel.gameObject.SetActive(true);
            _dirty = true;
        }

        public void Close() => _panel.gameObject.SetActive(false);

        void Update()
        {
            // Re-enable Sell Drone once the drone cooldown ends or the sub leaves the dock
            if (IsOpen && DroneReady() != _droneReadyShown) _dirty = true;
            if (IsOpen && _dirty) Refresh();
        }

        /// <summary>
        /// ActivateSellingDrone() ignores presses during the cooldown or while docked (the Q path of
        /// player.gd:291-293); the button shows that instead of doing nothing when pressed.
        /// </summary>
        static bool DroneReady()
        {
            var gs = GameState.Instance;
            return gs == null || gs.Player == null || gs.Player.DroneReady;
        }

        void Refresh()
        {
            _dirty = false;
            var gs = GameState.Instance;
            if (gs == null) return;
            var inv = gs.Inventory;
            _count.text = $"Fish Count: {inv.FishesCaught}";
            _weight.text = $"Total Weight: {inv.TotalWeight:F1} / {inv.GetMaxWeight():F1} kg";
            _value.text = $"Total Value: ${inv.TotalValue:F2}";

            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);

            // Stack identical fish (are_fish_stackable)
            var groups = new List<List<InventoryItem>>();
            foreach (var item in inv.Items)
            {
                var group = groups.Find(g => g[0].Type == item.Type && Mathf.Abs(g[0].Weight - item.Weight) < 0.1f &&
                                             g[0].Price == item.Price && g[0].Shiny == item.Shiny);
                if (group != null) group.Add(item);
                else groups.Add(new List<InventoryItem> { item });
            }
            foreach (var g in groups) FishCard(g);

            bool drone = gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0 && !gs.IsIntro();
            _sellDrone.gameObject.SetActive(drone);
            _droneReadyShown = DroneReady();
            _sellDrone.interactable = inv.Items.Count > 0 && _droneReadyShown;
        }

        void FishCard(List<InventoryItem> stack)
        {
            var item = stack[0];
            var card = UiKit.Panel(_grid, "FishItem", CardStyle).rectTransform;

            var slot = UiKit.Panel(card, "FishImageContainer", SlotStyle).rectTransform;
            UiKit.Place(slot, 0, 0, 1, 0, 6, 6, -6, _compact ? 74 : 96);
            var backing = UiKit.ColorRect(slot, "Background", item.Shiny ? new Color(0.8f, 0.7f, 0.2f, 0.3f) : new Color(0.1f, 0.1f, 0.1f, 0.5f));
            UiKit.Fill(backing.rectTransform, 2, 2, 2, 2);
            UiKit.Fill(UiKit.Picture(slot, "FishTexture", GodotAssets.Sprite(Achievements.IconPath(item.Type))).rectTransform, 6, 6, 6, 6);
            if (stack.Count > 1)
            {
                var badge = UiKit.Panel(slot, "CountBadge", BadgeStyle).rectTransform;
                UiKit.Place(badge, 1, 0, 1, 0, -30, 0, 0, 30);
                UiKit.Fill(UiKit.Label(badge, "CountLabel", stack.Count.ToString(), 14, Color.white, TextAnchor.MiddleCenter).rectTransform);
            }

            var weight = UiKit.Label(card, "WeightLabel", $"Weight: {item.Weight:F1} kg", _compact ? 14 : 12, UiKit.DefaultText, TextAnchor.MiddleCenter);
            var value = UiKit.Label(card, "ValueLabel", $"Value: ${item.Price}", _compact ? 14 : 12, UiKit.DefaultText, TextAnchor.MiddleCenter);
            var release = UiKit.Button(card, "ReleaseButton", "Release", _compact ? 16 : 12, UiKit.BlueButton, () => Release(stack[stack.Count - 1]));
            if (_compact)
            {
                // 170-unit card: image 6..74, 14 sp labels, 46-unit touch button 118..164
                UiKit.Place(weight.rectTransform, 0, 0, 1, 0, 6, 77, -6, 96);
                UiKit.Place(value.rectTransform, 0, 0, 1, 0, 6, 96, -6, 115);
                UiKit.Place(release.GetComponent<RectTransform>(), 0, 0, 1, 0, 6, 118, -6, 164);
            }
            else
            {
                UiKit.Place(weight.rectTransform, 0, 0, 1, 0, 6, 101, -6, 118);
                UiKit.Place(value.rectTransform, 0, 0, 1, 0, 6, 120, -6, 137);
                UiKit.Place(release.GetComponent<RectTransform>(), 0, 0, 1, 0, 6, 141, -6, 165);
            }
        }

        /// <summary>inventory.gd release_fish(): the fish swims off near the submarine.</summary>
        static void Release(InventoryItem item)
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.Inventory.Remove(item)) return;
            var player = gs.PlayerTransform;
            if (player == null) return;
            var pos = player.position + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1f, 1f), 0f);
            FishBehaviour.SpawnReleased(item, pos, player.parent);
        }

        void OnSellDrone()
        {
            var gs = GameState.Instance;
            if (gs?.Player != null) gs.Player.ActivateSellingDrone();
            _dirty = true;
        }
    }
}
