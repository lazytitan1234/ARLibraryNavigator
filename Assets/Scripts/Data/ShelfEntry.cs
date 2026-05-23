using System;
using System.Collections.Generic;

namespace ARLibraryNav.Data
{
    /// <summary>
    /// Represents a single shelf or shelf section in the library.
    /// Serializable for storage inside the ShelfDatabase ScriptableObject.
    /// </summary>
    [Serializable]
    public class ShelfEntry
    {
        [UnityEngine.Tooltip("Unique shelf identifier. Convention: L2-A1, L3-C4")]
        public string shelfID;

        [UnityEngine.Tooltip("Physical floor number: 2 or 3")]
        public int floor;

        [UnityEngine.Tooltip("nodeID of the nearest MarkerNode to this shelf. " +
                             "Must match a nodeID in LibraryNavGraph.asset exactly.")]
        public string markerNodeID;

        [UnityEngine.Tooltip("Human-readable location shown in the navigation UI. " +
                             "e.g. 'Level 2, Aisle A, Bay 1 – Computing'")]
        public string displayLocation;

        [UnityEngine.Tooltip("List of canonical genre/topic labels that map to this shelf. " +
                             "Must match canonicalLabel values in LibraryGenreMapper.asset exactly.")]
        public List<string> topicLabels = new List<string>();
    }
}
