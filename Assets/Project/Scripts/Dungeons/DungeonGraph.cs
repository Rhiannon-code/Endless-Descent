using System.Collections.Generic;
using EndlessDescent.Data;

namespace EndlessDescent.Dungeons
{
    public enum EdgeKind
    {
        Open,
        Locked,
        Secret,
        Shortcut
    }

    public sealed class DungeonNode
    {
        public int Id;
        public RoomRole Role;
        public int Depth;
        public int ContainsKeyId = -1;
        public readonly List<int> EdgeIds = new List<int>();
        public int Level;
        public bool OnMainPath;
    }

    public sealed class DungeonEdge
    {
        public int Id;
        public int From;
        public int To;
        public EdgeKind Kind;
        public int KeyId = -1;
        public bool IsSpanningTree;
        public bool OneWay;
    }

    public sealed class DungeonGraph
    {
        readonly List<DungeonNode> nodes = new List<DungeonNode>();
        readonly List<DungeonEdge> edges = new List<DungeonEdge>();

        public int Seed;
        public int EntranceId = -1;
        public int BossId = -1;
        public int KeyCount;
        public int LevelCount = 1;

        public IReadOnlyList<DungeonNode> Nodes => nodes;
        public IReadOnlyList<DungeonEdge> Edges => edges;

        public DungeonNode Node(int id) => nodes[id];
        public DungeonEdge Edge(int id) => edges[id];

        public DungeonNode AddNode(RoomRole role, int depth)
        {
            DungeonNode node = new DungeonNode { Id = nodes.Count, Role = role, Depth = depth };
            nodes.Add(node);
            return node;
        }

        public DungeonEdge Connect(int from, int to, EdgeKind kind, bool spanningTree, int keyId = -1, bool oneWay = false)
        {
            DungeonEdge edge = new DungeonEdge
            {
                Id = edges.Count,
                From = from,
                To = to,
                Kind = kind,
                KeyId = keyId,
                IsSpanningTree = spanningTree,
                OneWay = oneWay
            };

            edges.Add(edge);
            nodes[from].EdgeIds.Add(edge.Id);
            nodes[to].EdgeIds.Add(edge.Id);
            return edge;
        }

        public int Opposite(DungeonEdge edge, int fromNode) => edge.From == fromNode ? edge.To : edge.From;
    }
}
