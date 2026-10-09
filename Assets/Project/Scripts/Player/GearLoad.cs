using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    // What the worn set does to the person wearing it. Plate used to be a free upgrade, the armour
    // rating went up and nothing else changed. The cost is paid here, in the stamina that swings and
    // runs, in footing, and in how far away you can be heard (ArmourWeights), and whatever the gear
    // gives back is netted off in the same place, so nothing else has to know about either
    [DisallowMultipleComponent]
    public class GearLoad : MonoBehaviour
    {
        [SerializeField] Equipment equipment;

        void Awake()
        {
            if (equipment == null)
                equipment = GetComponent<Equipment>();
        }

        public int Load => equipment != null ? equipment.ArmourLoad : 0;

        public float StaminaRegenScale =>
            ArmourWeights.StaminaRegenScale(Load) * (equipment != null ? equipment.Bonuses.StaminaScale : 1f);

        public float MoveScale =>
            ArmourWeights.MoveScale(Load) * (equipment != null ? equipment.Bonuses.MoveScale : 1f);

        public float NoiseScale => ArmourWeights.NoiseScale(Load);

        public string Description => ArmourWeights.Describe(Load);
    }
}
