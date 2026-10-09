using System;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    [Serializable]
    public struct ItemInstanceState
    {
        public string ItemId;
        public int Count;
        public int Durability;
        public int Effect;
        public int EffectPower;
    }

    // One carried thing. Stackable goods keep a Count and no condition, equipment is always a single
    // instance that wears, so two swords in the pack are no longer interchangeable
    [Serializable]
    public class ItemInstance
    {
        public ItemDefinition Item;
        public int Count = 1;
        public int Durability = -1;

        // The reason instances exist rather than stacks of definitions
        public GearEffectId Effect;
        public int EffectPower;

        public const int NotApplicable = -1;

        public ItemInstance(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
            Durability = TracksCondition(item) ? MaxDurabilityOf(item) : NotApplicable;
        }

        public bool IsEmpty => Item == null || Count <= 0;
        public float Weight => Item == null ? 0f : Item.Weight * Count;
        public int Value => UnitValue * Count;

        // What one of them is worth as it is now, so a battered sword is not priced as a new one
        public int UnitValue => Item == null ? 0 : Mathf.RoundToInt(Item.BaseValue * ConditionScale);
        public bool HasCondition => Durability >= 0;
        public int MaxDurability => MaxDurabilityOf(Item);
        public bool IsBroken => HasCondition && Durability <= 0;
        public bool IsEnchanted => Effect != GearEffectId.None && EffectPower > 0;

        public string DisplayName => Item == null ? string.Empty
            : IsEnchanted ? $"{Item.DisplayName} of {GearEffects.Describe(Effect)}" : Item.DisplayName;

        // Broken gear still works, at half effect, so a failed weapon is a setback rather than a
        // silent nothing happens
        public float Effectiveness => !HasCondition ? 1f : IsBroken ? 0.5f : 1f;

        public float ConditionScale => !HasCondition || MaxDurability <= 0
            ? 1f
            : Mathf.Clamp01(Durability / (float)MaxDurability);

        public string ConditionLabel
        {
            get
            {
                if (!HasCondition) return string.Empty;
                if (IsBroken) return "Broken";

                float fraction = ConditionScale;
                if (fraction > 0.85f) return "New";
                if (fraction > 0.6f) return "Used";
                if (fraction > 0.35f) return "Worn";
                return "Battered";
            }
        }

        public void Wear(int amount)
        {
            if (HasCondition)
                Durability = Mathf.Max(0, Durability - amount);
        }

        public void Repair() => Durability = HasCondition ? MaxDurability : NotApplicable;

        public void Enchant(GearEffectId effect, int power)
        {
            Effect = effect;
            EffectPower = Mathf.Max(0, power);
        }

        public int PowerOf(GearEffectId effect) => Effect == effect ? EffectPower : 0;

        public static bool TracksCondition(ItemDefinition item) =>
            item != null && (item.Category == ItemCategory.Weapon || item.Category == ItemCategory.Armour);

        public static int MaxDurabilityOf(ItemDefinition item) =>
            item == null ? 0 : Mathf.Max(20, item.BaseValue);
    }
}
