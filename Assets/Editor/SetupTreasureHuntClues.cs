// SetupTreasureHuntClues.cs, run via  Tools > Setup Treasure Hunt Clues
//
// Does two things in one shot:
//   1. Sets Read/Write Enabled on every texture inside Resources/TreasureHuntClues/
//      (required so Texture2D.EncodeToJPG() works at runtime when building the Gemini request)
//   2. Populates TreasureHuntRoute.asset with the real clue data and reference image arrays.
//
// Safe to re-run clue list is rebuilt from scratch each time.

using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ARLibraryNav.Data;

public static class SetupTreasureHuntClues
{
    // Clue definitions
    // Add more clue blocks here as you photograph more locations.
    // referenceImagePaths: paths relative to Assets/  (use forward slashes)

    private static readonly ClueDefinition[] ClueDefinitions = new[]
    {
        new ClueDefinition
        {
            rawClueText =
                "Where daylight floods through blades of white, " +
                "two green thrones guard a wooden square. " +
                "The world stretches out beyond the glass — " +
                "but knowledge lives beside it.",

            targetNodeID = "",   // fill in after NavGraph is set up

            visionTargetDescription =
                "A small square wooden table with two green upholstered chairs with chrome legs, " +
                "positioned directly beside large floor-to-ceiling windows covered with white " +
                "vertical blinds, grey tiled floor.",

            referenceImagePaths = new[]
            {
                "Assets/Resources/TreasureHuntClues/Clue01/Media 1.jpg",
                "Assets/Resources/TreasureHuntClues/Clue01/Media 2.jpg",
                "Assets/Resources/TreasureHuntClues/Clue01/Media 3.jpg",
                "Assets/Resources/TreasureHuntClues/Clue01/Media 4.jpg",
            }
        },

        // Clue 2 bedroom window (test dummy)
        new ClueDefinition
        {
            rawClueText =
                "I let the light in but keep the heat out. " +
                "Golden blades tilt at my command, " +
                "a cord dangles like a question mark, " +
                "and stone walls stare back from the other side.",

            targetNodeID = "",

            visionTargetDescription =
                "A dark-framed double window with golden horizontal venetian blinds " +
                "partially raised at the top, a white pull cord hanging on the left, " +
                "a white marble windowsill with small objects on it, " +
                "and a view of a beige limestone block wall outside.",

            referenceImagePaths = new[]
            {
                "Assets/Resources/TreasureHuntClues/Clue02/window 1.jpg",
                "Assets/Resources/TreasureHuntClues/Clue02/window 2.jpg",
                "Assets/Resources/TreasureHuntClues/Clue02/window 3.jpg",
            }
        },

        // Clue 3 bedroom chair (test dummy)
        new ClueDefinition
        {
            rawClueText =
                "Four legs hold me steady, yet I never walk. " +
                "Arms outstretched but never reaching. " +
                "Dark as midnight, built for thinking — " +
                "I wait beside the wall in silence.",

            targetNodeID = "",

            visionTargetDescription =
                "A dark navy or black office-style chair with armrests, " +
                "standing on a light-coloured floor against a white wall, " +
                "with a wardrobe or door visible in the background.",

            referenceImagePaths = new[]
            {
                "Assets/Resources/TreasureHuntClues/Clue03/bedroom chair 1.jpg",
                "Assets/Resources/TreasureHuntClues/Clue03/bedroom chair 2.jpg",
                "Assets/Resources/TreasureHuntClues/Clue03/bedroom chair 3.jpg",
                "Assets/Resources/TreasureHuntClues/Clue03/bedroom chair 4.jpg",
            }
        },
    };

    // Menu Item

    [MenuItem("Tools/Setup Treasure Hunt Clues")]
    public static void Run()
    {
        // Step 1: Fix texture import settings
        FixTextureImportSettings();

        // Step 2: Populate TreasureHuntRoute.asset
        PopulateRoute();
    }

    // Texture import fixer

    static void FixTextureImportSettings()
    {
        string rootPath = "Assets/Resources/TreasureHuntClues";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogWarning($"[SetupTHClues] Folder not found: {rootPath}");
            return;
        }

        // Find every .jpg / .png under the folder
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootPath });
        int fixed_ = 0;

        foreach (var guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            var    importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            { importer.textureType = TextureImporterType.Default; changed = true; }

            if (!importer.isReadable)
            { importer.isReadable = true; changed = true; }

            // Disable mip-maps — not needed for reference images, saves memory
            if (importer.mipmapEnabled)
            { importer.mipmapEnabled = false; changed = true; }

            if (changed)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fixed_++;
                Debug.Log($"[SetupTHClues] Fixed import settings: {Path.GetFileName(path)}");
            }
        }

        Debug.Log($"[SetupTHClues] Texture import step done — {fixed_} texture(s) updated.");
    }

    // Route populator

    static void PopulateRoute()
    {
        const string routePath = "Assets/Scripts/Data/TreasureHuntRoute.asset";
        var route = AssetDatabase.LoadAssetAtPath<TreasureHuntRoute>(routePath);

        if (route == null)
        {
            // Try the other common path
            string[] guids = AssetDatabase.FindAssets("t:TreasureHuntRoute");
            if (guids.Length > 0)
            {
                string found = AssetDatabase.GUIDToAssetPath(guids[0]);
                route = AssetDatabase.LoadAssetAtPath<TreasureHuntRoute>(found);
                Debug.Log($"[SetupTHClues] Found TreasureHuntRoute at: {found}");
            }
        }

        if (route == null)
        {
            Debug.LogError("[SetupTHClues] TreasureHuntRoute.asset not found. " +
                           "Create one via: Assets > Create > ARLibraryNav > TreasureHuntRoute");
            return;
        }

        var so = new SerializedObject(route);

        // Rebuild the clues list from scratch
        var cluesProp = so.FindProperty("clues");
        cluesProp.arraySize = ClueDefinitions.Length;

        for (int i = 0; i < ClueDefinitions.Length; i++)
        {
            var def  = ClueDefinitions[i];
            var elem = cluesProp.GetArrayElementAtIndex(i);

            elem.FindPropertyRelative("rawClueText").stringValue          = def.rawClueText;
            elem.FindPropertyRelative("targetNodeID").stringValue         = def.targetNodeID;
            elem.FindPropertyRelative("visionTargetDescription").stringValue = def.visionTargetDescription;

            // Load and assign reference textures
            var imagesProp = elem.FindPropertyRelative("referenceImages");
            var loadedTextures = new List<Texture2D>();

            foreach (var imgPath in def.referenceImagePaths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(imgPath);
                if (tex != null)
                {
                    loadedTextures.Add(tex);
                    Debug.Log($"[SetupTHClues] Clue {i + 1}: loaded {Path.GetFileName(imgPath)}");
                }
                else
                {
                    Debug.LogWarning($"[SetupTHClues] Clue {i + 1}: texture not found at {imgPath}");
                }
            }

            imagesProp.arraySize = loadedTextures.Count;
            for (int t = 0; t < loadedTextures.Count; t++)
                imagesProp.GetArrayElementAtIndex(t).objectReferenceValue = loadedTextures[t];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(route);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SetupTHClues] Done — {ClueDefinitions.Length} clue(s) written to TreasureHuntRoute.asset.");
    }

    // Data struct

    private struct ClueDefinition
    {
        public string   rawClueText;
        public string   targetNodeID;
        public string   visionTargetDescription;
        public string[] referenceImagePaths;
    }
}
