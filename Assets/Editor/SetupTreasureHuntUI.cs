using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// One-shot editor script.
/// Adds TimerText, ScanMeButton to CluePanel and CompletionTimeText to CompletionPanel.
/// Run via: Tools > Setup Treasure Hunt UI
/// Safe to re-run skips elements that already exist.
/// </summary>
public class SetupTreasureHuntUI
{
    [MenuItem("Tools/Setup Treasure Hunt UI")]
    public static void Run()
    {
        // Find panels (may be inactive)
        Transform cluePanel       = FindTransform("CluePanel");
        Transform completionPanel = FindTransform("CompletionPanel");

        if (cluePanel == null)       { Debug.LogError("[SetupTHUI] CluePanel not found.");       return; }
        if (completionPanel == null) { Debug.LogError("[SetupTHUI] CompletionPanel not found."); return; }

        int created = 0;

        // 1. TimerText on CluePanel
        if (cluePanel.Find("TimerText") == null)
        {
            var go = CreateTMPText(cluePanel, "TimerText", "00:00",
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
                pivot: new Vector2(1, 1),
                anchoredPos: new Vector2(-24, -24),
                size: new Vector2(220, 60),
                fontSize: 38,
                alignment: TextAlignmentOptions.TopRight,
                color: Color.white);
            Undo.RegisterCreatedObjectUndo(go, "Create TimerText");
            created++;
            Debug.Log("[SetupTHUI] Created TimerText on CluePanel.");
        }
        else Debug.Log("[SetupTHUI] TimerText already exists — skipped.");

        // 2. ScanMeButton on CluePanel
        if (cluePanel.Find("ScanMeButton") == null)
        {
            var btn = CreateButton(cluePanel, "ScanMeButton", "SCAN ME",
                anchorMin: new Vector2(0.5f, 0),
                anchorMax: new Vector2(0.5f, 0),
                pivot: new Vector2(0.5f, 0),
                anchoredPos: new Vector2(0, 60),
                size: new Vector2(520, 110),
                bgColor: new Color(0.12f, 0.72f, 0.53f, 1f),
                fontSize: 48);
            Undo.RegisterCreatedObjectUndo(btn, "Create ScanMeButton");
            created++;
            Debug.Log("[SetupTHUI] Created ScanMeButton on CluePanel.");
        }
        else Debug.Log("[SetupTHUI] ScanMeButton already exists — skipped.");

        // 3. CompletionTimeText on CompletionPanel
        if (completionPanel.Find("CompletionTimeText") == null)
        {
            // Insert after CompleteTitle find its sibling index
            Transform title = completionPanel.Find("CompleteTitle");
            int insertIdx = title != null ? title.GetSiblingIndex() + 1 : 1;

            var go = CreateTMPText(completionPanel, "CompletionTimeText", "Completed in 00:00",
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(0, 60),
                size: new Vector2(800, 80),
                fontSize: 40,
                alignment: TextAlignmentOptions.Center,
                color: new Color(0.4f, 0.85f, 0.65f, 1f)); // mint green to stand out
            go.GetComponent<RectTransform>().SetSiblingIndex(insertIdx);
            Undo.RegisterCreatedObjectUndo(go, "Create CompletionTimeText");
            created++;
            Debug.Log("[SetupTHUI] Created CompletionTimeText on CompletionPanel.");
        }
        else Debug.Log("[SetupTHUI] CompletionTimeText already exists — skipped.");

        if (created > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[SetupTHUI] Done — {created} element(s) created. Save the scene.");
        }
        else
        {
            Debug.Log("[SetupTHUI] Nothing to do — all elements already present.");
        }
    }

    // Helpers

    private static Transform FindTransform(string name)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == name && go.scene.IsValid()) return go.transform;
        return null;
    }

    private static GameObject CreateTMPText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.pivot           = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = alignment;
        tmp.color     = color;
        tmp.enableWordWrapping = false;

        return go;
    }

    private static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        Color bgColor, float fontSize)
    {
        // Button root
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.pivot           = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = size;

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // Colors
        var colors = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor     = bgColor * 0.8f;
        colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        // Label child
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.layer = go.layer;
        labelGO.transform.SetParent(go.transform, false);

        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin       = Vector2.zero;
        lrt.anchorMax       = Vector2.one;
        lrt.offsetMin       = Vector2.zero;
        lrt.offsetMax       = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.enableWordWrapping = false;

        return go;
    }
}
