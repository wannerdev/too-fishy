using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/upgrades.gd: the 900×570 "SUBMARINE UPGRADES" panel that opens on its own
    /// while docked. Three columns (EQUIPMENT / PERFORMANCE / UTILITY) of upgrade cards with level
    /// pips, cost and an Upgrade button. Esc closes it until the submarine leaves the dock.
    /// </summary>
    public class UpgradeMenu : MonoBehaviour
    {
        static readonly StyleBox PanelStyle = new(new Color(0.05f, 0.2f, 0.3f, 0.95f), 3, new Color(0.2f, 0.4f, 0.7f), 12);
        static readonly StyleBox HeaderStyle = new(new Color(0.15f, 0.3f, 0.5f, 0.8f), 2, new Color(0.3f, 0.5f, 0.7f), 8);
        static readonly StyleBox MoneyStyle = new(new Color(0.05f, 0.15f, 0.3f, 0.7f), 2, new Color(0.7f, 0.7f, 0.2f), 8);
        static readonly StyleBox CardMax = new(new Color(0.2f, 0.35f, 0.2f, 0.7f), 2, new Color(0.5f, 0.8f, 0.3f), 8);
        static readonly StyleBox CardAffordable = new(new Color(0.1f, 0.25f, 0.4f, 0.7f), 2, new Color(0.3f, 0.5f, 0.7f), 8);
        static readonly StyleBox CardExpensive = new(new Color(0.25f, 0.15f, 0.15f, 0.7f), 2, new Color(0.5f, 0.3f, 0.3f), 8);
        static readonly StyleBox PipFilled = new(new Color(0.3f, 0.7f, 1f), 0, null, 3);
        static readonly StyleBox PipEmpty = new(new Color(0.545f, 0.545f, 0.545f), 0, null, 3);
        static readonly Color Yellow = new(0.9f, 0.9f, 0.5f);
        static readonly Color RedText = new(0.9f, 0.5f, 0.5f);
        static readonly Color GreenText = new(0.5f, 0.9f, 0.5f);

        static readonly UiKit.ButtonStyles UpgradeAffordable = new()
        {
            Normal = new StyleBox(new Color(0.2f, 0.4f, 0.7f), 1, new Color(0.4f, 0.6f, 0.9f), 5),
            Hover = UiKit.DefaultButton.Hover,
            Pressed = UiKit.DefaultButton.Pressed,
            Font = Color.white
        };
        static readonly UiKit.ButtonStyles UpgradeExpensive = new()
        {
            Normal = new StyleBox(new Color(0.3f, 0.3f, 0.35f), 1, new Color(0.4f, 0.4f, 0.45f), 5),
            Hover = UiKit.DefaultButton.Hover,
            Pressed = UiKit.DefaultButton.Pressed,
            Font = new Color(0.7f, 0.7f, 0.7f)
        };

        // upgrades.gd categories (AK47 entries are filtered as in Godot, see BuildColumns)
        static readonly Upgrade[] Equipment = { Upgrade.PickaxeUnlocked, Upgrade.LampUnlocked, Upgrade.Ak47, Upgrade.DualAk47, Upgrade.Harpoon, Upgrade.HarpoonRotation };
        static readonly Upgrade[] Performance = { Upgrade.CargoSize, Upgrade.DepthResistance, Upgrade.VertSpeed, Upgrade.HorSpeed };
        static readonly Upgrade[] Utility = { Upgrade.InventoryManagement, Upgrade.SurfaceBuoy, Upgrade.InventorySave, Upgrade.DroneSelling };

        class Card
        {
            public Upgrade Key;
            public Image Background;
            public Image[] Pips;
            public Text Info;
            public Button Button;
        }

        readonly List<Card> _cards = new();
        RectTransform _panel, _columns;
        Text _money;
        bool _manOverride;
        bool _wasVisible;
        int _builtLayoutKey = -1;

        public bool Visible => _panel.gameObject.activeSelf;

        public static UpgradeMenu Create(RectTransform canvas)
        {
            var panel = UiKit.Panel(canvas, "Upgrades", PanelStyle);
            UiKit.BlockRaycasts(panel);
            UiKit.Centered(panel.rectTransform, 900, 570);
            var menu = panel.gameObject.AddComponent<UpgradeMenu>();
            menu._panel = panel.rectTransform;
            menu.BuildStatic();
            panel.gameObject.SetActive(false);
            // The component must keep running while hidden
            var driver = new GameObject("UpgradesDriver").AddComponent<Driver>();
            driver.transform.SetParent(canvas, false);
            driver.Menu = menu;
            return menu;
        }

        class Driver : MonoBehaviour
        {
            public UpgradeMenu Menu;
            void Update() => Menu.Tick();
        }

        void BuildStatic()
        {
            var content = UiKit.Fill(UiKit.Rect(_panel, "MarginContainer"), 33, 33, 33, 33);

            var title = UiKit.Label(content, "Title", "SUBMARINE UPGRADES", 28, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(title.rectTransform, 0, 0, 1, 0, 0, 0, -250, 38);
            var subtitle = UiKit.Label(content, "Subtitle", "Select upgrades to enhance your submarine", 16, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperLeft);
            UiKit.Place(subtitle.rectTransform, 0, 0, 1, 0, 0, 38, -250, 60);

            var moneyPanel = UiKit.Panel(content, "MoneyPanel", MoneyStyle).rectTransform;
            UiKit.Place(moneyPanel, 1, 0, 1, 0, -175, 7, 0, 52);
            _money = UiKit.Label(moneyPanel, "MoneyLabel", "Available: $0", 18, new Color(1f, 1f, 0.2f), TextAnchor.MiddleCenter);
            UiKit.Fill(_money.rectTransform, 17, 12, 17, 12);
            moneyPanel.gameObject.AddComponent<FitWidthToText>().Init(_money, 34f, 175f);

            // header (60) + spacer 20
            _columns = UiKit.Fill(UiKit.Rect(content, "Columns"), 0, 80, 0, 0);
        }

        /// <summary>Resizes the money box to its text (Godot PanelContainer shrink-to-fit).</summary>
        class FitWidthToText : MonoBehaviour
        {
            Text _text;
            float _padding, _min;
            public void Init(Text t, float padding, float min) { _text = t; _padding = padding; _min = min; }
            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                float w = Mathf.Max(_min * 0.5f, _text.preferredWidth + _padding);
                rt.offsetMin = new Vector2(-w, rt.offsetMin.y);
            }
        }

        void BuildColumns()
        {
            var gs = GameState.Instance;
            for (int i = _columns.childCount - 1; i >= 0; i--) Destroy(_columns.GetChild(i).gameObject);
            _cards.Clear();

            // upgrades.gd: AK47 only after meeting the boss, the 2nd gun only after the first
            var equipment = new List<Upgrade>();
            foreach (var u in Equipment)
            {
                if (u == Upgrade.Ak47 && (gs == null || !gs.BossEncountered)) continue;
                if (u == Upgrade.DualAk47 && (gs == null || gs.GetUpgradeLevel(Upgrade.Ak47) == 0)) continue;
                equipment.Add(u);
            }

            float colWidth = (900f - 66f - 40f) / 3f;
            Column("EQUIPMENT", equipment, 0, colWidth);
            Column("PERFORMANCE", new List<Upgrade>(Performance), 1, colWidth);
            Column("UTILITY", new List<Upgrade>(Utility), 2, colWidth);
            _builtLayoutKey = LayoutKey(gs);
            _lastStateKey = int.MinValue;
        }

        void Column(string name, List<Upgrade> keys, int index, float width)
        {
            var col = UiKit.Rect(_columns, name);
            float x = index * (width + 20f);
            UiKit.Place(col, 0, 0, 0, 1, x, 0, x + width, 0);

            var header = UiKit.Panel(col, "Header", HeaderStyle).rectTransform;
            UiKit.Place(header, 0, 0, 1, 0, 0, 0, 0, 47);
            UiKit.Fill(UiKit.Label(header, "Label", name, 20, Yellow, TextAnchor.MiddleCenter).rectTransform);

            float y = 47f + 10f + 4f;
            foreach (var key in keys)
            {
                _cards.Add(MakeCard(col, key, y));
                y += 90f + 10f;
            }
        }

        Card MakeCard(RectTransform col, Upgrade key, float y)
        {
            var bg = UiKit.Panel(col, "Upgrade_" + key, CardAffordable);
            UiKit.Place(bg.rectTransform, 0, 0, 1, 0, 0, y, 0, y + 90);
            var card = new Card { Key = key, Background = bg };

            var name = UiKit.Label(bg.transform, "Name", GameState.UpgradeName(key), 16, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(name.rectTransform, 0, 0, 1, 0, 14, 8, -14, 30);

            int max = GameState.MaxUpgrades[key];
            card.Pips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var pip = UiKit.Panel(bg.transform, "Level" + i, PipEmpty);
                UiKit.TopLeft(pip.rectTransform, 14 + i * 30, 35, 25, 8);
                card.Pips[i] = pip;
            }

            card.Info = UiKit.Label(bg.transform, "Info", "", 16, Yellow, TextAnchor.MiddleLeft);
            UiKit.Place(card.Info.rectTransform, 0, 0, 1, 0, 14, 48, -14, 78);

            card.Button = UiKit.Button(bg.transform, "Upgrade", "Upgrade", 16, UpgradeAffordable, () => OnUpgrade(key));
            UiKit.Place(card.Button.GetComponent<RectTransform>(), 1, 0, 1, 0, -114, 48, -14, 78);
            return card;
        }

        void OnUpgrade(Upgrade key)
        {
            var gs = GameState.Instance;
            if (gs != null && gs.TryUpgrade(key)) RefreshCards();
        }

        void RefreshCards()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            foreach (var c in _cards)
            {
                int level = gs.GetUpgradeLevel(c.Key);
                int max = GameState.MaxUpgrades[c.Key];
                int cost = gs.GetUpgradeCost(c.Key);
                bool isMax = level >= max;
                bool afford = gs.Money >= cost;

                UiKit.ApplyStyle(c.Background, isMax ? CardMax : afford ? CardAffordable : CardExpensive);
                for (int i = 0; i < c.Pips.Length; i++)
                    UiKit.ApplyStyle(c.Pips[i], i < level ? PipFilled : PipEmpty);

                if (isMax)
                {
                    c.Info.text = "MAXIMUM LEVEL";
                    c.Info.color = GreenText;
                }
                else
                {
                    c.Info.text = "Cost: $" + cost;
                    c.Info.color = afford ? Yellow : RedText;
                }
                c.Button.gameObject.SetActive(!isMax);
                if (!isMax) UiKit.SetButtonStyles(c.Button, afford ? UpgradeAffordable : UpgradeExpensive);
            }
        }

        static int LayoutKey(GameState gs) =>
            gs == null ? 0 : (gs.BossEncountered ? 1 : 0) + (gs.GetUpgradeLevel(Upgrade.Ak47) > 0 ? 2 : 0);

        /// <summary>Esc while docked: close_upgrade_menu(). Returns true when Esc was used up.</summary>
        public bool HandleEscape()
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.IsDocked || gs.IsIntro()) return false;
            if (!_manOverride)
            {
                _manOverride = true;
                gs.IsDocked = false;
            }
            // pause_menu.gd ignores Esc while docked either way
            return true;
        }

        /// <summary>upgrades.gd _process(): visibility follows the dock, Esc closes until undocked.</summary>
        void Tick()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            bool show = false;
            if (gs.IsIntro())
            {
                _wasVisible = false;
            }
            else if (gs.IsDocked)
                show = !_manOverride;
            else
                _manOverride = false;

            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show)
            {
                _wasVisible = false;
                return;
            }

            if (_cards.Count == 0 || LayoutKey(gs) != _builtLayoutKey) BuildColumns();
            if (!_wasVisible)
            {
                _wasVisible = true;
                _lastStateKey = int.MinValue;
            }
            _money.text = "Available: $" + gs.Money;
            int stateKey = gs.Money;
            foreach (var kv in gs.Upgrades) stateKey = stateKey * 7 + kv.Value;
            if (stateKey != _lastStateKey)
            {
                _lastStateKey = stateKey;
                RefreshCards();
            }
        }

        int _lastStateKey = int.MinValue;
    }
}
