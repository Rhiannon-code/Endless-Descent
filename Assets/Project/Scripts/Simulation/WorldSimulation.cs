using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    // Everything time driven hangs off the one clock. Nothing here polls its own timer
    [DisallowMultipleComponent]
    public class WorldSimulation : MonoBehaviour
    {
        [SerializeField] NpcDirectory npcs;
        [SerializeField] PropertyRegistry properties;

        readonly List<Merchant> merchants = new List<Merchant>();

        WorldClock clock;

        public void RegisterMerchant(Merchant merchant)
        {
            if (merchant != null && !merchants.Contains(merchant))
                merchants.Add(merchant);
        }

        public void UnregisterMerchant(Merchant merchant) => merchants.Remove(merchant);

        void Start()
        {
            clock = WorldClock.Instance;

            if (clock == null)
            {
                Debug.LogError($"{nameof(WorldSimulation)} found no {nameof(WorldClock)}.", this);
                enabled = false;
                return;
            }

            clock.HourChanged += OnHourChanged;
            clock.DayChanged += OnDayChanged;

            OnHourChanged(clock.Hour);
            OnDayChanged(clock.Day);
        }

        void OnDestroy()
        {
            if (clock == null)
                return;

            clock.HourChanged -= OnHourChanged;
            clock.DayChanged -= OnDayChanged;
        }

        void OnHourChanged(int hour) => npcs?.ApplyHour(hour);

        void OnDayChanged(int day)
        {
            properties?.ApplyDay(day);

            foreach (Merchant merchant in merchants)
                merchant.RestockIfDue(day);
        }
    }
}
