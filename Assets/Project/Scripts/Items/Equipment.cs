using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    [Serializable]
    public struct EquipmentState
    {
        public string[] SlotNames;
        public string[] ItemIds;
        public int[] Durabilities;
        public int[] Effects;
        public int[] EffectPowers;
    }

    [DisallowMultipleComponent]
    public class Equipment : MonoBehaviour, ISaveable
    {
        [SerializeField] GameDatabase database;
        [SerializeField] Inventory inventory;
        [SerializeField] string saveKey = "equipment.player";

        readonly Dictionary<EquipSlot, ItemInstance> equipped = new Dictionary<EquipSlot, ItemInstance>();

        public event Action Changed;

        public string SaveKey => saveKey;
        public WeaponDefinition Weapon => Get(EquipSlot.MainHand) as WeaponDefinition;
        public ItemInstance WornInstance(EquipSlot slot) =>
            equipped.TryGetValue(slot, out ItemInstance instance) ? instance : null;

        public int ArmourRating
        {
            get
            {
                int total = 0;
                foreach (ItemInstance instance in equipped.Values)
                {
                    // Condition scales what the piece is actually worth, broken plate is half plate
                    if (instance.Item is ArmourDefinition armour)
                        total += Mathf.RoundToInt(armour.Armour * instance.Effectiveness);
                }

                return total + Bonuses.Armour;
            }
        }

        public ShieldDefinition Shield => Get(EquipSlot.OffHand) as ShieldDefinition;

        // What the whole set costs to wear. Weight is paid for by the set, not the piece, so one
        // heavy helm is nearly free and a full harness is not (ArmourWeights)
        public int ArmourLoad
        {
            get
            {
                int load = 0;
                foreach (ItemInstance instance in equipped.Values)
                    if (instance.Item is ArmourDefinition armour) load += armour.Load;

                return load;
            }
        }

        public GearBonuses Bonuses
        {
            get
            {
                if (bonusesStale)
                {
                    bonuses = GearBonuses.From(equipped);
                    bonusesStale = false;
                }

                return bonuses;
            }
        }

        GearBonuses bonuses = GearBonuses.None;
        bool bonusesStale = true;

        public ItemDefinition Get(EquipSlot slot) =>
            equipped.TryGetValue(slot, out ItemInstance instance) ? instance.Item : null;

        public bool Equip(ItemDefinition item) => Equip(inventory != null ? inventory.Find(item) : null);

        public bool Equip(ItemInstance instance)
        {
            if (instance?.Item == null || instance.Item.Slot == EquipSlot.None)
                return false;

            // A swap puts what was worn into the pack, so there has to be room once this one is out of it
            if (inventory != null && equipped.ContainsKey(instance.Item.Slot) &&
                inventory.Stacks.Count - (instance.Count == 1 ? 1 : 0) >= inventory.Slots)
                return false;

            // Taken out of the pack as the same object, so its condition travels with it.
            ItemInstance moved = instance.Count > 1
                ? new ItemInstance(instance.Item, 1) { Durability = instance.Durability }
                : instance;

            if (inventory != null && !inventory.Remove(instance))
                return false;

            Unequip(instance.Item.Slot);
            equipped[moved.Item.Slot] = moved;
            Raise();
            return true;
        }

        public void NotifyChanged() => Raise();

        void Raise()
        {
            bonusesStale = true;
            Changed?.Invoke();
        }

        public void Wear(EquipSlot slot, int amount)
        {
            if (!equipped.TryGetValue(slot, out ItemInstance instance))
                return;

            bool wasBroken = instance.IsBroken;
            instance.Wear(amount);

            if (instance.IsBroken != wasBroken)
                Raise();
        }

        public int MissingCondition
        {
            get
            {
                int missing = 0;
                foreach (ItemInstance instance in equipped.Values)
                    if (instance.HasCondition) missing += instance.MaxDurability - instance.Durability;

                return missing;
            }
        }

        // Decided by whether anything is worn down, not by the price, a guild that repairs for free
        // charges nothing and used to be turned away for it
        public bool RepairAll(int goldPerPoint, System.Func<int, bool> pay, out int cost)
        {
            int missing = MissingCondition;
            cost = missing * goldPerPoint;

            if (missing <= 0 || pay == null || (cost > 0 && !pay(cost)))
                return false;

            foreach (ItemInstance instance in equipped.Values)
                instance.Repair();

            Raise();
            return true;
        }

        public bool Unequip(EquipSlot slot)
        {
            if (!equipped.TryGetValue(slot, out ItemInstance current) || current?.Item == null)
                return false;

            // Returned as the same instance so its condition is not quietly reset by re-equipping. With
            // the pack full it stays on: taking it off used to drop it into a pack that refused it
            if (inventory != null && !inventory.AddInstance(current))
                return false;

            equipped.Remove(slot);
            Raise();
            return true;
        }

        public string CaptureJson()
        {
            List<string> names = new List<string>();
            List<string> ids = new List<string>();
            List<int> condition = new List<int>();
            List<int> effects = new List<int>();
            List<int> powers = new List<int>();

            foreach (KeyValuePair<EquipSlot, ItemInstance> pair in equipped)
            {
                if (pair.Value?.Item == null)
                    continue;

                names.Add(pair.Key.ToString());
                ids.Add(pair.Value.Item.Id);
                condition.Add(pair.Value.Durability);
                effects.Add((int)pair.Value.Effect);
                powers.Add(pair.Value.EffectPower);
            }

            return JsonUtility.ToJson(new EquipmentState
            {
                SlotNames = names.ToArray(),
                ItemIds = ids.ToArray(),
                Durabilities = condition.ToArray(),
                Effects = effects.ToArray(),
                EffectPowers = powers.ToArray()
            });
        }

        public void RestoreJson(string json)
        {
            equipped.Clear();

            EquipmentState state = JsonUtility.FromJson<EquipmentState>(json);
            if (state.SlotNames != null && state.ItemIds != null && database != null)
            {
                int count = Mathf.Min(state.SlotNames.Length, state.ItemIds.Length);
                for (int i = 0; i < count; i++)
                {
                    if (Enum.TryParse(state.SlotNames[i], out EquipSlot slot))
                    {
                        ItemDefinition item = database.Item(state.ItemIds[i]);
                        if (item == null)
                            continue;

                        ItemInstance instance = new ItemInstance(item, 1);

                        if (state.Durabilities != null && i < state.Durabilities.Length)
                            instance.Durability = state.Durabilities[i];

                        if (state.Effects != null && state.EffectPowers != null && i < state.Effects.Length && i < state.EffectPowers.Length)
                            instance.Enchant((GearEffectId)state.Effects[i], state.EffectPowers[i]);

                        equipped[slot] = instance;
                    }
                }
            }

            Raise();
        }
    }
}
