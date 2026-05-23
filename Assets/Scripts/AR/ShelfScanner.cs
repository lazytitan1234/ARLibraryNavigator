using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ARLibraryNav.Logging;

namespace ARLibraryNav.AR
{
    /// <summary>
    /// Identifies which library shelf the user is currently standing at by:
    ///   1. Receiving a JPEG image of the shelf label paper (captured by BookSearchController)
    ///   2. Passing it to ShelfLabelOCR (ML Kit) to extract the printed text
    ///   3. Matching the extracted Dewey decimal numbers + keywords against library_database.txt
    ///   4. Returning the best-matching shelf_id (e.g. "SHELF_B1_04")
    ///
    /// Attach to: Managers GameObject in ARScene.
    /// Requires: ShelfLabelOCR component on the same or another active GameObject.
    /// </summary>
    public class ShelfScanner : MonoBehaviour
    {
        // Inspector
        [Header("References")]
        [SerializeField] private ShelfLabelOCR labelOCR;
        [SerializeField] private SessionLogger  sessionLogger;

        [Header("Matching")]
        [Tooltip("Minimum score (0-1) to accept a shelf match. Lower = more permissive.")]
        [SerializeField] [Range(0.05f, 1f)] private float matchThreshold = 0.15f;

        // Private State
        /// <summary>Parsed shelf data: nodeID → ShelfEntry.</summary>
        private readonly Dictionary<string, ShelfEntry> _shelves
            = new Dictionary<string, ShelfEntry>(StringComparer.OrdinalIgnoreCase);

        private bool _databaseLoaded;

        // Types
        private class ShelfEntry
        {
            public string       nodeID;
            public string       shelfName;
            public HashSet<string> deweyPrefixes; // e.g. "658", "658.4", "658.40"
            public HashSet<string> keywords;      // lowercase words from name + topics
        }

        // Lifecycle
        private void Awake()
        {
            LoadDatabase();
        }

        // Public API

        /// <summary>
        /// Given a JPEG image of a shelf label paper, identifies the current shelf.
        /// Calls onFound(nodeID) on success; calls onFailed(reason) if no match.
        /// </summary>
        public void ScanCurrentShelf(byte[] jpegBytes,
                                     Action<string> onFound,
                                     Action<string> onFailed)
        {
            if (!_databaseLoaded)
            {
                onFailed?.Invoke("Shelf database not loaded.");
                return;
            }

            if (labelOCR == null)
            {
                onFailed?.Invoke("ShelfLabelOCR component not assigned.");
                return;
            }

            sessionLogger?.LogEvent("SHELF_SCAN_ATTEMPT", "");

            labelOCR.ReadText(jpegBytes,
                ocrText =>
                {
                    Debug.Log($"[ShelfScanner] OCR result ({ocrText.Length} chars):\n{ocrText}");
                    string nodeID = MatchToShelf(ocrText);

                    if (!string.IsNullOrEmpty(nodeID))
                    {
                        Debug.Log($"[ShelfScanner] Matched to: {nodeID}");
                        sessionLogger?.LogEvent("SHELF_SCAN_SUCCESS",
                            $"nodeID={nodeID} ocrLength={ocrText.Length}");
                        onFound?.Invoke(nodeID);
                    }
                    else
                    {
                        Debug.LogWarning("[ShelfScanner] No shelf matched OCR text.");
                        sessionLogger?.LogEvent("SHELF_SCAN_NO_MATCH",
                            $"ocrLength={ocrText.Length}");
                        onFailed?.Invoke(
                            "Could not identify this shelf. " +
                            "Try pointing the camera directly at the label paper.");
                    }
                },
                error =>
                {
                    Debug.LogWarning($"[ShelfScanner] OCR failed: {error}");
                    sessionLogger?.LogEvent("SHELF_SCAN_OCR_ERROR", $"error={error}");
                    onFailed?.Invoke($"Camera read failed: {error}");
                });
        }

        // Matching

