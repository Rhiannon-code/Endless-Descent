using EndlessDescent.Core;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.World
{
    // Repairs everything worn, for gold priced by how much condition is missing. Placed in dungeons
    // for now so gear can be restored without a town to walk back to
    [DisallowMultipleComponent]
    public class RepairBench : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] int goldPerPoint = 1;

        public string Prompt => "Repair equipment";

        public bool CanInteract(GameObject actor) => actor.GetComponentInParent<Equipment>() != null;

        public void Interact(GameObject actor)
        {
            Equipment equipment = actor.GetComponentInParent<Equipment>();
            Wallet wallet = actor.GetComponentInParent<Wallet>();

            if (equipment == null)
                return;

            if (equipment.RepairAll(goldPerPoint, cost => wallet != null && wallet.TrySpend(cost), out int paid))
                Notice.Show($"Repaired equipment for {paid} gold.");
        }
    }
}
