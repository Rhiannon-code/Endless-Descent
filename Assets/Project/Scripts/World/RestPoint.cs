using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Sleeping in a bed or making camp in a dungeon. Finds the sleeper's own components rather than
    // needing them wired, because these are spawned into generated dungeons where nothing can be
    [DisallowMultipleComponent]
    public class RestPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] bool comfortable = true;
        [SerializeField, Min(1)] int hours = 8;
        [SerializeField, Range(0f, 1f)] float healthPerHour = 0.09f;
        [SerializeField] float dangerRadius = 24f;
        [SerializeField, Range(0f, 1f)] float safetyPerWatcher = 0.82f;

        readonly Collider[] nearby = new Collider[32];

        public string Prompt => comfortable ? $"Sleep ({hours}h)" : $"Make camp ({hours}h)";

        public bool CanInteract(GameObject actor) => WorldClock.Instance != null;

        public void Interact(GameObject actor)
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
                return;

            SurvivalNeeds needs = actor.GetComponentInParent<SurvivalNeeds>();
            Health health = actor.GetComponentInParent<Health>();

            int watchers = CountWatchers();
            float safetyPerHour = Mathf.Pow(safetyPerWatcher, watchers);

            int rested = 0;
            bool interrupted = false;

            for (int hour = 0; hour < hours; hour++)
            {
                // Every hour asleep is another chance for something nearby to find you. Camping in a
                // dungeon full of things is meant to be a gamble, not a free heal
                if (Random.value > safetyPerHour)
                {
                    interrupted = true;
                    break;
                }

                // Skipping the clock is what makes the world move: schedules, rent and restocks all
                // fire off the same events a real night would have produced
                clock.Skip(WorldClock.MinutesPerHour);
                rested++;
            }

            if (rested > 0)
            {
                needs?.Sleep(comfortable ? rested : rested * 0.7f);
                health?.Heal(Mathf.RoundToInt(health.Max * healthPerHour * rested * (comfortable ? 1f : 0.7f)));
            }

            Notice.Show(interrupted
                ? $"Rest interrupted after {rested}h -- something found you."
                : $"Rested {rested}h.");
        }

        int CountWatchers()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, dangerRadius, nearby,
                ~0, QueryTriggerInteraction.Ignore);

            int watchers = 0;
            for (int i = 0; i < count; i++)
            {
                // A corpse keeps its brain switched on, and was counted as something that might find you
                EnemyBrain brain = nearby[i].GetComponentInParent<EnemyBrain>();
                if (brain != null && brain.isActiveAndEnabled &&
                    brain.TryGetComponent(out Health health) && health.IsAlive)
                    watchers++;
            }

            return watchers;
        }
    }
}
