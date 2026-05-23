// WireSceneReferences.cs run via  AR Library > Wire Scene References
//
// Wires all Inspector object reference fields AND button onClick events that
//
// Object references (23)
//   [Existing]
//   1.  NavigationController.navigationRoot           -> NavigationRoot Transform
//   2.  MarkerLocalizationManager.uiCanvas             -> UIRoot Canvas
//   3.  TreasureHuntController.navigationController   -> Managers
//   4.  TreasureHuntController.localizationManager    -> Managers
//   5.  TreasureHuntController.timerText              -> CluePanel/TimerText
//   6.  TreasureHuntController.scanMeButton           -> CluePanel/ScanMeButton
//   7.  TreasureHuntController.completionTimeText     -> CompletionPanel/CompletionTimeText
//   [Floor Plan / Shelf Scan — NEW]
//   8.  BookSearchController.shelfScanner             -> ShelfScanner on Managers
//   9.  BookSearchController.floorPlanViewController  -> FloorPlanViewController on Managers
//   10. BookSearchController.navGraph                 -> LibraryNavGraph.asset
//   11. BookSearchController.scanLocationPanel        -> UIRoot/ScanLocationPanel
//   12. BookSearchController.targetFoundText          -> ScanLocationPanel/TargetFoundText
//   13. BookSearchController.scanStatusText           -> ScanLocationPanel/StatusText
//   14. BookSearchController.scanLocationButton       -> ScanLocationPanel/ScanLocationButton
//   15. FloorPlanViewController.navGraph              -> LibraryNavGraph.asset
//   16. FloorPlanViewController.floorPlanPanel        -> UIRoot/FloorPlanPanel
//   17. FloorPlanViewController.floorPlanImage        -> FloorPlanPanel/MapContainer/FloorPlanImage
//   18. FloorPlanViewController.overlayRoot           -> FloorPlanPanel/MapContainer/OverlayRoot
//   19. FloorPlanViewController.stepsText             -> FloorPlanPanel/StepsPanel/StepsText
//   20. FloorPlanViewController.titleText             -> FloorPlanPanel/Header/TitleText
//   21. FloorPlanViewController.floorPlanLevel2       -> FloorPlans/Second Floor Plan - Level 2...
//   22. FloorPlanViewController.floorPlanLevel3       -> FloorPlans/Third Floor Plan - Level 3...
//   23. ShelfScanner.labelOCR                         -> ShelfLabelOCR component
//   24. ShelfScanner.sessionLogger                    -> SessionLogger on Managers
//
// Button onClick events (9)
//   [Existing]
//   A.  BookSearchPanel/TopBar/ExitButton             -> AppModeController.ReturnToMainMenu()
//   B.  BookSearchPanel/ActionRow/ExitBtn             -> AppModeController.ReturnToMainMenu()
//   C.  TreasureHuntPanel/CompletionPanel/ExitButton  -> TreasureHuntController.OnExitClicked()
//   D.  TreasureHuntPanel/CluePanel/ScanMeButton      -> TreasureHuntController.OnScanMeClicked()
//   [New]
//   E.  ScanLocationPanel/ScanLocationButton          -> BookSearchController.OnScanLocationClicked()
//   F.  ScanLocationPanel/SkipButton                  -> BookSearchController.OnSkipScanClicked()
//   G.  ScanLocationPanel/BackButton                  -> BookSearchController.OnSearchAgainClicked()
//   H.  FloorPlanPanel/Header/CloseButton             -> FloorPlanViewController.OnCloseClicked()
//   I.  FloorPlanPanel/SearchAgainButton              -> BookSearchController.OnSearchAgainClicked()
//
// Scene cleanup
//   - ShelfDataLoader (deprecated) — disabled if still active
//   - ShelfLabelOCR GameObject — created if missing
//   - ShelfScanner component — added to Managers if missing

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ARLibraryNav.Navigation;
using ARLibraryNav.AR;
using ARLibraryNav.UI;

