using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using EndlessDescent.Settlements;
using UnityEngine;

namespace EndlessDescent.World
{
    // Builds places and puts the player in them. Which places should exist while the player walks is
    // PlaceStreamer's decision, this knows how. Every move goes through a transition, so the building
    // happens while the screen is dark and the game keeps drawing
    [DisallowMultipleComponent]
    public class PlaceLoader : MonoBehaviour
    {
        [SerializeField] RegionMap region;
        [SerializeField] DungeonGenerator dungeons;
        [SerializeField] SettlementGenerator settlements;
        [SerializeField] DungeonPopulator populator;
        [SerializeField] Transform player;
        [SerializeField] WorldTerrainStreamer world;
        [SerializeField] Light sun;
        [SerializeField] GameObject entrancePrefab;
        [SerializeField] GameObject exitPrefab;
        [SerializeField] int startAt;
        [SerializeField] bool enterOnStart = true;

        const float UndergroundMetres = -3000f;

        // How close a town has to be to stand built. PlaceStreamer builds on approach at this distance
        // and a load builds one this close before arriving
        public const float TownReachKm = 2.5f;
        // Walking up to a place, it goes up a little a frame. With the screen dark there is no frame
        // rate to protect, only the wait, so it goes up as fast as the caption still moves
        const float WalkingBudgetMs = 6f;
        const float DarkBudgetMs = 40f;
        const float BlockedMessageSeconds = 1.5f;

        public event Action Arrived;

        public RegionMap Region => region;
        public WorldTerrainStreamer World => world;
        public int Current { get; private set; } = -1;
        public IMapSource Map { get; private set; }
        public int BuiltSettlement { get; private set; } = -1;
        public int PreparedDungeon { get; private set; } = -1;
        public bool DungeonReady { get; private set; }
        public bool Inside { get; private set; }
        public Transform Player => player;

        readonly Dictionary<int, GameObject> entrances = new Dictionary<int, GameObject>();
        readonly HashSet<int> blocked = new HashSet<int>();
        Coroutine building;
        Coroutine preparing;
        IEnumerator construction;
        IEnumerator dungeonWork;
        Vector3 doorstep;
        bool entrancesStood;

        // Where a journey starts, where the player is standing, not the last place they arrived at
        public Vector2 HereKm
        {
            get
            {
                if (player != null && world != null)
                    return WorldOrigin.KmAt(player.position);

                return region != null && region.Has(Current)
                    ? region.Locations[Current].Position
                    : Vector2.zero;
            }
        }

        void OnEnable()
        {
            CurrentSpace.Changed += OnSpaceChanged;
            WorldOrigin.Shifted += OnShifted;
            Transition.Ended += OnTransitionEnded;
        }

        void OnDisable()
        {
            CurrentSpace.Changed -= OnSpaceChanged;
            WorldOrigin.Shifted -= OnShifted;
            Transition.Ended -= OnTransitionEnded;
        }

        public int StartAt => startAt;

        // The opening entry is PlaceLoader's by default, because pressing Play on the world scene has
        // to keep working. When the game is entered through the front end, the prewarm owns the
        // sequence instead so the building happens behind a progress bar rather than a bare fade
        public void DeferOpeningEntry() => enterOnStart = false;

        public IEnumerator OpeningArrival() => Arrival(startAt);

        void Start()
        {
            if (!enterOnStart)
                return;

            if (world == null)
            {
                Enter(startAt);
                return;
            }

            Transition.StartInDark("Entering the world", Arrival(startAt));
        }

        void OnSpaceChanged(SpaceKind kind)
        {
            if (sun != null)
                sun.enabled = kind == SpaceKind.Overworld;
        }

        void OnShifted(Vector3 by) => doorstep -= by;

        // However a transition ended, the ring belongs to the player again
        void OnTransitionEnded()
        {
            if (world != null)
                world.Release();
        }

        public int SeedFor(int index) =>
            unchecked((world != null ? world.Seed : 0) * 486187739 + (index + 1) * 16777619);

        public bool IsBlocked(int index) => blocked.Contains(index);

        public void SetNearest(int index) => Current = index;

        public int TravelDays(int to) => region != null ? region.TravelDays(Mathf.Max(0, Current), to) : 0;

        public bool TravelTo(int index) =>
            TravelTo(index, TravelDays(index) * WorldClock.HoursPerDay);

        // Hours rather than days, and given rather than derived: a journey costed off the real route
        // knows what it took, and walking it has already spent the time before arriving here
        public bool TravelTo(int index, float hours)
        {
            if (region == null || !region.Has(index))
                return false;

            if (world == null)
            {
                SkipClock(hours);
                return Enter(index);
            }

            if (!Transition.Start($"Travelling to {region.Locations[index].DisplayName}", Transition.Long, Arrival(index)))
                return false;

            SkipClock(hours);
            return true;
        }

