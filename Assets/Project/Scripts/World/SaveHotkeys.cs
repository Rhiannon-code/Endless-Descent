using EndlessDescent.Core;
using EndlessDescent.Player;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Nothing ever called SaveCoordinator, so every ISaveable in the project was untested. F5 and F9
    // give it a way to actually run
    [DisallowMultipleComponent]
    public class SaveHotkeys : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] SaveCoordinator coordinator;

        void Update()
        {
            if (input == null || coordinator == null)
                return;

            if (input.QuickSavePressed)
            {
                coordinator.Rebuild();
                Notice.Show(coordinator.Save() ? "Saved." : "Save failed.");
            }

            if (input.QuickLoadPressed)
            {
                coordinator.Rebuild();
                Notice.Show(coordinator.Load() ? "Loaded." : "Nothing to load.");
            }
        }
    }
}
