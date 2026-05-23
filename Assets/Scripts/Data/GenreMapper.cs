using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.Data
{
    /// <summary>
    /// ScriptableObject that normalises raw Gemini API output to canonical genre/topic labels.
    ///
    /// Three-tier matching (in order):
    ///   1. Exact match against canonicalLabel (case-insensitive)
    ///   2. Alias match against any alias in the genre's alias list (case-insensitive)
    ///   3. Contains match — canonicalLabel contains the raw string (case-insensitive)
    ///
    /// If no tier matches, returns null -> triggers the fallback manual genre picker in UI.
    ///
    /// Create via: Assets > Create > ARLibraryNav > GenreMapper
    /// Save as: Assets/Scripts/Data/LibraryGenreMapper.asset
    /// </summary>
    [CreateAssetMenu(fileName = "LibraryGenreMapper", menuName = "ARLibraryNav/GenreMapper")]
    public class GenreMapper : ScriptableObject
    {
        // Inner Type
        [Serializable]
        public class GenreEntry
        {
            [Tooltip("The authoritative label used throughout the system. " +
                     "Must exactly match values in ShelfEntry.topicLabels. " +
                     "e.g. 'Computing', 'History', 'Natural Sciences'")]
            public string canonicalLabel;

            [Tooltip("Alternative strings Gemini might return for this genre. " +
                     "e.g. for 'Computing': ['computer science','programming','coding','software','CS','IT']")]
            public List<string> aliases = new List<string>();
        }

        // Serialized Data
        public List<GenreEntry> genres = new List<GenreEntry>();

        // Public API

        /// <summary>
        /// Attempts to normalise rawLabel into a canonical genre label.
        /// Returns the canonical label string on success, null on failure.
        /// </summary>
        public string Normalize(string rawLabel)
        {
            if (string.IsNullOrWhiteSpace(rawLabel)) return null;

            string trimmed = rawLabel.Trim();

            // Tier 1: exact canonical match
            foreach (var entry in genres)
            {
                if (string.Equals(entry.canonicalLabel, trimmed, StringComparison.OrdinalIgnoreCase))
                    return entry.canonicalLabel;
            }

            // Tier 2: alias match
            foreach (var entry in genres)
            {
                foreach (var alias in entry.aliases)
                {
                    if (string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase))
                        return entry.canonicalLabel;
                }
            }

            // Tier 3: canonical label contains the raw string
            foreach (var entry in genres)
            {
                if (entry.canonicalLabel.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    return entry.canonicalLabel;
            }

            Debug.LogWarning($"[GenreMapper] Could not normalise label: '{rawLabel}'");
            return null;
        }

        /// <summary>
        /// Returns all canonical labels. Used to build the valid labels list in the Gemini prompt
        /// and to populate the fallback genre picker UI.
        /// </summary>
        public List<string> GetAllCanonicalLabels()
        {
            var labels = new List<string>(genres.Count);
            foreach (var entry in genres)
            {
                if (!string.IsNullOrEmpty(entry.canonicalLabel))
                    labels.Add(entry.canonicalLabel);
            }
            return labels;
        }
    }
}
