using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    public class PlayerDefence : DamageMitigation
    {
        [SerializeField] PlayerCombat combat;
        [SerializeField] Equipment equipment;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] Spellcasting spellcasting;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] float parryPoiseReturn = 40f;

        [Header("Used only when the off hand holds something that is not a shield")]
        [SerializeField] float parryWindow = 0.25f;
        [SerializeField, Range(0f, 1f)] float blockReduction = 0.6f;
        [SerializeField, Min(0f)] float blockStaminaPerHit = 12f;

        float raisedAt = float.NegativeInfinity;
        bool wasBlocking;

        public bool IsBlocking =>
            input != null && input.BlockHeld &&
            equipment != null && equipment.Get(EquipSlot.OffHand) != null &&
            (stamina == null || stamina.Current > 0f);

        ShieldDefinition Shield => equipment != null ? equipment.Shield : null;

        // Timing lives inside the block rather than on a button of its own, catching a swing on a
        // shield you have only just raised is the parry, so holding it up is always the right move
        // How long that window stays open is the shield's, not one number for every shield
        public bool IsParrying => IsBlocking && Time.time < raisedAt + (Shield != null ? Shield.ParryWindow : parryWindow);

        void Awake()
        {
            if (sheet == null)
                sheet = GetComponent<CharacterSheet>();
        }

        void Update()
        {
            bool blocking = IsBlocking;

            if (blocking && !wasBlocking)
                raisedAt = Time.time;

            wasBlocking = blocking;
        }

        public override DamageInfo Modify(DamageInfo info)
        {
            // Held still with the controller off while a transition prepares somewhere, taking a hit
            // there is what killed players mid fade, in a frame they could do nothing about
            if (EndlessDescent.Core.Transition.Busy)
            {
                info.Amount = 0;
                info.PoiseDamage = 0f;
                return info;
            }

            if (combat != null && combat.IsInvulnerable)
            {
                info.Amount = 0;
                info.PoiseDamage = 0f;
                return info;
            }

            if (IsParrying && info.Parryable)
            {
                info.Amount = 0;
                info.PoiseDamage = 0f;

                // A read parry turns the exchange around, the attacker eats the stagger instead
                if (info.Source != null)
                    info.Source.GetComponentInParent<Poise>()?.Apply(parryPoiseReturn);

                return info;
            }

            int armour = equipment != null ? equipment.ArmourRating : 0;

            if (spellcasting != null)
                armour += spellcasting.WardArmour;

            // A raised shield soaks most of the hit, but only while there is stamina to hold it
            // How much it soaks, and what holding it costs, is what the Block skill is for
            ShieldDefinition shield = Shield;

            if (IsBlocking)
            {
                float soaked = shield != null
                    ? shield.BlockFractionFor(sheet)
                    : CharacterMaths.BlockFraction(sheet, blockReduction);

                float cost = shield != null ? shield.BlockStaminaCost : blockStaminaPerHit;

                stamina?.TrySpend(CharacterMaths.StaminaCost(sheet, SkillId.Block, cost));
                equipment?.Wear(EquipSlot.OffHand, 2);
                info.Amount = Mathf.RoundToInt(info.Amount * (1f - soaked));
                info.PoiseDamage *= 1f - soaked;
            }

            // Armour wears where it is struck, the chest stands in for the whole set for now
            equipment?.Wear(EquipSlot.Chest, 1);

            int stopped = Mathf.RoundToInt(CharacterMaths.Armour(sheet, armour) * (1f - Mathf.Clamp01(info.Penetration)));
            info.Amount = Mathf.Max(1, info.Amount - stopped) + Mathf.Max(0, info.Unstoppable);

            // Resilience is the last thing applied, so it is a share of what actually got through
            if (equipment != null)
                info.Amount = Mathf.Max(1, Mathf.RoundToInt(info.Amount * equipment.Bonuses.ResistScale));

            return info;
        }
    }
}
