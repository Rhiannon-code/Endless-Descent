using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    // Equips what it can and stows the rest, once, on the first frame
    [DisallowMultipleComponent]
    public class StartingKitApplier : MonoBehaviour
    {
        [SerializeField] Inventory inventory;
        [SerializeField] Equipment equipment;
        [SerializeField] ItemDefinition[] worn;
        [SerializeField] ItemDefinition[] carried;

        void Start()
        {
            // A created character arrives with their background's kit, this is the fallback for the
            // playtest scenes that skip the creator, and two kits is worse than either
            if (inventory == null || EndlessDescent.Data.GameStart.Kind == GameStartKind.NewCharacter)
                return;

            foreach (ItemDefinition item in worn)
            {
                if (item == null)
                    continue;

                inventory.Add(item, 1);
                equipment?.Equip(item);
            }

            foreach (ItemDefinition item in carried)
            {
                if (item != null)
                    inventory.Add(item, 1);
            }
        }
    }
}
