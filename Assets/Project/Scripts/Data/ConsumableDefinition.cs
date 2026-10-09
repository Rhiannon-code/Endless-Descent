using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Items/Consumable", fileName = "Consumable")]
    public class ConsumableDefinition : ItemDefinition
    {
        [SerializeField] int restoreHealth;
        [SerializeField] float restoreStamina;
        [SerializeField] float reduceHunger = 25f;
        [SerializeField] float reduceFatigue;

        public int RestoreHealth => restoreHealth;
        public float RestoreStamina => restoreStamina;
        public float ReduceHunger => reduceHunger;
        public float ReduceFatigue => reduceFatigue;
    }
}
