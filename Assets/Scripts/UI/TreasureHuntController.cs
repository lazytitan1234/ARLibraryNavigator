using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ARLibraryNav.API;
using ARLibraryNav.Data;
using ARLibraryNav.Logging;

namespace ARLibraryNav.UI
{
    /// <summary>
    /// Manages the Treasure Hunt mode flow:
    ///   1. BeginHunt() -> shows Start Screen (title, timer preview, Start/Exit buttons).
    ///   2. User taps Start -> timer starts, first clue shown.
    ///   3. User reads clue, walks to location, taps SCAN ME.
    ///   4. Native Android camera opens; user takes photo and confirms inside camera app.
    ///   5. App regains focus; photo shown in preview panel for final confirmation.
    ///   6. Confirmed photo sent to Gemini Vision with clue's visionTargetDescription.
    ///      YES -> next clue (or completion).   NO -> feedback message, stay on same clue.
    ///   7. After all clues -> Completion screen with total elapsed time.
    /// </summary>
    public class TreasureHuntController : MonoBehaviour
    {
        // Inspector References
        [Header("Data")]
        [SerializeField] private TreasureHuntRoute route;

        [Header("Dependencies")]
        [SerializeField] private GeminiClassifier geminiClassifier;
        [SerializeField] private SessionLogger    sessionLogger;
        [SerializeField] private Navigation.NavigationController navigationController;
        [SerializeField] private AR.MarkerLocalizationManager    localizationManager;

