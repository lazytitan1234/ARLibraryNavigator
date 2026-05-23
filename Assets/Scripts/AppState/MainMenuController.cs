using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARLibraryNav.AppState
{
    /// <summary>
    /// Attached to an empty GameObject in MainMenu scene.
    /// Buttons call OnBookSearchClicked / OnTreasureHuntClicked via Inspector onClick events.
    /// Writes mode to PlayerPrefs so AppModeController can read it reliably after scene load.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string MODE_KEY = "ARLibNav_AppMode";

        [SerializeField] private string arSceneName = "ARScene";

        public void OnBookSearchClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.BookSearch);
            PlayerPrefs.Save();
            SceneManager.LoadScene(arSceneName);
        }

        public void OnTreasureHuntClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.TreasureHunt);
            PlayerPrefs.Save();
            SceneManager.LoadScene(arSceneName);
        }

        public void OnTestClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.Test);
            PlayerPrefs.Save();
            SceneManager.LoadScene(arSceneName);
        }
    }
}
