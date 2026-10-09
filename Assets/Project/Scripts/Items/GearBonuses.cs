using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    // What the worn set adds up to. Gathered once when the equipment changes rather than walked
    // every time something wants to know, because combat, movement, stealth and the sheet all ask
    public struct GearBonuses
    {
        public int Damage;
        public int Burning;
        public int LeechPercent;
        public int Armour;
        public int StaminaPercent;
        public int MovePercent;
        public int ResistPercent;
        public int[] Attributes;

        public static GearBonuses None => new GearBonuses { Attributes = new int[8] };

        public int Attribute(CharacterAttribute attribute) =>
            Attributes == null ? 0 : Attributes[(int)attribute];

        public float MoveScale => 1f + MovePercent / 100f;
        public float StaminaScale => 1f + StaminaPercent / 100f;
        public float ResistScale => Mathf.Clamp01(1f - ResistPercent / 100f);

        // A blade's Sharpness in the pack is worth nothing, so only what is actually worn counts,
        // and the three weapon effects only count from the hand holding the weapon
        public static GearBonuses From(IEnumerable<KeyValuePair<EquipSlot, ItemInstance>> worn)
        {
            GearBonuses bonuses = None;

            if (worn == null)
                return bonuses;

            foreach (KeyValuePair<EquipSlot, ItemInstance> pair in worn)
            {
                ItemInstance instance = pair.Value;

                if (instance == null || !instance.IsEnchanted)
                    continue;

                // Broken gear carries its effect at half strength, the same as its own numbers
                int power = Mathf.RoundToInt(instance.EffectPower * instance.Effectiveness);
                if (power <= 0)
                    continue;

                bool inHand = pair.Key == EquipSlot.MainHand;

                switch (instance.Effect)
                {
                    case GearEffectId.Sharpness: if (inHand) bonuses.Damage += power; break;
                    case GearEffectId.Ember: if (inHand) bonuses.Burning += power; break;
                    case GearEffectId.Leeching: if (inHand) bonuses.LeechPercent += power; break;
                    case GearEffectId.Warding: bonuses.Armour += power; break;
                    case GearEffectId.Vigour: bonuses.StaminaPercent += power; break;
                    case GearEffectId.Swiftness: bonuses.MovePercent += power; break;
                    case GearEffectId.Resilience: bonuses.ResistPercent += power; break;
                    default:
                        if (GearEffects.RaisesAttribute(instance.Effect, out CharacterAttribute attribute))
                            bonuses.Attributes[(int)attribute] += power;
                        break;
                }
            }

            // Stacking four pieces of Resilience should not make you immune to being hit
            bonuses.ResistPercent = Mathf.Min(bonuses.ResistPercent, 60);
            return bonuses;
        }
    }
}
