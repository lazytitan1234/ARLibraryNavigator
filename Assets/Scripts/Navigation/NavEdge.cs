using System;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Represents a weighted edge between two MarkerNodes in the navigation graph.
    /// Cost is the real world distance in metres between the two nodes,
    /// calculated from their worldPosition coordinates during graph authoring.
    ///
    /// Pure data class no MonoBehaviour, fully serializable for NavGraph ScriptableObject.
    /// </summary>
    [Serializable]
    public class NavEdge
    {
        [UnityEngine.Tooltip("nodeID of the origin node.")]
        public string fromNodeID;

        [UnityEngine.Tooltip("nodeID of the destination node.")]
        public string toNodeID;

        [UnityEngine.Tooltip("Real-world walking distance in metres. " +
                             "Use the distance between node worldPositions as a baseline, " +
                             "then adjust upward for obstacles or indirect routes.")]
        public float cost;

        [UnityEngine.Tooltip("If true, this edge is traversable in both directions. " +
                             "Set false only for one-way connections (e.g. exit-only corridors).")]
        public bool isBidirectional = true;
    }
}
