using System;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct NpcActorState
    {
        public string NpcId;
        public bool Alive;
        public string Situation;
    }

    [DisallowMultipleComponent]
    public class NpcActor : MonoBehaviour
    {
        [SerializeField] NpcDefinition definition;
        [SerializeField] NpcDirectory directory;

        public event Action<NpcActivity, LocationDefinition> ScheduleChanged;

        public NpcDefinition Definition => definition;
        public bool IsAlive { get; private set; } = true;
        public NpcActivity Activity { get; private set; } = NpcActivity.Idle;
        public LocationDefinition CurrentLocation { get; private set; }
        public string Situation { get; private set; }

        public string Id => definition != null ? definition.Id : string.Empty;

        // Somebody in the background, a passer by, not one of the people a town's quests are about.
        // Kept out of the directory, so nothing can send you looking for them
        public bool IsListed { get; private set; } = true;

        public void Unlist() => IsListed = false;

        public string DisplayName => definition != null ? definition.DisplayName : "Unknown";

        void Awake()
        {
            if (definition != null)
            {
                Situation = definition.Situation;
                CurrentLocation = definition.Home;
            }

            if (TryGetComponent(out EndlessDescent.Combat.Health health))
                health.Died += Kill;
        }

        // Townspeople are instantiated from a handful of prefabs as a settlement is built, and which
        // person they are is decided then
        public void Adopt(NpcDefinition npc)
        {
            if (npc == null)
                return;

            definition = npc;
            Situation = npc.Situation;
            CurrentLocation = npc.Home;
        }

        void Start()
        {
            if (!IsListed)
                return;

            if (directory == null)
                directory = FindFirstObjectByType<NpcDirectory>();

            directory?.Register(this);
        }

        void OnDestroy() => directory?.Unregister(this);

        public void ApplyHour(int hour)
        {
            if (definition?.Schedule == null || !IsAlive)
                return;

            ScheduleDefinition.Block block = definition.Schedule.BlockFor(hour);
            if (block == null)
                return;

            if (block.Activity == Activity && block.Location == CurrentLocation)
                return;

            Activity = block.Activity;
            CurrentLocation = block.Location;
            ScheduleChanged?.Invoke(Activity, CurrentLocation);
        }

        public void SetSituation(string situation) => Situation = situation;

        public void Kill()
        {
            IsAlive = false;
            Activity = NpcActivity.Idle;
            directory?.Bury(this);
        }

        public NpcActorState Capture() => new NpcActorState { NpcId = Id, Alive = IsAlive, Situation = Situation };

        public void Restore(NpcActorState state)
        {
            IsAlive = state.Alive;
            Situation = state.Situation;
        }
    }
}
