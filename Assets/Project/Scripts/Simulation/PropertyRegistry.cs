using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct PropertyHoldingState
    {
        public string PropertyId;
        public bool Owned;
        public int RentPaidUntilDay;
        public ItemInstanceState[] Storage;
    }

    [Serializable]
    public struct PropertyRegistryState
    {
        public PropertyHoldingState[] Holdings;
    }

    public class PropertyHolding
    {
        public PropertyDefinition Definition;
        public bool Owned;
        public int RentPaidUntilDay;
        public readonly List<ItemInstance> Storage = new List<ItemInstance>();
    }

    [DisallowMultipleComponent]
    public class PropertyRegistry : MonoBehaviour, ISaveable
    {
        [SerializeField] GameDatabase database;
        [SerializeField] string saveKey = "properties";

        readonly Dictionary<string, PropertyHolding> holdings = new Dictionary<string, PropertyHolding>();

        public event Action<PropertyDefinition> HoldingChanged;
        public event Action<PropertyDefinition> RentLapsed;

        public string SaveKey => saveKey;
        public IEnumerable<PropertyHolding> Holdings => holdings.Values;

        public PropertyHolding Find(PropertyDefinition property)
        {
            return property != null && holdings.TryGetValue(property.Id, out PropertyHolding holding) ? holding : null;
        }

        public bool HasBedAt(PropertyDefinition property)
        {
            PropertyHolding holding = Find(property);
            return holding != null && property.HasBed && (holding.Owned || holding.RentPaidUntilDay > 0);
        }

        public bool Buy(PropertyDefinition property, Wallet wallet)
        {
            if (property == null || wallet == null || Find(property)?.Owned == true)
                return false;

            if (!wallet.TrySpend(property.PurchasePrice))
                return false;

            PropertyHolding holding = GetOrCreate(property);
            holding.Owned = true;
            HoldingChanged?.Invoke(property);
            return true;
        }

        public bool Rent(PropertyDefinition property, Wallet wallet, int days, int currentDay)
        {
            if (property == null || wallet == null || days <= 0)
                return false;

            if (!wallet.TrySpend(property.RentPerDay * days))
                return false;

            PropertyHolding holding = GetOrCreate(property);
            holding.RentPaidUntilDay = Mathf.Max(holding.RentPaidUntilDay, currentDay) + days;
            HoldingChanged?.Invoke(property);
            return true;
        }

        public void ApplyDay(int day)
        {
            foreach (PropertyHolding holding in holdings.Values)
            {
                if (!holding.Owned && holding.RentPaidUntilDay > 0 && day >= holding.RentPaidUntilDay)
                {
                    holding.RentPaidUntilDay = 0;
                    RentLapsed?.Invoke(holding.Definition);
                }
            }
        }

        public bool Store(PropertyDefinition property, ItemDefinition item, int count)
        {
            PropertyHolding holding = Find(property);
            if (holding == null || item == null || count <= 0 || holding.Storage.Count >= property.StorageSlots)
                return false;

            holding.Storage.Add(new ItemInstance(item, count));
            return true;
        }

        PropertyHolding GetOrCreate(PropertyDefinition property)
        {
            if (holdings.TryGetValue(property.Id, out PropertyHolding existing))
                return existing;

            PropertyHolding created = new PropertyHolding { Definition = property };
            holdings[property.Id] = created;
            return created;
        }

        public string CaptureJson()
        {
            List<PropertyHoldingState> states = new List<PropertyHoldingState>();

            foreach (PropertyHolding holding in holdings.Values)
            {
                ItemInstanceState[] storage = new ItemInstanceState[holding.Storage.Count];
                for (int i = 0; i < holding.Storage.Count; i++)
                    storage[i] = new ItemInstanceState { ItemId = holding.Storage[i].Item.Id, Count = holding.Storage[i].Count };

                states.Add(new PropertyHoldingState
                {
                    PropertyId = holding.Definition.Id,
                    Owned = holding.Owned,
                    RentPaidUntilDay = holding.RentPaidUntilDay,
                    Storage = storage
                });
            }

            return JsonUtility.ToJson(new PropertyRegistryState { Holdings = states.ToArray() });
        }

        public void RestoreJson(string json)
        {
            holdings.Clear();

            PropertyRegistryState state = JsonUtility.FromJson<PropertyRegistryState>(json);
            if (state.Holdings == null || database == null)
                return;

            foreach (PropertyHoldingState holdingState in state.Holdings)
            {
                PropertyDefinition definition = database.Property(holdingState.PropertyId);
                if (definition == null)
                    continue;

                PropertyHolding holding = GetOrCreate(definition);
                holding.Owned = holdingState.Owned;
                holding.RentPaidUntilDay = holdingState.RentPaidUntilDay;

                if (holdingState.Storage == null)
                    continue;

                foreach (ItemInstanceState stack in holdingState.Storage)
                {
                    ItemDefinition item = database.Item(stack.ItemId);
                    if (item != null)
                        holding.Storage.Add(new ItemInstance(item, stack.Count));
                }
            }
        }
    }
}
