using System.Collections.Generic;
using System.Threading;
using EndlessDescent.Core;
using EndlessDescent.Data;

namespace EndlessDescent.Dungeons
{
    // A room module as the grid sees it: which faces have a doorway, and how far it reaches from its
    // centre. Sides are 0 = +Z, 1 = +X, 2 = -Z, 3 = -X, before any turn
    public readonly struct GridModule
    {
        public readonly int Doors;
        public readonly float Reach;
        public readonly int Weight;
        public readonly int Roles;

        public GridModule(int doors, float reach, int weight, int roles)
        {
            Doors = doors;
            Reach = reach;
            Weight = weight;
            Roles = roles;
        }

        public bool Serves(RoomRole role) => (Roles & (1 << (int)role)) != 0;
    }

    // Every room gets a cell of its own before any geometry exists. A door on a side leads
    // to the neighbouring cell that way, level or one storey down, so the only thing left for the
    // builder to decide is how long each corridor is. Placing rooms greedily in open space boxed itself
    // in at about fifty rooms; this found a layout for 500 dungeons of 445-1,234 rooms out of 500
    public sealed class DungeonGridLayout
    {
        public const float Cell = 38f;
        public const float Storey = 5f;
        // Half the widest room a cell holds. The 10 m left between two cells is where stairs go, and
        // no room can reach into it, so a stair never runs under or over a room
        public const float Envelope = 14f;
        public const long StepBudget = 1_000_000;

        public static readonly int[] StepX = { 0, 1, 0, -1 };
        public static readonly int[] StepY = { 1, 0, -1, 0 };

        public readonly int[] X, Y, Level, Turns, Module, Parent;
        public int LevelCount { get; private set; } = 1;
        public long Steps { get; private set; }
        public int CrossingsRefused { get; private set; }

        readonly DungeonGraph graph;
        readonly IReadOnlyList<GridModule> modules;
        readonly List<int>[] children;
        readonly Dictionary<long, int> cells = new Dictionary<long, int>();
        readonly List<long> log = new List<long>();
        DeterministicRandom rng;

        public IReadOnlyList<int> ChildrenOf(int node) => children[node];

        // Which side of the parent's cell a room sits on
        public int SideFromParent(int node)
        {
            int p = Parent[node];
            int dx = X[node] - X[p], dy = Y[node] - Y[p];
            return dy == 1 ? 0 : dx == 1 ? 1 : dy == -1 ? 2 : 3;
        }

        DungeonGridLayout(DungeonGraph layoutGraph, IReadOnlyList<GridModule> moduleSet)
        {
            graph = layoutGraph;
            modules = moduleSet;

            int n = graph.Nodes.Count;
            X = new int[n]; Y = new int[n]; Level = new int[n]; Turns = new int[n]; Module = new int[n];
            Parent = new int[n];
            children = TreeChildren(graph, Parent);
            rng = new DeterministicRandom(graph.Seed);
        }

