using UnityEngine;
using ARLibraryNav.AR;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Switches the active floor plan Plane GameObject when the user moves between floors.
    ///
    /// Listens to MarkerLocalizationManager.OnMarkerDetected and compares the new node's
    /// floor number to the currently displayed floor. If they differ, the correct floor
    /// plan is enabled and the other disabled.
    ///
    /// Attach to: Managers in ARScene.
    /// Wire MarkerLocalizationManager and both floor plan GameObjects in the Inspector.
    /// </summary>
    public class FloorPlanSwitcher : MonoBehaviour
    {
        // Inspector References
        [Header("Floor Plan GameObjects (Plane meshes in FloorPlanRoot)")]
        [SerializeField] private GameObject floorPlan_L2;
        [SerializeField] private GameObject floorPlan_L3;

        [Header("Dependencies")]
        [SerializeField] private MarkerLocalizationManager localizationManager;

        // Runtime State
        private int _currentFloor = 2; // Default to Level 2 (matches FloorPlan_L2 active on Start)

        // Unity Lifecycle
        private void Start()
        {
            // Ensure correct initial state
            SwitchToFloor(2);

            if (localizationManager != null)
            {
                localizationManager.OnMarkerDetected.AddListener(OnMarkerDetected);
            }
            else
            {
                Debug.LogWarning("[FloorPlanSwitcher] MarkerLocalizationManager not assigned.");
            }
        }

        private void OnDestroy()
        {
            if (localizationManager != null)
                localizationManager.OnMarkerDetected.RemoveListener(OnMarkerDetected);
        }

        // Public API

        /// <summary>
        /// Activates the floor plan for the given floor number and deactivates the other.
        /// Valid values: 2 or 3.
        /// </summary>
        public void SwitchToFloor(int floor)
        {
            if (floor == _currentFloor) return;

            _currentFloor = floor;

            if (floorPlan_L2 != null) floorPlan_L2.SetActive(floor == 2);
            if (floorPlan_L3 != null) floorPlan_L3.SetActive(floor == 3);

            Debug.Log($"[FloorPlanSwitcher] Switched to Level {floor}.");
        }

        // Private

        private void OnMarkerDetected(Navigation.MarkerNode node)
        {
            if (node.floor != _currentFloor)
            {
                SwitchToFloor(node.floor);
            }
        }
    }
}