public static class WireSceneReferences
{
    [MenuItem("AR Library/Wire Scene References")]
    static void Wire()
    {
        int wired = 0;

        // Locate root GameObjects
        var managers           = FindAny("Managers");
        var navigationRoot     = FindAny("NavigationRoot");
        var uiRoot             = FindAny("UIRoot");
        var bookSearchPanel    = FindAny("BookSearchPanel");
        var treasureHuntPanel  = FindAny("TreasureHuntPanel");
        var scanLocationPanel  = FindAny("ScanLocationPanel");
        var floorPlanPanelGO   = FindAny("FloorPlanPanel");

        if (managers == null) { Debug.LogError("[WireRefs] 'Managers' not found."); return; }
        if (uiRoot   == null)   Debug.LogWarning("[WireRefs] 'UIRoot' not found.");

        // Ensure ShelfScanner is on Managers
        var shelfScanner = managers.GetComponent<ShelfScanner>();
        if (shelfScanner == null)
        {
            shelfScanner = managers.AddComponent<ShelfScanner>();
            Debug.Log("[WireRefs] Added ShelfScanner to Managers.");
        }

        // Ensure ShelfLabelOCR has its own GameObject
        ShelfLabelOCR labelOCR = null;
        var allLabelOCRs = Resources.FindObjectsOfTypeAll<ShelfLabelOCR>();
        if (allLabelOCRs.Length > 0)
        {
            labelOCR = allLabelOCRs[0];
            Debug.Log("[WireRefs] ShelfLabelOCR found on: " + labelOCR.gameObject.name);
        }
        else
        {
            // new GameObject() automatically belongs to the active scene
            var bridgeGO = new GameObject("ShelfLabelOCR");
            labelOCR = bridgeGO.AddComponent<ShelfLabelOCR>();
            Debug.Log("[WireRefs] Created ShelfLabelOCR GameObject.");
        }

        // Load ScriptableObject assets via AssetDatabase
        const string navGraphPath = "Assets/Scripts/Navigation/LibraryNavGraph.asset";
        var navGraphAsset = AssetDatabase.LoadAssetAtPath<NavGraph>(navGraphPath);
        if (navGraphAsset == null)
            Debug.LogWarning($"[WireRefs] NavGraph not found at: {navGraphPath}");

        const string level2TexPath = "Assets/FloorPlans/Second Floor Plan - Level 2 05.02_p1.png";
        const string level3TexPath = "Assets/FloorPlans/Third Floor Plan - Level 3 05_p1.png";
        var level2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>(level2TexPath);
        var level3Tex = AssetDatabase.LoadAssetAtPath<Texture2D>(level3TexPath);
        if (level2Tex == null) Debug.LogWarning($"[WireRefs] Level 2 floor plan texture not found at: {level2TexPath}");
        if (level3Tex == null) Debug.LogWarning($"[WireRefs] Level 3 floor plan texture not found at: {level3TexPath}");

        // Locate components on Managers
        var navCtrl  = managers.GetComponent<ARLibraryNav.Navigation.NavigationController>();
        var mlm      = managers.GetComponent<ARLibraryNav.AR.MarkerLocalizationManager>();
        var thc      = managers.GetComponent<ARLibraryNav.UI.TreasureHuntController>();
        var amc      = managers.GetComponent<ARLibraryNav.AppState.AppModeController>();
        var bsc      = managers.GetComponent<ARLibraryNav.UI.BookSearchController>()
                    ?? (bookSearchPanel != null
                        ? bookSearchPanel.GetComponent<ARLibraryNav.UI.BookSearchController>()
                        : null);
        var fpvc     = managers.GetComponent<FloorPlanViewController>();
        var sessLog  = managers.GetComponent<ARLibraryNav.Logging.SessionLogger>();

        if (bsc  == null) Debug.LogWarning("[WireRefs] BookSearchController not found on Managers or BookSearchPanel.");
        if (fpvc == null) Debug.LogWarning("[WireRefs] FloorPlanViewController not found on Managers — run 'Tools/Setup Floor Plan UI' first.");

  
        // 1. NavigationController.navigationRoot -> NavigationRoot

        if (navCtrl != null && navigationRoot != null)
        {
            var so   = new SerializedObject(navCtrl);
            var prop = so.FindProperty("navigationRoot");
            if (prop != null)
            {
                prop.objectReferenceValue = navigationRoot.transform;
                so.ApplyModifiedProperties();
                Debug.Log("[WireRefs] 1. NavigationController.navigationRoot -> NavigationRoot");
                wired++;
            }
        }

        // 2. MarkerLocalizationManager.uiCanvas -> UIRoot Canvas

        if (mlm != null && uiRoot != null)
        {
            var canvas = uiRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                var so   = new SerializedObject(mlm);
                var prop = so.FindProperty("uiCanvas");
                if (prop != null) { prop.objectReferenceValue = canvas; so.ApplyModifiedProperties(); wired++; }
                Debug.Log("[WireRefs] 2. MarkerLocalizationManager.uiCanvas -> UIRoot");
            }
        }

