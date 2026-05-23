using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FixTreasureHuntUI
{
    [MenuItem("Tools/Fix Treasure Hunt UI")]
    public static void Run()
    {
        FixCluePanel();
        FixStartPanel();
        FixCompletionPanel();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        Debug.Log("[FixTHUI] Done.");
    }

    // Finds a named GO even if inactive
    static GameObject Find(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.hideFlags == HideFlags.None)
                return t.gameObject;
        Debug.LogWarning($"[FixTHUI] '{name}' not found.");
        return null;
    }

    static GameObject Child(GameObject parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        Debug.LogWarning($"[FixTHUI] Child '{name}' not found under '{parent.name}'.");
        return null;
    }

    // RectTransform helpers 

    /// <summary>Stretch-anchors a rect to a normalized region of its parent.</summary>
    static void Anchor(GameObject go, float xMin, float yMin, float xMax, float yMax,
                       float padL = 0, float padR = 0, float padB = 0, float padT = 0)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(padL,  padB);
        rt.offsetMax = new Vector2(-padR, -padT);
    }

    static void StyleTMP(GameObject go, float size, TextAlignmentOptions align, Color col,
                         FontStyles style = FontStyles.Normal)
    {
        var t = go.GetComponent<TextMeshProUGUI>();
        if (t == null) return;
        t.fontSize  = size;
        t.alignment = align;
        t.color     = col;
        t.fontStyle = style;
        t.enableWordWrapping = true;
    }

    static void BgColor(GameObject go, Color col)
    {
        var img = go.GetComponent<Image>();
        if (img != null) img.color = col;
    }

    // CluePanel

    static void FixCluePanel()
    {
        var panel = Find("CluePanel");
        if (panel == null) return;

        // Full screen dark overlay so the AR camera shows through around it
        Anchor(panel, 0, 0, 1, 1);
        BgColor(panel, new Color(0.07f, 0.07f, 0.13f, 0.88f));

        // Progress label top-left strip
        var prog = Child(panel, "ProgressLabel");
        if (prog != null)
        {
            Anchor(prog, 0f, 0.93f, 0.65f, 1f, padL: 24, padT: 8);
            StyleTMP(prog, 34, TextAlignmentOptions.MidlineLeft, new Color(0.7f, 0.85f, 1f));
        }

        // Timer top-right strip
        var timer = Child(panel, "TimerText");
        if (timer != null)
        {
            Anchor(timer, 0.65f, 0.93f, 1f, 1f, padR: 24, padT: 8);
            StyleTMP(timer, 38, TextAlignmentOptions.MidlineRight, Color.white, FontStyles.Bold);
        }

        // Clue text big centred block in the middle
        var clue = Child(panel, "ClueText");
        if (clue != null)
        {
            Anchor(clue, 0f, 0.30f, 1f, 0.93f, padL: 40, padR: 40, padB: 12, padT: 12);
            StyleTMP(clue, 50, TextAlignmentOptions.Center, Color.white, FontStyles.Italic);
        }

        // Scan status thin band above the button
        var status = Child(panel, "ScanStatusText");
        if (status != null)
        {
            Anchor(status, 0f, 0.18f, 1f, 0.30f, padL: 30, padR: 30);
            StyleTMP(status, 32, TextAlignmentOptions.Center, new Color(0.65f, 0.85f, 1f));
        }

        // SCAN ME button large, bottom-anchored
        var btn = Child(panel, "ScanMeButton");
        if (btn != null)
        {
            Anchor(btn, 0.07f, 0.05f, 0.93f, 0.17f);
            BgColor(btn, new Color(0.07f, 0.52f, 0.28f)); // green

            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null)
            {
                lbl.text      = "SCAN ME";
                lbl.fontSize  = 54;
                lbl.fontStyle = FontStyles.Bold;
                lbl.color     = Color.white;
                lbl.alignment = TextAlignmentOptions.Center;
            }
        }

        Debug.Log("[FixTHUI] CluePanel done.");
    }

    // StartPanel

    static void FixStartPanel()
    {
        var panel = Find("StartPanel");
        if (panel == null) return;

        Anchor(panel, 0, 0, 1, 1);
        BgColor(panel, new Color(0.07f, 0.07f, 0.13f, 0.92f));

        var title = Child(panel, "TitleText");
        if (title != null)
        {
            Anchor(title, 0.05f, 0.62f, 0.95f, 0.80f);
            StyleTMP(title, 68, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        }

        var sub = Child(panel, "SubText");
        if (sub != null)
        {
            Anchor(sub, 0.05f, 0.50f, 0.95f, 0.62f);
            StyleTMP(sub, 36, TextAlignmentOptions.Center, new Color(0.75f, 0.85f, 1f));
        }

        // Start button
        var startBtn = Child(panel, "StartButton");
        if (startBtn != null)
        {
            Anchor(startBtn, 0.12f, 0.34f, 0.88f, 0.47f);
            BgColor(startBtn, new Color(0.07f, 0.52f, 0.28f));
            var lbl = startBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = "START"; lbl.fontSize = 52; lbl.fontStyle = FontStyles.Bold; lbl.color = Color.white; lbl.alignment = TextAlignmentOptions.Center; }
        }

        // Any other button = Exit
        foreach (var b in panel.GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject == startBtn) continue;
            Anchor(b.gameObject, 0.25f, 0.22f, 0.75f, 0.32f);
            BgColor(b.gameObject, new Color(0.55f, 0.1f, 0.1f));
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = "EXIT"; lbl.fontSize = 42; lbl.fontStyle = FontStyles.Bold; lbl.color = Color.white; lbl.alignment = TextAlignmentOptions.Center; }
        }

        Debug.Log("[FixTHUI] StartPanel done.");
    }

    // CompletionPanel

    static void FixCompletionPanel()
    {
        var panel = Find("CompletionPanel");
        if (panel == null) return;

        Anchor(panel, 0, 0, 1, 1);
        BgColor(panel, new Color(0.04f, 0.18f, 0.07f, 0.93f));

        var title = Child(panel, "CompleteTitle");
        if (title != null)
        {
            Anchor(title, 0.05f, 0.63f, 0.95f, 0.80f);
            StyleTMP(title, 70, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            var t = title.GetComponent<TextMeshProUGUI>();
            if (t != null && string.IsNullOrEmpty(t.text)) t.text = "Hunt Complete!";
        }

        var timeText = Child(panel, "CompletionTimeText");
        if (timeText != null)
        {
            Anchor(timeText, 0.05f, 0.50f, 0.95f, 0.63f);
            StyleTMP(timeText, 52, TextAlignmentOptions.Center, new Color(0.5f, 1f, 0.55f));
        }

        var body = Child(panel, "CompleteBody");
        if (body != null)
        {
            Anchor(body, 0.05f, 0.38f, 0.95f, 0.50f);
            StyleTMP(body, 36, TextAlignmentOptions.Center, new Color(0.8f, 0.9f, 0.8f));
        }

        foreach (var b in panel.GetComponentsInChildren<Button>(true))
        {
            Anchor(b.gameObject, 0.18f, 0.24f, 0.82f, 0.36f);
            BgColor(b.gameObject, new Color(0.12f, 0.12f, 0.20f));
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = "< Main Menu"; lbl.fontSize = 42; lbl.color = Color.white; lbl.alignment = TextAlignmentOptions.Center; }
        }

        Debug.Log("[FixTHUI] CompletionPanel done.");
    }
}
