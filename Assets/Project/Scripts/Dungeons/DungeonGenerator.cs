using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    [Serializable]
    public struct DungeonState
    {
        public int Seed;
        public int[] CollectedKeyIds;
        public int[] ClearedRoomIds;
    }

    [DisallowMultipleComponent]
    public class DungeonGenerator : MonoBehaviour, IMapSource
    {
        [SerializeField] DungeonTypeDefinition dungeonType;
        [SerializeField] DungeonScale scale = DungeonScale.Wayside;
        [SerializeField] int seed = 12345;
        [SerializeField] bool generateOnStart = true;

        const int LayoutAttempts = 6;
        const int LayoutSeedStride = 104729;

        readonly HashSet<int> collectedKeys = new HashSet<int>();
        readonly HashSet<int> clearedRooms = new HashSet<int>();

        DungeonBuildResult pending;
        int generation;

        // Null until the whole dungeon stands, culling indexes the rooms the moment this appears
        public DungeonBuildResult Current { get; private set; }
        public DungeonGraph Graph => Current?.Graph;
        public int Seed => seed;
        public DungeonTypeDefinition DungeonType => dungeonType;
        public bool Building { get; private set; }

        void Start()
        {
            if (generateOnStart)
                Generate(seed);
        }

        // Travel swaps which place this generator builds, and how big that place is
        public void SetType(DungeonTypeDefinition type, DungeonScale size = DungeonScale.Wayside)
        {
            dungeonType = type;
            scale = size;
        }

        public bool Generate(int newSeed)
        {
            Steps.RunToEnd(Generation(newSeed, false));
            return Current != null;
        }

        // The same build a room a step, the layout is found on a worker, and the caller
        // decides how many rooms go up in a frame
        public IEnumerator GenerateInSteps(int newSeed) => Generation(newSeed, true);

        IEnumerator Generation(int newSeed, bool threaded)
        {
            Clear();
            int mine = generation;
            seed = newSeed;

            if (dungeonType == null)
            {
                Debug.LogError($"{nameof(DungeonGenerator)} has no dungeon type assigned.", this);
                yield break;
            }

            DungeonBuilder.Catalogue catalogue = DungeonBuilder.Catalogue.From(dungeonType, out string unusable);
            if (catalogue == null)
            {
                Debug.LogError($"Dungeon type '{dungeonType.name}' cannot be built: {unusable}", this);
                yield break;
            }

            DungeonLayoutSettings settings = DungeonLayoutSettings.From(dungeonType, scale);
            System.Text.StringBuilder attempts = new System.Text.StringBuilder();
            Building = true;

            // A solvable layout can still have no grid embedding within the search budget. None has been
            // seen in 500, but a different layout is cheaper than failing the dungeon
            for (int attempt = 0; attempt < LayoutAttempts; attempt++)
            {
                int layoutSeed = seed + attempt * LayoutSeedStride;
                DungeonGraph graph = null;
                DungeonGridLayout layout = null;
                string failure = null;

                void Find()
                {
                    graph = DungeonLayoutGenerator.Generate(settings, layoutSeed, out failure);
                    if (graph != null)
                        layout = DungeonGridLayout.Solve(graph, catalogue.Modules, out failure);
                }

                if (threaded)
                {
                    Task finding = Task.Run(Find);

                    while (!finding.IsCompleted)
                        yield return Steps.WaitFrame;

                    if (mine != generation)
                        yield break;

                    if (finding.IsFaulted)
                    {
                        Building = false;
                        Debug.LogException(finding.Exception.InnerException, this);
                        yield break;
                    }
                }
                else
                {
                    Find();
                }

                if (layout == null)
                {
                    attempts.Append(graph == null
                        ? $"\n  layout {attempt + 1} (seed {layoutSeed}): {failure}"
                        : $"\n  layout {attempt + 1} (seed {layoutSeed}, {graph.Nodes.Count} rooms): {failure}");
                    continue;
                }

                if (attempt > 0)
                    Debug.LogWarning($"Seed {seed} needed layout attempt {attempt + 1}; the dungeon is " +
                                     $"built from layout seed {layoutSeed}.", this);

                pending = DungeonBuilder.Begin(graph, layout, dungeonType, transform);

                foreach (object step in DungeonBuilder.Realise(pending, catalogue, dungeonType))
                {
                    yield return step;

                    if (mine != generation)
                        yield break;
                }

                Current = pending;
                pending = null;
                Building = false;

                // The builder cannot see the generator, so doors learn who holds the keys here
                foreach (Door door in Current.Doors)
                    door.BindGenerator(this);

                yield break;
            }

            Building = false;
            Debug.LogError($"Dungeon build failed for seed {seed} after {LayoutAttempts} layouts " +
                           $"of '{dungeonType.name}':{attempts}", this);
        }

        // Destroys the previous dungeon outright, and stops one still going up. Nothing hand placed under
        // this object survives a regenerate, which is why module setup belongs on the donor prefabs
        public void Clear()
        {
            generation++;
            Building = false;

            DungeonBuilder.Discard(pending?.Root);
            DungeonBuilder.Discard(Current?.Root);

            pending = null;
            Current = null;
            collectedKeys.Clear();
            clearedRooms.Clear();
        }

        public string PlaceName => dungeonType != null ? dungeonType.DisplayName : "Dungeon";
        public int LevelCount => Current != null ? Current.LevelCount : 1;

        // The map is read back off the rooms that were actually placed. Levels come from where things
        // stand in the dungeon, not from the graph, the grid adds stairs the layout did not ask for
        public void CollectMarkers(List<MapMarker> into)
        {
            if (Current == null)
                return;

            foreach (KeyValuePair<int, GameObject> pair in Current.Rooms)
            {
                if (pair.Value == null)
                    continue;

                RoomModule module = pair.Value.GetComponent<RoomModule>();
                Vector3 size = module != null ? module.LocalBounds.size : Vector3.one * 8f;

                into.Add(new MapMarker
                {
                    Centre = new Vector2(pair.Value.transform.position.x, pair.Value.transform.position.z),
                    Size = new Vector2(size.x, size.z),
                    Rotation = pair.Value.transform.eulerAngles.y,
                    Level = Current.Layout.Level[pair.Key],
                    Label = module != null && module.Purpose != RoomPurpose.None ? module.Purpose.ToString() : null,
                    Kind = pair.Key == Graph?.EntranceId ? MapMarkerKind.Entrance
                        : pair.Key == Graph?.BossId ? MapMarkerKind.Boss : MapMarkerKind.Room
                });
            }

            foreach (GameObject corridor in Current.Corridors)
            {
                if (corridor == null)
                    continue;

                RoomModule module = corridor.GetComponent<RoomModule>();
                Vector3 size = Vector3.Scale(module != null ? module.LocalBounds.size : new Vector3(4f, 4f, 4f),
                    corridor.transform.localScale);

                into.Add(new MapMarker
                {
                    Centre = new Vector2(corridor.transform.position.x, corridor.transform.position.z),
                    Size = new Vector2(size.x, size.z),
                    Rotation = corridor.transform.eulerAngles.y,
                    Level = Mathf.RoundToInt(-Current.Root.transform.InverseTransformPoint(corridor.transform.position).y
                                             / DungeonGridLayout.Storey),
                    Kind = MapMarkerKind.Corridor
                });
            }
        }

        public bool HasKey(int keyId) => collectedKeys.Contains(keyId);
        public bool CollectKey(int keyId) => collectedKeys.Add(keyId);
        public bool IsRoomCleared(int roomId) => clearedRooms.Contains(roomId);
        public bool MarkRoomCleared(int roomId) => clearedRooms.Add(roomId);

        public bool CanPass(DungeonEdge edge)
        {
            return edge.Kind != EdgeKind.Locked || collectedKeys.Contains(edge.KeyId);
        }

        public DungeonState CaptureState()
        {
            int[] keys = new int[collectedKeys.Count];
            collectedKeys.CopyTo(keys);

            int[] rooms = new int[clearedRooms.Count];
            clearedRooms.CopyTo(rooms);

            return new DungeonState { Seed = seed, CollectedKeyIds = keys, ClearedRoomIds = rooms };
        }

        public bool RestoreState(DungeonState state)
        {
            if (!Generate(state.Seed))
                return false;

            if (state.CollectedKeyIds != null)
            {
                foreach (int keyId in state.CollectedKeyIds)
                    collectedKeys.Add(keyId);
            }

            if (state.ClearedRoomIds != null)
            {
                foreach (int roomId in state.ClearedRoomIds)
                    clearedRooms.Add(roomId);
            }

            return true;
        }
    }
}
