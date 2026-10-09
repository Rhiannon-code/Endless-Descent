using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    // Watches what the player actually does and credits the matching skill. Nothing else calls
    // CharacterSheet.Use, so there is one place to look when a skill is not advancing
    [DisallowMultipleComponent]
    public class SkillProgression : MonoBehaviour
    {
        [SerializeField] CharacterSheet sheet;
        [SerializeField] MeleeAttacker attacker;
        [SerializeField] PlayerDefence defence;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Spellcasting spellcasting;
        [SerializeField] Equipment equipment;
        [SerializeField] PlayerLocomotion locomotion;

        AttackPhase lastPhase = AttackPhase.Idle;
        bool wasBlocking;
        bool wasDodging;
        float sprinted;

        // Running trains by running, a point's worth of use every few seconds of it
        const float SprintSecondsPerUse = 4f;

        void OnEnable()
        {
            if (locomotion == null)
                locomotion = GetComponent<PlayerLocomotion>();

            if (spellcasting != null)
                spellcasting.SpellCast += OnSpellCast;

            if (attacker != null)
                attacker.SneakHit += OnSneakHit;
        }

        void OnDisable()
        {
            if (spellcasting != null)
                spellcasting.SpellCast -= OnSpellCast;

            if (attacker != null)
                attacker.SneakHit -= OnSneakHit;
        }

        void OnSneakHit() => sheet?.Use(SkillId.CriticalStrike, 3f);

        void OnSpellCast(SpellDefinition spell)
        {
            if (sheet == null || spell == null)
                return;

            // The spell says which school it belongs to. Inferring it from the effect was a guess
            // that broke as soon as two circles could produce the same effect by different means
            sheet.Use(spell.School);
        }

        void Update()
        {
            if (sheet == null)
                return;

            if (attacker != null)
            {
                if (lastPhase != AttackPhase.Active && attacker.Phase == AttackPhase.Active)
                    sheet.Use(WeaponSkill());

                lastPhase = attacker.Phase;
            }

            if (defence != null)
            {
                bool blocking = defence.IsBlocking;
                if (blocking && !wasBlocking)
                    sheet.Use(SkillId.Block);

                wasBlocking = blocking;
            }

            if (combat != null)
            {
                if (combat.IsDodging && !wasDodging)
                    sheet.Use(SkillId.Dodging);

                wasDodging = combat.IsDodging;
            }

            if (locomotion != null && locomotion.IsSprinting)
            {
                sprinted += Time.deltaTime;

                if (sprinted >= SprintSecondsPerUse)
                {
                    sprinted = 0f;
                    sheet.Use(SkillId.Running);
                }
            }
        }

        // The weapon says which skill swings it. Guessing from its name broke the moment a weapon
        // was called something the list did not expect
        SkillId WeaponSkill()
        {
            WeaponDefinition weapon = equipment != null ? equipment.Weapon : null;
            return weapon != null ? weapon.Skill : SkillId.HandToHand;
        }
    }
}
