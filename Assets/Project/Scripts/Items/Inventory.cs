using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    [Serializable]
    public struct InventoryState
    {
        public ItemInstanceState[] Stacks;
    }

    [DisallowMultipleComponent]
    public class Inventory : MonoBehaviour, ISaveable
    {
        [SerializeField] GameDatabase database;
        [SerializeField, Min(1)] int slots = 40;
        [SerializeField, Min(0f)] float carryLimit = 120f;
        [SerializeField] string saveKey = "inventory.player";

        readonly List<ItemInstance> stacks = new List<ItemInstance>();

        public event Action Changed;

        public IReadOnlyList<ItemInstance> Stacks => stacks;
        public int Slots => slots;
        public string SaveKey => saveKey;

        public float CarriedWeight
        {
            get
            {
                float total = 0f;
                foreach (ItemInstance stack in stacks)
                    total += stack.Weight;

                return total;
            }
        }

        public bool IsOverloaded => CarriedWeight > carryLimit;

        public ItemInstance Find(ItemDefinition item)
        {
            foreach (ItemInstance instance in stacks)
                if (instance.Item == item) return instance;

            return null;
        }

        public bool Remove(ItemInstance instance)
        {
            if (instance == null || !stacks.Contains(instance))
                return false;

            if (--instance.Count <= 0)
                stacks.Remove(instance);

            Changed?.Invoke();
            return true;
        }

        public bool AddInstance(ItemInstance instance)
        {
            if (instance == null || instance.Item == null || stacks.Count >= slots)
                return false;

            stacks.Add(instance);
            Changed?.Invoke();
            return true;
        }
        public float CarryLimit => carryLimit;

        public void SetCapacity(float limit)
        {
            carryLimit = Mathf.Max(1f, limit);
            Changed?.Invoke();
        }

        public int Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0)
                return count;

            int remaining = count;

            for (int i = 0; i < stacks.Count && remaining > 0; i++)
            {
                if (stacks[i].Item != item || stacks[i].Count >= item.MaxStack ||
                    ItemInstance.TracksCondition(item))
                    continue;

                int space = item.MaxStack - stacks[i].Count;
                int moved = Mathf.Min(space, remaining);
                stacks[i].Count += moved;
                remaining -= moved;
            }

            while (remaining > 0 && stacks.Count < slots)
            {
                int moved = Mathf.Min(item.MaxStack, remaining);
                stacks.Add(new ItemInstance(item, moved));
                remaining -= moved;
            }

            if (remaining != count)
                Changed?.Invoke();

            return remaining;
        }

        public bool Remove(ItemDefinition item, int count)
        {
            if (item == null || count <= 0 || CountOf(item) < count)
                return false;

            int remaining = count;

            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (stacks[i].Item != item)
                    continue;

                int taken = Mathf.Min(stacks[i].Count, remaining);
                remaining -= taken;

                if (stacks[i].Count == taken)
                    stacks.RemoveAt(i);
                else
                    stacks[i].Count -= taken;
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(ItemDefinition item)
        {
            if (item == null)
                return 0;

            int total = 0;
            foreach (ItemInstance stack in stacks)
            {
                if (stack.Item == item)
                    total += stack.Count;
            }

            return total;
        }

        public bool Has(ItemDefinition item, int count) => CountOf(item) >= count;

        public void Clear()
        {
            stacks.Clear();
            Changed?.Invoke();
        }

        public string CaptureJson()
        {
            ItemInstanceState[] state = new ItemInstanceState[stacks.Count];
            for (int i = 0; i < stacks.Count; i++)
                state[i] = new ItemInstanceState
                {
                    ItemId = stacks[i].Item.Id,
                    Count = stacks[i].Count,
                    Durability = stacks[i].Durability,
                    Effect = (int)stacks[i].Effect,
                    EffectPower = stacks[i].EffectPower
                };

            return JsonUtility.ToJson(new InventoryState { Stacks = state });
        }

        public void RestoreJson(string json)
        {
            stacks.Clear();

            InventoryState state = JsonUtility.FromJson<InventoryState>(json);
            GameDatabase items = database != null ? database : GameDatabase.Loaded;

            if (state.Stacks != null && items != null)
            {
                foreach (ItemInstanceState entry in state.Stacks)
                {
                    ItemDefinition item = items.Item(entry.ItemId);
                    if (item != null)
                        stacks.Add(new ItemInstance(item, entry.Count)
                        {
                            Durability = entry.Durability,
                            Effect = (EndlessDescent.Data.GearEffectId)entry.Effect,
                            EffectPower = entry.EffectPower
                        });
                }
            }

            Changed?.Invoke();
        }
    }
}
