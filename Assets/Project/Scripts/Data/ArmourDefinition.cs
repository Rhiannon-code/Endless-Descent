using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Items/Armour", fileName = "Armour")]
    public class ArmourDefinition : ItemDefinition
    {
        [SerializeField] EquipSlot slot = EquipSlot.Chest;
        [SerializeField, Min(0)] int armour = 4;

        // What it costs to wear, rather than a per piece penalty nobody read: the whole set is
        // weighed together, so three heavy pieces cost three times what one does (ArmourWeights)
        [SerializeField] ArmourWeight weightClass = ArmourWeight.Light;

        public int Armour => armour;
        public ArmourWeight WeightClass => weightClass;
        public int Load => ArmourWeights.Load(weightClass);

        public override EquipSlot Slot => slot;
    }
}
