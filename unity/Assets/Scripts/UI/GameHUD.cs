using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    public class GameHUD : MonoBehaviour
    {
        Text _depth, _money, _health, _cargo, _stage, _hint;
        Image _healthFill;
        GameObject _upgradePanel;
        GameObject _deathPanel;
        bool _upgradesVisible;

        public static GameHUD Create(Transform canvasRoot)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(canvasRoot, false);
            var hud = go.AddComponent<GameHUD>();
            hud.Build(canvasRoot as RectTransform ?? canvasRoot.GetComponent<RectTransform>());
            return hud;
        }

        void Build(RectTransform canvas)
        {
            _depth = MakeLabel(canvas, "DepthText", new Vector2(20, -20), TextAnchor.UpperLeft, 28);
            _money = MakeLabel(canvas, "MoneyText", new Vector2(20, -55), TextAnchor.UpperLeft, 28);
            _cargo = MakeLabel(canvas, "CargoText", new Vector2(20, -90), TextAnchor.UpperLeft, 22);
            _stage = MakeLabel(canvas, "StageText", new Vector2(20, -120), TextAnchor.UpperLeft, 20);
            _hint = MakeLabel(canvas, "HintText", new Vector2(0, 30), TextAnchor.LowerCenter, 18);
            _hint.alignment = TextAnchor.MiddleCenter;
            _hint.text = "WASD/Arrows move · LMB harpoon · E upgrades at dock · Esc pause";

            // Health bar
            var barBg = MakePanel(canvas, "HealthBarBg", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150, -30), new Vector2(300, 18), new Color(0.1f, 0.1f, 0.1f, 0.7f));
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _healthFill = fillGo.GetComponent<Image>();
            _healthFill.color = new Color(0.2f, 0.85f, 0.35f);
            _health = MakeLabel(barBg.GetComponent<RectTransform>(), "HealthText", new Vector2(0, 0), TextAnchor.MiddleCenter, 14);
            _health.alignment = TextAnchor.MiddleCenter;
            var hrt = _health.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

            BuildUpgradePanel(canvas);
            BuildDeathPanel(canvas);

            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.OnInventoryUpdated += Refresh;
                gs.OnMoneyChanged += Refresh;
                gs.OnHealthChanged += Refresh;
                gs.OnDepthChanged += Refresh;
                gs.OnUpgradesChanged += RefreshUpgrades;
                gs.OnDeath += ShowDeath;
                gs.OnRespawn += HideDeath;
            }
            Refresh();
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab))
            {
                if (gs.IsDocked || _upgradesVisible)
                    ToggleUpgrades();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                gs.Paused = !gs.Paused;
                Time.timeScale = gs.Paused ? 0f : 1f;
            }
            Refresh();
        }

        void Refresh()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            _depth.text = $"Depth: {gs.Depth}m";
            _money.text = $"${gs.Money}";
            _cargo.text = $"Cargo: {gs.Inventory.TotalWeight:F0}/{gs.Inventory.GetMaxWeight()} kg  ({gs.Inventory.FishesCaught} fish, ${gs.Inventory.TotalValue})";
            _stage.text = GameState.StageName(gs.PlayerInStage);
            if (gs.Headroom < 0)
                _stage.text += $"  ⚠ Pressure {-gs.Headroom:F0}m over";
            _health.text = $"{gs.Health:F0} HP";
            if (_healthFill != null)
            {
                _healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(gs.Health / 100f), 1f);
                _healthFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.85f, 0.35f), gs.Health / 100f);
            }

            if (_upgradePanel != null)
                _upgradePanel.SetActive(_upgradesVisible && (gs.IsDocked || gs.Paused));
        }

        void ToggleUpgrades()
        {
            _upgradesVisible = !_upgradesVisible;
            RefreshUpgrades();
            Refresh();
        }

        void BuildUpgradePanel(RectTransform canvas)
        {
            _upgradePanel = MakePanel(canvas, "UpgradePanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-420, -250), new Vector2(400, 500), new Color(0.05f, 0.1f, 0.18f, 0.92f)).gameObject;
            _upgradePanel.SetActive(false);

            var title = MakeLabel(_upgradePanel.GetComponent<RectTransform>(), "Title", new Vector2(20, -15), TextAnchor.UpperLeft, 24);
            title.text = "Upgrades (dock)";

            float y = -50f;
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade)))
            {
                if (u == Upgrade.Ak47 || u == Upgrade.DualAk47) continue; // unlock later
                CreateUpgradeButton(_upgradePanel.GetComponent<RectTransform>(), u, ref y);
            }
        }

        void CreateUpgradeButton(RectTransform parent, Upgrade upgrade, ref float y)
        {
            var btnGo = MakePanel(parent, $"Up_{upgrade}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-180, y), new Vector2(360, 32), new Color(0.12f, 0.22f, 0.35f, 1f));
            var label = MakeLabel(btnGo.GetComponent<RectTransform>(), "L", new Vector2(10, 0), TextAnchor.MiddleLeft, 16);
            label.alignment = TextAnchor.MiddleLeft;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(10, 0); lrt.offsetMax = new Vector2(-10, 0);
            label.text = GameState.UpgradeName(upgrade);

            var btn = btnGo.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnGo;
            var captured = upgrade;
            var capturedLabel = label;
            btn.onClick.AddListener(() =>
            {
                if (GameState.Instance.TryUpgrade(captured))
                    PopupText.Show("Upgraded!", GameState.Instance.PlayerTransform.position + Vector3.up);
                RefreshUpgrades();
            });
            // store label ref via name convention for refresh
            btnGo.gameObject.name = $"Up_{upgrade}";
            y -= 36f;
        }

        void RefreshUpgrades()
        {
            if (_upgradePanel == null) return;
            var gs = GameState.Instance;
            foreach (Transform child in _upgradePanel.transform)
            {
                if (!child.name.StartsWith("Up_")) continue;
                if (!System.Enum.TryParse(child.name.Substring(3), out Upgrade u)) continue;
                var label = child.GetComponentInChildren<Text>();
                if (label == null) continue;
                int level = gs.GetUpgradeLevel(u);
                int max = GameState.MaxUpgrades[u];
                if (level >= max)
                    label.text = $"{GameState.UpgradeName(u)}  MAX";
                else
                    label.text = $"{GameState.UpgradeName(u)}  Lv{level}/{max}  ${gs.GetUpgradeCost(u)}";
            }
        }

        void BuildDeathPanel(RectTransform canvas)
        {
            _deathPanel = MakePanel(canvas, "DeathPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -100), new Vector2(400, 200), new Color(0.15f, 0.02f, 0.02f, 0.95f)).gameObject;
            var title = MakeLabel(_deathPanel.GetComponent<RectTransform>(), "DeathTitle", new Vector2(0, -30), TextAnchor.UpperCenter, 36);
            title.alignment = TextAnchor.UpperCenter;
            title.text = "You drowned...";
            var hrt = title.GetComponent<RectTransform>();
            hrt.anchoredPosition = new Vector2(0, -30);

            var btnGo = MakePanel(_deathPanel.GetComponent<RectTransform>(), "RespawnBtn", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-80, 30), new Vector2(160, 40), new Color(0.25f, 0.45f, 0.7f, 1f));
            var bl = MakeLabel(btnGo.GetComponent<RectTransform>(), "BL", Vector2.zero, TextAnchor.MiddleCenter, 22);
            bl.alignment = TextAnchor.MiddleCenter;
            bl.text = "Respawn";
            var brt = bl.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var btn = btnGo.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnGo;
            btn.onClick.AddListener(() => GameState.Instance.Respawn());
            _deathPanel.SetActive(false);
        }

        void ShowDeath() { if (_deathPanel) _deathPanel.SetActive(true); }
        void HideDeath() { if (_deathPanel) _deathPanel.SetActive(false); }

        static Text MakeLabel(RectTransform parent, string name, Vector2 pos, TextAnchor anchor, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = AnchorToVec(anchor);
            rt.pivot = AnchorToVec(anchor);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(520, 40);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Image MakePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = (anchorMin + anchorMax) * 0.5f;
            if (anchorMin == anchorMax)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
            }
            else
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
            }
            // For our usage we pass same min/max as pivot point
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin.x >= 0.99f) rt.pivot = new Vector2(1f, 0.5f);
            if (anchorMin.y >= 0.99f && anchorMin.x < 0.6f) rt.pivot = new Vector2(0.5f, 1f);
            if (anchorMin == new Vector2(1, 0.5f)) { rt.pivot = new Vector2(1, 0.5f); }
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static Vector2 AnchorToVec(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => new Vector2(0, 1),
            TextAnchor.UpperCenter => new Vector2(0.5f, 1),
            TextAnchor.UpperRight => new Vector2(1, 1),
            TextAnchor.MiddleLeft => new Vector2(0, 0.5f),
            TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
            TextAnchor.MiddleRight => new Vector2(1, 0.5f),
            TextAnchor.LowerLeft => new Vector2(0, 0),
            TextAnchor.LowerCenter => new Vector2(0.5f, 0),
            TextAnchor.LowerRight => new Vector2(1, 0),
            _ => new Vector2(0.5f, 0.5f)
        };
    }
}
