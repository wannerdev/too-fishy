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

        RectTransform _panel, _grid;
        Text _count, _weight, _value;
        Button _sellDrone;
        bool _dirty = true;

        public bool IsOpen => _panel.gameObject.activeSelf;

        public static InventoryMenu Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "InventoryMenu", PanelStyle);
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 800, 600);
            var menu = canvas.gameObject.AddComponent<InventoryMenu>();
            menu._panel = panel.rectTransform;
            menu.Build();
            panel.gameObject.SetActive(false);
            return menu;
        }

        void Build()
        {
            var c = UiKit.Fill(UiKit.Rect(_panel, "MarginContainer"), 33, 33, 33, 33);
            UiKit.Place(UiKit.Label(c, "Title", "INVENTORY", 32, Color.white, TextAnchor.UpperCenter).rectTransform, 0, 0, 1, 0, 0, 0, 0, 44);
            UiKit.Place(UiKit.Label(c, "Subtitle", "Use release button to free fish", 16, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperCenter).rectTransform, 0, 0, 1, 0, 0, 44, 0, 66);
            UiKit.Place(UiKit.Separator(c).rectTransform, 0, 0, 1, 0, 0, 87, 0, 88);

            _count = UiKit.Label(c, "FishCount", "Fish Count: 0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);
            UiKit.Place(_count.rectTransform, 0, 0, 1, 0, 0, 110, 0, 135);
            _weight = UiKit.Label(c, "TotalWeight", "Total Weight: 0 / 0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);
            UiKit.Place(_weight.rectTransform, 0, 0, 1, 0, 0, 135, 0, 160);
            _value = UiKit.Label(c, "TotalValue", "Total Value: $0", 18, UiKit.DefaultText, TextAnchor.MiddleLeft);
            UiKit.Place(_value.rectTransform, 0, 0, 1, 0, 0, 160, 0, 185);

            // ScrollContainer with its panel style, 10 px margin, GridContainer 5 columns, 15 px gaps
            var scrollBg = UiKit.Panel(c, "ScrollContainer", ScrollStyle);
            UiKit.Place(scrollBg.rectTransform, 0, 0, 1, 1, 0, 205, 0, -70);
            var viewport = UiKit.Fill(UiKit.Rect(scrollBg.transform, "Viewport"), 2, 2, 2, 2);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            _grid = UiKit.Rect(viewport, "FishGrid");
            _grid.anchorMin = new Vector2(0, 1);
            _grid.anchorMax = new Vector2(1, 1);
            _grid.pivot = new Vector2(0.5f, 1);
            _grid.offsetMin = Vector2.zero;
            _grid.offsetMax = Vector2.zero;
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(130, 175);
            grid.spacing = new Vector2(15, 15);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // ButtonsContainer: 150×50, separation 20, centred
            var buttons = UiKit.Rect(c, "ButtonsContainer");
            UiKit.Place(buttons, 0, 1, 1, 1, 0, -50, 0, 0);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 20;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var close = UiKit.Button(buttons, "CloseButton", "Close", 18, UiKit.BlueButton, Close);
            close.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 50);
            _sellDrone = UiKit.Button(buttons, "SellDroneButton", "Sell Drone", 18, UiKit.BlueButton, OnSellDrone);
            _sellDrone.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 50);
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
            if (IsOpen && _dirty) Refresh();
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
            _sellDrone.interactable = inv.Items.Count > 0;
        }

        void FishCard(List<InventoryItem> stack)
        {
            var item = stack[0];
            var card = UiKit.Panel(_grid, "FishItem", CardStyle).rectTransform;

            var slot = UiKit.Panel(card, "FishImageContainer", SlotStyle).rectTransform;
            UiKit.Place(slot, 0, 0, 1, 0, 6, 6, -6, 96);
            var backing = UiKit.ColorRect(slot, "Background", item.Shiny ? new Color(0.8f, 0.7f, 0.2f, 0.3f) : new Color(0.1f, 0.1f, 0.1f, 0.5f));
            UiKit.Fill(backing.rectTransform, 2, 2, 2, 2);
            UiKit.Fill(UiKit.Picture(slot, "FishTexture", GodotAssets.Sprite(Achievements.IconPath(item.Type))).rectTransform, 6, 6, 6, 6);
            if (stack.Count > 1)
            {
                var badge = UiKit.Panel(slot, "CountBadge", BadgeStyle).rectTransform;
                UiKit.Place(badge, 1, 0, 1, 0, -30, 0, 0, 30);
                UiKit.Fill(UiKit.Label(badge, "CountLabel", stack.Count.ToString(), 14, Color.white, TextAnchor.MiddleCenter).rectTransform);
            }

            UiKit.Place(UiKit.Label(card, "WeightLabel", $"Weight: {item.Weight:F1} kg", 12, UiKit.DefaultText, TextAnchor.MiddleCenter).rectTransform,
                0, 0, 1, 0, 6, 101, -6, 118);
            UiKit.Place(UiKit.Label(card, "ValueLabel", $"Value: ${item.Price}", 12, UiKit.DefaultText, TextAnchor.MiddleCenter).rectTransform,
                0, 0, 1, 0, 6, 120, -6, 137);
            var release = UiKit.Button(card, "ReleaseButton", "Release", 12, UiKit.BlueButton, () => Release(stack[stack.Count - 1]));
            UiKit.Place(release.GetComponent<RectTransform>(), 0, 0, 1, 0, 6, 141, -6, 165);
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
