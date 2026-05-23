using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ARLibraryNav.API;
using ARLibraryNav.Data;
using ARLibraryNav.Navigation;

namespace ARLibraryNav.AR
{
    /// <summary>
    /// Test/Debug mode panel three independent test functions on one scrollable screen.
    ///
    /// Function 1 Book Search DB Test:
    ///   Type a query -> searches the local library database -> shows shelf name + floor.
    ///
    /// Function 2 Movement Detection:
    ///   Live accelerometer readout shows Left / Right / Forward / Backward / Still.
    ///
    /// Function 3 Gemini Vision Location ID:
    ///   "Scan Location" -> captures camera frame -> Gemini identifies which library node it is.
    ///
    /// Opened automatically by AppModeController.ForceShow() when Test mode is selected.
    /// Also accessible via triple tap on bottom left corner in any mode.
    ///
    /// Attach to: Managers in ARScene. Wire [SerializeField] references in Inspector.
    /// </summary>
    public class DebugLocalizationPanel : MonoBehaviour
    {
        // Inspector Dependencies
        [Header("Dependencies")]
        [SerializeField] private NavGraph                navGraph;
        [SerializeField] private MarkerLocalizationManager localizationManager;
        [SerializeField] private LibraryDatabaseSearch   librarySearch;
        [SerializeField] private GeminiClassifier        geminiClassifier;
        [SerializeField] private MovementDetector        movementDetector;

        // Palette
        static readonly Color C_BG        = new Color(0.07f, 0.09f, 0.14f, 1f);
        static readonly Color C_HEADER    = new Color(0.08f, 0.47f, 0.98f, 1f);
        static readonly Color C_SECTION   = new Color(0.10f, 0.14f, 0.22f, 1f);
        static readonly Color C_BTN       = new Color(0.18f, 0.32f, 0.55f, 1f);
        static readonly Color C_BTN_ACT   = new Color(0.08f, 0.47f, 0.98f, 1f);
        static readonly Color C_TOGGLE    = new Color(0.18f, 0.24f, 0.38f, 1f);
        static readonly Color C_DIM       = new Color(0.70f, 0.78f, 0.90f, 1f);

        // UI State
        private GameObject      _panel;
        private bool            _visible = false;

        // Function 1
        private TMP_InputField  _f1Input;
        private TextMeshProUGUI _f1Result;
        private Button          _f1Button;

        // Function 2
        private TextMeshProUGUI _f2Direction;

        // Function 3
        private TextMeshProUGUI _f3Result;
        private Button          _f3Button;

        // Triple tap
        private int   _tapCount = 0;
        private float _tapWindow = 0f;

        // Unity Lifecycle

        private void Awake()
        {
            BuildUI();
        }

        private void Update()
        {
            UpdateMovementDisplay();
            HandleTripleTap();
        }

        // Public API

        /// <summary>Called by AppModeController when entering Test mode.</summary>
        public void ForceShow()
        {
            if (_panel != null)
            {
                _visible = true;
                _panel.SetActive(true);
            }
        }

        // Function 1 Logic

        private void OnF1Search()
        {
            if (librarySearch == null)
            {
                SetF1Result("LibraryDatabaseSearch not wired.", Color.red);
                return;
            }

            string query = _f1Input != null ? _f1Input.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(query)) return;

            SetF1Result("Searching…", C_DIM);
            if (_f1Button != null) _f1Button.interactable = false;

            librarySearch.Search(query,
                result =>
                {
                    string txt = $"OK  {result.ShelfLabel}\n    Floor {result.Floor}  ·  {result.CallNumber}";
                    SetF1Result(txt, new Color(0.2f, 0.9f, 0.4f, 1f));
                    if (_f1Button != null) _f1Button.interactable = true;
                },
                err =>
                {
                    SetF1Result($"FAIL  {err}", new Color(0.9f, 0.3f, 0.3f, 1f));
                    if (_f1Button != null) _f1Button.interactable = true;
                });
        }

        private void SetF1Result(string text, Color color)
        {
            if (_f1Result == null) return;
            _f1Result.text  = text;
            _f1Result.color = color;
        }

        // Function 2 Logic

        private void UpdateMovementDisplay()
        {
            if (_f2Direction == null || movementDetector == null) return;
            var dir = movementDetector.CurrentDirection;
            string arrow = dir switch
            {
                MovementDetector.Direction.Forward  => "▲  Forward",
                MovementDetector.Direction.Backward => "▼  Backward",
                MovementDetector.Direction.Left     => "◄  Left",
                MovementDetector.Direction.Right    => "►  Right",
                _                                   => "●  Still"
            };
            _f2Direction.text = arrow;
        }

        // Function 3 Logic

