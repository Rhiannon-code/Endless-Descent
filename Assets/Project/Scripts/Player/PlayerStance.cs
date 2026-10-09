using System;
using UnityEngine;

namespace EndlessDescent.Player
{
    public enum CombatStance { Melee, Spell }

    // Which hand the attack button obeys. Melee swings what is in the main hand, Spell casts the
    // selected spell. One button either way, so the player never has to learn two attack keys
    [DisallowMultipleComponent]
    public class PlayerStance : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] Spellcasting spellcasting;

        public event Action<CombatStance> Changed;

        public CombatStance Stance { get; private set; } = CombatStance.Melee;
        public bool IsMelee => Stance == CombatStance.Melee;
        public bool IsCasting => Stance == CombatStance.Spell;

        bool CanCast => spellcasting != null && spellcasting.Known.Count > 0;

        void Update()
        {
            if (input == null)
                return;

            if (input.StancePressed)
                Set(IsMelee ? CombatStance.Spell : CombatStance.Melee);

            // Reaching for another spell is itself a statement of intent, so it draws the focus
            if (input.NextSpellPressed)
                Set(CombatStance.Spell);
        }

        public void Set(CombatStance stance)
        {
            if (stance == CombatStance.Spell && !CanCast)
                stance = CombatStance.Melee;

            if (stance == Stance)
                return;

            Stance = stance;
            Changed?.Invoke(Stance);
        }
    }
}