        /// <summary>
        /// Matches OCR text to a shelf by scoring Dewey numbers and keywords.
        /// Returns the shelf_id with the highest normalised score, or null if below threshold.
        /// </summary>
        private string MatchToShelf(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;

            string ocrLower = ocrText.ToLowerInvariant();

            // Extract Dewey like numbers from OCR: 3+ digit sequences, optionally with decimals
            // Matches: "658", "658.4", "658.404"
            var deweyPattern = new Regex(@"\b(\d{3}(?:\.\d+)?)\b");
            var ocrDeweys    = new HashSet<string>();
            foreach (Match m in deweyPattern.Matches(ocrText))
                ocrDeweys.Add(m.Value);

            // Also grab 3-digit prefixes of any longer numbers found
            var ocrPrefixes = new HashSet<string>();
            foreach (var d in ocrDeweys)
                if (d.Length >= 3) ocrPrefixes.Add(d.Substring(0, 3));

            string bestNodeID = null;
            float  bestScore  = 0f;

            foreach (var kvp in _shelves)
            {
                var shelf = kvp.Value;
                float score = 0f;
                int   total = shelf.deweyPrefixes.Count + shelf.keywords.Count;
                if (total == 0) continue;

                // Dewey matching 
                foreach (var dp in shelf.deweyPrefixes)
                {
                    if (ocrDeweys.Contains(dp))         score += 1.5f; // exact match
                    else if (ocrPrefixes.Contains(dp))  score += 0.8f; // prefix match
                }

                // Keyword matching 
                foreach (var kw in shelf.keywords)
                {
                    if (ocrLower.Contains(kw)) score += 0.5f;
                }

                // Normalise by total possible score
                float normScore = score / (shelf.deweyPrefixes.Count * 1.5f
                                         + shelf.keywords.Count * 0.5f + 0.001f);

                if (normScore > bestScore)
                {
                    bestScore  = normScore;
                    bestNodeID = shelf.nodeID;
                }
            }

            Debug.Log($"[ShelfScanner] Best match: {bestNodeID} (score {bestScore:F3}, threshold {matchThreshold})");
            return bestScore >= matchThreshold ? bestNodeID : null;
        }

        // Database Parsing

        private void LoadDatabase()
        {
            var asset = Resources.Load<TextAsset>("library_database");
            if (asset == null)
            {
                Debug.LogError("[ShelfScanner] library_database.txt not found in Resources.");
                return;
            }

            _shelves.Clear();
            ParseDatabase(asset.text);
            _databaseLoaded = true;
            Debug.Log($"[ShelfScanner] Loaded {_shelves.Count} shelves from database.");
        }

        private void ParseDatabase(string text)
        {
            // Format per shelf block:
            //   SHELF: <name>
            //   ID: SHELF_XX_YY | Floor: N
            //   Topics: Topic [000], Topic [000.0], ...

            string currentName = null;
            string currentID   = null;

            var deweyInBrackets = new Regex(@"\[(\d{3}(?:\.\d+)?)\]");
            var lines           = text.Split('\n');

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("SHELF:"))
                {
                    currentName = line.Substring("SHELF:".Length).Trim();
                    currentID   = null;
                }
                else if (line.StartsWith("ID:") && currentName != null)
                {
                    currentID = line.Split('|')[0].Replace("ID:", "").Trim();
                }
                else if (line.StartsWith("Topics:") && currentID != null)
                {
                    string topicsRaw = line.Substring("Topics:".Length);

                    // Extract Dewey numbers from brackets 
                    var deweys = new HashSet<string>();
                    foreach (Match m in deweyInBrackets.Matches(topicsRaw))
                    {
                        string d = m.Groups[1].Value;
                        deweys.Add(d);
                        if (d.Length >= 3) deweys.Add(d.Substring(0, 3)); // 3-digit prefix
                    }

                    // Extract keywords from shelf name 
                    var keywords = new HashSet<string>();
                    string nameLower = currentName.ToLowerInvariant()
                        .Replace(" shelf", "").Replace("/", " ");
                    foreach (var word in nameLower.Split(
                        new[] { ' ', '-', '/', '(', ')' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (word.Length >= 4) // skip short stop words
                            keywords.Add(word);
                    }

                    _shelves[currentID] = new ShelfEntry
                    {
                        nodeID        = currentID,
                        shelfName     = currentName,
                        deweyPrefixes = deweys,
                        keywords      = keywords
                    };

                    currentName = null;
                    currentID   = null;
                }
            }
        }
    }
}
