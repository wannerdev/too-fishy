using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/upgrades.gd: the 900×570 "SUBMARINE UPGRADES" panel that opens on its own
    /// while docked. Three columns (EQUIPMENT / PERFORMANCE / UTILITY) of upgrade cards with level
    /// pips, cost and an Upgrade button. Esc closes it until the submarine leaves the dock.
    ///
    /// On a phone-sized canvas (<see cref="UiScale.Compact"/>) the panel fills the screen with an
    /// 8-unit margin, the cards get a shorter two-row design with a 44-unit Upgrade button, and the
    /// cards scroll vertically under fixed column headers. Desktop keeps Godot's exact layout.
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
        // upgrades.gd:329-330 update_button_state() redraws empty pips like this on every menu open
        static readonly StyleBox PipEmpty = new(new Color(0.307f, 0.307f, 0.307f, 0.5f), 0, null, 3);
        // Compact only: slim scroll position indicator in the panel's right padding
        static readonly StyleBox ScrollTrack = new(new Color(0f, 0f, 0f, 0.25f), 0, null, 2);
        static readonly StyleBox ScrollHandle = new(new Color(0.3f, 0.5f, 0.7f, 0.9f), 0, null, 2);
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
            public Text Name;
            public Image[] Pips;
            public Text Info;
            public Button Button;
        }

        class Column
        {
            public RectTransform Rect, Header;
            public Text HeaderLabel;
            public readonly List<Card> Cards = new();
        }

        /// <summary>All sizes of one layout. <see cref="Desktop"/> holds the Godot numbers.</summary>
        struct Metrics
        {
            public bool Compact;
            /// <summary>Cards sit in the clipped, vertically scrolling viewport (phone, or a canvas too short for them).</summary>
            public bool Scroll;
            public float PanelW, PanelH, PadX, PadY;
            /// <summary>Tops (in the margin container) of the column headers and of the card viewport;
            /// <see cref="CardsTop"/> is the first card's y inside the viewport.</summary>
            public float HeadersTop, ViewportTop;
            public float ColHeaderH, CardsTop, CardH, CardGap, ColGap;
            public int TitleFont, MoneyFont, ColHeaderFont;
        }

        readonly List<Card> _cards = new();
        readonly List<Column> _cols = new();
        RectTransform _panel, _content, _headers, _viewport, _columns, _moneyPanel;
        Text _title, _subtitle, _money;
        FitWidthToText _moneyFit;
        Image _viewportHit;
        RectMask2D _mask;
        ScrollRect _scroll;
        Scrollbar _scrollbar;
        bool _manOverride;
        bool _wasVisible;
        bool _scrollApplied;
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
            menu.ApplyLayout();
            UiScale.Changed += menu.OnUiScaleChanged;
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
            // The menu's panel may never have been active, so its own OnDestroy may not run
            void OnDestroy()
            {
                if (!ReferenceEquals(Menu, null)) UiScale.Changed -= Menu.OnUiScaleChanged;
            }
        }

        void OnDestroy() => UiScale.Changed -= OnUiScaleChanged;

        void OnUiScaleChanged()
        {
            if (this == null)
            {
                UiScale.Changed -= OnUiScaleChanged;
                return;
            }
            ApplyLayout();
        }

        void BuildStatic()
        {
            _content = UiKit.Rect(_panel, "MarginContainer");

            _title = UiKit.Label(_content, "Title", "SUBMARINE UPGRADES", 28, Color.white, TextAnchor.UpperLeft);
            _subtitle = UiKit.Label(_content, "Subtitle", "Select upgrades to enhance your submarine", 16, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperLeft);

            _moneyPanel = UiKit.Panel(_content, "MoneyPanel", MoneyStyle).rectTransform;
            _money = UiKit.Label(_moneyPanel, "MoneyLabel", "Available: $0", 18, new Color(1f, 1f, 0.2f), TextAnchor.MiddleCenter);
            _moneyFit = _moneyPanel.gameObject.AddComponent<FitWidthToText>();

            // Column headers stay put; the cards scroll underneath them on a phone
            _headers = UiKit.Rect(_content, "ColumnHeaders");

            _viewport = UiKit.Rect(_content, "Viewport");
            _mask = _viewport.gameObject.AddComponent<RectMask2D>();
            _viewportHit = _viewport.gameObject.AddComponent<Image>();
            _viewportHit.color = new Color(0, 0, 0, 0); // catches drags that start between cards
            _columns = UiKit.Rect(_viewport, "Columns");

            var bar = UiKit.Panel(_content, "Scrollbar", ScrollTrack);
            bar.raycastTarget = false;
            var sliding = UiKit.Fill(UiKit.Rect(bar.transform, "SlidingArea"));
            var handle = UiKit.Panel(sliding, "Handle", ScrollHandle);
            handle.raycastTarget = false;
            UiKit.Fill(handle.rectTransform);
            _scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            _scrollbar.handleRect = handle.rectTransform;
            _scrollbar.targetGraphic = handle;
            _scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scrollbar.transition = Selectable.Transition.None;
            _scrollbar.interactable = false; // indicator only; drag the cards to scroll
            var nav = _scrollbar.navigation;
            nav.mode = Navigation.Mode.None;
            _scrollbar.navigation = nav;

            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _columns;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            _scroll.verticalScrollbar = _scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>Resizes the money box to its text (Godot PanelContainer shrink-to-fit).</summary>
        class FitWidthToText : MonoBehaviour
        {
            Text _text;
            float _padding, _min;
            public void Init(Text t, float padding, float min) { _text = t; _padding = padding; _min = min; }
            void LateUpdate()
            {
                if (_text == null) return;
                var rt = (RectTransform)transform;
                float w = Mathf.Max(_min * 0.5f, _text.preferredWidth + _padding);
                rt.offsetMin = new Vector2(-w, rt.offsetMin.y);
            }
        }

        // ------------------------------------------------------------------ layout

        static Metrics Desktop(Vector2 canvas, int maxCards)
        {
            const float columnsTop = 80f; // header (60) + spacer 20
            var m = new Metrics
            {
                Compact = false,
                Scroll = false,
                // Godot: 900×570; only narrower if the canvas itself is (never on a monitor or tablet)
                PanelW = Mathf.Min(900f, canvas.x - 16f),
                PanelH = Mathf.Min(570f, canvas.y - 16f),
                PadX = 33f, PadY = 33f,
                HeadersTop = columnsTop, ViewportTop = columnsTop,
                ColHeaderH = 47f,
                CardsTop = 47f + 10f + 4f,
                CardH = 90f, CardGap = 10f, ColGap = 20f,
                TitleFont = 28, MoneyFont = 18, ColHeaderFont = 20
            };
            // With 5-6 equipment cards (after the boss) the cards hang below the 570 panel, as they
            // always have on desktop, where they still end inside the 1080 canvas (y 1019 at most).
            // A shorter canvas (a 1362×851 tablet) would cut them off: there the panel grows to hold
            // them, and if even that does not fit, the cards scroll under the fixed column headers.
            float cardsBottom = m.CardsTop + Mathf.Max(1, maxCards) * (m.CardH + m.CardGap) - m.CardGap;
            float panelTop = (canvas.y - m.PanelH) * 0.5f;
            if (panelTop + m.PadY + columnsTop + cardsBottom > canvas.y - 8f)
            {
                float needed = 2f * m.PadY + columnsTop + cardsBottom;
                m.PanelH = Mathf.Min(canvas.y - 16f, needed);
                if (needed > m.PanelH)
                {
                    m.Scroll = true;
                    m.ViewportTop = columnsTop + m.CardsTop; // the first card's top, as before
                    m.CardsTop = 0f;
                }
            }
            return m;
        }

        static Metrics CompactMetrics(Vector2 canvas, int maxCards)
        {
            var m = new Metrics
            {
                Compact = true,
                Scroll = true,
                PanelW = Mathf.Min(900f, canvas.x - 16f),
                PadX = 12f, PadY = 8f,
                HeadersTop = 42f,  // title row 36 + 6
                ViewportTop = 78f, // + column header 30 + 6
                ColHeaderH = 30f,
                CardsTop = 0f,     // inside the scroll content
                CardH = 74f, CardGap = 6f, ColGap = 8f,
                TitleFont = 22, MoneyFont = 16, ColHeaderFont = 18
            };
            float cards = Mathf.Max(1, maxCards) * (m.CardH + m.CardGap) - m.CardGap;
            m.PanelH = Mathf.Min(canvas.y - 16f, Mathf.Min(570f, m.PadY * 2f + m.ViewportTop + cards));
            return m;
        }

        int MaxCards()
        {
            int n = 4;
            foreach (var c in _cols) n = Mathf.Max(n, c.Cards.Count);
            return n;
        }

        /// <summary>Places everything for the current <see cref="UiScale.CanvasSize"/>; runs at creation,
        /// after every column rebuild and on <see cref="UiScale.Changed"/>.</summary>
        void ApplyLayout()
        {
            if (_panel == null) return;
            var canvas = UiScale.CanvasSize;
            var m = UiScale.Compact ? CompactMetrics(canvas, MaxCards()) : Desktop(canvas, MaxCards());

            UiKit.Centered(_panel, m.PanelW, m.PanelH);
            UiKit.Fill(_content, m.PadX, m.PadY, m.PadX, m.PadY);

            if (!m.Compact)
            {
                _title.fontSize = 28;
                _title.alignment = TextAnchor.UpperLeft;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, -250, 38);
                _subtitle.gameObject.SetActive(true);
                UiKit.Place(_subtitle.rectTransform, 0, 0, 1, 0, 0, 38, -250, 60);

                UiKit.Place(_moneyPanel, 1, 0, 1, 0, -175, 7, 0, 52);
                _money.fontSize = 18;
                UiKit.Fill(_money.rectTransform, 17, 12, 17, 12);
                _moneyFit.Init(_money, 34f, 175f);
            }
            else
            {
                // One 36-unit row: title left, money right. The subtitle is dropped to save height.
                _title.fontSize = m.TitleFont;
                _title.alignment = TextAnchor.MiddleLeft;
                UiKit.Place(_title.rectTransform, 0, 0, 1, 0, 0, 0, -170, 36);
                _subtitle.gameObject.SetActive(false);

                UiKit.Place(_moneyPanel, 1, 0, 1, 0, -150, 0, 0, 36);
                _money.fontSize = m.MoneyFont;
                UiKit.Fill(_money.rectTransform, 12, 4, 12, 4);
                _moneyFit.Init(_money, 24f, 150f);
            }

            UiKit.Fill(_headers, 0, m.HeadersTop, 0, 0);
            UiKit.Fill(_viewport, 0, m.ViewportTop, 0, 0);

            // Scrolling on a phone (or a canvas too short for the cards); on desktop the extra
            // objects are inert (no clip, no raycast)
            _mask.enabled = m.Scroll;
            _viewportHit.enabled = m.Scroll;
            _viewportHit.raycastTarget = m.Scroll;
            _scroll.StopMovement();
            _scroll.enabled = m.Scroll;
            var barRt = (RectTransform)_scrollbar.transform;
            // In the panel's right padding, inside its 3-unit border
            UiKit.Place(barRt, 1, 0, 1, 1, 3, m.ViewportTop, 7, 0);
            if (!m.Scroll) _scrollbar.gameObject.SetActive(false);

            float contentW = m.PanelW - 2f * m.PadX;
            float colWidth = (contentW - 2f * m.ColGap) / 3f;
            float cardsHeight = 0f;
            for (int i = 0; i < _cols.Count; i++)
            {
                var col = _cols[i];
                float x = i * (colWidth + m.ColGap);
                UiKit.Place(col.Rect, 0, 0, 0, 1, x, 0, x + colWidth, 0);

                UiKit.Place(col.Header, 0, 0, 0, 0, x, 0, x + colWidth, m.ColHeaderH);
                col.HeaderLabel.fontSize = m.ColHeaderFont;

                float y = m.CardsTop;
                foreach (var card in col.Cards)
                {
                    LayoutCard(card, y, m.Compact);
                    y += m.CardH + m.CardGap;
                }
                if (col.Cards.Count > 0) cardsHeight = Mathf.Max(cardsHeight, y - m.CardGap);
            }

            if (m.Scroll)
            {
                // Scroll content: top-anchored, as tall as the longest column
                _columns.pivot = new Vector2(0.5f, 1f);
                _columns.anchorMin = new Vector2(0f, 1f);
                _columns.anchorMax = new Vector2(1f, 1f);
                _columns.sizeDelta = new Vector2(0f, cardsHeight);
                if (!_scrollApplied) _columns.anchoredPosition = Vector2.zero;
            }
            else
            {
                _columns.pivot = new Vector2(0.5f, 0.5f);
                UiKit.Fill(_columns);
            }
            _scrollApplied = m.Scroll;
        }

        static void LayoutCard(Card card, float y, bool compact)
        {
            if (!compact)
            {
                // upgrades.gd card: 90 tall, name / pips / cost + 100×30 button
                UiKit.Place(card.Background.rectTransform, 0, 0, 1, 0, 0, y, 0, y + 90);
                UiKit.Place(card.Name.rectTransform, 0, 0, 1, 0, 14, 8, -14, 30);
                for (int i = 0; i < card.Pips.Length; i++)
                    UiKit.TopLeft(card.Pips[i].rectTransform, 14 + i * 30, 35, 25, 8);
                UiKit.Place(card.Info.rectTransform, 0, 0, 1, 0, 14, 48, -14, 78);
                UiKit.Place(card.Button.GetComponent<RectTransform>(), 1, 0, 1, 0, -114, 48, -14, 78);
                return;
            }
            // Phone card, 74 tall: name across the top; pips and cost on the left under it,
            // a 90×44 Upgrade button on the right.
            UiKit.Place(card.Background.rectTransform, 0, 0, 1, 0, 0, y, 0, y + 74);
            UiKit.Place(card.Name.rectTransform, 0, 0, 1, 0, 10, 4, -10, 26);
            for (int i = 0; i < card.Pips.Length; i++)
                UiKit.TopLeft(card.Pips[i].rectTransform, 10 + i * 22, 32, 18, 7);
            UiKit.Place(card.Info.rectTransform, 0, 0, 1, 0, 10, 42, -10, 66);
            UiKit.Place(card.Button.GetComponent<RectTransform>(), 1, 0, 1, 0, -98, 26, -8, 70);
        }

        // ------------------------------------------------------------------ cards

        void BuildColumns()
        {
            var gs = GameState.Instance;
            for (int i = _columns.childCount - 1; i >= 0; i--) Destroy(_columns.GetChild(i).gameObject);
            for (int i = _headers.childCount - 1; i >= 0; i--) Destroy(_headers.GetChild(i).gameObject);
            _cards.Clear();
            _cols.Clear();

            // upgrades.gd: AK47 only after meeting the boss, the 2nd gun only after the first
            var equipment = new List<Upgrade>();
            foreach (var u in Equipment)
            {
                if (u == Upgrade.Ak47 && (gs == null || !gs.BossEncountered)) continue;
                if (u == Upgrade.DualAk47 && (gs == null || gs.GetUpgradeLevel(Upgrade.Ak47) == 0)) continue;
                equipment.Add(u);
            }

            AddColumn("EQUIPMENT", equipment);
            AddColumn("PERFORMANCE", new List<Upgrade>(Performance));
            AddColumn("UTILITY", new List<Upgrade>(Utility));
            _builtLayoutKey = LayoutKey(gs);
            _lastStateKey = int.MinValue;
            ApplyLayout();
        }

        void AddColumn(string name, List<Upgrade> keys)
        {
            var col = new Column { Rect = UiKit.Rect(_columns, name) };
            col.Header = UiKit.Panel(_headers, name + "Header", HeaderStyle).rectTransform;
            col.HeaderLabel = UiKit.Label(col.Header, "Label", name, 20, Yellow, TextAnchor.MiddleCenter);
            UiKit.Fill(col.HeaderLabel.rectTransform);

            foreach (var key in keys)
            {
                var card = MakeCard(col.Rect, key);
                col.Cards.Add(card);
                _cards.Add(card);
            }
            _cols.Add(col);
        }

        Card MakeCard(RectTransform col, Upgrade key)
        {
            var bg = UiKit.Panel(col, "Upgrade_" + key, CardAffordable);
            var card = new Card { Key = key, Background = bg };

            card.Name = UiKit.Label(bg.transform, "Name", GameState.UpgradeName(key), 16, Color.white, TextAnchor.MiddleLeft);

            int max = GameState.MaxUpgrades[key];
            card.Pips = new Image[max];
            for (int i = 0; i < max; i++)
                card.Pips[i] = UiKit.Panel(bg.transform, "Level" + i, PipEmpty);

            card.Info = UiKit.Label(bg.transform, "Info", "", 16, Yellow, TextAnchor.MiddleLeft);
            card.Button = UiKit.Button(bg.transform, "Upgrade", "Upgrade", 16, UpgradeAffordable, () => OnUpgrade(key));
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
            // upgrades.gd also clears is_docked, but the player sets it again every frame;
            // the override alone keeps the menu closed until the submarine leaves the dock.
            _manOverride = true;
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
                if (_scrollApplied)
                {
                    // Each dock visit starts at the top of the card list
                    _scroll.StopMovement();
                    _columns.anchoredPosition = Vector2.zero;
                }
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
