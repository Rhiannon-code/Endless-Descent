using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Combat
{
    public enum AttackPhase { Idle, Windup, Active, Recovery }

    [DisallowMultipleComponent]
    public class MeleeAttacker : MonoBehaviour
    {
        [SerializeField] Transform origin;
        [SerializeField] LayerMask targetMask = ~0;
        [SerializeField, Min(0.05f)] float sweepRadius = 0.55f;
        [SerializeField, Min(1f)] float unawareMultiplier = 3f;

        [Header("Defaults, overwritten by Configure")]
        [SerializeField] int damage = 12;
        [SerializeField] float reach = 2.2f;
        [SerializeField] float poiseDamage = 10f;
        [SerializeField] float windup = 0.28f;
        [SerializeField] float active = 0.12f;
        [SerializeField] float recovery = 0.36f;

        ICharacterStats wielder;
        SkillId skill = SkillId.HandToHand;
        WeaponClass weaponClass = WeaponClass.Unarmed;
        int unstoppable;

        readonly HashSet<Health> hitThisSwing = new HashSet<Health>();
        readonly Collider[] overlap = new Collider[16];

        float phaseEndsAt;

        public event System.Action SneakHit;
        public event System.Action<int> Landed;

        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;
        public bool IsBusy => Phase != AttackPhase.Idle;
        public bool IsWindingUp => Phase == AttackPhase.Windup;

        void Awake()
        {
            if (origin == null)
                origin = transform;
        }

        public void Configure(int newDamage, float newReach, float newPoiseDamage, float newWindup, float newActive, float newRecovery)
        {
            damage = newDamage;
            reach = newReach;
            poiseDamage = newPoiseDamage;
            windup = newWindup;
            active = newActive;
            recovery = newRecovery;
        }

        // Who is swinging it, and with which skill, the same weapon hits differently in different hands
        public void Wield(ICharacterStats stats, SkillId weaponSkill)
        {
            wielder = stats;
            skill = weaponSkill;
            weaponClass = WeaponClasses.Of(weaponSkill);
        }

        // What the blade is carrying, separately from what it is, an ordinary sword and a burning
        // one are configured identically and differ only here
        public void Carry(int unstoppableDamage) => unstoppable = Mathf.Max(0, unstoppableDamage);

        public bool TryStartAttack()
        {
            if (IsBusy)
                return false;

            hitThisSwing.Clear();
            Phase = AttackPhase.Windup;
            phaseEndsAt = Time.time + windup;
            return true;
        }

        public void Cancel()
        {
            Phase = AttackPhase.Idle;
            hitThisSwing.Clear();
        }

        void Update()
        {
            if (Phase == AttackPhase.Idle || Time.time < phaseEndsAt)
            {
                if (Phase == AttackPhase.Active)
                    Sweep();

                return;
            }

            switch (Phase)
            {
                case AttackPhase.Windup:
                    Phase = AttackPhase.Active;
                    phaseEndsAt = Time.time + active;
                    break;
                case AttackPhase.Active:
                    Phase = AttackPhase.Recovery;
                    phaseEndsAt = Time.time + recovery;
                    break;
                case AttackPhase.Recovery:
                    Phase = AttackPhase.Idle;
                    break;
            }
        }

        void Sweep()
        {
            // How wide the swing is is the class's business, a dagger reaches one throat and a
            // greataxe catches what is standing beside it
            float arc = (sweepRadius + reach * 0.5f) * WeaponClasses.ArcScale(weaponClass);
            Vector3 centre = origin.position + origin.forward * (reach * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(centre, arc, overlap, targetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Health target = overlap[i].GetComponentInParent<Health>();

                if (target == null || target.gameObject == gameObject || !target.IsAlive || !hitThisSwing.Add(target))
                    continue;

                Vector3 direction = (target.transform.position - origin.position).normalized;
                // Striking something that has not noticed you is the payoff for sneaking. Enemies
                // hitting the player are unaffected, the player carries no senses component
                EnemySenses senses = target.GetComponentInParent<EnemySenses>();
                bool unaware = senses != null && !senses.IsAware;

                int dealt = CharacterMaths.MeleeDamage(wielder, skill, damage);
                float stagger = poiseDamage * WeaponClasses.PoiseScale(weaponClass);

                // Hybrid combat, the swing always lands, and skill decides whether it lands
                // hard enough to stagger
                if (Random.value < CharacterMaths.CriticalChance(wielder, skill))
                {
                    dealt = Mathf.RoundToInt(dealt * CharacterMaths.CriticalDamage);
                    stagger *= CharacterMaths.CriticalPoise;
                }

                if (unaware)
                    dealt = Mathf.RoundToInt(dealt * unawareMultiplier * WeaponClasses.SneakScale(weaponClass));

                DamageInfo info = DamageInfo.Melee(dealt, stagger, gameObject, direction);
                info.Penetration = WeaponClasses.Penetration(weaponClass);
                info.Unstoppable = unstoppable;

                target.TakeDamage(info);
                Landed?.Invoke(dealt + unstoppable);

                if (unaware)
                    SneakHit?.Invoke();
            }
        }

        void OnDrawGizmosSelected()
        {
            Transform t = origin != null ? origin : transform;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(t.position + t.forward * (reach * 0.5f), sweepRadius + reach * 0.5f);
        }
    }
}
