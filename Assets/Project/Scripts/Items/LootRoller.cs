using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    public struct LootResult
    {
        public List<ItemInstance> Items;
        public int Gold;
    }

    public static class LootRoller
    {
        // Depth is how far down this was found, 0 at the entrance and 1 at the deepest level. An
        // entry only turns up at or below its own depth, so the good gear is not lying by the door
        public static LootResult Roll(LootTableDefinition table, ref DeterministicRandom rng, float depth = 1f)
        {
            LootResult result = new LootResult { Items = new List<ItemInstance>(), Gold = 0 };

            if (table == null || table.Entries == null)
                return result;

            result.Gold = rng.NextInt(table.MinGold, table.MaxGold + 1);

            int totalWeight = 0;
            foreach (LootTableDefinition.Entry entry in table.Entries)
                if (Eligible(entry, depth)) totalWeight += Mathf.Max(1, entry.Weight);

            if (totalWeight <= 0)
                return result;

            int rolls = rng.NextInt(table.MinRolls, table.MaxRolls + 1);
            for (int i = 0; i < rolls; i++)
            {
                int pick = rng.NextInt(totalWeight);

                foreach (LootTableDefinition.Entry entry in table.Entries)
                {
                    if (!Eligible(entry, depth))
                        continue;

                    pick -= entry.Weight < 1 ? 1 : entry.Weight;
                    if (pick >= 0)
                        continue;

                    result.Items.Add(Found(table, entry, ref rng));
                    break;
                }
            }

            return result;
        }

        static bool Eligible(LootTableDefinition.Entry entry, float depth) =>
            entry != null && entry.Item != null && depth >= entry.MinDepth;

        // Nothing lying in a dungeon is new. Gear comes up worn, which is what gives the repair bench
        // and its skill something to do, and occasionally comes up carrying something
        static ItemInstance Found(LootTableDefinition table, LootTableDefinition.Entry entry, ref DeterministicRandom rng)
        {
            ItemInstance instance = new ItemInstance(entry.Item, rng.NextInt(entry.MinCount, entry.MaxCount + 1));

            if (!instance.HasCondition)
                return instance;

            instance.Wear(Mathf.RoundToInt(instance.MaxDurability * rng.Range(0.15f, 0.65f)));

            if (rng.Chance(table.EnchantChance))
                instance.Enchant(EffectFor(entry.Item.Category, ref rng), rng.NextInt(table.MinEnchantPower, table.MaxEnchantPower + 1));

            return instance;
        }

        // Everything the category can carry, picked evenly. Which effects suit which kind of gear is
        // GearEffects' to answer, so adding one there puts it into the loot without touching this
        static GearEffectId EffectFor(ItemCategory category, ref DeterministicRandom rng)
        {
            List<GearEffectId> suitable = new List<GearEffectId>();

            foreach (GearEffectId effect in System.Enum.GetValues(typeof(GearEffectId)))
                if (GearEffects.Suits(effect, category)) suitable.Add(effect);

            return suitable.Count == 0 ? GearEffectId.None : suitable[rng.NextInt(suitable.Count)];
        }
    }
}
