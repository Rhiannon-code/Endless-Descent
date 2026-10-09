using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Items/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ItemDefinition
    {
        [Header("Damage")]
        [SerializeField, Min(0)] int damage = 12;
        [SerializeField, Min(0f)] float reach = 2.2f;
        [SerializeField, Min(0f)] float poiseDamage = 10f;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.01f)] float windup = 0.28f;
        [SerializeField, Min(0.01f)] float active = 0.12f;
        [SerializeField, Min(0.01f)] float recovery = 0.36f;

        [Header("Skill")]
        [SerializeField] SkillId skill = SkillId.Longsword;

        [Header("Cost")]
        [SerializeField, Min(0f)] float staminaCost = 16f;

        public int Damage => damage;
        public float Reach => reach;
        public float PoiseDamage => poiseDamage;
        public float Windup => windup;
        public float Active => active;
        public float Recovery => recovery;
        public float StaminaCost => staminaCost;
        public SkillId Skill => skill;

        public override EquipSlot Slot => EquipSlot.MainHand;
    }
}
