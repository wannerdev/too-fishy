using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// The UI node of scenes/main_scene.tscn: builds every screen on a 1920×1080 canvas (Godot's
    /// viewport with stretch "canvas_items" / aspect "expand"), routes Esc and E/Tab like the Godot
    /// scripts, and freezes the game (Godot tree pause) while the pause menu or a story dialog
    /// is open.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        PauseMenu _pause;
        UpgradeMenu _upgrades;
        InventoryMenu _inventory;
        InvUi _invUi;

        public static GameUI Create()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Rings sit on a CanvasLayer below the UI in Godot
            var rings = NewCanvas("CooldownCanvas", -1);
            CooldownRings.Create(rings);

            var canvas = NewCanvas("UI", 0);
            var ui = canvas.gameObject.AddComponent<GameUI>();

            ui._invUi = InvUi.Create(canvas);        // z -2: under the HUD
            HudBar.Create(canvas);                   // z -1
            AchievementPanel.Create(canvas);
            BossHealthBar.Create(canvas);
            DamageEffects.Create(canvas);
            FpsCounter.Create(canvas);

            // CenterContainer children, in scene order
            ui._inventory = InventoryMenu.Create(canvas);
            DialogPanel.Create(canvas);
            ui._upgrades = UpgradeMenu.Create(canvas);
            DeathScreen.Create(canvas);
            ui._pause = PauseMenu.Create(canvas);

            if (GameInput.TouchMode)
            {
                // Below the UI canvas so menus and buttons get touches first; everything else
                // falls through to the joystick pad.
                var touch = NewCanvas("TouchCanvas", -2);
                TouchControls.Create(touch);
            }
            return ui;
        }

        static RectTransform NewCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;
            return (RectTransform)go.transform;
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            // level.gd calls Boss.process_dialog_depth() every frame
            Dialogs.ProcessDepth(gs.MaxDepthReached);

            if (GameInput.ConsumePause() && !Dialogs.PausesGame)
            {
                // upgrades.gd closes the shop on Esc while docked; pause_menu.gd ignores Esc there
                if (!_upgrades.HandleEscape())
                    _pause.Toggle();
            }

            // inv_toggle (E / Tab): inv_ui.gd hides its panel, player.gd opens the inventory menu
            if (GameInput.ConsumeInventory() && !_pause.IsPaused && !Dialogs.PausesGame)
            {
                _inventory.Toggle();
                _invUi.gameObject.SetActive(!_invUi.gameObject.activeSelf);
            }

            bool frozen = _pause.IsPaused || Dialogs.PausesGame;
            Time.timeScale = frozen ? 0f : 1f;
            gs.Paused = frozen || gs.DeathScreen;
        }
    }
}