        private void OnF3Scan()
        {
            if (geminiClassifier == null)
            {
                SetF3Result("GeminiClassifier not wired.", Color.red);
                return;
            }

            if (navGraph == null)
            {
                SetF3Result("NavGraph not wired.", Color.red);
                return;
            }

            var descriptions = new Dictionary<string, string>();
            foreach (string id in navGraph.AllNodeIDs())
            {
                var node = navGraph.GetNode(id);
                if (node != null && !string.IsNullOrWhiteSpace(node.visualDescription))
                    descriptions[node.nodeID] = node.visualDescription;
            }

            if (descriptions.Count == 0)
            {
                SetF3Result("No visualDescriptions on NavGraph nodes.", Color.yellow);
                return;
            }

            SetF3Result("Scanning…", C_DIM);
            if (_f3Button != null) _f3Button.interactable = false;

            StartCoroutine(CaptureAndIdentify(descriptions));
        }

        private IEnumerator CaptureAndIdentify(Dictionary<string, string> descriptions)
        {
            yield return new WaitForEndOfFrame();

            byte[] jpeg = CaptureFrameAsJpeg();
            if (jpeg == null)
            {
                SetF3Result("Frame capture failed.", Color.red);
                if (_f3Button != null) _f3Button.interactable = true;
                yield break;
            }

            geminiClassifier.IdentifyLocation(descriptions, jpeg,
                nodeID =>
                {
                    var node = navGraph.GetNode(nodeID);
                    string label = node != null ? node.displayLabel : nodeID;
                    string txt = nodeID == "UNKNOWN"
                        ? "FAIL  Location not recognised"
                        : $"OK  {label}\n    ({nodeID})";
                    SetF3Result(txt, nodeID == "UNKNOWN"
                        ? new Color(0.9f, 0.7f, 0.2f, 1f)
                        : new Color(0.2f, 0.9f, 0.4f, 1f));
                    if (_f3Button != null) _f3Button.interactable = true;
                },
                err =>
                {
                    SetF3Result($"FAIL  {err}", new Color(0.9f, 0.3f, 0.3f, 1f));
                    if (_f3Button != null) _f3Button.interactable = true;
                });
        }

        private void SetF3Result(string text, Color color)
        {
            if (_f3Result == null) return;
            _f3Result.text  = text;
            _f3Result.color = color;
        }

        // UI Construction

        private void BuildUI()
        {
            // Root canvas (overlay, on top of everything)
            var canvasGO = MakeGO("DebugLocCanvas", gameObject);
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2160);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // DBG toggle button (bottom left corner, always visible)
            var toggleGO = MakeGO("ToggleBtn", canvasGO);
            var toggleRT = toggleGO.AddComponent<RectTransform>();
            toggleRT.anchorMin = new Vector2(0, 0);
            toggleRT.anchorMax = new Vector2(0, 0);
            toggleRT.pivot     = new Vector2(0, 0);
            toggleRT.anchoredPosition = new Vector2(16, 16);
            toggleRT.sizeDelta        = new Vector2(88, 44);
            toggleGO.AddComponent<Image>().color = C_TOGGLE;
            var toggleBtn = toggleGO.AddComponent<Button>();
            toggleBtn.onClick.AddListener(TogglePanel);
            var toggleLbl = MakeTMP(toggleGO, "DBG", 18, Color.white, TextAlignmentOptions.Center);
            StretchFill(toggleLbl.gameObject, toggleGO);

            // Full screen panel
            _panel = MakeGO("DebugPanel", canvasGO);
            var panelRT = _panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            _panel.AddComponent<Image>().color = C_BG;

