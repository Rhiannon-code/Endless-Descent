using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [DisallowMultipleComponent]
    public class SaveCoordinator : MonoBehaviour
    {
        [SerializeField] string defaultSlot = "slot1";

        readonly List<ISaveable> participants = new List<ISaveable>();

        public IReadOnlyList<ISaveable> Participants => participants;

        public void Rebuild()
        {
            participants.Clear();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is ISaveable saveable && !RebuiltFromSeed(behaviour))
                    participants.Add(saveable);
            }
        }

        // Townspeople and creatures are made again from their place's seed, and are keyed by prefab, so
        // every Skeleton saved as one entry and loaded one Skeleton's health into all of them.
        // What a townsperson has to keep goes through NpcDirectory and ShopLedger instead
        static bool RebuiltFromSeed(MonoBehaviour behaviour) =>
            behaviour.GetComponentInParent<NpcActor>(true) != null ||
            behaviour.GetComponentInParent<EndlessDescent.Combat.EnemyBrain>(true) != null;

        public bool Save(string slot = null)
        {
            Rebuild();

            double minutes = WorldClock.Instance != null ? WorldClock.Instance.TotalMinutes : 0d;
            return SaveSystem.Save(slot ?? defaultSlot, participants, minutes);
        }

        public bool Load(string slot = null)
        {
            GameSave save = SaveSystem.Load(slot ?? defaultSlot);
            if (save == null)
                return false;

            Rebuild();

            // The clock is restored first, needs, schedules and rent all resolve against it, and
            // restoring them against the old time would apply a phantom elapsed period
            WorldClock.Instance?.RestoreState(new WorldClockState { TotalMinutes = save.WorldMinutes });
            SaveSystem.Apply(save, participants);
            WorldClock.Instance?.Announce();
            return true;
        }
    }
}
