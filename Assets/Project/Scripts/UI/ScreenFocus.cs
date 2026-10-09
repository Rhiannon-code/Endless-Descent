using UnityEngine;

namespace EndlessDescent.UI
{
    // A screen opened by something in the world, a shop counter, a posting board, needs the same
    // hands off treatment the menu gets, the cursor back, and the player's hands off the weapon.
    // Without it a click meant for a button swings a sword at the board instead
    public static class ScreenFocus
    {
        public static void Take() => Apply(true);
        public static void Release() => Apply(false);

        static void Apply(bool held)
        {
            GameMenu menu = Object.FindFirstObjectByType<GameMenu>();

            if (menu != null)
            {
                menu.HoldForScreen(held);
                return;
            }

            // No menu in this scene, at least give the cursor back
            Cursor.lockState = held ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = held;
        }
    }
}