        // 3 & 4. TreasureHuntController navCtrl + localizationManager

        if (thc != null)
        {
            var so = new SerializedObject(thc);
            if (navCtrl != null)
            {
                var p = so.FindProperty("navigationController");
                if (p != null) { p.objectReferenceValue = navCtrl; wired++; }
            }
            if (mlm != null)
            {
                var p = so.FindProperty("localizationManager");
                if (p != null) { p.objectReferenceValue = mlm; wired++; }
            }
            so.ApplyModifiedProperties();
            Debug.Log("[WireRefs] 3-4. TreasureHuntController.navigationController + localizationManager");
        }

        // 5-7. TreasureHuntController timerText, scanMeButton, completionTimeText

        if (thc != null)
        {
            var cluePanel       = FindAny("CluePanel");
            var completionPanel = FindAny("CompletionPanel");
            var so              = new SerializedObject(thc);

            if (cluePanel != null)
            {
                TryWireChild(so, "timerText",   cluePanel, "TimerText",   typeof(TMPro.TextMeshProUGUI), ref wired);
                TryWireChild(so, "scanMeButton", cluePanel, "ScanMeButton", typeof(Button), ref wired);
            }
            if (completionPanel != null)
                TryWireChild(so, "completionTimeText", completionPanel, "CompletionTimeText", typeof(TMPro.TextMeshProUGUI), ref wired);

            so.ApplyModifiedProperties();
            Debug.Log("[WireRefs] 5-7. TreasureHuntController timer/scan/completion refs");
        }

        // 8. BookSearchController.shelfScanner -> ShelfScanner on Managers

        if (bsc != null && shelfScanner != null)
        {
            var so   = new SerializedObject(bsc);
            var prop = so.FindProperty("shelfScanner");
            if (prop != null) { prop.objectReferenceValue = shelfScanner; so.ApplyModifiedProperties(); wired++; }
            Debug.Log("[WireRefs] 8.  BookSearchController.shelfScanner -> Managers");
        }

        // 9. BookSearchController.floorPlanViewController -> FloorPlanViewController

        if (bsc != null && fpvc != null)
        {
            var so   = new SerializedObject(bsc);
            var prop = so.FindProperty("floorPlanViewController");
            if (prop != null) { prop.objectReferenceValue = fpvc; so.ApplyModifiedProperties(); wired++; }
            Debug.Log("[WireRefs] 9.  BookSearchController.floorPlanViewController -> Managers");
        }

        // 10. BookSearchController.navGraph -> LibraryNavGraph.asset

        if (bsc != null && navGraphAsset != null)
        {
            var so   = new SerializedObject(bsc);
            var prop = so.FindProperty("navGraph");
            if (prop != null) { prop.objectReferenceValue = navGraphAsset; so.ApplyModifiedProperties(); wired++; }
            Debug.Log("[WireRefs] 10. BookSearchController.navGraph -> LibraryNavGraph.asset");
        }

        // 11-14. BookSearchController — ScanLocationPanel refs

