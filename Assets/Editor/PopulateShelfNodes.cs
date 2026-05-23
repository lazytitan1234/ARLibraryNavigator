using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using ARLibraryNav.Navigation;

/// <summary>
/// One-shot editor script.
/// Reads library_database.txt and ensures every shelf has a node in LibraryNavGraph.asset.
/// Existing nodes are left untouched; only missing ones are added.
///
/// Run via: Tools > Populate Shelf Nodes from Database
///
/// After running:
///   - All 24 shelf nodes will exist in LibraryNavGraph.asset.
///   - worldPosition is (0, 0) fill these in after the library visit.
///   - visualDescription is auto generated from topics refine after visit.
///   - Edges between shelves must be added manually once you know the layout.
/// </summary>
public static class PopulateShelfNodes
{
    [MenuItem("Tools/Populate Shelf Nodes from Database")]
    public static void Run()
    {
        // 1. Load NavGraph asset
        const string assetPath = "Assets/Scripts/Navigation/LibraryNavGraph.asset";
        var navGraph = AssetDatabase.LoadAssetAtPath<NavGraph>(assetPath);
        if (navGraph == null)
        {
            Debug.LogError($"[PopulateShelfNodes] NavGraph not found at: {assetPath}");
            return;
        }

        // 2. Load library_database.txt
        var dbAsset = Resources.Load<TextAsset>("library_database");
        if (dbAsset == null)
        {
            Debug.LogError("[PopulateShelfNodes] library_database.txt not found in Resources.");
            return;
        }

        // 3. Parse shelves
        var shelves = ParseDatabase(dbAsset.text);
        if (shelves.Count == 0)
        {
            Debug.LogError("[PopulateShelfNodes] No shelves parsed from database.");
            return;
        }

        // 4. Build set of existing nodeIDs
        var existing = new HashSet<string>();
        foreach (var node in navGraph.nodes)
            existing.Add(node.nodeID);

        // 5. Add missing nodes via SerializedObject
        var so    = new SerializedObject(navGraph);
        var nodesProp = so.FindProperty("nodes");

        int added = 0;
        foreach (var shelf in shelves)
        {
            if (existing.Contains(shelf.nodeID))
            {
                Debug.Log($"[PopulateShelfNodes] Already exists — skipped: {shelf.nodeID}");
                continue;
            }

            int idx = nodesProp.arraySize;
            nodesProp.arraySize++;
            var elem = nodesProp.GetArrayElementAtIndex(idx);

            elem.FindPropertyRelative("nodeID").stringValue         = shelf.nodeID;
            elem.FindPropertyRelative("markerName").stringValue     = ""; // no Vuforia target
            elem.FindPropertyRelative("floor").intValue             = shelf.floor;
            elem.FindPropertyRelative("displayLabel").stringValue   = shelf.displayLabel;
            elem.FindPropertyRelative("isStairNode").boolValue      = false;
            elem.FindPropertyRelative("linkedStairNodeID").stringValue = "";
            elem.FindPropertyRelative("visualDescription").stringValue = shelf.draftDescription;

            var posX = elem.FindPropertyRelative("worldPosition.x");
            var posY = elem.FindPropertyRelative("worldPosition.y");
            if (posX != null) posX.floatValue = 0f;
            if (posY != null) posY.floatValue = 0f;

            added++;
            Debug.Log($"[PopulateShelfNodes] Added node: {shelf.nodeID} ({shelf.displayLabel})");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(navGraph);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PopulateShelfNodes] Done — {added} node(s) added, " +
                  $"{shelves.Count - added} already existed. " +
                  $"Fill in worldPosition and visualDescription after your library visit.");
    }

    // Parser

    private struct ShelfData
    {
        public string nodeID;
        public string displayLabel;
        public int    floor;
        public string draftDescription;
    }

    private static List<ShelfData> ParseDatabase(string text)
    {
        var result  = new List<ShelfData>();
        var lines   = text.Split('\n');

        string currentName  = null;
        string currentID    = null;
        int    currentFloor = 1;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;

            // SHELF: <name>
            if (line.StartsWith("SHELF:"))
            {
                currentName = line.Substring("SHELF:".Length).Trim();
                // Remove " Shelf" suffix if present, for a cleaner displayLabel
                if (currentName.EndsWith(" Shelf"))
                    currentName = currentName.Substring(0, currentName.Length - " Shelf".Length).Trim();
                currentID    = null;
                currentFloor = 1;
            }
            // ID: SHELF_XX_YY | Floor: N
            else if (line.StartsWith("ID:") && currentName != null)
            {
                var parts = line.Split('|');
                currentID = parts[0].Replace("ID:", "").Trim();
                if (parts.Length > 1)
                {
                    var floorPart = parts[1].Replace("Floor:", "").Trim();
                    int.TryParse(floorPart, out currentFloor);
                }
            }
            // Topics: ... last line of each shelf block
            else if (line.StartsWith("Topics:") && currentID != null && currentName != null)
            {
                string topicsRaw = line.Substring("Topics:".Length).Trim();
                string draft     = BuildDraftDescription(currentName, topicsRaw);

                result.Add(new ShelfData
                {
                    nodeID        = currentID,
                    displayLabel  = $"{currentID.Replace("SHELF_", "Shelf ")} — {currentName}",
                    floor         = currentFloor,
                    draftDescription = draft
                });

                // Reset for next shelf
                currentName = null;
                currentID   = null;
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a visual description suitable for Gemini to match against a shelf label photo.
    /// Uses the first 4 topics (with Dewey numbers) to approximate what's on the paper.
    /// The user should update this with real visual details after the library visit.
    /// </summary>
    private static string BuildDraftDescription(string shelfName, string topicsRaw)
    {
        // Extract individual topic entries (format: "Topic name [000]")
        var topics    = new List<string>();
        var topicParts = topicsRaw.Split(',');
        foreach (var part in topicParts)
        {
            string t = part.Trim();
            if (!string.IsNullOrEmpty(t))
                topics.Add(t);
        }

        // Take first 4 topics to approximate the shelf label paper content
        int    take = Mathf.Min(4, topics.Count);
        var    sb   = new StringBuilder();
        sb.Append($"Shelf label paper for: {shelfName}. ");
        sb.Append("Key topics shown on paper: ");
        for (int i = 0; i < take; i++)
        {
            sb.Append(topics[i]);
            if (i < take - 1) sb.Append("; ");
        }
        sb.Append(". [PLACEHOLDER — update worldPosition and this description after library visit]");

        return sb.ToString();
    }
}
