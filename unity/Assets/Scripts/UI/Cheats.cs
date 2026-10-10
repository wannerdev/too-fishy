using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>Port of scenes/cheats.tscn + scripts/cheats.gd (C toggles it, not while docked).</summary>
    public class Cheats : MonoBehaviour
    {
        RectTransform _panel;

        public static Cheats Create(RectTransform canvas)
        {
            var panel = UiKit.ThemePanel(canvas, "Cheats");
            panel.raycastTarget = true;
            // main_scene.tscn placement (130, 262)-(444, 438)
            UiKit.TopLeft(panel.rectTransform, 130, 262, 314, 176);
            var c = canvas.gameObject.AddComponent<Cheats>();
            c._panel = panel.rectTransform;
            var content = UiKit.Fill(UiKit.Rect(panel.transform, "VBoxContainer"), 25, 25, 25, 25);
            UiKit.Place(UiKit.Label(content, "Label", "Cheats", 16, UiKit.DefaultText).rectTransform, 0, 0, 1, 0, 0, 0, 0, 23);
            var grid = UiKit.Fill(UiKit.Rect(content, "GridContainer"), 0, 27, 0, 0);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(84, 24);
            g.spacing = new Vector2(4, 4);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;

            void Add(string text, UnityAction a) => UiKit.Button(grid, text, text, 12, UiKit.DefaultButton, a);
            Add("Upgrade All", () =>
            {
                var gs = GameState.Instance;
                foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade))) gs.Upgrades[u] = GameState.MaxUpgrades[u];
            });
            Add("1000+ $", () => GameState.Instance.Money += 1000);
            Add("Down 100", () => Move(-100f, true));
            Add("Go Up", () => Move(0f, false));
            Add("God mode", () => GameState.Instance.GodMode = !GameState.Instance.GodMode);
            Add("Heal", () => GameState.Instance.Health = 100f);
            Add("Kill", () => GameState.Instance.Damage(GameState.Instance.Health));
            Add("Skip Dialog", Dialogs.Skip);
            Add("Skip Intro", () => GameState.Instance.Level?.SkipIntro());
            Add("trauma 1", () => SetShake(1));
            Add("trauma 2", () => SetShake(2));
            Add("trauma 3", () => SetShake(3));
            panel.gameObject.SetActive(false);
            return c;
        }

        static void Move(float y, bool relative)
        {
            var p = GameState.Instance?.Player;
            if (p == null) return;
            var pos = p.transform.position;
            pos.y = relative ? pos.y + y : y;
            p.Teleport(pos);
        }

        static void SetShake(int mode)
        {
            var p = GameState.Instance?.Player;
            if (p != null) p.TraumaShakeMode = mode;
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            if (gs.GodMode) gs.Health = 100f;
            if (gs.IsDocked && _panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
            if (GameInput.ConsumeCheats() && !gs.IsDocked)
                _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);
        }
    }
}
