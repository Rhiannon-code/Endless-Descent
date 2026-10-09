using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.World
{
    // A way in, standing on the world where the map says the dungeon is. A dungeon is not
    // somewhere you are teleported to, it is a door you walk through, and walking back out of it puts
    // you where you were standing
    [DisallowMultipleComponent]
    public class DungeonEntrance : MonoBehaviour, IInteractable
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] int index = -1;
        [SerializeField] string displayName = "a dungeon";

        public string Prompt => place != null && place.IsBlocked(index)
            ? $"The way into {displayName} is blocked"
            : $"Enter {displayName}";

        public void Bind(PlaceLoader loader, int regionIndex, string name)
        {
            place = loader;
            index = regionIndex;
            displayName = name;
        }

        public bool CanInteract(GameObject actor) => place != null && index >= 0;

        public void Interact(GameObject actor) => place?.Descend(index, transform.position);
    }

    // The way back. Sits in the dungeon's entrance room and returns the player to the doorway they
    // came in by, which is the only reason the world stays loaded while they are down there
    [DisallowMultipleComponent]
    public class DungeonExit : MonoBehaviour, IInteractable
    {
        [SerializeField] PlaceLoader place;

        public string Prompt => "Leave";

        public void Bind(PlaceLoader loader) => place = loader;

        public bool CanInteract(GameObject actor) => place != null;

        public void Interact(GameObject actor) => place?.Surface();
    }
}
