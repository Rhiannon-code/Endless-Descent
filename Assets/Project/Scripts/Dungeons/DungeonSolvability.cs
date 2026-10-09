using System.Collections.Generic;

namespace EndlessDescent.Dungeons
{
    public static class DungeonSolvability
    {
        public static bool Verify(DungeonGraph graph, out string failure)
        {
            failure = null;

            if (graph == null || graph.EntranceId < 0 || graph.BossId < 0)
            {
                failure = "graph has no entrance or boss";
                return false;
            }

            HashSet<int> keys = new HashSet<int>();
            HashSet<int> reached = new HashSet<int> { graph.EntranceId };

            bool progress = true;
            while (progress)
            {
                progress = false;

                foreach (int id in reached)
                {
                    int keyId = graph.Node(id).ContainsKeyId;
                    if (keyId >= 0 && keys.Add(keyId))
                        progress = true;
                }

                List<int> newlyReached = new List<int>();
                foreach (DungeonEdge edge in graph.Edges)
                {
                    if (!CanTraverse(edge, keys))
                        continue;

                    bool fromReached = reached.Contains(edge.From);
                    bool toReached = reached.Contains(edge.To);

                    if (fromReached && !toReached)
                        newlyReached.Add(edge.To);
                    else if (toReached && !fromReached && !edge.OneWay)
                        newlyReached.Add(edge.From);
                }

                foreach (int id in newlyReached)
                {
                    if (reached.Add(id))
                        progress = true;
                }
            }

            if (!reached.Contains(graph.BossId))
            {
                failure = "boss room is unreachable";
                return false;
            }

            foreach (DungeonNode node in graph.Nodes)
            {
                if (!reached.Contains(node.Id))
                {
                    failure = $"room {node.Id} ({node.Role}) is unreachable";
                    return false;
                }
            }

            return true;
        }

        static bool CanTraverse(DungeonEdge edge, HashSet<int> keys)
        {
            return edge.Kind != EdgeKind.Locked || keys.Contains(edge.KeyId);
        }
    }
}