        [Header("Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("UI – Start Panel")]
        [SerializeField] private GameObject      startPanel;
        [SerializeField] private TextMeshProUGUI startTitleText;

        [Header("UI – Clue Panel")]
        [SerializeField] private GameObject      cluePanel;
        [SerializeField] private TextMeshProUGUI clueText;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI scanStatusText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button          scanMeButton;

        [Header("UI – Completion Panel")]
        [SerializeField] private GameObject      completionPanel;
        [SerializeField] private TextMeshProUGUI completionTimeText;

        // Photo preview panel created at runtime
        private GameObject              photoConfirmPanel;
        private UnityEngine.UI.RawImage photoPreviewImage;
        private Button                  usePhotoButton;
        private Button                  retakePhotoButton;

        // Runtime State
        private int       _currentClueIndex     = 0;
        private bool      _scanInProgress       = false;
        private bool      _huntRunning          = false;
        private float     _huntStartTime        = 0f;
        private float     _clueStartTime        = 0f;
        private byte[]    _pendingJpeg          = null;
        private Texture2D _previewTex           = null;

        private bool                    _waitingForCameraResult = false;
        private string                  _nativeCameraFilePath   = null;
        private TreasureHuntRoute.Clue  _pendingClue            = null;

        private List<TreasureHuntRoute.Clue> _shuffledClues = new List<TreasureHuntRoute.Clue>();

        // Unity Lifecycle

        private void Start()
        {
            if (startPanel      != null) startPanel.SetActive(false);
            if (cluePanel       != null) cluePanel.SetActive(false);
            if (completionPanel != null) completionPanel.SetActive(false);
            BuildPhotoPreviewPanel();
        }

        private void Update()
        {
            if (_huntRunning && timerText != null)
                timerText.text = FormatTime(Time.time - _huntStartTime);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _waitingForCameraResult)
            {
                _waitingForCameraResult = false;
                StartCoroutine(LoadCapturedPhotoCoroutine());
            }
        }

        private void OnDestroy()
        {
            if (_previewTex != null) Destroy(_previewTex);
        }

        // Public API (called by AppModeController)

        public void BeginHunt()
        {
            _shuffledClues    = route != null ? ShuffleClues(route.clues) : new List<TreasureHuntRoute.Clue>();
            _currentClueIndex = 0;
            _scanInProgress   = false;
            _huntRunning      = false;

            if (completionPanel != null) completionPanel.SetActive(false);
            if (cluePanel       != null) cluePanel.SetActive(false);

            if (startTitleText != null && route != null)
                startTitleText.text = route.routeName;

            if (startPanel != null) startPanel.SetActive(true);

            sessionLogger?.LogEvent("TREASURE_HUNT_OPENED", route != null ? route.routeName : "");
        }

        // Public (wired to Buttons in Inspector)

        public void OnStartClicked()
        {
            if (route == null || _shuffledClues.Count == 0)
            {
                Debug.LogError("[TreasureHuntController] No route or clues assigned.");
                return;
            }

            if (startPanel != null) startPanel.SetActive(false);
            if (cluePanel  != null) cluePanel.SetActive(true);

            _huntStartTime = Time.time;
            _huntRunning   = true;
            _clueStartTime = Time.time;

            sessionLogger?.LogEvent("TREASURE_HUNT_START",
                $"route={route.routeName} clues={_shuffledClues.Count}");

            ShowCurrentClue();
        }

        public void OnScanMeClicked()
        {
            if (_scanInProgress) return;

            var clue = CurrentClue();
            if (clue == null) return;

            if (string.IsNullOrWhiteSpace(clue.visionTargetDescription))
            {
                SetScanStatus("This clue has no vision target set.");
                return;
            }

            if (geminiClassifier == null)
            {
                SetScanStatus("Scanner not ready.");
                return;
            }

            StartCoroutine(PerformScan(clue));
        }

        public void OnExitClicked()
        {
            float elapsed = _huntRunning ? Time.time - _huntStartTime : 0f;

            _huntRunning    = false;
            _scanInProgress = false;
            if (navigationController != null) navigationController.StopNavigation();

            sessionLogger?.LogEvent("TREASURE_HUNT_EXIT",
                $"clue={_currentClueIndex} elapsed={FormatTime(elapsed)}");

            SceneManager.LoadScene(mainMenuSceneName);
        }

        // Private Clue Flow

        private void ShowCurrentClue()
        {
            StartCoroutine(ShowCurrentClueCoroutine());
        }

        private IEnumerator ShowCurrentClueCoroutine()
        {
            var clue = CurrentClue();
            if (clue == null) yield break;

            if (progressLabel != null) progressLabel.text = $"Clue {_currentClueIndex + 1} of {_shuffledClues.Count}";
            if (clueText      != null) clueText.text      = "Preparing your clue\u2026";
            if (scanMeButton  != null) scanMeButton.interactable = false;
            SetScanStatus("");

            _clueStartTime = Time.time;

            if (navigationController != null && !string.IsNullOrEmpty(clue.targetNodeID))
                navigationController.StartNavigation(clue.targetNodeID);

            bool   responseReceived = false;
            string displayText      = clue.rawClueText;
            bool   wasReworded      = false;

            if (geminiClassifier != null && !string.IsNullOrWhiteSpace(clue.rawClueText))
            {
                geminiClassifier.RephraseClue(
                    clue.rawClueText,
                    reworded => { displayText = reworded; wasReworded = true; responseReceived = true; },
                    err     => { Debug.LogWarning($"[TreasureHuntController] Rephrase failed: {err}"); responseReceived = true; });

                float deadline = Time.unscaledTime + 20f;
                yield return new WaitUntil(() => responseReceived || Time.unscaledTime > deadline);
                if (!responseReceived)
                    Debug.LogWarning("[TreasureHuntController] RephraseClue timed out — using raw clue.");
            }

            if (clueText != null) clueText.text = displayText;
            SetScanStatus("Walk to the location, then tap SCAN ME.");
            if (scanMeButton != null) scanMeButton.interactable = true;

            sessionLogger?.LogEvent("CLUE_SHOWN",
                $"index={_currentClueIndex} reworded={wasReworded} text=\"{displayText}\"");

            Debug.Log($"[TreasureHuntController] Clue {_currentClueIndex + 1} " +
                      $"({(wasReworded ? "AI-reworded" : "raw")}): {displayText}");
        }

        private IEnumerator PerformScan(TreasureHuntRoute.Clue clue)
        {
            _scanInProgress = true;
            _pendingClue    = clue;

            if (scanMeButton != null) scanMeButton.interactable = false;
            SetScanStatus("Opening camera…");

            LaunchNativeCamera();
            yield break;
            // Remainder handled by OnApplicationFocus -> LoadCapturedPhotoCoroutine
        }

        private IEnumerator LoadCapturedPhotoCoroutine()
        {
            // Brief delay to allow the file system to flush after the camera app closes
            yield return new WaitForSeconds(0.5f);

            if (string.IsNullOrEmpty(_nativeCameraFilePath) || !File.Exists(_nativeCameraFilePath))
            {
                Debug.Log("[TreasureHuntController] No photo file found — user likely cancelled.");
                SetScanStatus("No photo taken. Tap SCAN ME to try again.");
                _scanInProgress = false;
                if (scanMeButton != null) scanMeButton.interactable = true;
                yield break;
            }

            byte[] jpeg = File.ReadAllBytes(_nativeCameraFilePath);
            Debug.Log($"[TreasureHuntController] Loaded captured photo: {jpeg.Length / 1024} KB");

            if (_previewTex != null) Destroy(_previewTex);
            _previewTex = new Texture2D(2, 2);
            _previewTex.LoadImage(jpeg);

            _pendingJpeg = jpeg;

            if (photoPreviewImage != null) photoPreviewImage.texture = _previewTex;
            if (photoConfirmPanel != null) photoConfirmPanel.SetActive(true);
            SetScanStatus("Happy with this photo?");

            bool userConfirmed = false;
            bool userRetook    = false;

            void OnConfirm() { userConfirmed = true; }
            void OnRetake()  { userRetook    = true; }

            if (usePhotoButton    != null) usePhotoButton.onClick.AddListener(OnConfirm);
            if (retakePhotoButton != null) retakePhotoButton.onClick.AddListener(OnRetake);

            yield return new WaitUntil(() => userConfirmed || userRetook);

            if (usePhotoButton    != null) usePhotoButton.onClick.RemoveListener(OnConfirm);
            if (retakePhotoButton != null) retakePhotoButton.onClick.RemoveListener(OnRetake);
            if (photoConfirmPanel != null) photoConfirmPanel.SetActive(false);

            if (userRetook)
            {
                SetScanStatus("Walk to the location, then tap SCAN ME.");
                _scanInProgress = false;
                if (scanMeButton != null) scanMeButton.interactable = true;
                yield break;
            }

            // Send confirmed photo to Gemini
            var clue = _pendingClue;
            if (clue == null) { _scanInProgress = false; yield break; }

            SetScanStatus("Scanning\u2026");
            sessionLogger?.LogEvent("SCAN_ATTEMPT",
                $"clue={_currentClueIndex} target=\"{clue.visionTargetDescription}\"");

            bool responseReceived = false;
            bool arrived          = false;
            string errorMsg       = null;

            geminiClassifier.CheckArrival(
                clue.referenceImages,
                clue.visionTargetDescription,
                _pendingJpeg,
                () => { arrived = true;  responseReceived = true; },
                err => { errorMsg = err; responseReceived = true; });

            float scanDeadline = Time.unscaledTime + 30f;
            yield return new WaitUntil(() => responseReceived || Time.unscaledTime > scanDeadline);
            if (!responseReceived) errorMsg = "Scan timed out — please try again.";

            _scanInProgress = false;

            if (arrived)
            {
                float clueTime = Time.time - _clueStartTime;
                sessionLogger?.LogEvent("CLUE_CONFIRMED",
                    $"index={_currentClueIndex} clue_time={clueTime:F1}s");
                OnArrived();
            }
            else
            {
                string msg = string.IsNullOrEmpty(errorMsg)
                    ? "Not quite — keep looking!"
                    : $"Scan error: {errorMsg}";
                SetScanStatus(msg);
                sessionLogger?.LogEvent("SCAN_REJECTED",
                    $"clue={_currentClueIndex} reason=\"{msg}\"");
                if (scanMeButton != null) scanMeButton.interactable = true;
            }
        }

        private void OnArrived()
        {
            if (navigationController != null) navigationController.StopNavigation();
            _currentClueIndex++;

            if (_currentClueIndex >= _shuffledClues.Count)
            {
                OnHuntComplete();
                return;
            }

            ShowCurrentClue();
        }

        private void OnHuntComplete()
        {
            _huntRunning = false;
            float totalTime = Time.time - _huntStartTime;

            if (cluePanel       != null) cluePanel.SetActive(false);
            if (completionPanel != null) completionPanel.SetActive(true);

            if (completionTimeText != null)
                completionTimeText.text = $"Completed in {FormatTime(totalTime)}";

            if (navigationController != null) navigationController.StopNavigation();

            sessionLogger?.LogEvent("TREASURE_HUNT_COMPLETE",
                $"route={route.routeName} total_time={FormatTime(totalTime)} clues={_shuffledClues.Count}");

            Debug.Log($"[TreasureHuntController] Hunt complete! Total time: {FormatTime(totalTime)}");
        }

        // Native Camera

        private void LaunchNativeCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string photoDir = Path.Combine(Application.persistentDataPath, "Photos");
                Directory.CreateDirectory(photoDir);
                _nativeCameraFilePath = Path.Combine(photoDir, "scan_capture.jpg");

                if (File.Exists(_nativeCameraFilePath))
                    File.Delete(_nativeCameraFilePath);

                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                var intent = new AndroidJavaObject("android.content.Intent", "android.media.action.IMAGE_CAPTURE");

                var fileProvider = new AndroidJavaClass("com.DefaultCompany.ARLibraryNavigator.ARNavFileProvider");
                var file         = new AndroidJavaObject("java.io.File", _nativeCameraFilePath);
                string authority = Application.identifier + ".fileprovider";
                var photoUri     = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);