        public static DungeonGridLayout Solve(DungeonGraph graph, IReadOnlyList<GridModule> modules, out string failure)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].Reach > Envelope + 0.01f)
                {
                    failure = $"module {i} reaches {modules[i].Reach:0.#} m from its centre and a cell holds {Envelope} m";
                    return null;
                }
            }

            DungeonGridLayout layout = new DungeonGridLayout(graph, modules);
            bool found = false;

            // The search recurses once per room along the deepest branch, several frames a room, and a
            // thread's default stack is not sized for a dungeon of a thousand rooms
            Thread search = new Thread(() => found = layout.Place(graph.EntranceId, 0, 0, 0, -1), 64 * 1024 * 1024);
            search.Start();
            search.Join();

            if (!found)
            {
                failure = layout.Steps > StepBudget
                    ? $"no grid layout found within {StepBudget} search steps"
                    : "no grid layout exists for this module set";
                return null;
            }

            for (int i = 0; i < layout.Level.Length; i++)
                layout.LevelCount = System.Math.Max(layout.LevelCount, layout.Level[i] + 1);

            failure = null;
            return layout;
        }

        static List<int>[] TreeChildren(DungeonGraph graph, int[] parent)
        {
            List<int>[] children = new List<int>[graph.Nodes.Count];
            for (int i = 0; i < children.Length; i++) children[i] = new List<int>();

            bool[] seen = new bool[graph.Nodes.Count];
            Queue<int> open = new Queue<int>();
            open.Enqueue(graph.EntranceId);
            seen[graph.EntranceId] = true;
            parent[graph.EntranceId] = -1;

            while (open.Count > 0)
            {
                int at = open.Dequeue();

                foreach (int e in graph.Node(at).EdgeIds)
                {
                    DungeonEdge edge = graph.Edge(e);
                    if (!edge.IsSpanningTree) continue;

                    int next = graph.Opposite(edge, at);
                    if (seen[next]) continue;

                    seen[next] = true;
                    parent[next] = at;
                    children[at].Add(next);
                    open.Enqueue(next);
                }
            }

            return children;
        }

        // Cells on even coordinates and the strips between them on odd ones, so a stair's strip is
        // reserved in the same table as the rooms and rolled back with them
        static long Key(int x, int y, int level) =>
            ((long)(x + (1 << 20)) << 42) | ((long)(y + (1 << 20)) << 21) | (long)(level + (1 << 20));

        static long CellKey(int x, int y, int level) => Key(2 * x, 2 * y, 2 * level);

        void Occupy(long key, int node)
        {
            cells[key] = node;
            log.Add(key);
        }

        void Rollback(int mark)
        {
            for (int i = log.Count - 1; i >= mark; i--) cells.Remove(log[i]);
            log.RemoveRange(mark, log.Count - mark);
        }

        bool Place(int v, int x, int y, int level, int entry)
        {
            if (++Steps > StepBudget) return false;

            RoomRole role = v == graph.EntranceId ? RoomRole.Entrance : graph.Node(v).Role;
            int degree = children[v].Count + (entry >= 0 ? 1 : 0);

            foreach (int m in Candidates(role, degree))
            for (int turns = 0; turns < 4; turns++)
            {
                int doors = Turn(modules[m].Doors, turns);
                if (entry >= 0 && (doors & (1 << entry)) == 0) continue;

                int mark = log.Count;
                X[v] = x; Y[v] = y; Level[v] = level; Module[v] = m; Turns[v] = turns;
                Occupy(CellKey(x, y, level), v);

                if (Assign(v, entry >= 0 ? doors & ~(1 << entry) : doors, 0, new int[children[v].Count]))
                    return true;

                Rollback(mark);
                if (Steps > StepBudget) return false;
            }

            return false;
        }

        static int Turn(int doors, int turns)
        {
            int turned = 0;
            for (int side = 0; side < 4; side++)
                if ((doors & (1 << side)) != 0) turned |= 1 << ((side + turns) % 4);
            return turned;
        }

        static int DoorCount(int doors)
        {
            int count = 0;
            for (; doors != 0; doors &= doors - 1) count++;
            return count;
        }

        // Weighted, in a shuffled order, each module once
        List<int> Candidates(RoomRole role, int degree)
        {
            List<int> weighted = new List<int>();

            for (int m = 0; m < modules.Count; m++)
            {
                if (!modules[m].Serves(role) || DoorCount(modules[m].Doors) < degree) continue;
                for (int w = 0; w < System.Math.Max(1, modules[m].Weight); w++) weighted.Add(m);
            }

            rng.Shuffle(weighted);

            List<int> order = new List<int>();
            foreach (int m in weighted)
                if (!order.Contains(m)) order.Add(m);

            return order;
        }

        // Each child gets a free side and a cell. A child boxed in on its own level may take the cell
        // one level down instead, that adds a stair the layout did not ask for, and is what lets a
        // compact dungeon be found at all
        bool Assign(int v, int free, int i, int[] sides)
        {
            if (i == children[v].Count) return Subtrees(v, 0, sides);

            int c = children[v][i];
            int natural = graph.Node(c).Level != graph.Node(v).Level ? 1 : 0;

            for (int drop = natural; drop <= 1; drop++)
            foreach (int s in ShuffledSides())
            {
                if ((free & (1 << s)) == 0) continue;

                long key = CellKey(X[v] + StepX[s], Y[v] + StepY[s], Level[v] + drop);
                if (cells.ContainsKey(key)) continue;

                long strip = Key(2 * X[v] + StepX[s], 2 * Y[v] + StepY[s], 2 * Level[v] + 1);

                // Two stairs crossing in one strip would pass through each other
                if (drop == 1 && cells.ContainsKey(strip))
                {
                    CrossingsRefused++;
                    continue;
                }

                int mark = log.Count;
                Occupy(key, c);
                if (drop == 1) Occupy(strip, c);
                sides[i] = s + 4 * drop;

                if (Assign(v, free & ~(1 << s), i + 1, sides)) return true;
                Rollback(mark);
            }

            return false;
        }

        int[] ShuffledSides()
        {
            int[] sides = { 0, 1, 2, 3 };
            rng.Shuffle(sides);
            return sides;
        }

        bool Subtrees(int v, int i, int[] sides)
        {
            if (i == children[v].Count) return true;

            int c = children[v][i], s = sides[i] % 4;
            int x = X[v] + StepX[s], y = Y[v] + StepY[s], level = Level[v] + sides[i] / 4;
            long key = CellKey(x, y, level);

            int mark = log.Count;
            cells.Remove(key);

            if (Place(c, x, y, level, (s + 2) % 4) && Subtrees(v, i + 1, sides)) return true;

            Rollback(mark);
            cells[key] = c;
            return false;
        }
    }
}
