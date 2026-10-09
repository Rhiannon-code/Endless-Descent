using EndlessDescent.Core;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class Bed : MonoBehaviour, IInteractable
    {
        [SerializeField] SurvivalNeeds needs;
        [SerializeField, Min(1)] int sleepHours = 8;

        public string Prompt => $"Sleep {sleepHours} hours";

        public bool CanInteract(GameObject actor) => WorldClock.Instance != null;

        public void Interact(GameObject actor)
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
                return;

            // Skipping the clock is what makes the world move, schedules, rent and restocks all
            // fire off the same events a real night would have produced
            clock.Skip(sleepHours * WorldClock.MinutesPerHour);
            (needs != null ? needs : actor.GetComponentInParent<SurvivalNeeds>())?.Sleep(sleepHours);
        }
    }
}
