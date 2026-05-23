using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.AppState
{
    /// <summary>
    /// Justified singleton: survives scene transitions (DontDestroyOnLoad) and holds
    /// inter scene state. All other systems read from this after loading ARScene.
    /// </summary>
    public class AppStateManager : MonoBehaviour
    {
        // Singleton
        public static AppStateManager Instance { get; private set; }

        // State
        public AppMode CurrentMode      { get; private set; } = AppMode.None;
        public string  TargetShelfID    { get; private set; } = string.Empty;
        public List<string> TreasureHuntWaypoints { get; private set; } = new List<string>();

        // Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this); // destroy only the component, not the whole GameObject
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Public API
        public void SetMode(AppMode mode)
        {
            CurrentMode = mode;
        }

        public void SetBookSearchTarget(string shelfID)
        {
            TargetShelfID = shelfID;
        }

        public void SetTreasureHuntRoute(List<string> waypointNodeIDs)
        {
            TreasureHuntWaypoints = waypointNodeIDs ?? new List<string>();
        }

        public void ClearState()
        {
            CurrentMode = AppMode.None;
            TargetShelfID = string.Empty;
            TreasureHuntWaypoints.Clear();
        }
    }
}
