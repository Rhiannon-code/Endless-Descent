using System.Collections.Generic;
using System;
using EndlessDescent.Core;
using EndlessDescent.Data;

namespace EndlessDescent.Dungeons
{
    public static class DungeonLayoutGenerator
    {
        const int MaxAttempts = 8;
        const int AttemptSeedStride = 7919;

        public static DungeonGraph Generate(DungeonLayoutSettings settings, int seed, out string failure)
        {
            failure = null;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Retries derive their seed from the original, so a given seed still always
                // produces the same dungeon
                DungeonGraph graph = BuildOnce(settings, seed + attempt * AttemptSeedStride);

                if (DungeonSolvability.Verify(graph, out failure))
                    return graph;
            }

            failure = $"no solvable layout after {MaxAttempts} attempts: {failure}";
            return null;
        }

        static DungeonGraph BuildOnce(DungeonLayoutSettings settings, int seed)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);
            DungeonGraph graph = new DungeonGraph { Seed = seed };

            List<int> main = new List<int>();
            List<int> mainEdges = new List<int>();
            BuildMainPath(graph, settings, ref rng, main, mainEdges);

            List<int> gateEdgeIndices = ChooseGateEdges(settings, ref rng, mainEdges.Count);
            PlaceGatesAndKeys(graph, settings, ref rng, main, mainEdges, gateEdgeIndices);
            AddOptionalBranches(graph, settings, ref rng, main);
            EnsureDescents(graph, settings, ref rng, main);
            AddShortcut(graph, settings, ref rng, main);
            ReconnectDeadEnds(graph, settings, ref rng);
            AddLoops(graph, settings, ref rng);

            // Branches can drop below the spine's deepest room, so the count is taken from the graph
            int deepest = 0;
            foreach (DungeonNode node in graph.Nodes)
                deepest = Math.Max(deepest, node.Level);

            graph.LevelCount = deepest + 1;

            return graph;
        }

        static void BuildMainPath(DungeonGraph graph, DungeonLayoutSettings settings, ref DeterministicRandom rng,
            List<int> main, List<int> mainEdges)
        {
            int length = Math.Max(3, RangeInclusive(ref rng, settings.MinMainRooms, settings.MaxMainRooms));

            DungeonNode entrance = graph.AddNode(RoomRole.Entrance, 0);
            entrance.OnMainPath = true;
            graph.EntranceId = entrance.Id;
            main.Add(entrance.Id);

            // The main path is what descends, branches stay on the level they hang off, so each
            // level is a place rather than a corridor you pass through
            int level = 0;
            int roomsOnLevel = 1;
            int levelTarget = NextLevelSize(settings, ref rng);

            for (int i = 1; i < length; i++)
            {
                RoomRole role = i == length - 1 ? RoomRole.Boss : RoomRole.Junction;
                DungeonNode node = graph.AddNode(role, i);

                if (roomsOnLevel >= levelTarget)
                {
                    level++;
                    roomsOnLevel = 0;
                    levelTarget = NextLevelSize(settings, ref rng);
                }

                node.Level = level;
                node.OnMainPath = true;
                roomsOnLevel++;

                mainEdges.Add(graph.Connect(main[i - 1], node.Id, EdgeKind.Open, true).Id);
                main.Add(node.Id);
            }

            graph.LevelCount = level + 1;

            graph.BossId = main[main.Count - 1];
        }

        // Two sizes of floor: one you can get lost in, and one that is a landing between them
        static int NextLevelSize(DungeonLayoutSettings settings, ref DeterministicRandom rng) =>
            rng.Chance(settings.BigLevelChance)
                ? RangeInclusive(ref rng, settings.MinBigLevelRooms, settings.MaxBigLevelRooms)
                : RangeInclusive(ref rng, settings.MinRoomsPerLevel, settings.MaxRoomsPerLevel);

        // How many locked gates stand between the entrance and each room. Two rooms may only be
        // joined if this matches, a loop between rooms on opposite sides of a gate would be a way
        // round it, and the whole point of the gate is that there is no way round it
        static int[] GateDepths(DungeonGraph graph)
        {
            int[] depth = new int[graph.Nodes.Count];

            for (int i = 0; i < depth.Length; i++)
                depth[i] = -1;

            if (graph.EntranceId < 0)
                return depth;

            depth[graph.EntranceId] = 0;
            Queue<int> open = new Queue<int>();
            open.Enqueue(graph.EntranceId);

            while (open.Count > 0)
            {
                int at = open.Dequeue();

                foreach (int edgeId in graph.Node(at).EdgeIds)
                {
                    DungeonEdge edge = graph.Edge(edgeId);

                    if (edge.OneWay && edge.From != at)
                        continue;

                    int next = graph.Opposite(edge, at);
                    if (depth[next] >= 0) continue;

                    depth[next] = depth[at] + (edge.Kind == EdgeKind.Locked ? 1 : 0);
                    open.Enqueue(next);
                }
            }

            return depth;
        }

        static Dictionary<int, List<int>> RoomsByLevel(DungeonGraph graph)
        {
            Dictionary<int, List<int>> byLevel = new Dictionary<int, List<int>>();

            foreach (DungeonNode node in graph.Nodes)
            {
                if (!byLevel.TryGetValue(node.Level, out List<int> rooms))
                    byLevel[node.Level] = rooms = new List<int>();

                rooms.Add(node.Id);
            }

            return byLevel;
        }

        // Hops between two rooms, given up on beyond a limit. Only used to refuse joining rooms that
        // are already neighbours, which would make a loop that loops around nothing
        static int Hops(DungeonGraph graph, int from, int to, int limit)
        {
            Dictionary<int, int> seen = new Dictionary<int, int> { { from, 0 } };
            Queue<int> open = new Queue<int>();
            open.Enqueue(from);

            while (open.Count > 0)
            {
                int at = open.Dequeue();

                if (at == to)
                    return seen[at];

                if (seen[at] >= limit)
                    continue;

                foreach (int edgeId in graph.Node(at).EdgeIds)
                {
                    int next = graph.Opposite(graph.Edge(edgeId), at);

                    if (seen.ContainsKey(next)) continue;

                    seen[next] = seen[at] + 1;
                    open.Enqueue(next);
                }
            }

            return int.MaxValue;
        }

        // A branch that ends in a room with one door is a culdesac. Most of them are joined back
        // into their own floor instead, which turns them from somewhere you must walk out of into
        // another way round. Secrets and set pieces are left alone, a reward you stumble on is
        // supposed to be a pocket, not a corridor
        static void ReconnectDeadEnds(DungeonGraph graph, DungeonLayoutSettings settings,
            ref DeterministicRandom rng)
        {
            int[] gate = GateDepths(graph);
            Dictionary<int, List<int>> byLevel = RoomsByLevel(graph);

            List<int> tips = new List<int>();

            foreach (DungeonNode node in graph.Nodes)
            {
                if (node.Id == graph.EntranceId || node.EdgeIds.Count != 1) continue;
                if (node.Role == RoomRole.Secret || node.Role == RoomRole.SetPiece) continue;

                tips.Add(node.Id);
            }

            foreach (int tip in tips)
            {
                if (!rng.Chance(settings.DeadEndReconnectChance)) continue;

                if (TryJoin(graph, settings, byLevel, gate, tip, settings.MinLoopSpan, ref rng))
                    continue;
            }
        }

        // Extra ways round, on top of the reconnected tips
        static void AddLoops(DungeonGraph graph, DungeonLayoutSettings settings,
            ref DeterministicRandom rng)
        {
            int[] gate = GateDepths(graph);
            Dictionary<int, List<int>> byLevel = RoomsByLevel(graph);

            int count = graph.Nodes.Count;

            for (int id = 0; id < count; id++)
            {
                if (!rng.Chance(settings.LoopChance)) continue;

                TryJoin(graph, settings, byLevel, gate, id, settings.MinLoopSpan, ref rng);
            }
        }

        static bool TryJoin(DungeonGraph graph, DungeonLayoutSettings settings,
            Dictionary<int, List<int>> byLevel, int[] gate, int from, int minSpan,
            ref DeterministicRandom rng)
        {
            DungeonNode node = graph.Node(from);

            if (node.EdgeIds.Count >= settings.MaxRoomExits || gate[from] < 0)
                return false;

            if (!byLevel.TryGetValue(node.Level, out List<int> peers))
                return false;

            int start = rng.NextInt(0, peers.Count);

            for (int i = 0; i < peers.Count; i++)
            {
                int other = peers[(start + i) % peers.Count];

                if (other == from) continue;
                if (gate[other] != gate[from]) continue;
                if (graph.Node(other).EdgeIds.Count >= settings.MaxRoomExits) continue;
                if (Hops(graph, from, other, minSpan) < minSpan) continue;

                graph.Connect(from, other, EdgeKind.Open, false);
                return true;
            }

            return false;
        }

        static List<int> ChooseGateEdges(DungeonLayoutSettings settings, ref DeterministicRandom rng, int mainEdgeCount)
        {
            int target = RangeInclusive(ref rng, settings.MinGates, settings.MaxGates);

            List<int> candidates = new List<int>();
            // Never lock the first edge, there must be at least one room before a gate to hang its key branch on
            for (int i = 1; i < mainEdgeCount; i++)
                candidates.Add(i);

            rng.Shuffle(candidates);

            List<int> chosen = new List<int>();
            foreach (int candidate in candidates)
            {
                if (chosen.Count >= target)
                    break;

                bool spaced = true;
                foreach (int gate in chosen)
                {
                    if (Math.Abs(gate - candidate) < settings.MinRoomsBetweenGates)
                    {
                        spaced = false;
                        break;
                    }
                }

                if (spaced)
                    chosen.Add(candidate);
            }

            chosen.Sort();
            return chosen;
        }

        static void PlaceGatesAndKeys(DungeonGraph graph, DungeonLayoutSettings settings, ref DeterministicRandom rng,
            List<int> main, List<int> mainEdges, List<int> gateEdgeIndices)
        {
            for (int keyId = 0; keyId < gateEdgeIndices.Count; keyId++)
            {
                int gateIndex = gateEdgeIndices[keyId];

                DungeonEdge gate = graph.Edge(mainEdges[gateIndex]);
                gate.Kind = EdgeKind.Locked;
                gate.KeyId = keyId;

                // Anchoring the key branch on a room at or before the gate's near side is what makes
                // the key reachable without crossing its own gate
                if (!TryPickAnchor(graph, main, 0, gateIndex + 1, settings.MaxRoomExits, ref rng, out int anchor))
                    continue;

                int length = Math.Max(1, RangeInclusive(ref rng, settings.MinKeyBranch, settings.MaxKeyBranch));

                DungeonNode last = Extend(graph, anchor, length, RoomRole.Key, EdgeKind.Open, ref rng);
                last.ContainsKeyId = keyId;
            }

            graph.KeyCount = gateEdgeIndices.Count;
        }

        static void AddOptionalBranches(DungeonGraph graph, DungeonLayoutSettings settings, ref DeterministicRandom rng,
            List<int> main)
        {
            int count = RangeInclusive(ref rng, settings.MinExtraBranches, settings.MaxExtraBranches);

            for (int i = 0; i < count; i++)
            {
                if (!TryPickAnchor(graph, main, 0, main.Count - 1, settings.MaxRoomExits, ref rng, out int anchor))
                    continue;

                int length = Math.Max(1, RangeInclusive(ref rng, settings.MinExtraBranchLength, settings.MaxExtraBranchLength));

                bool secret = rng.Chance(settings.SecretChance);

                RoomRole endRole;
                if (secret)
                    endRole = RoomRole.Secret;
                else if (rng.Chance(settings.SetPieceChance))
                    endRole = RoomRole.SetPiece;
                else
                    endRole = RoomRole.Treasure;

                EdgeKind lastEdge = secret ? EdgeKind.Secret : EdgeKind.Open;
                int drop = rng.Chance(settings.BranchDescendChance) ? 1 : 0;
                Extend(graph, anchor, length, endRole, lastEdge, ref rng, drop);
            }
        }

        // Optional branches descend by chance, which leaves the occasional floor with a single way
        // down. This tops each floor up to MinStairsPerLevel, anchoring on spine rooms spaced along
        // the floor so the stairs are not all in one corner
        static void EnsureDescents(DungeonGraph graph, DungeonLayoutSettings settings, ref DeterministicRandom rng,
            List<int> main)
        {
            Dictionary<int, List<int>> spineByLevel = new Dictionary<int, List<int>>();
            int deepest = 0;

            foreach (int id in main)
            {
                int level = graph.Node(id).Level;
                deepest = Math.Max(deepest, level);

                if (!spineByLevel.TryGetValue(level, out List<int> rooms))
                    spineByLevel[level] = rooms = new List<int>();

                rooms.Add(id);
            }

            foreach (KeyValuePair<int, List<int>> pair in spineByLevel)
            {
                if (pair.Key >= deepest)
                    continue;

                int have = CountDescents(graph, pair.Value);
                int want = settings.MinStairsPerLevel - have;
                if (want <= 0)
                    continue;

                // Walked at a stride so the added stairs land at opposite ends of the floor rather
                // than next to each other
                List<int> usable = new List<int>();
                foreach (int id in pair.Value)
                {
                    if (TreeDegree(graph, id) < settings.MaxRoomExits)
                        usable.Add(id);
                }

                if (usable.Count == 0)
                    continue;

                int stride = Math.Max(1, usable.Count / Math.Max(1, want));

                for (int i = 0, added = 0; added < want && i < usable.Count; i += stride, added++)
                {
                    int anchor = usable[i];
                    if (TreeDegree(graph, anchor) >= settings.MaxRoomExits)
                        continue;

                    int length = Math.Max(2, RangeInclusive(ref rng, settings.MinExtraBranchLength,
                        settings.MaxExtraBranchLength));

                    Extend(graph, anchor, length, RoomRole.Treasure, EdgeKind.Open, ref rng, 1);
                }
            }
        }

        static int CountDescents(DungeonGraph graph, List<int> rooms)
        {
            int count = 0;

            foreach (int id in rooms)
            {
                foreach (int edgeId in graph.Node(id).EdgeIds)
                {
                    DungeonEdge edge = graph.Edge(edgeId);
                    if (!edge.IsSpanningTree)
                        continue;

                    int other = graph.Opposite(edge, id);
                    if (graph.Node(other).Level > graph.Node(id).Level)
                        count++;
                }
            }

            return count;
        }

        static void AddShortcut(DungeonGraph graph, DungeonLayoutSettings settings, ref DeterministicRandom rng,
            List<int> main)
        {
            if (main.Count < settings.MinShortcutSpan + 1 || !rng.Chance(settings.ShortcutChance))
                return;

            int near = rng.NextInt(0, main.Count - settings.MinShortcutSpan);
            int far = rng.NextInt(near + settings.MinShortcutSpan, main.Count);

            // One way, deep end back towards the entrance. That is what lets a shortcut cross a
            // gate without bypassing it, reaching the far end still requires the key
            graph.Connect(main[far], main[near], EdgeKind.Shortcut, false, oneWay: true);
        }

        static DungeonNode Extend(DungeonGraph graph, int anchor, int length, RoomRole endRole, EdgeKind lastEdgeKind,
            ref DeterministicRandom rng, int levelDelta = 0)
        {
            int current = anchor;
            DungeonNode last = null;

            for (int i = 0; i < length; i++)
            {
                bool isLast = i == length - 1;
                RoomRole role = isLast ? endRole : RoomRole.Junction;
                EdgeKind kind = isLast ? lastEdgeKind : EdgeKind.Open;

                DungeonNode node = graph.AddNode(role, graph.Node(current).Depth + 1);
                node.Level = graph.Node(current).Level + (i == 0 ? levelDelta : 0);
                graph.Connect(current, node.Id, kind, true);

                current = node.Id;
                last = node;
            }

            return last;
        }

        // A room can only carry as many connections as its module has faces, so a branch may only
        // anchor on a room that still has a spare one. Callers skip the branch when none is left
        static bool TryPickAnchor(DungeonGraph graph, List<int> main, int from, int toExclusive, int maxExits,
            ref DeterministicRandom rng, out int anchor)
        {
            List<int> spare = new List<int>();

            for (int i = from; i < toExclusive; i++)
            {
                if (TreeDegree(graph, main[i]) < maxExits)
                    spare.Add(main[i]);
            }

            if (spare.Count == 0)
            {
                anchor = -1;
                return false;
            }

            anchor = spare[rng.NextInt(0, spare.Count)];
            return true;
        }

        static int TreeDegree(DungeonGraph graph, int nodeId)
        {
            int degree = 0;

            foreach (int edgeId in graph.Node(nodeId).EdgeIds)
            {
                if (graph.Edge(edgeId).IsSpanningTree)
                    degree++;
            }

            return degree;
        }

        static int RangeInclusive(ref DeterministicRandom rng, int a, int b)
        {
            int min = Math.Min(a, b);
            int max = Math.Max(a, b);
            return rng.NextInt(min, max + 1);
        }
    }
}