        if (bsc != null && scanLocationPanel != null)
        {
            var so = new SerializedObject(bsc);

            var panelProp = so.FindProperty("scanLocationPanel");
            if (panelProp != null) { panelProp.objectReferenceValue = scanLocationPanel; wired++; }

            TryWireChild(so, "targetFoundText",   scanLocationPanel, "TargetFoundText",   typeof(TMPro.TextMeshProUGUI), ref wired);
            TryWireChild(so, "scanStatusText",    scanLocationPanel, "StatusText",         typeof(TMPro.TextMeshProUGUI), ref wired);
            TryWireChild(so, "scanLocationButton",scanLocationPanel, "ScanLocationButton", typeof(Button), ref wired);

            so.ApplyModifiedProperties();
            Debug.Log("[WireRefs] 11-14. BookSearchController ScanLocationPanel refs");
        }
        else if (scanLocationPanel == null)
            Debug.LogWarning("[WireRefs] ScanLocationPanel not found — run 'Tools/Setup Floor Plan UI' first.");

        // 15. FloorPlanViewController.navGraph -> LibraryNavGraph.asset

        if (fpvc != null && navGraphAsset != null)
        {
            var so   = new SerializedObject(fpvc);
            var prop = so.FindProperty("navGraph");
            if (prop != null) { prop.objectReferenceValue = navGraphAsset; so.ApplyModifiedProperties(); wired++; }
            Debug.Log("[WireRefs] 15. FloorPlanViewController.navGraph -> LibraryNavGraph.asset");
        }

        // 16-22. FloorPlanViewController — panel, image, overlay, texts, textures

        if (fpvc != null && floorPlanPanelGO != null)
        {
            var so = new SerializedObject(fpvc);

            // 16. floorPlanPanel (GameObject)
            var panelProp = so.FindProperty("floorPlanPanel");
            if (panelProp != null) { panelProp.objectReferenceValue = floorPlanPanelGO; wired++; }

            // 17. floorPlanImage (RawImage inside MapContainer)
            var mapContainer = floorPlanPanelGO.transform.Find("MapContainer");
            if (mapContainer != null)
            {
                var fpImgTransform = mapContainer.Find("FloorPlanImage");
                if (fpImgTransform != null)
                {
                    var imgProp = so.FindProperty("floorPlanImage");
                    if (imgProp != null)
                    {
                        imgProp.objectReferenceValue = fpImgTransform.GetComponent<UnityEngine.UI.RawImage>();
                        wired++;
                    }
                }
                else Debug.LogWarning("[WireRefs] FloorPlanPanel/MapContainer/FloorPlanImage not found.");

                // 18. overlayRoot (RectTransform)
                var overlayTransform = mapContainer.Find("OverlayRoot");
                if (overlayTransform != null)
                {
                    var overlayProp = so.FindProperty("overlayRoot");
                    if (overlayProp != null)
                    {
                        overlayProp.objectReferenceValue = overlayTransform.GetComponent<RectTransform>();
                        wired++;
                    }
                }
                else Debug.LogWarning("[WireRefs] FloorPlanPanel/MapContainer/OverlayRoot not found.");
            }
            else Debug.LogWarning("[WireRefs] FloorPlanPanel/MapContainer not found.");

            // 19. stepsText
            var stepsPanel = floorPlanPanelGO.transform.Find("StepsPanel");
            if (stepsPanel != null)
            {
                var stepsTextT = stepsPanel.Find("StepsText");
                if (stepsTextT != null)
                {
                    var p = so.FindProperty("stepsText");
                    if (p != null) { p.objectReferenceValue = stepsTextT.GetComponent<TMPro.TextMeshProUGUI>(); wired++; }
                }
                else Debug.LogWarning("[WireRefs] FloorPlanPanel/StepsPanel/StepsText not found.");
            }

            // 20. titleText
            var header = floorPlanPanelGO.transform.Find("Header");
            if (header != null)
            {
                var titleT = header.Find("TitleText");
                if (titleT != null)
                {
                    var p = so.FindProperty("titleText");
                    if (p != null) { p.objectReferenceValue = titleT.GetComponent<TMPro.TextMeshProUGUI>(); wired++; }
                }
                else Debug.LogWarning("[WireRefs] FloorPlanPanel/Header/TitleText not found.");
            }

            // 21 & 22. Floor plan textures
            if (level2Tex != null)
            {
                var p = so.FindProperty("floorPlanLevel2");
                if (p != null) { p.objectReferenceValue = level2Tex; wired++; }
                Debug.Log("[WireRefs] 21. FloorPlanViewController.floorPlanLevel2 -> Level 2 texture");
            }
            if (level3Tex != null)
            {
                var p = so.FindProperty("floorPlanLevel3");
                if (p != null) { p.objectReferenceValue = level3Tex; wired++; }
                Debug.Log("[WireRefs] 22. FloorPlanViewController.floorPlanLevel3 -> Level 3 texture");
            }

            so.ApplyModifiedProperties();
            Debug.Log("[WireRefs] 16-22. FloorPlanViewController panel/image/overlay/text/texture refs");
        }
        else if (floorPlanPanelGO == null)
            Debug.LogWarning("[WireRefs] FloorPlanPanel not found — run 'Tools/Setup Floor Plan UI' first.");