        static void SkipClock(float hours)
        {
            if (hours > 0f)
                WorldClock.Instance?.Skip(hours * WorldClock.MinutesPerHour);
        }

        // Dying wakes you in the nearest town rather than wherever the scene happened to start
        public bool Respawn()
        {
            int town = NearestSettlement();

            if (town < 0)
                return false;

            if (world == null)
                return Enter(town);

            return Transition.Start("You wake somewhere safe", Transition.Long, Arrival(town));
        }

        // Dying while the screen is already dark has to wait for it rather than fall through to
        // something that moves the player without preparing anywhere for them to land
        public void RespawnWhenFree() => StartCoroutine(RespawnSoon());

        IEnumerator RespawnSoon()
        {
            while (Transition.Busy)
                yield return null;

            Respawn();
        }

        int NearestSettlement() => NearestSettlement(HereKm);

        int NearestSettlement(Vector2 here)
        {
            if (region == null)
                return -1;

            int nearest = -1;
            float best = float.MaxValue;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];
                if (!location.IsSettlement)
                    continue;

                float km = Vector2.Distance(here, location.Position);

                if (km >= best)
                    continue;

                best = km;
                nearest = i;
            }

            return nearest;
        }

        // Everything a place needs before the player can stand in it, a step at a time
        IEnumerator Arrival(int index)
        {
            while (!world.Ready)
                yield return Steps.WaitFrame;

            RegionLocation location = region.Locations[index];

            if (PreparedDungeon >= 0 || Inside)
                ClearDungeon();

            if (location.IsSettlement && settlements != null)
                yield return ArriveAtTown(index, location);
            else
                yield return ArriveAtEntrance(index);

            Current = index;
            CurrentSpace.Enter(SpaceKind.Overworld);
            Arrived?.Invoke();
        }

        IEnumerator ArriveAtTown(int index, RegionLocation location)
        {
            yield return BuildTown(index, location);
            yield return world.Prepare(WorldOrigin.ToWorld(settlements.Entrance));

            Place(settlements.Entrance, settlements.Facing);
        }

        IEnumerator BuildTown(int index, RegionLocation location)
        {
            SettlementPlan plan = BuiltSettlement == index ? settlements.Planned : null;

            if (plan == null)
            {
                WorldSurface surface = world.Surface;
                SettlementGrowth growth = SettlementGrowth.From(location.Settlement);
                int seed = SeedFor(index);
                Vector2 at = location.Position;

                Task<SettlementPlan> planning = Task.Run(() => SettlementGenerator.PlanOn(growth, seed, surface, at));

                while (!planning.IsCompleted)
                    yield return Steps.WaitFrame;

                plan = planning.Result;
            }

            if (BuiltSettlement != index)
            {
                StopBuilding();
                Stand(index);
                construction = settlements.GenerateInSteps(SeedFor(index), plan);
            }
            else if (building != null)
            {
                // Already going up a little a frame for someone walking towards it; finish it now
                StopCoroutine(building);
                building = null;
            }

            if (construction != null)
                yield return Steps.Budgeted(construction, DarkBudgetMs);

            construction = null;
            Map = settlements;
        }

        // Back to where a save was made. It is always somewhere outdoors (SavedPlace), and a town within
        // reach of it is built before the screen comes up rather than rising around the player
        public bool ReturnTo(Vector3 worldMetres, float yaw) =>
            world != null && Transition.Start("Returning", Transition.Long, Returning(worldMetres, yaw));

        public IEnumerator Returning(Vector3 worldMetres, float yaw)
        {
            while (!world.Ready)
                yield return Steps.WaitFrame;

            if (PreparedDungeon >= 0 || Inside)
                ClearDungeon();

            Vector2 km = new Vector2(worldMetres.x, worldMetres.z) / WorldSurface.MetresPerKm;
            int town = NearestSettlement(km);

            if (town >= 0 && Vector2.Distance(km, region.Locations[town].Position) <= TownReachKm && settlements != null)
                yield return BuildTown(town, region.Locations[town]);

            yield return world.Prepare(worldMetres);

            Place(WorldOrigin.ToUnity(worldMetres), Quaternion.Euler(0f, yaw, 0f));
            Current = town;
            CurrentSpace.Enter(SpaceKind.Overworld);
            Arrived?.Invoke();
        }

        IEnumerator ArriveAtEntrance(int index)
        {
            StandEntrances();

            if (!entrances.TryGetValue(index, out GameObject arch))
                yield break;

            yield return world.Prepare(WorldOrigin.ToWorld(Doorstep(arch.transform.position)));

            Map = BuiltSettlement >= 0 ? settlements : null;
            Place(Doorstep(arch.transform.position), Quaternion.identity);
        }

        // Scenes with no world of their own, the place is the whole scene, built where it stands
        public bool Enter(int index)
        {
            if (region == null || !region.Has(index))
                return false;

            RegionLocation location = region.Locations[index];

            if (location.IsSettlement && settlements != null)
            {
                if (dungeons != null) dungeons.Clear();

                settlements.SetDefinition(location.Settlement);
                BuiltSettlement = index;

                if (!settlements.Generate(SeedFor(index)))
                    return false;

                Map = settlements;
                Place(settlements.Entrance, settlements.Facing);
                CurrentSpace.Enter(SpaceKind.Overworld);
            }
            else if (dungeons != null && location.Dungeon != null)
            {
                if (settlements != null) settlements.Clear();

                BuiltSettlement = -1;
                dungeons.SetType(location.Dungeon, location.Scale);

                if (!dungeons.Generate(SeedFor(index)))
                    return false;

                if (populator != null) populator.Populate();

                Map = dungeons;
                Place(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
                CurrentSpace.Enter(SpaceKind.Dungeon);
            }
            else
            {
                return false;
            }

            Current = index;
            Arrived?.Invoke();
            return true;
        }

        void Stand(int index)
        {
            RegionLocation location = region.Locations[index];
            settlements.SetDefinition(location.Settlement);

            if (world != null)
                settlements.StandOn(world.Surface, location.Position);

            settlements.SetCast(location.Residents);
            BuiltSettlement = index;
        }

        public void BuildSettlement(int index, SettlementPlan plan)
        {
            if (settlements == null || region == null || !region.Has(index) || BuiltSettlement == index)
                return;

            StopBuilding();
            Stand(index);

            if (!Inside)
                Map = settlements;

            construction = settlements.GenerateInSteps(SeedFor(index), plan);
            building = StartCoroutine(Construct());
        }

        IEnumerator Construct()
        {
            IEnumerator work = Steps.Budgeted(construction, WalkingBudgetMs);

            while (work.MoveNext())
                yield return work.Current;

            building = null;
            construction = null;
        }

        public void ClearSettlement()
        {
            StopBuilding();

            if (settlements != null)
                settlements.Clear();

            BuiltSettlement = -1;

            if (ReferenceEquals(Map, settlements))
                Map = null;
        }

        void StopBuilding()
        {
            if (building != null)
                StopCoroutine(building);

            building = null;
            construction = null;
            BuiltSettlement = -1;
        }

        public void StandEntrances()
        {
            if (entrancesStood || world == null || !world.Ready || entrancePrefab == null || region == null)
                return;

            entrancesStood = true;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];
                if (location.IsSettlement || location.Dungeon == null)
                    continue;

                GameObject arch = Instantiate(entrancePrefab,
                    WorldOrigin.UnityAt(location.Position, world.Surface.Height(location.Position)),
                    Quaternion.identity, transform);

                arch.name = $"Entrance_{location.DisplayName}";

                DungeonEntrance door = arch.GetComponent<DungeonEntrance>();
                if (door == null) door = arch.AddComponent<DungeonEntrance>();

                door.Bind(this, i, location.DisplayName);
                entrances[i] = arch;
            }
        }

        static Vector3 Doorstep(Vector3 entrance) => entrance + new Vector3(0f, 1.2f, 3.5f);

        // Built directly under its own entrance, so going in does not move the player across the map
        // and the world above keeps the ground they will come back out onto
        public void PrepareDungeon(int index)
        {
            if (PreparedDungeon == index || dungeons == null || region == null || !region.Has(index) || Inside)
                return;

            ClearDungeon();
            PreparedDungeon = index;

            if (blocked.Contains(index))
                return;

            dungeonWork = DungeonSteps(index);
            preparing = StartCoroutine(Prepare());
        }

        IEnumerator Prepare()
        {
            IEnumerator work = Steps.Budgeted(dungeonWork, WalkingBudgetMs);

            while (work.MoveNext())
                yield return work.Current;

            preparing = null;
            dungeonWork = null;
        }

        IEnumerator DungeonSteps(int index)
        {
            RegionLocation location = region.Locations[index];

            dungeons.SetType(location.Dungeon, location.Scale);
            dungeons.transform.position = WorldOrigin.UnityAt(location.Position, UndergroundMetres);

            IEnumerator build = dungeons.GenerateInSteps(SeedFor(index));

            while (build.MoveNext())
                yield return build.Current;

            if (dungeons.Current == null)
            {
                blocked.Add(index);
                yield break;
            }

            if (populator != null)
                foreach (object step in populator.Populating())
                    yield return step;

            if (exitPrefab != null)
            {
                GameObject back = Instantiate(exitPrefab, dungeons.Current.Root.transform);
                dungeons.Current.Rooms.TryGetValue(dungeons.Graph.EntranceId, out GameObject entrance);
                AgainstClosedWall(back.transform, entrance);

                DungeonExit exit = back.GetComponent<DungeonExit>();
                if (exit == null) exit = back.AddComponent<DungeonExit>();

                exit.Bind(this);
            }

            DungeonReady = true;
        }

        // On the entrance room's floor against a wall with no passage in it, and to one side of the
        // torch that sits mid wall. It used to hang at a fixed 1.2 m, half into the ceiling
        const float ExitBesideTorch = 2.2f;
        const float WallFace = 0.2f;

        static void AgainstClosedWall(Transform exit, GameObject room)
        {
            if (room == null)
                return;

            RoomModule module = room.GetComponent<RoomModule>();
            Bounds bounds = module != null ? module.LocalBounds : new Bounds(Vector3.up * 2f, new Vector3(8f, 4f, 8f));

            Vector3 wall = Vector3.back;

            foreach (Vector3 side in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
            {
                if (HasPassage(module, side))
                    continue;

                wall = side;
                break;
            }

            Vector3 along = Vector3.Cross(Vector3.up, wall);
            float reach = Mathf.Abs(Vector3.Dot(bounds.extents, wall)) - WallFace;
            float span = Mathf.Abs(Vector3.Dot(bounds.extents, along));

            Vector3 local = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
                            + wall * reach + along * Mathf.Clamp(ExitBesideTorch, 0f, span - 1.3f);

            exit.SetPositionAndRotation(room.transform.TransformPoint(local),
                room.transform.rotation * Quaternion.LookRotation(-wall));
        }

        static bool HasPassage(RoomModule module, Vector3 side)
        {
            if (module == null)
                return false;

            foreach (ModuleConnector connector in module.Connectors)
            {
                Vector3 local = module.LocalPositionOf(connector);
                Vector3 flat = new Vector3(local.x, 0f, local.z);

                if (flat.sqrMagnitude > 0.01f && Vector3.Dot(flat.normalized, side) > 0.7f)
                    return true;
            }

            return false;
        }

        public void ClearDungeon()
        {
            if (preparing != null)
                StopCoroutine(preparing);

            preparing = null;
            dungeonWork = null;

            if (dungeons != null)
                dungeons.Clear();

            if (populator != null)
                populator.Forget();

            PreparedDungeon = -1;
            DungeonReady = false;
            Inside = false;
        }

        public bool Descend(int index, Vector3 from)
        {
            if (region == null || !region.Has(index) || region.Locations[index].IsSettlement)
                return false;

            doorstep = Doorstep(from);

            bool ready = PreparedDungeon == index && DungeonReady;

            return Transition.Start($"Entering {region.Locations[index].DisplayName}",
                ready ? Transition.Quick : Transition.Long, IntoDungeon(index));
        }

        IEnumerator IntoDungeon(int index)
        {
            PrepareDungeon(index);

            if (preparing != null)
            {
                StopCoroutine(preparing);
                preparing = null;
            }

            if (dungeonWork != null)
                yield return Steps.Budgeted(dungeonWork, DarkBudgetMs);

            dungeonWork = null;

            if (!DungeonReady)
            {
                Transition.Report($"The way into {region.Locations[index].DisplayName} is blocked");

                float until = Time.unscaledTime + BlockedMessageSeconds;
                while (Time.unscaledTime < until)
                    yield return Steps.WaitFrame;

                yield break;
            }

            Inside = true;
            Map = dungeons;
            Place(dungeons.transform.position + new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            CurrentSpace.Enter(SpaceKind.Dungeon);
            Arrived?.Invoke();
        }

        // The dungeon stays built behind you, so going straight back in is instant, walking away is
        // what releases it
        public void Surface()
        {
            if (!Inside)
                return;

            Transition.Start(string.Empty, Transition.Quick, OutOfDungeon());
        }

        IEnumerator OutOfDungeon()
        {
            if (world != null)
                yield return world.Prepare(WorldOrigin.ToWorld(doorstep));

            Inside = false;
            Map = BuiltSettlement >= 0 ? settlements : null;
            Place(doorstep, Quaternion.identity);
            CurrentSpace.Enter(SpaceKind.Overworld);
            Arrived?.Invoke();
        }

        void Place(Vector3 position, Quaternion facing)
        {
            if (player == null)
                return;

            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;

            if (wasEnabled)
                controller.enabled = false;

            player.SetPositionAndRotation(position, facing);

            if (wasEnabled)
                controller.enabled = true;

            if (world != null)
            {
                world.Release();

                // Straight away rather than next frame: nothing should run a frame at a position
                // hundreds of kilometres from the origin
                FloatingOrigin origin = world.GetComponent<FloatingOrigin>();
                if (origin != null) origin.Recentre();
            }

            Physics.SyncTransforms();
        }
    }
}
