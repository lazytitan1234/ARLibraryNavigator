using System.Collections.Generic;
using UnityEngine;
using ARLibraryNav.AR;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Drives AR navigation: computes the path via Pathfinder, renders it via LineRenderer,
    /// and advances through waypoints as the user detects successive markers.
    ///
    /// Path rendering uses a LineRenderer for rapid implementation.
    /// Arrow prefabs (WaypointArrowBehaviour) can be added later as a visual upgrade.
    ///
    /// Attach to: Managers or NavigationRoot in ARScene.
    /// Wire all [SerializeField] references in the Inspector.
    /// Subscribe OnMarkerDetected event from MarkerLocalizationManager to OnMarkerReached().
    /// </summary>
    public class NavigationController : MonoBehaviour
    {
        // Inspector References
        [Header("Data")]
        [SerializeField] private NavGraph navGraph;
        [SerializeField] private MarkerLocalizationManager localizationManager;
        [SerializeField] private Transform navigationRoot; // The parent of all visual nodes/path elements

        [Header("Line Renderer Path (Phase D – quick implementation)")]
        [SerializeField] private LineRenderer pathLine;
        [SerializeField] private float        pathLineY = 0.05f;   // Height above floor plane

        [Header("Arrow Prefabs (Phase D upgrade – optional)")]
        [SerializeField] private WaypointArrowBehaviour waypointArrow; // Ref to a single persistent arrow for live guidance
        [SerializeField] private Transform   waypointArrowPool;    // Parent for pooled arrows

        [Header("UI References")]
        [SerializeField] private TMPro.TextMeshProUGUI nextWaypointLabel;
        [SerializeField] private GameObject            floorTransitionPanel;
        [SerializeField] private TMPro.TextMeshProUGUI floorTransitionText;
        [SerializeField] private GameObject            arrivalPanel;

        // Runtime State
        private List<MarkerNode> _path            = new List<MarkerNode>();
        private int              _waypointIndex   = 0;
        private bool             _isNavigating    = false;
        private string           _pendingTargetID = null; // retry when localization arrives
        private readonly List<GameObject> _activeArrows = new List<GameObject>();

        public bool IsNavigating  => _isNavigating;
        public MarkerNode NextWaypoint =>
            (_isNavigating && _waypointIndex + 1 < _path.Count)
                ? _path[_waypointIndex + 1]
                : null;

        /// <summary>
        /// Optional callback invoked when the player arrives at the destination.
        /// Set by TreasureHuntController to auto advance clues instead of showing the arrival panel.
        /// Leave null for Book Search mode (arrival panel shows as normal).
        /// </summary>
        public System.Action OnArrivalCallback { get; set; }

        // Unity Lifecycle
        private void Start()
        {
            if (pathLine != null)
                pathLine.positionCount = 0;

            if (arrivalPanel != null)
                arrivalPanel.SetActive(false);

            if (floorTransitionPanel != null)
                floorTransitionPanel.SetActive(false);

            if (waypointArrow != null)
                waypointArrow.SetVisible(false);

            // Subscribe to localization events
            if (localizationManager != null)
            {
                localizationManager.OnMarkerDetected.AddListener(OnMarkerReached);
            }
            else
            {
                Debug.LogWarning("[NavigationController] MarkerLocalizationManager not assigned.");
            }
        }

        private void Update()
        {
            if (!_isNavigating || _path.Count == 0 || _waypointIndex >= _path.Count)
            {
                if (waypointArrow != null) waypointArrow.SetVisible(false);
                
                // Still update UI to show "Scanning" status if relevant
                UpdateUI();
                return;
            }

            UpdateLiveGuidance();
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (localizationManager != null)
                localizationManager.OnMarkerDetected.RemoveListener(OnMarkerReached);
        }

        // Public API

        /// <summary>
        /// Computes the shortest path from the user's current location to targetNodeID
        /// and begins rendering navigation guidance.
        /// </summary>
        public void StartNavigation(string targetNodeID)
        {
            if (navGraph == null)
            {
                Debug.LogError("[NavigationController] NavGraph not assigned.");
                return;
            }

            if (!localizationManager.HasLocalization)
            {
                Debug.Log("[NavigationController] No localization yet — auto-triggering vision scan.");
                _pendingTargetID = targetNodeID;
                ShowNextWaypointLabel("Identifying your location via Gemini...");
                
                // Trigger the Gemini Vision identification
                localizationManager.TryVisionLocalization();
                return;
            }

            _pendingTargetID = null;
            string startID = localizationManager.CurrentNode.nodeID;
            _path          = Pathfinder.FindPath(navGraph, startID, targetNodeID);

            if (_path.Count <= 1 && startID != targetNodeID)
            {
                Debug.LogWarning($"[NavigationController] No path found from '{startID}' to '{targetNodeID}'.");
                ShowNextWaypointLabel("No route found.");
                return;
            }

            _waypointIndex = 0;
            _isNavigating  = true;

            Debug.Log($"[NavigationController] Path found: {_path.Count} nodes from '{startID}' to '{targetNodeID}'.");

            if (waypointArrow != null) waypointArrow.SetVisible(true);
            RenderPath();
            UpdateUI();
        }

        /// <summary>
        /// Anchors the navigation map to the user's current AR camera position and orientation.
        /// Called when Gemini identifies a node but no physical marker is tracked.
        /// </summary>
        public void CalibrateAtNode(MarkerNode node)
        {
            if (navigationRoot == null) return;

            // Get current camera pose in Unity world space
            Vector3 camPos = Camera.main.transform.position;
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0; // look along floor plane

            // Move the navigation root so the identified node is at the camera's current position
            Vector3 nodeLocalPos = node.WorldPosition3D();
            
            // Default: Align map such that Unity +Z (North) aligns with real-world North if compass is available.
            // If compass is not available or hasn't started, we'll align the map to the camera's forward direction.
            // This assumes the user is "facing" the shelf they just identified.
            float targetRotationY = 0f;

            if (Input.compass.enabled && Input.compass.trueHeading != 0)
            {
                // Align Unity +Z with magnetic North
                targetRotationY = Input.compass.trueHeading;
                Debug.Log($"[NavigationController] Calibrating rotation via Compass: {targetRotationY}");
            }
            else
            {
                // Fallback: Align map so identified node is "in front" of user
                // (or rather, user is facing 'forward' relative to the node's expected orientation)
                // For simplicity, we align the root to the camera's current Y rotation.
                targetRotationY = Camera.main.transform.eulerAngles.y;
                Debug.Log($"[NavigationController] Compass unavailable. Calibrating rotation to Camera Heading: {targetRotationY}");
            }

            navigationRoot.rotation = Quaternion.Euler(0, targetRotationY, 0);
            
            // After rotating, set position so the node aligns with camera
            // We must recalculate the world position of nodeLocalPos based on new rotation
            Vector3 rotatedNodePos = navigationRoot.rotation * nodeLocalPos;
            navigationRoot.position = new Vector3(camPos.x, 0, camPos.z) - rotatedNodePos;

            Debug.Log($"[NavigationController] Calibrated map to node: {node.nodeID} at user position {camPos}");
        }

        /// <summary>Stops navigation and clears all visual elements.</summary>
        public void StopNavigation()
        {
            _isNavigating    = false;
            _pendingTargetID = null;
            OnArrivalCallback = null;
            _path.Clear();
            _waypointIndex = 0;

            ClearPathVisuals();
            ShowNextWaypointLabel(string.Empty);

            if (waypointArrow != null)         waypointArrow.SetVisible(false);
            if (floorTransitionPanel != null) floorTransitionPanel.SetActive(false);
            if (arrivalPanel != null)         arrivalPanel.SetActive(false);
        }

        /// <summary>
        /// Called by MarkerLocalizationManager.OnMarkerDetected event when a new marker is seen.
        /// Checks if the detected node is the expected next waypoint and advances accordingly.
        /// </summary>
        public void OnMarkerReached(MarkerNode detectedNode)
        {
            // If we don't have active Vuforia tracking, perform a "Soft Calibration" using Gemini's result
            if (!localizationManager.IsCurrentlyTracking)
            {
                CalibrateAtNode(detectedNode);
            }

            // If navigation was queued before localization existed, start it now
            if (!_isNavigating && !string.IsNullOrEmpty(_pendingTargetID))
            {
                Debug.Log($"[NavigationController] Localization established — starting queued navigation to '{_pendingTargetID}'.");
                StartNavigation(_pendingTargetID);
                return;
            }

            if (!_isNavigating || _path.Count == 0) return;

            // Check if the detected node is anywhere in our remaining path
            for (int i = _waypointIndex; i < _path.Count; i++)
            {
                if (_path[i].nodeID == detectedNode.nodeID)
                {
                    _waypointIndex = i;
                    break;
                }
            }

            // Check if we've arrived at the final destination
            if (_waypointIndex >= _path.Count - 1)
            {
                OnArrival();
                return;
            }

            // Advance to next segment
            RenderPath();
            UpdateUI();
        }

        // Private

        private void UpdateLiveGuidance()
        {
            if (waypointArrow == null || navigationRoot == null) return;

            // Find the next node in the path that isn't the one we are currently at
            MarkerNode target = (_waypointIndex + 1 < _path.Count)
                ? _path[_waypointIndex + 1]
                : _path[_path.Count - 1];

            Vector3 camPos = Camera.main.transform.position;
            
            // TRANSFORM the local node position into World Space using the navigationRoot (the map)
            Vector3 targetPos = navigationRoot.TransformPoint(target.WorldPosition3D(pathLineY));

            // Set the arrow to point from the camera toward the target node
            // The arrow is placed slightly in front of the camera so it's always visible (HUD style)
            Vector3 arrowPos = camPos + Camera.main.transform.forward * 0.75f + Vector3.down * 0.2f;
            
            waypointArrow.transform.position = arrowPos;
            waypointArrow.SetDirection(camPos, targetPos, false);
        }

        /// <summary>Draws the remaining path using the LineRenderer.</summary>
        private void RenderPath()
        {
            if (pathLine == null || navigationRoot == null) return;

            int remaining = _path.Count - _waypointIndex;
            pathLine.positionCount = remaining;

            for (int i = 0; i < remaining; i++)
            {
                var node = _path[_waypointIndex + i];
                // TRANSFORM each path point into World Space so it lines up with the real floor
                pathLine.SetPosition(i, navigationRoot.TransformPoint(node.WorldPosition3D(pathLineY)));
            }
        }

        private void ClearPathVisuals()
        {
            if (pathLine != null)
                pathLine.positionCount = 0;

            foreach (var arrow in _activeArrows)
            {
                if (arrow != null) arrow.SetActive(false);
            }
        }

        private void UpdateUI()
        {
            // If Gemini is currently identifying the location, show a scanning message
            if (localizationManager != null && localizationManager.IsVisionRequestInFlight)
            {
                ShowNextWaypointLabel("Searching for your location...");
                return;
            }

            if (!_isNavigating || _path.Count == 0) return;

            // Show next waypoint label
            MarkerNode next = NextWaypoint;
            string labelText = next != null
                ? $"Head to: {next.displayLabel}"
                : $"Almost there: {_path[_path.Count - 1].displayLabel}";
            ShowNextWaypointLabel(labelText);

            // Floor transition check
            CheckFloorTransition();
        }

        private void CheckFloorTransition()
        {
            if (floorTransitionPanel == null) return;

            // Look ahead in the path for a stair node
            bool hasStairAhead = false;
            int  targetFloor   = -1;

            for (int i = _waypointIndex; i < _path.Count; i++)
            {
                if (_path[i].isStairNode && i < _path.Count - 1)
                {
                    hasStairAhead = true;
                    targetFloor   = _path[i + 1].floor;
                    break;
                }
            }

            floorTransitionPanel.SetActive(hasStairAhead);

            if (hasStairAhead && floorTransitionText != null)
                floorTransitionText.text = $"Take stairs to Level {targetFloor}";
        }

        private void OnArrival()
        {
            _isNavigating = false;
            ClearPathVisuals();
            ShowNextWaypointLabel(string.Empty);

            if (floorTransitionPanel != null) floorTransitionPanel.SetActive(false);

            Debug.Log("[NavigationController] Arrived at destination.");

            if (OnArrivalCallback != null)
            {
                // Treasure Hunt mode: notify controller to advance to next clue
                OnArrivalCallback.Invoke();
            }
            else
            {
                // Book Search mode: show the arrival panel
                if (arrivalPanel != null) arrivalPanel.SetActive(true);
            }
        }

        private void ShowNextWaypointLabel(string text)
        {
            if (nextWaypointLabel != null)
                nextWaypointLabel.text = text;
        }
    }
}
