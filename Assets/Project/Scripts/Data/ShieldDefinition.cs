using UnityEngine;

namespace EndlessDescent.Data
{
    // A shield used to be an armour rating in the off hand, so a plank and a steel tower shield
    // blocked identically. What it soaks, what holding it costs and how long the parry stays open
    // are the shield's own numbers now
    [CreateAssetMenu(menuName = "Endless Descent/Items/Shield", fileName = "Shield")]
    public class ShieldDefinition : ArmourDefinition
    {
        [Header("Blocking")]
        [SerializeField, Range(0.1f, 0.95f)] float blockFraction = 0.55f;
        [SerializeField, Min(0f)] float blockStaminaCost = 12f;
        [SerializeField, Min(0.02f)] float parryWindow = 0.25f;

        public float BaseBlockFraction => blockFraction;
        public float BlockStaminaCost => blockStaminaCost;
        public float ParryWindow => parryWindow;

        public override EquipSlot Slot => EquipSlot.OffHand;

        // Skill widens what the shield already soaks rather than replacing it, so a good arm behind
        // a bad shield is still a bad shield
        public float BlockFractionFor(ICharacterStats stats) =>
            Mathf.Clamp01(blockFraction + (stats == null ? 0 : stats.Skill(SkillId.Block)) / 300f);
    }
}
