using UnityEngine;
using UnityEngine.SceneManagement;
using ARLibraryNav.AR;
using ARLibraryNav.Logging;
using ARLibraryNav.UI;

namespace ARLibraryNav.AppState
{
    /// <summary>
    /// Reads the app mode on ARScene load and activates the correct UI panel and controller.
    ///
    /// On device: mode is read from PlayerPrefs (written by MainMenuController before scene load).
    /// In Editor: mode is read from editorStartMode inspector field for quick testing.
    ///
    /// Attach to: Managers in ARScene.
    /// Wire all [SerializeField] references in the Inspector.
    /// </summary>
    public class AppModeController : MonoBehaviour
    {
        // Inspector References
        [Header("Panels")]
        [SerializeField] private GameObject bookSearchPanel;
        [SerializeField] private GameObject treasureHuntPanel;

        [Header("Controllers")]
        [SerializeField] private BookSearchController  bookSearchController;
        [SerializeField] private TreasureHuntController treasureHuntController;

        [Header("Treasure Hunt Data")]
        [SerializeField] private ARLibraryNav.Data.TreasureHuntRoute defaultTreasureHuntRoute;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Dependencies")]
        [SerializeField] private SessionLogger sessionLogger;
        [SerializeField] private DebugLocalizationPanel debugPanel;

        [Header("Editor Testing Only (ignored in device builds)")]
        [Tooltip("Override the mode when pressing Play directly in the Editor. " +
                 "Has no effect on device — the mode is always set by MainMenuController via PlayerPrefs.")]
        [SerializeField] private AppMode editorStartMode = AppMode.BookSearch;

        private const string MODE_KEY = "ARLibNav_AppMode";

        // Unity Lifecycle
        private void Start()
        {
            AppMode mode;

#if UNITY_EDITOR
            // In Editor Play Mode, use the inspector override so any mode can be
            // tested directly without going through MainMenu.
            mode = editorStartMode;
            Debug.Log($"[AppModeController] Editor override mode: {mode}");
#else
            // On device, mode is always written to PlayerPrefs by MainMenuController
            // immediately before SceneManager.LoadScene("ARScene") is called.
            if (PlayerPrefs.HasKey(MODE_KEY))
            {
                mode = (AppMode)PlayerPrefs.GetInt(MODE_KEY, (int)AppMode.BookSearch);
                Debug.Log($"[AppModeController] Mode from PlayerPrefs: {mode}");
            }
            else
            {
                mode = AppMode.BookSearch;
                Debug.Log("[AppModeController] No PlayerPrefs key found — defaulting to BookSearch.");
            }
#endif

            ActivateMode(mode);
        }

        // Public API (wired to Back button in Inspector)

        public void ReturnToMainMenu()
        {
            sessionLogger?.EndSession();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        // Private

        private void ActivateMode(AppMode mode)
        {
            Debug.Log($"[AppModeController] Activating mode: {mode}");

            if (bookSearchPanel == null)
                Debug.LogError("[AppModeController] bookSearchPanel not assigned in Inspector.");
            if (treasureHuntPanel == null)
                Debug.LogError("[AppModeController] treasureHuntPanel not assigned in Inspector.");

            switch (mode)
            {
                case AppMode.BookSearch:
                    bookSearchPanel?.SetActive(true);
                    treasureHuntPanel?.SetActive(false);
                    break;

                case AppMode.TreasureHunt:
                    bookSearchPanel?.SetActive(false);
                    treasureHuntPanel?.SetActive(true);
                    StartCoroutine(BeginHuntNextFrame());
                    break;

                case AppMode.Test:
                    bookSearchPanel?.SetActive(true);
                    treasureHuntPanel?.SetActive(false);
                    if (debugPanel != null)
                        debugPanel.ForceShow();
                    else
                        Debug.LogWarning("[AppModeController] DebugLocalizationPanel not assigned.");
                    break;

                default:
                    Debug.LogWarning($"[AppModeController] Unknown mode '{mode}' — defaulting to BookSearch.");
                    bookSearchPanel?.SetActive(true);
                    treasureHuntPanel?.SetActive(false);
                    break;
            }
        }

        private System.Collections.IEnumerator BeginHuntNextFrame()
        {
            yield return null;
            if (treasureHuntController == null)
            {
                Debug.LogError("[AppModeController] treasureHuntController not assigned in Inspector.");
                yield break;
            }
            treasureHuntController.BeginHunt();
        }
    }
}
