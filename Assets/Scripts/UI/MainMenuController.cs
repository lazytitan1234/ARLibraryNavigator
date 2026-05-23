using UnityEngine;
using UnityEngine.SceneManagement;
using ARLibraryNav.AppState;

namespace ARLibraryNav.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private const string MODE_KEY = "ARLibNav_AppMode";

        public void OnBookSearchClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.BookSearch);
            PlayerPrefs.Save();
            SceneManager.LoadScene("ARScene");
        }

        public void OnTreasureHuntClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.TreasureHunt);
            PlayerPrefs.Save();
            SceneManager.LoadScene("ARScene");
        }

        public void OnTestClicked()
        {
            PlayerPrefs.SetInt(MODE_KEY, (int)AppMode.Test);
            PlayerPrefs.Save();
            SceneManager.LoadScene("ARScene");
        }
    }
}
