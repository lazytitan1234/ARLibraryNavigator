using UnityEngine;
using ARLibraryNav.Navigation;

namespace ARLibraryNav.UI
{
    public class NavigationPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NavigationController navController;

        [Header("Panels")]
        [SerializeField] private GameObject bookSearchPanel;
        [SerializeField] private GameObject navigationPanel; // this panel (self)

        // Button Handlers

        /// <summary>Called by StopButton. Cancels active navigation and returns to search.</summary>
        public void OnStopClicked()
        {
            navController.StopNavigation();
            navigationPanel.SetActive(false);
            bookSearchPanel.SetActive(true);
        }

        /// <summary>Called by ArrivalPanel/DoneButton. Clears state and returns to search.</summary>
        public void OnArrivalDoneClicked()
        {
            navController.StopNavigation();
            navigationPanel.SetActive(false);
            bookSearchPanel.SetActive(true);
        }
    }
}