            // Header bar
            var headerGO = MakeGO("Header", _panel);
            var hRT = headerGO.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0, 1);
            hRT.anchorMax = new Vector2(1, 1);
            hRT.pivot     = new Vector2(0.5f, 1);
            hRT.anchoredPosition = Vector2.zero;
            hRT.sizeDelta = new Vector2(0, 80);
            headerGO.AddComponent<Image>().color = C_HEADER;
            var hTmp = MakeTMP(headerGO, "TEST MODE", 28, Color.white, TextAlignmentOptions.Center);
            hTmp.fontStyle = FontStyles.Bold;
            StretchFill(hTmp.gameObject, headerGO);

            // Exit to Menu button (top left of panel)
            var exitMenuGO = MakeGO("ExitMenuBtn", _panel);
            var exitMenuRT = exitMenuGO.AddComponent<RectTransform>();
            exitMenuRT.anchorMin = new Vector2(0, 1);
            exitMenuRT.anchorMax = new Vector2(0, 1);
            exitMenuRT.pivot     = new Vector2(0, 1);
            exitMenuRT.anchoredPosition = new Vector2(16, -16);
            exitMenuRT.sizeDelta = new Vector2(140, 48);
            exitMenuGO.AddComponent<Image>().color = new Color(0.5f, 0.12f, 0.12f, 1f);
            var exitMenuBtn = exitMenuGO.AddComponent<Button>();
            exitMenuBtn.onClick.AddListener(ExitToMainMenu);
            var exitMenuLbl = MakeTMP(exitMenuGO, "< Main Menu", 17, Color.white, TextAlignmentOptions.Center);
            StretchFill(exitMenuLbl.gameObject, exitMenuGO);

            // Close button (top right hides the debug panel, stays in current mode)
            var closeGO = MakeGO("CloseBtn", _panel);
            var closeRT = closeGO.AddComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 1);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot     = new Vector2(1, 1);
            closeRT.anchoredPosition = new Vector2(-16, -16);
            closeRT.sizeDelta = new Vector2(88, 48);
            closeGO.AddComponent<Image>().color = new Color(0.25f, 0.28f, 0.38f, 1f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.onClick.AddListener(TogglePanel);
            var closeLbl = MakeTMP(closeGO, "X  Close", 18, Color.white, TextAlignmentOptions.Center);
            StretchFill(closeLbl.gameObject, closeGO);

            // Scrollable content area
            var scrollGO = MakeGO("ScrollView", _panel);
            var sRT = scrollGO.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0, 0);
            sRT.anchorMax = new Vector2(1, 1);
            sRT.offsetMin = new Vector2(0, 0);
            sRT.offsetMax = new Vector2(0, -80);
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;

            var vpGO = MakeGO("Viewport", scrollGO);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
            vpGO.AddComponent<RectMask2D>();

            var contentGO = MakeGO("Content", vpGO);
            var cRT = contentGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot     = new Vector2(0.5f, 1);
            cRT.anchoredPosition = Vector2.zero;
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding              = new RectOffset(20, 20, 16, 16);
            vlg.spacing              = 20;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vpRT;
            sr.content  = cRT;

            // Build the three function sections
            BuildF1Section(contentGO);
            BuildF2Section(contentGO);
            BuildF3Section(contentGO);

            _panel.SetActive(false);
        }

        // Section Builders

        private void BuildF1Section(GameObject parent)
        {
            var sec = MakeSection(parent, "1 — Book Search DB Test");

            // Input field
            var inputGO = MakeGO("InputField", sec);
            var iRT = inputGO.AddComponent<RectTransform>();
            var iLE = inputGO.AddComponent<LayoutElement>();
            iLE.preferredHeight = 70;

            var bg = inputGO.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.26f, 1f);

            _f1Input = inputGO.AddComponent<TMP_InputField>();

            // Viewport
            var vpGO = MakeGO("Viewport", inputGO);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(8, 4); vpRT.offsetMax = new Vector2(-8, -4);
            vpGO.AddComponent<RectMask2D>();

            // Placeholder
            var phGO = MakeGO("Placeholder", vpGO);
            phGO.AddComponent<RectTransform>();
            StretchFill(phGO, vpGO);
            var phTmp = phGO.AddComponent<TextMeshProUGUI>();
            phTmp.text      = "Type a topic or book title…";
            phTmp.fontSize  = 22;
            phTmp.color     = new Color(0.5f, 0.55f, 0.65f, 1f);
            phTmp.alignment = TextAlignmentOptions.Left;

            // Text
            var txtGO = MakeGO("Text", vpGO);
            txtGO.AddComponent<RectTransform>();
            StretchFill(txtGO, vpGO);
            var txtTmp = txtGO.AddComponent<TextMeshProUGUI>();
            txtTmp.text     = "";
            txtTmp.fontSize = 22;
            txtTmp.color    = Color.white;
            txtTmp.alignment = TextAlignmentOptions.Left;

            _f1Input.textViewport   = vpGO.GetComponent<RectTransform>();
            _f1Input.textComponent  = txtTmp;
            _f1Input.placeholder    = phTmp;
            _f1Input.caretWidth     = 2;

            // Search button
            _f1Button = MakeSectionButton(sec, "Test Search", OnF1Search, C_BTN);

            // Result label
            _f1Result = MakeResultLabel(sec, "Result will appear here.");
        }

        private void BuildF2Section(GameObject parent)
        {
            var sec = MakeSection(parent, "2 — Movement Detection");
            MakeSectionLabel(sec, "Live accelerometer readout:", C_DIM, 22);

            var dirGO = MakeGO("DirectionLbl", sec);
            var dRT = dirGO.AddComponent<RectTransform>();
            var dLE = dirGO.AddComponent<LayoutElement>();
            dLE.preferredHeight = 80;
            dirGO.AddComponent<Image>().color = C_SECTION;
            _f2Direction = MakeTMP(dirGO, "●  Still", 36, Color.white, TextAlignmentOptions.Center);
            _f2Direction.fontStyle = FontStyles.Bold;
            StretchFill(_f2Direction.gameObject, dirGO);

            MakeSectionLabel(sec, "Hold phone upright and walk to test.", C_DIM, 20);
        }

        private void BuildF3Section(GameObject parent)
        {
            var sec = MakeSection(parent, "3 — Vision Location ID");
            MakeSectionLabel(sec, "Point camera at a library location then tap Scan.", C_DIM, 22);
            _f3Button = MakeSectionButton(sec, "Scan Location", OnF3Scan, C_BTN);
            _f3Result = MakeResultLabel(sec, "Result will appear here.");
        }

        // Section Helpers

        private GameObject MakeSection(GameObject parent, string title)
        {
            var sec = MakeGO($"Section_{title.Substring(0,1)}", parent);
            sec.AddComponent<RectTransform>();
            var vl = sec.AddComponent<VerticalLayoutGroup>();
            vl.padding              = new RectOffset(16, 16, 12, 12);
            vl.spacing              = 10;
            vl.childControlWidth    = true;
            vl.childControlHeight   = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            sec.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var img = sec.AddComponent<Image>();
            img.color = C_SECTION;

            // Section title
            var titleGO = MakeGO("SectionTitle", sec);
            titleGO.AddComponent<RectTransform>();
            var le = titleGO.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            var tmp = MakeTMP(titleGO, title, 24, C_BTN_ACT, TextAlignmentOptions.Left);
            tmp.fontStyle = FontStyles.Bold;
            StretchFill(tmp.gameObject, titleGO);

            return sec;
        }

        private void MakeSectionLabel(GameObject parent, string text, Color color, float size)
        {
            var go = MakeGO("Label", parent);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size + 10f;
            var tmp = MakeTMP(go, text, size, color, TextAlignmentOptions.Left);
            tmp.enableWordWrapping = true;
            StretchFill(tmp.gameObject, go);
        }

        private Button MakeSectionButton(GameObject parent, string label, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var go = MakeGO($"Btn_{label}", parent);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 72;
            go.AddComponent<Image>().color = color;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = color;
            colors.highlightedColor = new Color(color.r + 0.08f, color.g + 0.08f, color.b + 0.08f, 1f);
            colors.pressedColor     = C_BTN_ACT;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            var tmp = MakeTMP(go, label, 28, Color.white, TextAlignmentOptions.Center);
            tmp.fontStyle = FontStyles.Bold;
            StretchFill(tmp.gameObject, go);
            return btn;
        }

        private TextMeshProUGUI MakeResultLabel(GameObject parent, string placeholder)
        {
            var go = MakeGO("ResultLbl", parent);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            go.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 1f);
            var tmp = MakeTMP(go, placeholder, 22, C_DIM, TextAlignmentOptions.Left);
            tmp.enableWordWrapping = true;
            tmp.margin = new Vector4(10, 0, 10, 0);
            StretchFill(tmp.gameObject, go);
            return tmp;
        }

        // Panel Toggle & Navigation

        private void TogglePanel()
        {
            _visible = !_visible;
            _panel.SetActive(_visible);
        }

        /// <summary>Exits the AR scene entirely and returns to the Main Menu.</summary>
        private void ExitToMainMenu()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private void HandleTripleTap()
        {
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                var pos = Input.GetTouch(0).position;
                if (pos.x < 120 && pos.y < 120)
                {
                    _tapWindow = 0.6f;
                    _tapCount++;
                    if (_tapCount >= 3) { TogglePanel(); _tapCount = 0; }
                }
            }
            if (_tapWindow > 0) { _tapWindow -= Time.deltaTime; if (_tapWindow <= 0) _tapCount = 0; }
        }

        // Frame Capture

        private byte[] CaptureFrameAsJpeg()
        {
            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            if (screen == null) return null;
            try
            {
                int w  = 512;
                int h  = Mathf.RoundToInt(screen.height * (w / (float)screen.width));
                var rt = RenderTexture.GetTemporary(w, h);
                Graphics.Blit(screen, rt);
                var resized = new Texture2D(w, h, TextureFormat.RGB24, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                resized.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                resized.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                byte[] jpeg = resized.EncodeToJPG(75);
                Destroy(screen);
                Destroy(resized);
                return jpeg;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DebugLocalizationPanel] Capture failed: {ex.Message}");
                Destroy(screen);
                return null;
            }
        }

        // UI Factories

        private static GameObject MakeGO(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static TextMeshProUGUI MakeTMP(GameObject parent, string text, float size,
                                               Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("TMP");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text      = text;
            t.fontSize  = size;
            t.color     = color;
            t.alignment = align;
            t.enableWordWrapping  = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private static void StretchFill(GameObject go, GameObject parent)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
