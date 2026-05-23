using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.Navigation
{

    public static class Pathfinder
    {
        private const float Infinity = float.MaxValue;

        public static List<MarkerNode> FindPath(NavGraph graph, string startNodeID, string goalNodeID)
        {
            // Validation
            if (graph == null)
            {
                Debug.LogError("[Pathfinder] NavGraph is null.");
                return new List<MarkerNode>();
            }

            if (string.IsNullOrEmpty(startNodeID) || string.IsNullOrEmpty(goalNodeID))
            {
                Debug.LogError("[Pathfinder] Start or goal nodeID is null/empty.");
                return new List<MarkerNode>();
            }

            if (startNodeID == goalNodeID)
            {
                var single = graph.GetNode(startNodeID);
                return single != null ? new List<MarkerNode> { single } : new List<MarkerNode>();
            }

            // Initialise Dijkstra structures
            var dist     = new Dictionary<string, float>();   // Best known cost to each node
            var prev     = new Dictionary<string, string>();  // Previous node on best path
            var visited  = new HashSet<string>();

            foreach (var id in graph.AllNodeIDs())
            {
                dist[id] = Infinity;
                prev[id] = null;
            }

            if (!dist.ContainsKey(startNodeID))
            {
                Debug.LogError($"[Pathfinder] Start node '{startNodeID}' not found in graph.");
                return new List<MarkerNode>();
            }

            if (!dist.ContainsKey(goalNodeID))
            {
                Debug.LogError($"[Pathfinder] Goal node '{goalNodeID}' not found in graph.");
                return new List<MarkerNode>();
            }

            dist[startNodeID] = 0f;

            // Main Dijkstra Loop
            while (true)
            {
                // Extract unvisited node with minimum distance (linear scan — O(V) per iteration)
                string u = ExtractMinNode(dist, visited);
                if (u == null) break;               // All reachable nodes visited
                if (u == goalNodeID) break;          // Shortest path to goal found
                if (dist[u] >= Infinity) break;      // Remaining nodes are unreachable

                visited.Add(u);

                // Relax all edges from u
                foreach (var edge in graph.GetEdgesFrom(u))
                {
                    string v = edge.toNodeID;
                    if (visited.Contains(v)) continue;
                    if (!dist.ContainsKey(v))
                    {
                        Debug.LogWarning($"[Pathfinder] Edge points to unknown node '{v}' — skipped.");
                        continue;
                    }

                    float alt = dist[u] + edge.cost;
                    if (alt < dist[v])
                    {
                        dist[v] = alt;
                        prev[v] = u;
                    }
                }
            }

            // Reconstruct Path
            return ReconstructPath(graph, prev, startNodeID, goalNodeID);
        }

        // Private Helpers
        /// <summary>
        /// Linear scan to find the unvisited node with the smallest tentative distance.
        /// Returns null when all nodes are visited.
        /// </summary>
        private static string ExtractMinNode(Dictionary<string, float> dist, HashSet<string> visited)
        {
            string minNode  = null;
            float  minCost  = Infinity;

            foreach (var kvp in dist)
            {
                if (visited.Contains(kvp.Key)) continue;
                if (kvp.Value < minCost)
                {
                    minCost  = kvp.Value;
                    minNode  = kvp.Key;
                }
            }

            return minNode;
        }

        /// <summary>
        /// Walks the prev dictionary backwards from goal to start to build the ordered path.
        /// Returns empty list if goal was unreachable.
        /// </summary>
        private static List<MarkerNode> ReconstructPath(
            NavGraph graph,
            Dictionary<string, string> prev,
            string startNodeID,
            string goalNodeID)
        {
            var path = new List<MarkerNode>();

            // Check the goal was actually reached
            if (prev[goalNodeID] == null && goalNodeID != startNodeID)
            {
                Debug.LogWarning($"[Pathfinder] No path found from '{startNodeID}' to '{goalNodeID}'.");
                return path;
            }

            // Walk backwards from goal to start
            string current = goalNodeID;
            while (current != null)
            {
                var node = graph.GetNode(current);
                if (node != null)
                    path.Add(node);
                else
                    Debug.LogWarning($"[Pathfinder] Node '{current}' in path not found in graph.");

                prev.TryGetValue(current, out current);
            }

            // Reverse to get start → goal order
            path.Reverse();

            // Sanity check: path should start at startNodeID
            if (path.Count > 0 && path[0].nodeID != startNodeID)
            {
                Debug.LogError("[Pathfinder] Reconstructed path does not begin at startNodeID.");
                return new List<MarkerNode>();
            }

            return path;
        }
    }
}
