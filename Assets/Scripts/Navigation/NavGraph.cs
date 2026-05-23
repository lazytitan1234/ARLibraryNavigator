using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// ScriptableObject that stores the complete library navigation graph.
    ///
    /// Holds two serialized lists (nodes + edges) editable in the Inspector,
    /// and builds runtime adjacency list dictionaries in OnEnable() for fast lookup.
    ///
    /// Create via: Assets > Create > ARLibraryNav > NavGraph
    /// Save as: Assets/Scripts/Navigation/LibraryNavGraph.asset
    /// </summary>
    [CreateAssetMenu(fileName = "LibraryNavGraph", menuName = "ARLibraryNav/NavGraph")]
    public class NavGraph : ScriptableObject
    {
        // Serialized Data (edit in Inspector)
        public List<MarkerNode> nodes = new List<MarkerNode>();
        public List<NavEdge>    edges = new List<NavEdge>();

        // Runtime Lookups (rebuilt from serialized data, never serialized)
        private Dictionary<string, MarkerNode>       _nodeByID      = new Dictionary<string, MarkerNode>();
        private Dictionary<string, MarkerNode>       _nodeByMarker  = new Dictionary<string, MarkerNode>();
        private Dictionary<string, List<NavEdge>>    _adjacency     = new Dictionary<string, List<NavEdge>>();

        // Lifecycle
        private void OnEnable()
        {
            BuildLookups();
        }

        // Public API

        /// <summary>Returns the node with the given nodeID, or null if not found.</summary>
        public MarkerNode GetNode(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return null;
            _nodeByID.TryGetValue(nodeID, out var node);
            return node;
        }

        /// <summary>
        /// Returns the node whose markerName matches the Vuforia ImageTarget name.
        /// Used by MarkerLocalizationManager to translate tracking events to graph nodes.
        /// </summary>
        public MarkerNode GetNodeByMarkerName(string markerName)
        {
            if (string.IsNullOrEmpty(markerName)) return null;
            _nodeByMarker.TryGetValue(markerName, out var node);
            return node;
        }

        /// <summary>Returns all edges that originate from the given nodeID.</summary>
        public List<NavEdge> GetEdgesFrom(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return new List<NavEdge>();
            _adjacency.TryGetValue(nodeID, out var edgeList);
            return edgeList ?? new List<NavEdge>();
        }

        /// <summary>Returns all node IDs in the graph. Used by Pathfinder for initialisation.</summary>
        public IEnumerable<string> AllNodeIDs()
        {
            return _nodeByID.Keys;
        }

        /// <summary>
        /// Rebuilds all runtime lookup structures from the serialized nodes and edges lists.
        /// Call this if you modify nodes/edges at runtime (e.g. in Editor tools).
        /// </summary>
        public void BuildLookups()
        {
            _nodeByID.Clear();
            _nodeByMarker.Clear();
            _adjacency.Clear();

            foreach (var node in nodes)
            {
                if (string.IsNullOrEmpty(node.nodeID))
                {
                    Debug.LogWarning($"[NavGraph] Node with displayLabel '{node.displayLabel}' has an empty nodeID — skipped.");
                    continue;
                }

                if (_nodeByID.ContainsKey(node.nodeID))
                {
                    Debug.LogWarning($"[NavGraph] Duplicate nodeID '{node.nodeID}' — second entry skipped.");
                    continue;
                }

                _nodeByID[node.nodeID] = node;

                if (!string.IsNullOrEmpty(node.markerName))
                {
                    _nodeByMarker[node.markerName] = node;
                }

                _adjacency[node.nodeID] = new List<NavEdge>();
            }

            foreach (var edge in edges)
            {
                // Forward direction
                if (_adjacency.ContainsKey(edge.fromNodeID))
                {
                    _adjacency[edge.fromNodeID].Add(edge);
                }
                else
                {
                    Debug.LogWarning($"[NavGraph] Edge references unknown fromNodeID '{edge.fromNodeID}'.");
                }

                // Reverse direction for bidirectional edges
                if (edge.isBidirectional)
                {
                    if (_adjacency.ContainsKey(edge.toNodeID))
                    {
                        // Create a synthetic reversed edge so Pathfinder doesn't need to know about direction
                        var reversed = new NavEdge
                        {
                            fromNodeID      = edge.toNodeID,
                            toNodeID        = edge.fromNodeID,
                            cost            = edge.cost,
                            isBidirectional = false // already handled
                        };
                        _adjacency[edge.toNodeID].Add(reversed);
                    }
                    else
                    {
                        Debug.LogWarning($"[NavGraph] Bidirectional edge references unknown toNodeID '{edge.toNodeID}'.");
                    }
                }
            }
        }
    }
}
