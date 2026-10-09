using System.Collections;
using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    public sealed class DungeonBuildResult
    {
        public DungeonGraph Graph;
        public DungeonGridLayout Layout;
        public GameObject Root;
        public readonly Dictionary<int, GameObject> Rooms = new Dictionary<int, GameObject>();
        public readonly List<GameObject> Corridors = new List<GameObject>();
        public readonly List<GameObject> Seals = new List<GameObject>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<SecretDoor> Secrets = new List<SecretDoor>();
        public readonly List<int> UnrealisedEdgeIds = new List<int>();

        public int LevelCount => Layout != null ? Layout.LevelCount : 1;
    }

    // Turns a grid layout into rooms, corridors, stairs and doors (ADR 0042). Nothing here searches,
    // DungeonGridLayout has already given every room a cell, so what is left is corridor lengths
    public static class DungeonBuilder
    {
        // A run shorter than this loses its torch, or the half-metre between a stair and a room would
        // carry a light of its own
        const float LitRunMetres = 6f;

        // What the grid needs to know about each module, read off the prefabs on the main thread once.
        // Solving a layout from it touches nothing in Unity, so the solve can run on a worker
        public sealed class Catalogue
        {
            public readonly List<RoomModuleDefinition> Definitions = new List<RoomModuleDefinition>();
            public readonly List<GridModule> Modules = new List<GridModule>();
            public readonly List<int[]> ConnectorSides = new List<int[]>();

            public static Catalogue From(DungeonTypeDefinition type, out string failure)
            {
                if (type == null || type.ModuleSet == null)
                {
                    failure = "dungeon type or module set missing";
                    return null;
                }

                if (!type.ModuleSet.CoversEveryRole(out RoomRole missing))
                {
                    failure = $"module set has no module for role '{missing}'";
                    return null;
                }

                if (type.CorridorStraight == null || type.CorridorStraight.Prefab == null || type.Stair == null || type.Stair.Prefab == null)
                {
                    failure = "dungeon type has no straight corridor or no stair";
                    return null;
                }

                Catalogue catalogue = new Catalogue();

                foreach (RoomModuleDefinition definition in type.ModuleSet.Modules)
                {
                    RoomModule module = definition != null && definition.Prefab != null
                        ? definition.Prefab.GetComponent<RoomModule>()
                        : null;

                    if (module == null || module.Connectors == null)
                        continue;

                    int doors = 0;
                    int[] sides = new int[module.Connectors.Count];

                    for (int i = 0; i < sides.Length; i++)
                    {
                        Vector3 at = module.LocalPositionOf(module.Connectors[i]);
                        Vector3 facing = module.LocalRotationOf(module.Connectors[i]) * Vector3.forward;

                        sides[i] = Mathf.Abs(facing.z) >= Mathf.Abs(facing.x)
                            ? (facing.z > 0f ? 0 : 2)
                            : (facing.x > 0f ? 1 : 3);

                        // Two facing doors only line up if every doorway is in the middle of its face
                        float across = sides[i] % 2 == 0 ? at.x : at.z;

                        if (Mathf.Abs(across) > 0.01f || (doors & (1 << sides[i])) != 0)
                        {
                            failure = $"'{definition.Prefab.name}' has a doorway off the middle of a face, or two on one face";
                            return null;
                        }

                        doors |= 1 << sides[i];
                    }

                    Bounds bounds = module.LocalBounds;
                    float reach = Mathf.Max(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                        Mathf.Abs(bounds.center.z) + bounds.extents.z);

                    int roles = 0;
                    foreach (RoomRole role in System.Enum.GetValues(typeof(RoomRole)))
                        if (definition.CanServe(role)) roles |= 1 << (int)role;

                    catalogue.Definitions.Add(definition);
                    catalogue.Modules.Add(new GridModule(doors, reach, definition.Weight, roles));
                    catalogue.ConnectorSides.Add(sides);
                }

                failure = null;
                return catalogue;
            }
        }

        public static DungeonBuildResult Build(DungeonGraph graph, DungeonTypeDefinition type, Transform parent,
            out string failure)
        {
            Catalogue catalogue = Catalogue.From(type, out failure);
            if (catalogue == null)
                return null;

            DungeonGridLayout layout = DungeonGridLayout.Solve(graph, catalogue.Modules, out failure);
            if (layout == null)
                return null;

            DungeonBuildResult result = Begin(graph, layout, type, parent);
            foreach (object _ in Realise(result, catalogue, type)) { }

            return result;
        }

        public static DungeonBuildResult Begin(DungeonGraph graph, DungeonGridLayout layout, DungeonTypeDefinition type,
            Transform parent)
        {
            DungeonBuildResult result = new DungeonBuildResult { Graph = graph, Layout = layout };

            result.Root = new GameObject($"Dungeon_{type.DisplayName}_{graph.Seed}");
            if (parent != null)
                result.Root.transform.SetParent(parent, false);

            foreach (DungeonEdge edge in graph.Edges)
                if (!edge.IsSpanningTree) result.UnrealisedEdgeIds.Add(edge.Id);

            return result;
        }

        // A room a step, with the corridor and door that lead into it, so the caller decides how much
        // of the dungeon goes up in one frame
        public static IEnumerable Realise(DungeonBuildResult result, Catalogue catalogue, DungeonTypeDefinition type)
        {
            DungeonGraph graph = result.Graph;
            DungeonGridLayout layout = result.Layout;
            Dictionary<int, HashSet<int>> used = new Dictionary<int, HashSet<int>>();
            Queue<int> order = new Queue<int>();
            order.Enqueue(graph.EntranceId);

            while (order.Count > 0)
            {
                int node = order.Dequeue();
                RoomModuleDefinition definition = catalogue.Definitions[layout.Module[node]];

                GameObject room = Object.Instantiate(definition.Prefab, result.Root.transform);
                room.transform.SetLocalPositionAndRotation(CellCentre(layout, node),
                    Quaternion.Euler(0f, 90f * layout.Turns[node], 0f));
                room.name = $"Room_{node}_{graph.Node(node).Role}";

                result.Rooms[node] = room;
                used[node] = new HashSet<int>();

                int parent = layout.Parent[node];

                if (parent >= 0)
                {
                    int side = layout.SideFromParent(node);
                    int outOf = ConnectorOn(catalogue, layout, parent, side);
                    int into = ConnectorOn(catalogue, layout, node, (side + 2) % 4);

                    used[parent].Add(outOf);
                    used[node].Add(into);

                    ModuleConnector mouth = result.Rooms[parent].GetComponent<RoomModule>().Connectors[outOf];
                    ModuleConnector entry = room.GetComponent<RoomModule>().Connectors[into];

                    Connect(result, type, layout, parent, mouth.transform, entry.transform, room.transform);

                    DungeonEdge edge = TreeEdge(graph, parent, node);
                    if (edge != null)
                        HangDoor(type, result, mouth, edge, room.transform);
                }

                foreach (int child in layout.ChildrenOf(node))
                    order.Enqueue(child);

                yield return null;
            }

            foreach (object step in Seal(type, result, used))
                yield return step;
        }

        static Vector3 CellCentre(DungeonGridLayout layout, int node) => new Vector3(
            layout.X[node] * DungeonGridLayout.Cell,
            -layout.Level[node] * DungeonGridLayout.Storey,
            layout.Y[node] * DungeonGridLayout.Cell);

        static int ConnectorOn(Catalogue catalogue, DungeonGridLayout layout, int node, int side)
        {
            int[] sides = catalogue.ConnectorSides[layout.Module[node]];

            for (int i = 0; i < sides.Length; i++)
                if ((sides[i] + layout.Turns[node]) % 4 == side) return i;

            throw new System.InvalidOperationException($"room {node} has no doorway on side {side}");
        }

        static DungeonEdge TreeEdge(DungeonGraph graph, int from, int to)
        {
            foreach (int id in graph.Node(to).EdgeIds)
            {
                DungeonEdge edge = graph.Edge(id);
                if (edge.IsSpanningTree && graph.Opposite(edge, to) == from)
                    return edge;
            }

            return null;
        }

        // Worked in the dungeon's own space, where the grid is axis aligned
        static void Connect(DungeonBuildResult result, DungeonTypeDefinition type, DungeonGridLayout layout, int parent,
            Transform mouth, Transform entry, Transform owner)
        {
            Transform root = result.Root.transform;
            Vector3 a = root.InverseTransformPoint(mouth.position);
            Vector3 b = root.InverseTransformPoint(entry.position);

            if (Mathf.Abs(a.y - b.y) < 0.01f)
            {
                Run(result, type, a, b, owner);
                return;
            }

            // Level from the doorway to the band between the cells, the stair inside the band, and
            // level again on to the room below. Kept in the band, a stair never passes under a room
            Vector3 ahead = Flat(b - a).normalized;
            float toBand = DungeonGridLayout.Envelope - Vector3.Dot(a - CellCentre(layout, parent), ahead);
            Vector3 top = a + ahead * toBand;

            Run(result, type, a, top, owner);
            Vector3 bottom = Stair(result, type, top, ahead, owner);
            Run(result, type, bottom, b, owner);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 Stair(DungeonBuildResult result, DungeonTypeDefinition type, Vector3 top, Vector3 ahead,
            Transform owner)
        {
            RoomModule module = type.Stair.Prefab.GetComponent<RoomModule>();
            ModuleConnector upper = module.Connectors[0];
            ModuleConnector lower = module.Connectors[1];

            if (module.LocalPositionOf(lower).y > module.LocalPositionOf(upper).y)
                (upper, lower) = (lower, upper);

            Mate(module, upper, top, Quaternion.LookRotation(ahead), out Vector3 position, out Quaternion rotation);
            Piece(result, type.Stair.Prefab, position, rotation, owner);

            ConnectorPose(module, lower, position, rotation, out Vector3 bottom, out _);
            return bottom;
        }

        // A straight corridor stretched to the gap. Corridors are 10-31 m and the authored piece is one
        // length, so it is scaled along its run rather than tiled
        static void Run(DungeonBuildResult result, DungeonTypeDefinition type, Vector3 from, Vector3 to, Transform owner)
        {
            Vector3 span = Flat(to - from);
            float length = span.magnitude;

            if (length < 0.05f)
                return;

            RoomModule module = type.CorridorStraight.Prefab.GetComponent<RoomModule>();
            float authored = Vector3.Distance(module.LocalPositionOf(module.Connectors[0]),
                module.LocalPositionOf(module.Connectors[1]));

            GameObject corridor = Piece(result, type.CorridorStraight.Prefab, (from + to) * 0.5f,
                Quaternion.LookRotation(span), null);

            float stretch = length / authored;
            corridor.transform.localScale = new Vector3(1f, 1f, stretch);
            KeepFittings(corridor.transform, stretch, length >= LitRunMetres);

            corridor.transform.SetParent(owner, true);
        }

        static GameObject Piece(DungeonBuildResult result, GameObject prefab, Vector3 localPosition,
            Quaternion localRotation, Transform owner)
        {
            GameObject piece = Object.Instantiate(prefab, result.Root.transform);
            piece.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            piece.name = $"Corridor_{result.Corridors.Count}";
            result.Corridors.Add(piece);

            if (owner != null)
                piece.transform.SetParent(owner, true);

            return piece;
        }

        // The corridor stretches, the torches on its walls should not
        static void KeepFittings(Transform corridor, float stretch, bool lit)
        {
            HashSet<Transform> done = new HashSet<Transform>();

            foreach (Light light in corridor.GetComponentsInChildren<Light>(true))
            {
                Transform fitting = light.transform;
                while (fitting.parent != corridor)
                    fitting = fitting.parent;

                if (!done.Add(fitting))
                    continue;

                if (!lit)
                {
                    Discard(fitting.gameObject);
                    continue;
                }

                Vector3 along = Quaternion.Inverse(fitting.localRotation) * Vector3.forward;
                Vector3 scale = fitting.localScale;

                if (Mathf.Abs(along.x) >= Mathf.Abs(along.y) && Mathf.Abs(along.x) >= Mathf.Abs(along.z))
                    scale.x /= stretch;
                else if (Mathf.Abs(along.y) >= Mathf.Abs(along.z))
                    scale.y /= stretch;
                else
                    scale.z /= stretch;

                fitting.localScale = scale;
            }
        }

        // Build is driven from edit mode tooling as well as from Play, and Destroy is a no op outside Play
        public static void Discard(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        static void Mate(RoomModule module, ModuleConnector entry, Vector3 targetPos, Quaternion targetRot,
            out Vector3 position, out Quaternion rotation)
        {
            Quaternion facing = targetRot * Quaternion.Euler(0f, 180f, 0f);
            rotation = facing * Quaternion.Inverse(module.LocalRotationOf(entry));
            position = targetPos - rotation * module.LocalPositionOf(entry);
        }

        static void ConnectorPose(RoomModule module, ModuleConnector connector, Vector3 position,
            Quaternion rotation, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = position + rotation * module.LocalPositionOf(connector);
            worldRot = rotation * module.LocalRotationOf(connector);
        }

        // One door per connection, at the parent's doorway. A locked edge takes the key its layout
        // already placed, which is what gives the key pickups something to open
        static void HangDoor(DungeonTypeDefinition type, DungeonBuildResult result, ModuleConnector mouth,
            DungeonEdge edge, Transform owner)
        {
            if (type.Door == null || mouth == null)
                return;

            // A secret edge gets a panel that reads as wall, not a door announcing itself
            if (edge.Kind == EdgeKind.Secret && type.SecretDoor != null)
            {
                GameObject hidden = Object.Instantiate(type.SecretDoor, mouth.transform.position,
                    mouth.transform.rotation, owner);

                hidden.name = $"Secret_{result.Secrets.Count}";

                SecretDoor secret = hidden.GetComponent<SecretDoor>();
                if (secret != null)
                    result.Secrets.Add(secret);

                return;
            }

            GameObject instance = Object.Instantiate(type.Door, mouth.transform.position,
                mouth.transform.rotation, owner);

            Door door = instance.GetComponent<Door>();
            if (door == null)
                return;

            bool locked = type.LockDoors && edge.Kind == EdgeKind.Locked;
            door.SetKey(locked ? edge.KeyId : -1);
            instance.name = locked ? $"Door_Locked_{edge.KeyId}" : $"Door_{result.Doors.Count}";
            result.Doors.Add(door);
        }

        // Every doorway the layout did not use is still a hole in an outside wall
        // The jambs and lintel around a doorway are trim coloured, so a doorway filled with wall still
        // showed as a door. The room prefab names each side's wall and connector by the same number
        static void MatchFrame(Transform room, ModuleConnector connector, GameObject plug)
        {
            Transform frame = room.Find(connector.name.Replace("Connector_", "Wall_"));
            Renderer filler = plug.GetComponentInChildren<Renderer>();

            if (frame == null || filler == null)
                return;

            foreach (Renderer piece in frame.GetComponentsInChildren<Renderer>())
                piece.sharedMaterial = filler.sharedMaterial;
        }

        static IEnumerable Seal(DungeonTypeDefinition type, DungeonBuildResult result, Dictionary<int, HashSet<int>> used)
        {
            if (type.DoorPlug == null)
                yield break;

            foreach (KeyValuePair<int, GameObject> pair in result.Rooms)
            {
                IReadOnlyList<ModuleConnector> connectors = pair.Value.GetComponent<RoomModule>().Connectors;

                for (int i = 0; i < connectors.Count; i++)
                {
                    if (connectors[i] == null || used[pair.Key].Contains(i))
                        continue;

                    GameObject plug = Object.Instantiate(type.DoorPlug, connectors[i].transform.position,
                        connectors[i].transform.rotation, pair.Value.transform);

                    plug.name = $"Seal_{result.Seals.Count}";
                    result.Seals.Add(plug);
                    MatchFrame(pair.Value.transform, connectors[i], plug);
                }

                yield return null;
            }
        }
    }
}