        // 23 & 24. ShelfScanner labelOCR + sessionLogger

        if (shelfScanner != null)
        {
            var so = new SerializedObject(shelfScanner);

            if (labelOCR != null)
            {
                var p = so.FindProperty("labelOCR");
                if (p != null) { p.objectReferenceValue = labelOCR; wired++; }
                Debug.Log("[WireRefs] 23. ShelfScanner.labelOCR -> ShelfLabelOCR");
            }

            if (sessLog != null)
            {
                var p = so.FindProperty("sessionLogger");
                if (p != null) { p.objectReferenceValue = sessLog; wired++; }
                Debug.Log("[WireRefs] 24. ShelfScanner.sessionLogger -> SessionLogger on Managers");
            }

            so.ApplyModifiedProperties();
        }

        // Button onClick A & B: BookSearchPanel exit buttons -> ReturnToMainMenu

        if (bookSearchPanel != null && amc != null)
        {
            var btn = FindButtonInChild(bookSearchPanel, "TopBar/ExitButton");
            if (btn != null) { WireButtonOnClick(btn, amc, "ReturnToMainMenu"); wired++; Debug.Log("[WireRefs] A. BookSearch ExitButton -> ReturnToMainMenu"); }
            btn = FindButtonInChild(bookSearchPanel, "ActionRow/ExitBtn");
            if (btn != null) { WireButtonOnClick(btn, amc, "ReturnToMainMenu"); wired++; Debug.Log("[WireRefs] B. BookSearch ExitBtn -> ReturnToMainMenu"); }
        }

        // Button onClick C & D: TreasureHunt buttons

        if (treasureHuntPanel != null && thc != null)
        {
            var btn = FindButtonInChild(treasureHuntPanel, "CompletionPanel/ExitButton");
            if (btn != null) { WireButtonOnClick(btn, thc, "OnExitClicked"); wired++; Debug.Log("[WireRefs] C. TH CompletionPanel ExitButton -> OnExitClicked"); }
            btn = FindButtonInChild(treasureHuntPanel, "CluePanel/ScanMeButton");
            if (btn != null) { WireButtonOnClick(btn, thc, "OnScanMeClicked"); wired++; Debug.Log("[WireRefs] D. TH ScanMeButton -> OnScanMeClicked"); }
        }

        // Button onClick E, F, G: ScanLocationPanel buttons

        if (scanLocationPanel != null && bsc != null)
        {
            var btn = FindButtonInChild(scanLocationPanel, "ScanLocationButton");
            if (btn != null) { WireButtonOnClick(btn, bsc, "OnScanLocationClicked"); wired++; Debug.Log("[WireRefs] E. ScanLocationButton -> OnScanLocationClicked"); }
            btn = FindButtonInChild(scanLocationPanel, "SkipButton");
            if (btn != null) { WireButtonOnClick(btn, bsc, "OnSkipScanClicked"); wired++; Debug.Log("[WireRefs] F. SkipButton -> OnSkipScanClicked"); }
            btn = FindButtonInChild(scanLocationPanel, "BackButton");
            if (btn != null) { WireButtonOnClick(btn, bsc, "OnSearchAgainClicked"); wired++; Debug.Log("[WireRefs] G. BackButton -> OnSearchAgainClicked"); }
        }
        else if (scanLocationPanel == null)
            Debug.LogWarning("[WireRefs] ScanLocationPanel buttons not wired — panel missing.");

