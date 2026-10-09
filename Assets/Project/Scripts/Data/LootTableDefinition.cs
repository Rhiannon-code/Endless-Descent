using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Items/Loot Table", fileName = "LootTable")]
    public class LootTableDefinition : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemDefinition Item;
            [Min(1)] public int Weight = 1;
            [Min(1)] public int MinCount = 1;
            [Min(1)] public int MaxCount = 1;

            // How far down before this can turn up at all, as a fraction of the dungeon's depth
            // Going deeper paying better is the whole reason to go deeper
            [Range(0f, 1f)] public float MinDepth;
        }

        [SerializeField] Entry[] entries;
        [SerializeField, Min(0)] int minRolls = 1;
        [SerializeField, Min(0)] int maxRolls = 2;
        [SerializeField, Min(0)] int minGold;
        [SerializeField, Min(0)] int maxGold = 25;

        [Header("Enchanted")]
        [SerializeField, Range(0f, 1f)] float enchantChance;
        [SerializeField, Min(0)] int minEnchantPower = 4;
        [SerializeField, Min(0)] int maxEnchantPower = 10;

        public IReadOnlyList<Entry> Entries => entries;
        public int MinRolls => minRolls;
        public int MaxRolls => Mathf.Max(minRolls, maxRolls);
        public int MinGold => minGold;
        public int MaxGold => Mathf.Max(minGold, maxGold);
        public float EnchantChance => enchantChance;
        public int MinEnchantPower => minEnchantPower;
        public int MaxEnchantPower => Mathf.Max(minEnchantPower, maxEnchantPower);

        public int TotalWeight
        {
            get
            {
                int total = 0;
                if (entries != null)
                {
                    foreach (Entry entry in entries)
                    {
                        if (entry != null && entry.Item != null)
                            total += Mathf.Max(1, entry.Weight);
                    }

                }

                return total;
            }
        }
    }
}
