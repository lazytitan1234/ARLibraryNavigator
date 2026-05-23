#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ARLibraryNav.Navigation;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Editor only Gizmo overlay that visualises the NavGraph on top of the floor plan
    /// in the Scene view.
    ///
    /// Usage:
    ///   1. Create an empty GameObject in the scene named "NavGraphGizmos".
    ///   2. Add Component → search "NavNodePlacer".
    ///   3. Drag LibraryNavGraph.asset into the Target Graph field.
    ///   4. Open the Scene view nodes appear as coloured spheres, edges as lines.
    ///   5. DISABLE or DELETE this GameObject before building the APK.
    ///
    /// Node colours:
    ///   Cyan   = regular navigation node
    ///   Yellow = stair / lift node (floor transition)
    ///
    /// Edge colours:
    ///   Green = bidirectional edge
    ///   Red   = one-way edge
    /// </summary>
    [ExecuteInEditMode]
    public class NavNodePlacer : MonoBehaviour
    {
        [Header("Graph Reference")]
        public NavGraph targetGraph;

        [Header("Gizmo Settings")]
        public float nodeRadius         = 0.4f;
        public Color regularNodeColor   = Color.cyan;
        public Color stairNodeColor     = Color.yellow;
        public Color bidirectionalColor = Color.green;
        public Color onewayColor        = Color.red;
        public bool  showLabels         = true;
        public float labelYOffset       = 0.6f;   // Height above sphere centre for label

        private void OnDrawGizmos()
        {
            if (targetGraph == null) return;

            // Rebuild lookups in case the asset was modified since the last draw
            targetGraph.BuildLookups();

            // Draw nodes
            foreach (var node in targetGraph.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeID)) continue;

                Vector3 pos = node.WorldPosition3D(nodeRadius);

                Gizmos.color = node.isStairNode ? stairNodeColor : regularNodeColor;
                Gizmos.DrawSphere(pos, nodeRadius);

                if (showLabels)
                {
                    Handles.color = Color.white;
                    Handles.Label(
                        pos + Vector3.up * labelYOffset,
                        $"{node.nodeID}\n({node.displayLabel})",
                        EditorStyles.miniLabel);
                }
            }

            // Draw edges
            foreach (var edge in targetGraph.edges)
            {
                if (string.IsNullOrEmpty(edge.fromNodeID) || string.IsNullOrEmpty(edge.toNodeID)) continue;

                var fromNode = targetGraph.GetNode(edge.fromNodeID);
                var toNode   = targetGraph.GetNode(edge.toNodeID);

                if (fromNode == null || toNode == null) continue;

                Gizmos.color = edge.isBidirectional ? bidirectionalColor : onewayColor;
                Gizmos.DrawLine(
                    fromNode.WorldPosition3D(nodeRadius),
                    toNode.WorldPosition3D(nodeRadius));

                // Cost label at midpoint
                if (showLabels)
                {
                    Vector3 mid = Vector3.Lerp(
                        fromNode.WorldPosition3D(nodeRadius),
                        toNode.WorldPosition3D(nodeRadius), 0.5f);

                    Handles.color = Color.white;
                    Handles.Label(mid, $"{edge.cost:F1}m", EditorStyles.miniLabel);
                }
            }
        }
    }
}
#endif
