using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    [DisallowMultipleComponent]
    public class DungeonKeyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] DungeonGenerator generator;
        [SerializeField] int keyId;
        [SerializeField] string displayName = "Iron Key";

        public string Prompt => $"Take {displayName}";

        public void Bind(DungeonGenerator owner, int id)
        {
            generator = owner;
            keyId = id;
        }

        public bool CanInteract(GameObject actor) => generator != null && !generator.HasKey(keyId);

        public void Interact(GameObject actor)
        {
            if (generator == null)
                return;

            generator.CollectKey(keyId);
            Destroy(gameObject);
        }
    }
}
