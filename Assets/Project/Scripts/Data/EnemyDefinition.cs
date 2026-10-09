using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Combat/Enemy", fileName = "Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] string id = "enemy.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField] GameObject prefab;

        [Header("Sheet")]
        [SerializeField] CreatureStats stats = new CreatureStats();
        [SerializeField] SkillId weaponSkill = SkillId.HandToHand;

        [Header("Vitals")]
        [SerializeField, Min(1)] int maxHealth = 60;
        [SerializeField, Min(0)] int armour;
        [SerializeField, Min(1f)] float poise = 30f;

        [Header("Attack")]
        [SerializeField, Min(0)] int damage = 10;
        [SerializeField, Min(0f)] float reach = 2f;
        [SerializeField, Min(0.05f)] float windup = 0.55f;
        [SerializeField, Min(0.05f)] float active = 0.15f;
        [SerializeField, Min(0.05f)] float recovery = 0.7f;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] float moveSpeed = 2.4f;
        [SerializeField, Min(0f)] float aggroRange = 12f;
        [SerializeField, Min(0f)] float attackCooldown = 1.1f;

        [Header("Rewards")]
        [SerializeField] LootTableDefinition loot;
        [SerializeField, Min(0)] int experience = 10;
        [SerializeField] AfflictionId inflicts = AfflictionId.None;
        [SerializeField, Range(0f, 1f)] float inflictChance = 0.2f;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public CreatureStats Stats => stats;
        public SkillId WeaponSkill => weaponSkill;
        // What the sheet says, falling back to the hand set number for anything not yet given one
        public int MaxHealth => stats != null && stats.Level > 0 ? stats.MaxHealth : maxHealth;
        public int Armour => armour;
        public float Poise => poise;
        public int Damage => damage;
        public float Reach => reach;
        public float Windup => windup;
        public float Active => active;
        public float Recovery => recovery;
        public float MoveSpeed => moveSpeed;
        public float AggroRange => aggroRange;
        public float AttackCooldown => attackCooldown;
        public LootTableDefinition Loot => loot;
        public int Experience => experience;
        public AfflictionId Inflicts => inflicts;
        public float InflictChance => inflictChance;
    }
}
