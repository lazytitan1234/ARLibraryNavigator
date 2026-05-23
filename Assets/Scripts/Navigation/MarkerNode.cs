using System;
using UnityEngine;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Represents a single vertex in the library navigation graph.
    /// Pure data class no MonoBehaviour, fully serializable for use inside NavGraph ScriptableObject.
    ///
    /// worldPosition is (X, Z) in Unity world space metres.
    /// Y is always 0 for flat graph navigation; the 3D position is reconstructed as (x, 0, z).
    /// </summary>
    [Serializable]
    public class MarkerNode
    {
        [Tooltip("Unique identifier. Convention: L2_SectionA, L3_Stairs_North")]
        public string nodeID;

        [Tooltip("Must match the Vuforia ImageTarget name exactly (case-sensitive). " +
                 "Leave empty for virtual nodes such as stair landings.")]
        public string markerName;

        [Tooltip("Physical floor number: 2 or 3")]
        public int floor;

        [Tooltip("World-space position in metres on the XZ plane (Y is always 0 for navigation).")]
        public Vector2 worldPosition;

        [Tooltip("Human-readable label shown in the navigation UI, e.g. 'Section A – Computing'")]
        public string displayLabel;

        [Tooltip("True for nodes that represent stair/lift connection points between floors.")]
        public bool isStairNode;

        [Tooltip("The nodeID of the corresponding stair node on the other floor. Only used when isStairNode = true.")]
        public string linkedStairNodeID;

        [TextArea(2, 5)]
        [Tooltip("Describes what the camera sees at this location, used for Gemini Vision localization. " +
                 "Example: 'Metal shelving with blue 000-099 labels. Large window on north wall. " +
                 "Nearest pillar has a yellow fire exit sign.'")]
        public string visualDescription;

        /// <summary>
        /// Returns the world-space Vector3 position for rendering (Y raised slightly above floor).
        /// </summary>
        public Vector3 WorldPosition3D(float yOffset = 0f)
        {
            return new Vector3(worldPosition.x, yOffset, worldPosition.y);
        }
    }
}
