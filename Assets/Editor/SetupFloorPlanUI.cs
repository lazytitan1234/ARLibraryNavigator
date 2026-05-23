// SetupFloorPlanUI.cs — one-shot editor script
// Run via: Tools > Setup Floor Plan UI
//
// Creates two new panels inside UIRoot:
//   - FloorPlanPanel shows floor plan image with path overlay + step text
//   - ScanLocationPanel "Scan My Location" prompt between search result and floor plan
//
// Also adds FloorPlanViewController component to the Managers GameObject (if absent).
//
// Run BEFORE WireSceneReferences the wire script needs these panels to exist first.
// Safe to re-run: skips any panel that already exists.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SetupFloorPlanUI
{
    // Colours
    private static readonly Color32 ColPanelBg     = new Color32(15,  15,  18,  242); // near-black
    private static readonly Color32 ColHeaderBg    = new Color32(25,  40,  55,  255); // dark navy
    private static readonly Color32 ColScanBtn     = new Color32(22, 130, 130, 255);  // teal
    private static readonly Color32 ColSkipBtn     = new Color32(70,  70,  80,  255); // mid-grey
    private static readonly Color32 ColSearchAgain = new Color32(50,  80, 120, 255);  // steel blue
    private static readonly Color32 ColStepsPanel  = new Color32(20,  20,  25,  230);
    private static readonly Color32 ColWhiteText   = new Color32(240, 240, 240, 255);
    private static readonly Color32 ColGreenText   = new Color32( 50, 210,  80, 255);
    private static readonly Color32 ColGreyText    = new Color32(160, 160, 170, 255);

    [MenuItem("Tools/Setup Floor Plan UI")]
    public static void Run()
    {
        int created = 0;

        // Locate root GameObjects
        GameObject uiRoot   = FindAny("UIRoot");
        GameObject managers = FindAny("Managers");

        if (uiRoot == null)
        {
            Debug.LogError("[SetupFP] 'UIRoot' not found in the active scene. Open ARScene first.");
            return;
        }

        // Add FloorPlanViewController component to Managers
        if (managers != null)
        {
            var existing = managers.GetComponent("ARLibraryNav.UI.FloorPlanViewController")
                        ?? managers.GetComponent("FloorPlanViewController");
            if (existing == null)
            {
                managers.AddComponent<ARLibraryNav.UI.FloorPlanViewController>();
                Debug.Log("[SetupFP] Added FloorPlanViewController component to Managers.");
                created++;
            }
            else
            {
                Debug.Log("[SetupFP] FloorPlanViewController already on Managers — skipped.");
            }
        }
        else
        {
            Debug.LogWarning("[SetupFP] 'Managers' not found — FloorPlanViewController not added. Add it manually.");
        }

        // Create FloorPlanPanel
        if (uiRoot.transform.Find("FloorPlanPanel") == null)
        {
            CreateFloorPlanPanel(uiRoot);
            Debug.Log("[SetupFP] Created UIRoot/FloorPlanPanel.");
            created++;
        }
        else
        {
            Debug.Log("[SetupFP] UIRoot/FloorPlanPanel already exists — skipped.");
        }

        // Create ScanLocationPanel
        if (uiRoot.transform.Find("ScanLocationPanel") == null)
        {
            CreateScanLocationPanel(uiRoot);
            Debug.Log("[SetupFP] Created UIRoot/ScanLocationPanel.");
            created++;
        }
        else
        {
            Debug.Log("[SetupFP] UIRoot/ScanLocationPanel already exists — skipped.");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[SetupFP] Done — {created} item(s) created. " +
                  "Now run: AR Library > Wire Scene References, then assign floor plan textures in the Inspector.");
    }

    // FloorPlanPanel
    //
    // Hierarchy:
    //   FloorPlanPanel
    //     Background          (Image, dark, full fill)
    //     Header              (Image navy, top 5% of screen)
    //       TitleText         (TMP, left-aligned)
    //       CloseButton       (Button, right corner, "X")
    //     MapContainer        (raw image area, 57% of screen)
    //       FloorPlanImage    (RawImage, fills MapContainer)
    //       OverlayRoot       (RectTransform only, same size holds dots/lines)
    //     StepsPanel          (dark panel, 29% of screen)
    //       StepsText         (TMP, fills panel)
    //     SearchAgainButton   (Button, 5% from bottom)

    private static void CreateFloorPlanPanel(GameObject uiRoot)
    {
        // Root panel
        var panel = MakePanel("FloorPlanPanel", uiRoot.transform);
        panel.SetActive(false);

        // Background
        var bg = MakeImage("Background", panel.transform, ColPanelBg);
        SetStretchFill(bg);

        // Header strip (top 5%)
        var header = MakeImage("Header", panel.transform, ColHeaderBg);
        SetAnchors(header, 0f, 0.955f, 1f, 1f);
        SetOffsets(header, 0, 0, 0, 0);

        // Header / TitleText
        var titleGO = MakeText("TitleText", header.transform,
            "Level 2  —  Route", 26, ColWhiteText, TextAlignmentOptions.MidlineLeft);
        SetAnchors(titleGO, 0f, 0f, 0.82f, 1f);
        SetOffsets(titleGO, 16, 0, 0, 0);

        // Header / CloseButton
        var closeBtn = MakeButton("CloseButton", header.transform, "X", 28, ColWhiteText);
        SetAnchors(closeBtn, 0.82f, 0f, 1f, 1f);
        SetOffsets(closeBtn, 0, 4, -4, -4);

        // MapContainer (rows 0.37–0.955 = 58.5% height)
        var mapContainer = MakePanel("MapContainer", panel.transform);
        SetAnchors(mapContainer, 0f, 0.365f, 1f, 0.955f);
        SetOffsets(mapContainer, 0, 0, 0, 0);

        // FloorPlanImage — fills MapContainer
        var fpImgGO = new GameObject("FloorPlanImage", typeof(RawImage));
        fpImgGO.transform.SetParent(mapContainer.transform, false);
        SetStretchFill(fpImgGO);
        var rawImg = fpImgGO.GetComponent<RawImage>();
        rawImg.color = Color.white;
        // Aspect ratio will be maintained by RawImage UV rect auto-fitting

        // OverlayRoot — transparent RT, same fill, parent for dots/lines
        var overlayGO = new GameObject("OverlayRoot", typeof(RectTransform));
        overlayGO.transform.SetParent(mapContainer.transform, false);
        SetStretchFill(overlayGO);
        // No Image component — it's transparent

        // StepsPanel (rows 0.07–0.365 = 29.5% height)
        var stepsPanel = MakeImage("StepsPanel", panel.transform, ColStepsPanel);
        SetAnchors(stepsPanel, 0f, 0.07f, 1f, 0.363f);
        SetOffsets(stepsPanel, 0, 0, 0, 0);

        // StepsPanel / StepsText
        var stepsTextGO = MakeText("StepsText", stepsPanel.transform,
            "Route steps will appear here.", 20, ColWhiteText, TextAlignmentOptions.TopLeft);
        SetStretchFill(stepsTextGO);
        SetOffsets(stepsTextGO, 20, 10, -20, -10);
        var stepsTMP = stepsTextGO.GetComponent<TextMeshProUGUI>();
        stepsTMP.enableWordWrapping = true;
        stepsTMP.overflowMode       = TextOverflowModes.Overflow;

        // SearchAgainButton (bottom strip, rows 0.01–0.065)
        var searchAgainBtn = MakeButton("SearchAgainButton", panel.transform,
            "Search Again", 26, ColWhiteText);
        SetAnchors(searchAgainBtn, 0.1f, 0.01f, 0.9f, 0.063f);
        SetOffsets(searchAgainBtn, 0, 0, 0, 0);
        SetButtonColor(searchAgainBtn, ColSearchAgain);
    }

    // ScanLocationPanel
    //
    // Hierarchy:
    //   ScanLocationPanel
    //     Background
    //     BackButton          ("< Back", top-left)
    //     TitleText           ("Find Your Location", top center)
    //     TargetFoundText     (shelf found by search, large)
    //     InstructionText     (camera pointing instruction)
    //     ScanLocationButton  (big teal, "SCAN MY LOCATION")
    //     SkipButton          (grey, "Skip show map only")
    //     StatusText          (scan progress messages)

    private static void CreateScanLocationPanel(GameObject uiRoot)
    {
        var panel = MakePanel("ScanLocationPanel", uiRoot.transform);
        panel.SetActive(false);

        // Background
        var bg = MakeImage("Background", panel.transform, ColPanelBg);
        SetStretchFill(bg);

        // BackButton top-left, 0.9 – 1.0 height, 0 – 0.22 width
        var backBtn = MakeButton("BackButton", panel.transform, "< Back", 24, ColGreyText);
        SetAnchors(backBtn, 0f, 0.93f, 0.28f, 0.995f);
        SetOffsets(backBtn, 10, 4, -4, -4);
        SetButtonColor(backBtn, ColSkipBtn);

        // TitleText top center
        var titleGO = MakeText("TitleText", panel.transform,
            "Find Your Location", 30, ColGreenText, TextAlignmentOptions.Center);
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        SetAnchors(titleGO, 0.25f, 0.91f, 0.75f, 0.995f);
        SetOffsets(titleGO, 0, 0, 0, 0);

        // TargetFoundText upper body, shows found shelf
        var targetTextGO = MakeText("TargetFoundText", panel.transform,
            "Found: [shelf name]  ·  Floor N", 26, ColWhiteText, TextAlignmentOptions.Center);
        var tTMP = targetTextGO.GetComponent<TextMeshProUGUI>();
        tTMP.enableWordWrapping = true;
        tTMP.fontStyle = FontStyles.Bold;
        SetAnchors(targetTextGO, 0.04f, 0.76f, 0.96f, 0.905f);
        SetOffsets(targetTextGO, 0, 0, 0, 0);

        // InstructionText
        var instrGO = MakeText("InstructionText", panel.transform,
            "Stand at the shelf where you are now.\nPoint your camera at the shelf label paper\n(the card listing Dewey numbers and subjects).",
            23, ColGreyText, TextAlignmentOptions.Center);
        instrGO.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
        SetAnchors(instrGO, 0.04f, 0.60f, 0.96f, 0.75f);
        SetOffsets(instrGO, 0, 0, 0, 0);

        // ScanLocationButton big teal
        var scanBtn = MakeButton("ScanLocationButton", panel.transform,
            "SCAN MY LOCATION", 30, ColWhiteText);
        SetAnchors(scanBtn, 0.1f, 0.46f, 0.9f, 0.58f);
        SetOffsets(scanBtn, 0, 0, 0, 0);
        SetButtonColor(scanBtn, ColScanBtn);
        scanBtn.GetComponent<TextMeshProUGUI>()?.GetComponentInParent<Button>(); // no-op
        var scanTMP = scanBtn.transform.GetComponentInChildren<TextMeshProUGUI>();
        if (scanTMP != null) scanTMP.fontStyle = FontStyles.Bold;

        // SkipButton smaller grey
        var skipBtn = MakeButton("SkipButton", panel.transform,
            "Skip  —  show map without my location", 21, ColGreyText);
        SetAnchors(skipBtn, 0.1f, 0.37f, 0.9f, 0.44f);
        SetOffsets(skipBtn, 0, 0, 0, 0);
        SetButtonColor(skipBtn, ColSkipBtn);

        // StatusText scan progress / error messages
        var statusGO = MakeText("StatusText", panel.transform,
            "", 22, ColGreyText, TextAlignmentOptions.Center);
        statusGO.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
        SetAnchors(statusGO, 0.04f, 0.27f, 0.96f, 0.37f);
        SetOffsets(statusGO, 0, 0, 0, 0);
    }

    // UI Factory Helpers

    /// <summary>Creates an empty GameObject with a RectTransform (no Image) acts as a layout container.</summary>
    private static GameObject MakePanel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Creates a GameObject with an Image component.</summary>
    private static GameObject MakeImage(string name, Transform parent, Color32 color)
    {
        var go  = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    /// <summary>Creates a TextMeshProUGUI label.</summary>
    private static GameObject MakeText(string name, Transform parent,
        string text, float fontSize, Color32 color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Midline)
    {
        var go  = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = alignment;
        return go;
    }

    /// <summary>
    /// Creates a Button with a child TextMeshProUGUI label.
    /// Returns the root Button GameObject.
    /// </summary>
    private static GameObject MakeButton(string name, Transform parent,
        string label, float fontSize, Color32 textColor)
    {
        var go  = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color32(60, 80, 100, 255); // default; overridden by caller

        var labelGO = new GameObject("Text", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(go.transform, false);
        SetStretchFill(labelGO);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.color     = textColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return go;
    }

    private static void SetButtonColor(GameObject btn, Color32 color)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // Anchor / Offset Helpers

    private static void SetStretchFill(GameObject go)
        => SetAnchors(go, 0f, 0f, 1f, 1f);

    private static void SetAnchors(GameObject go,
        float minX, float minY, float maxX, float maxY)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetOffsets(GameObject go,
        float left, float bottom, float right, float top)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.offsetMin = new Vector2(left,   bottom);
        rt.offsetMax = new Vector2(right,  top);
    }

    // Scene Search

    private static GameObject FindAny(string name)
    {
        var active = GameObject.Find(name);
        if (active != null) return active;

        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.scene == SceneManager.GetActiveScene())
                return go;
        }
        return null;
    }
}
