using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ARLibraryNav.Data
{
    /// <summary>
    /// Loads shelf/topic data from a CSV or JSON file into the runtime ShelfDatabase.
    ///
    /// HOW TO USE:
    ///   1. Attach this component to the Managers GameObject in ARScene.
    ///   2. Set databaseFilePath to your CSV or JSON file path (absolute, or relative to
    ///      Application.persistentDataPath / Application.streamingAssetsPath).
    ///   3. Set shelfDatabase reference to ShelfDatabase.asset.
    ///   4. The file is loaded on Start() and merges into the existing ShelfDatabase entries.
    ///
    /// CSV FORMAT (comma-separated, first row = header ignored):
    ///   ShelfID, DisplayLocation, Floor, MarkerNodeID, TopicLabels
    ///   Example:
    ///     L2-A1, "Level 2 Computing", 2, L2_SectionA, "programming,computers,AI,networking"
    ///     L2-B1, "Level 2 History",   2, L2_SectionB, "history,ancient,modern,world"
    ///
    /// JSON FORMAT:
    ///   [
    ///     { "shelfID":"L2-A1", "displayLocation":"Level 2 Computing",
    ///       "floor":2, "markerNodeID":"L2_SectionA",
    ///       "topicLabels":["programming","computers","AI"] },
    ///     ...
    ///   ]
    ///
    /// The path can use these tokens which are resolved at runtime:
    ///   {StreamingAssets}  → Application.streamingAssetsPath
    ///   {PersistentData}   → Application.persistentDataPath
    ///   {DataPath}         → Application.dataPath
    /// </summary>
    public class ShelfDataLoader : MonoBehaviour
    {
        [Header("File Source")]
        [Tooltip("Path to your CSV or JSON shelf database file. Use {StreamingAssets}, {PersistentData}, or {DataPath} tokens.")]
        [SerializeField] private string databaseFilePath = "{StreamingAssets}/shelves.csv";

        [Tooltip("Format of the file. Auto-detect reads the extension.")]
        [SerializeField] private FileFormat fileFormat = FileFormat.AutoDetect;

        [Header("Target Database")]
        [SerializeField] private ShelfDatabase shelfDatabase;

        [Header("Behaviour")]
        [Tooltip("If true, clears existing ShelfDatabase entries before loading.")]
        [SerializeField] private bool replaceExistingData = false;

        [Tooltip("Log loaded entries to Console.")]
        [SerializeField] private bool verboseLogging = true;

        public enum FileFormat { AutoDetect, CSV, JSON }

        // Public State
        public bool IsLoaded      { get; private set; }
        public int  LoadedCount   { get; private set; }
        public string LastError   { get; private set; }

        // Unity Lifecycle
        private void Start()
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
            {
                Debug.LogWarning("[ShelfDataLoader] No databaseFilePath set — skipping load.");
                return;
            }

            var resolved = ResolvePath(databaseFilePath);
            if (!File.Exists(resolved))
            {
                Debug.LogWarning($"[ShelfDataLoader] Database file not found: {resolved}\n" +
                                 "Set the path in ShelfDataLoader on the Managers object.");
                return;
            }

            var fmt = fileFormat == FileFormat.AutoDetect ? DetectFormat(resolved) : fileFormat;

            try
            {
                var entries = fmt == FileFormat.JSON
                    ? LoadFromJSON(resolved)
                    : LoadFromCSV(resolved);

                ApplyToDatabase(entries);
                IsLoaded    = true;
                LoadedCount = entries.Count;
                Debug.Log($"[ShelfDataLoader] Loaded {LoadedCount} shelves from: {resolved}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[ShelfDataLoader] Failed to load '{resolved}': {ex.Message}");
            }
        }

        // Load Methods

        private List<Data.ShelfEntry> LoadFromCSV(string path)
        {
            var entries = new List<ShelfEntry>();
            var lines   = File.ReadAllLines(path);

            for (int i = 1; i < lines.Length; i++)   // skip header row
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var cols = SplitCSVLine(line);
                if (cols.Length < 4)
                {
                    Debug.LogWarning($"[ShelfDataLoader] CSV line {i + 1} has < 4 columns — skipped.");
                    continue;
                }

                var entry = new ShelfEntry
                {
                    shelfID         = cols[0].Trim(),
                    displayLocation = cols[1].Trim(),
                    markerNodeID    = cols[3].Trim()
                };

                if (int.TryParse(cols[2].Trim(), out int floor)) entry.floor = floor;

                if (cols.Length > 4)
                    entry.topicLabels = new List<string>(cols[4].Trim('"').Split(','));

                entries.Add(entry);
                if (verboseLogging)
                    Debug.Log($"[ShelfDataLoader] CSV: {entry.shelfID} — {entry.displayLocation} ({entry.markerNodeID})");
            }

            return entries;
        }

        private List<Data.ShelfEntry> LoadFromJSON(string path)
        {
            var json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<ShelfEntryListWrapper>($"{{\"items\":{json}}}");
            if (wrapper?.items == null) return new List<Data.ShelfEntry>();

            if (verboseLogging)
                foreach (var e in wrapper.items)
                    Debug.Log($"[ShelfDataLoader] JSON: {e.shelfID} — {e.displayLocation} ({e.markerNodeID})");

            return wrapper.items;
        }

        private void ApplyToDatabase(List<Data.ShelfEntry> entries)
        {
            if (shelfDatabase == null)
            {
                Debug.LogError("[ShelfDataLoader] ShelfDatabase asset not assigned.");
                return;
            }

            if (replaceExistingData)
                shelfDatabase.shelves.Clear();

            foreach (var e in entries)
            {
                int existing = shelfDatabase.shelves.FindIndex(s => s.shelfID == e.shelfID);
                if (existing >= 0)
                    shelfDatabase.shelves[existing] = e;
                else
                    shelfDatabase.shelves.Add(e);
            }
        }

        // Helpers

        private string ResolvePath(string path)
        {
            return path
                .Replace("{StreamingAssets}", Application.streamingAssetsPath)
                .Replace("{PersistentData}",  Application.persistentDataPath)
                .Replace("{DataPath}",         Application.dataPath);
        }

        private FileFormat DetectFormat(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".json" ? FileFormat.JSON : FileFormat.CSV;
        }

        /// <summary>Handles quoted fields with commas inside them.</summary>
        private string[] SplitCSVLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            int  start    = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') { inQuotes = !inQuotes; continue; }
                if (line[i] == ',' && !inQuotes)
                {
                    fields.Add(line.Substring(start, i - start).Trim('"'));
                    start = i + 1;
                }
            }
            fields.Add(line.Substring(start).Trim('"'));
            return fields.ToArray();
        }

        // JSON helper wrapper

        [Serializable]
        private class ShelfEntryListWrapper
        {
            public List<Data.ShelfEntry> items;
        }
    }
}
