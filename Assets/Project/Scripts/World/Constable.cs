using EndlessDescent.Core;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Settles a bounty. Pay it if you can afford it, otherwise you serve the time, which costs days
    // on the clock instead of gold , and the world moves on while you are inside
    [DisallowMultipleComponent]
    public class Constable : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] int goldPerDayServed = 40;

        CrimeRecord record;
        CrimeRecord Record => record != null ? record : record = FindFirstObjectByType<CrimeRecord>();

        public string Prompt
        {
            get
            {
                CrimeRecord record = Record;

                if (record == null || !record.IsWanted)
                    return "Constable: nothing on you";

                return $"Answer for your crimes: pay {record.Bounty} gold, or serve " +
                       $"{Days(record.Bounty)} days";
            }
        }

        int Days(int bounty) => Mathf.Max(1, bounty / goldPerDayServed);

        public bool CanInteract(GameObject actor) => Record != null;

        public void Interact(GameObject actor)
        {
            CrimeRecord record = Record;

            if (record == null || !record.IsWanted)
                return;

            Wallet wallet = actor.GetComponentInParent<Wallet>();

            if (wallet != null && wallet.TrySpend(record.Bounty))
            {
                Notice.Show($"Fine of {record.Bounty} gold paid.");
                record.Clear();
                return;
            }

            int days = Days(record.Bounty);
            WorldClock.Instance?.Skip(days * WorldClock.HoursPerDay * WorldClock.MinutesPerHour);
            record.Clear();
            Notice.Show($"Served {days} days. Released.");
        }
    }
}