        // Button onClick H: FloorPlanPanel/Header/CloseButton -> OnCloseClicked

        if (floorPlanPanelGO != null && fpvc != null)
        {
            var btn = FindButtonInChild(floorPlanPanelGO, "Header/CloseButton");
            if (btn != null) { WireButtonOnClick(btn, fpvc, "OnCloseClicked"); wired++; Debug.Log("[WireRefs] H. FloorPlanPanel CloseButton -> OnCloseClicked"); }

            // Button onClick I: FloorPlanPanel/SearchAgainButton -> OnSearchAgainClicked

            if (bsc != null)
            {
                var btn2 = FindButtonInChild(floorPlanPanelGO, "SearchAgainButton");
                if (btn2 != null) { WireButtonOnClick(btn2, bsc, "OnSearchAgainClicked"); wired++; Debug.Log("[WireRefs] I. FloorPlanPanel SearchAgainButton -> OnSearchAgainClicked"); }
            }
        }

        // Scene cleanup disable deprecated ShelfDataLoader

        var sdl = managers.GetComponent("ShelfDataLoader") as MonoBehaviour;
        if (sdl != null && sdl.enabled)
        {
            sdl.enabled = false;
            Debug.Log("[WireRefs] Disabled deprecated ShelfDataLoader.");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[WireRefs] Done — {wired} references/events wired. Press Ctrl+S to save the scene.");
    }

    // Helpers

    /// <summary>
    /// Wires a single serialized field by child path if the component exists.
    /// field type must be a Component retrievable via GetComponent(type).
    /// </summary>
    static void TryWireChild(SerializedObject so, string fieldName,
        GameObject parentGO, string childName, System.Type componentType, ref int wired)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[WireRefs] Property '{fieldName}' not found on {so.targetObject.GetType().Name}.");
            return;
        }
        var childT = parentGO.transform.Find(childName);
        if (childT == null)
        {
            Debug.LogWarning($"[WireRefs] Child '{childName}' not found under '{parentGO.name}'.");
            return;
        }
        var comp = childT.GetComponent(componentType);
        if (comp == null)
        {
            Debug.LogWarning($"[WireRefs] No {componentType.Name} on '{parentGO.name}/{childName}'.");
            return;
        }
        prop.objectReferenceValue = comp;
        wired++;
    }

    /// <summary>Finds a Button by relative transform path from a root (works with inactive).</summary>
    static Button FindButtonInChild(GameObject root, string relativePath)
    {
        if (root == null) return null;
        var t = root.transform.Find(relativePath);
        if (t == null) { Debug.LogWarning($"[WireRefs] Path not found: {root.name}/{relativePath}"); return null; }
        var btn = t.GetComponent<Button>();
        if (btn == null) Debug.LogWarning($"[WireRefs] No Button at: {root.name}/{relativePath}");
        return btn;
    }

    /// <summary>Wires a Button's persistent onClick to a specific method on a MonoBehaviour.</summary>
    static void WireButtonOnClick(Button btn, MonoBehaviour target, string methodName)
    {
        if (btn == null || target == null) return;
        var so    = new SerializedObject(btn);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue              = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue       = target.GetType().AssemblyQualifiedName;
        call.FindPropertyRelative("m_MethodName").stringValue                   = methodName;
        call.FindPropertyRelative("m_Mode").intValue                            = 1; // Void (no args)
        call.FindPropertyRelative("m_CallState").intValue                       = 2; // RuntimeOnly
        so.ApplyModifiedProperties();
    }

    /// <summary>Finds a GameObject by name includes inactive objects in the active scene.</summary>
    static GameObject FindAny(string name)
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