                intent.Call<AndroidJavaObject>("putExtra", "output", photoUri);
                intent.Call<AndroidJavaObject>("addFlags", 3); // GRANT_READ | GRANT_WRITE

                _waitingForCameraResult = true;
                activity.Call("startActivity", intent);

                Debug.Log($"[TreasureHuntController] Native camera launched -> {_nativeCameraFilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TreasureHuntController] LaunchNativeCamera failed: {ex.Message}");
                SetScanStatus("Could not open camera. Try again.");
                _scanInProgress = false;
                if (scanMeButton != null) scanMeButton.interactable = true;
            }
#else
            // Editor / non-Android: simulate with a solid-colour JPEG for testing
            Debug.Log("[TreasureHuntController] Native camera not available in Editor — simulating.");
            _nativeCameraFilePath = null;
            _waitingForCameraResult = false;
            SetScanStatus("Camera only works on device. Tap SCAN ME again to retry.");
            _scanInProgress = false;
            if (scanMeButton != null) scanMeButton.interactable = true;
#endif
        }

        // UI Construction

        private void BuildPhotoPreviewPanel()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TreasureHuntController] No Canvas found — photo preview disabled.");
                return;
            }

            photoConfirmPanel = new GameObject("PhotoConfirmPanel");
            photoConfirmPanel.transform.SetParent(canvas.transform, false);
            var panelRect = photoConfirmPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var bg = photoConfirmPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.92f);

            var photoObj = new GameObject("PhotoPreview");
            photoObj.transform.SetParent(photoConfirmPanel.transform, false);
            var photoRect = photoObj.AddComponent<RectTransform>();
            photoRect.anchorMin = new Vector2(0.05f, 0.22f);
            photoRect.anchorMax = new Vector2(0.95f, 0.95f);
            photoRect.offsetMin = Vector2.zero;
            photoRect.offsetMax = Vector2.zero;
            photoPreviewImage = photoObj.AddComponent<UnityEngine.UI.RawImage>();

            usePhotoButton   = CreateButton(photoConfirmPanel.transform, "Looks Good!",
                new Vector2(0.05f, 0.02f), new Vector2(0.48f, 0.19f), new Color(0.2f, 0.7f, 0.3f));
            retakePhotoButton = CreateButton(photoConfirmPanel.transform, "Retake",
                new Vector2(0.52f, 0.02f), new Vector2(0.95f, 0.19f), new Color(0.7f, 0.3f, 0.2f));

            photoConfirmPanel.SetActive(false);
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var obj  = new GameObject(label + "Button");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = obj.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            var btn = obj.AddComponent<Button>();

            var textObj  = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = label;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize  = 28;
            tmp.color     = Color.white;

            return btn;
        }

        // Helpers

        private TreasureHuntRoute.Clue CurrentClue()
        {
            if (_shuffledClues == null || _currentClueIndex >= _shuffledClues.Count) return null;
            return _shuffledClues[_currentClueIndex];
        }

        private void SetScanStatus(string message)
        {
            if (scanStatusText != null) scanStatusText.text = message;
        }

        private static List<TreasureHuntRoute.Clue> ShuffleClues(List<TreasureHuntRoute.Clue> source)
        {
            // Resolve variants: for any clue that has alternatives, pick one randomly
            var resolved = new List<TreasureHuntRoute.Clue>();
            foreach (var clue in source)
            {
                if (clue.variants != null && clue.variants.Count > 0)
                    resolved.Add(clue.variants[Random.Range(0, clue.variants.Count)]);
                else
                    resolved.Add(clue);
            }

            // Fisher-Yates shuffle
            for (int i = resolved.Count - 1; i > 0; i--)
            {
                int j   = Random.Range(0, i + 1);
                var tmp = resolved[i];
                resolved[i] = resolved[j];
                resolved[j] = tmp;
            }
            return resolved;
        }

        private static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}
