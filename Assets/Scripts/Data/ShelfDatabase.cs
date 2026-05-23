using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.Data
{
    /// <summary>
    /// ScriptableObject that maps topic/genre labels to physical library shelves.
    ///
    /// Create via: Assets > Create > ARLibraryNav > ShelfDatabase
    /// Save as: Assets/Scripts/Data/LibraryShelfDatabase.asset
    ///
    /// Populate shelfEntries in the Inspector with one entry per shelf section.
    /// Each entry's topicLabels must exactly match canonicalLabels in GenreMapper.
    /// </summary>
    [CreateAssetMenu(fileName = "LibraryShelfDatabase", menuName = "ARLibraryNav/ShelfDatabase")]
    public class ShelfDatabase : ScriptableObject
    {
        // Serialized Data
        public List<ShelfEntry> shelves = new List<ShelfEntry>();

        // Runtime Lookups
        private Dictionary<string, ShelfEntry>        _byID    = new Dictionary<string, ShelfEntry>();
        private Dictionary<string, List<ShelfEntry>>  _byLabel = new Dictionary<string, List<ShelfEntry>>();

        // Lifecycle
        private void OnEnable()
        {
            BuildLookups();
        }

        // Public API

        /// <summary>Returns the ShelfEntry with the given shelfID, or null.</summary>
        public ShelfEntry GetByID(string shelfID)
        {
            if (string.IsNullOrEmpty(shelfID)) return null;
            _byID.TryGetValue(shelfID, out var entry);
            return entry;
        }

        /// <summary>
        /// Returns all shelves that carry the given canonical topic label.
        /// Returns an empty list if the label is unknown.
        /// </summary>
        public List<ShelfEntry> GetByTopicLabel(string canonicalLabel)
        {
            if (string.IsNullOrEmpty(canonicalLabel)) return new List<ShelfEntry>();
            _byLabel.TryGetValue(canonicalLabel.ToLowerInvariant(), out var list);
            return list ?? new List<ShelfEntry>();
        }

        /// <summary>
        /// Returns the first ShelfEntry that matches the canonical label.
        /// "First" is defined by insertion order in the shelves list (lowest index = priority).
        /// Returns null if no match is found.
        /// </summary>
        public ShelfEntry GetBestShelfForLabel(string canonicalLabel)
        {
            var matches = GetByTopicLabel(canonicalLabel);
            return matches.Count > 0 ? matches[0] : null;
        }

        // Private
        private void BuildLookups()
        {
            _byID.Clear();
            _byLabel.Clear();

            foreach (var shelf in shelves)
            {
                if (string.IsNullOrEmpty(shelf.shelfID))
                {
                    Debug.LogWarning($"[ShelfDatabase] Shelf with displayLocation '{shelf.displayLocation}' has an empty shelfID — skipped.");
                    continue;
                }

                if (_byID.ContainsKey(shelf.shelfID))
                {
                    Debug.LogWarning($"[ShelfDatabase] Duplicate shelfID '{shelf.shelfID}' — second entry skipped.");
                    continue;
                }

                _byID[shelf.shelfID] = shelf;

                foreach (var label in shelf.topicLabels)
                {
                    string key = label.ToLowerInvariant();
                    if (!_byLabel.ContainsKey(key))
                        _byLabel[key] = new List<ShelfEntry>();

                    _byLabel[key].Add(shelf);
                }
            }
        }
    }
}
