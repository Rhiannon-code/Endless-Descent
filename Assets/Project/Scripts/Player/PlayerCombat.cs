using System.Collections;
using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] Equipment equipment;
        [SerializeField] MeleeAttacker attacker;
        [SerializeField] PlayerStance stance;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] Health health;

        [Header("Unarmed fallback")]
        [SerializeField] int unarmedDamage = 5;
        [SerializeField] float unarmedReach = 1.4f;
        [SerializeField] float unarmedStaminaCost = 8f;

        [Header("Dodge")]
        [SerializeField] float dodgeDuration = 0.32f;
        [SerializeField] float dodgeSpeed = 9f;
        [SerializeField] float dodgeStaminaCost = 22f;
        [SerializeField] float dodgeInvulnerability = 0.22f;

        CharacterController controller;
        float invulnerableUntil;

        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public bool IsDodging { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (sheet == null)
                sheet = GetComponent<CharacterSheet>();

            if (health == null)
                health = GetComponent<Health>();

            if (attacker != null)
                attacker.Landed += Leech;
        }

        void OnDestroy()
        {
            if (attacker != null)
                attacker.Landed -= Leech;
        }

        // A leeching weapon pays you back out of what it just took
        void Leech(int dealt)
        {
            int percent = equipment != null ? equipment.Bonuses.LeechPercent : 0;

            if (percent > 0)
                health?.Heal(Mathf.Max(1, dealt * percent / 100));
        }

        void Update()
        {
            if (input == null || stamina == null)
                return;

            // A missing stance leaves the swing working rather than leaving the player unarmed
            if (input.AttackPressed && (stance == null || stance.IsMelee))
                TryAttack();

            if (input.DodgePressed && !IsDodging)
                TryDodge();
        }

        void TryAttack()
        {
            if (attacker == null || attacker.IsBusy || IsDodging)
                return;

            WeaponDefinition weapon = equipment != null ? equipment.Weapon : null;
            SkillId skill = weapon != null ? weapon.Skill : SkillId.HandToHand;

            // Swinging something you barely know how to hold costs more of the same bar
            float cost = CharacterMaths.StaminaCost(sheet, skill,
                weapon != null ? weapon.StaminaCost : unarmedStaminaCost);

            if (!stamina.TrySpend(cost))
                return;

            attacker.Wield(sheet, skill);

            if (weapon != null)
            {
                // A worn blade hits for less, and swinging it wears it further
                ItemInstance held = equipment.WornInstance(EquipSlot.MainHand);
                float condition = held != null ? held.Effectiveness : 1f;

                GearBonuses bonuses = equipment.Bonuses;

                attacker.Configure(Mathf.RoundToInt(weapon.Damage * condition) + bonuses.Damage, weapon.Reach,
                    weapon.PoiseDamage, weapon.Windup, weapon.Active, weapon.Recovery);

                attacker.Carry(bonuses.Burning);
                equipment.Wear(EquipSlot.MainHand, 1);
            }
            else
            {
                attacker.Carry(0);
                attacker.Configure(unarmedDamage, unarmedReach, 6f, 0.18f, 0.1f, 0.3f);
            }

            attacker.TryStartAttack();
        }

        void TryDodge()
        {
            if (!stamina.TrySpend(CharacterMaths.StaminaCost(sheet, SkillId.Dodging, dodgeStaminaCost)))
                return;

            Vector2 move = input.Move;
            Vector3 direction = move.sqrMagnitude > 0.01f
                ? (transform.right * move.x + transform.forward * move.y).normalized
                : -transform.forward;

            StartCoroutine(Dodge(direction));
        }

        IEnumerator Dodge(Vector3 direction)
        {
            IsDodging = true;
            invulnerableUntil = Time.time + dodgeInvulnerability;

            attacker?.Cancel();

            float endsAt = Time.time + dodgeDuration;
            while (Time.time < endsAt)
            {
                controller.Move(direction * (dodgeSpeed * Time.deltaTime));
                yield return null;
            }

            IsDodging = false;
        }
    }
}
