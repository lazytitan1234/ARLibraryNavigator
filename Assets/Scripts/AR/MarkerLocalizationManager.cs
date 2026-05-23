using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Vuforia;
using ARLibraryNav.Navigation;
using ARLibraryNav.API;

namespace ARLibraryNav.AR
{
    /// <summary>
    /// Bridges Vuforia tracking events to the NavGraph node system.
    ///
    /// Listens to every ImageTarget's ObserverBehaviour.OnTargetStatusChanged event.
    /// When a target is tracked, looks up the matching MarkerNode by markerName and
    /// stores it as CurrentNode (the player's current position in the navigation graph).
    ///
    /// Uses Vuforia Engine 11 API (ObserverBehaviour) NOT the deprecated ITrackableEventHandler.
    ///
    /// "Last known position" design: CurrentNode is retained after tracking loss so that
    /// navigation can continue from the most recent confirmed position.
    ///
    /// Attach to: Managers > MarkerLocalizationManager in ARScene.
    /// Inspector: drag LibraryNavGraph.asset into navGraph field.
    /// </summary>
    public class MarkerLocalizationManager : MonoBehaviour
    {
        // Inspector Fields
        [Header("Data")]
        [SerializeField] private NavGraph navGraph;

        [Header("Events")]
        [Tooltip("Fired when a new marker is successfully tracked and mapped to a NavGraph node.")]
        public UnityEvent<MarkerNode> OnMarkerDetected;

        [Tooltip("Fired when tracking is lost. CurrentNode is retained (last-known-position).")]
        public UnityEvent OnMarkerLost;

        [Header("Vision Localization")]
        [Tooltip("Enable Gemini Vision as a localization channel. Works alongside Vuforia markers.")]
        [SerializeField] private bool useVisionLocalization = false;

        [Tooltip("Seconds between automatic vision localization attempts. Set 0 to disable auto-polling.")]
        [SerializeField] private float visionCooldownSeconds = 5f;

        [SerializeField] private GeminiClassifier geminiClassifier;
        [SerializeField] private Canvas uiCanvas; // Reference to the main UI Canvas to hide during capture

        // Runtime State
        /// <summary>The most recently confirmed NavGraph node for the user's position.</summary>
        public MarkerNode CurrentNode { get; private set; }

        /// <summary>
        /// True once at least one marker has been successfully detected this session.
        /// False on startup until the first marker is seen.
        /// </summary>
        public bool HasLocalization { get; private set; }

        /// <summary>True while Vuforia is actively tracking a marker (not just last-known).</summary>
        public bool IsCurrentlyTracking { get; private set; }

        /// <summary>True while a Gemini Vision request is pending.</summary>
        public bool IsVisionRequestInFlight => _visionRequestInFlight;

        private readonly List<ObserverBehaviour> _observers = new List<ObserverBehaviour>();
        private float _visionCooldownRemaining = 0f; // set to visionCooldownSeconds in Start() to avoid instant first call
        private bool  _visionRequestInFlight   = false;

        // Unity Lifecycle
        private void Start()
        {
            if (navGraph == null)
            {
                Debug.LogError("[MarkerLocalizationManager] NavGraph is not assigned in the Inspector.");
                return;
            }

            // Don't fire vision on the very first frame wait one full cooldown cycle first.
            // Without this, _visionCooldownRemaining starts at 0 and fires immediately on startup,
            // burning an API call before the camera has even finished initialising.
            _visionCooldownRemaining = visionCooldownSeconds;

            Debug.Log("[MarkerLocalizationManager] Initialized in Vision-Only mode (No Markers).");
        }

        private void OnDestroy()
        {
            UnregisterAllObservers();
        }

        private void Update()
        {
            if (!useVisionLocalization || visionCooldownSeconds <= 0f || _visionRequestInFlight) return;
            _visionCooldownRemaining -= Time.deltaTime;
            if (_visionCooldownRemaining <= 0f)
            {
                _visionCooldownRemaining = visionCooldownSeconds;
                TryVisionLocalization();
            }
        }

        // Public API

        /// <summary>
        /// Forces the current node to a specific nodeID without requiring a physical marker scan.
        /// Useful for Editor testing and UI flow validation without a device.
        /// </summary>
        public void ForceSetCurrentNode(string nodeID)
        {
            var node = navGraph.GetNode(nodeID);
            if (node == null)
            {
                Debug.LogWarning($"[MarkerLocalizationManager] ForceSetCurrentNode: nodeID '{nodeID}' not found.");
                return;
            }

            CurrentNode      = node;
            HasLocalization  = true;
            IsCurrentlyTracking = true;

            Debug.Log($"[MarkerLocalizationManager] Forced current node to: {nodeID}");
            OnMarkerDetected?.Invoke(node);
        }

        /// <summary>
        /// Captures the current camera frame and asks Gemini Vision to identify the location.
        /// Updates CurrentNode and fires OnMarkerDetected on success same contract as Vuforia tracking.
        /// Suppressed while IsCurrentlyTracking is true (Vuforia takes priority).
        /// Safe to call manually from UI (e.g. a "Locate Me" button).
        /// </summary>
        public void TryVisionLocalization()
        {
            if (!useVisionLocalization || geminiClassifier == null) return;
            if (IsCurrentlyTracking || _visionRequestInFlight) return;

            var descriptions = BuildNodeDescriptions();
            if (descriptions.Count == 0)
            {
                Debug.LogWarning("[MarkerLocalizationManager] No nodes have visualDescription set — " +
                                 "fill them in on LibraryNavGraph.asset.");
                return;
            }

            _visionRequestInFlight = true;
            StartCoroutine(VisionLocalizationRoutine(descriptions));
        }

        // Private

        /// <summary>
        /// Finds all ObserverBehaviour components in the scene (on ImageTarget GameObjects)
        /// and subscribes to their status change events.
        /// </summary>
        private void RegisterAllObservers()
        {
            var allObservers = FindObjectsOfType<ObserverBehaviour>();

            foreach (var obs in allObservers)
            {
                obs.OnTargetStatusChanged += HandleTargetStatusChanged;
                _observers.Add(obs);
            }

            Debug.Log($"[MarkerLocalizationManager] Registered {_observers.Count} Vuforia observer(s).");
        }

        private void UnregisterAllObservers()
        {
            foreach (var obs in _observers)
            {
                if (obs != null)
                    obs.OnTargetStatusChanged -= HandleTargetStatusChanged;
            }
            _observers.Clear();
        }

        /// <summary>
        /// Called by Vuforia when any tracked target's status changes.
        /// Maps the target name to a MarkerNode and updates localization state.
        /// </summary>
        private void HandleTargetStatusChanged(ObserverBehaviour observer, TargetStatus targetStatus)
        {
            var statusCode = targetStatus.Status;

            bool isTracked = statusCode == Status.TRACKED ||
                             statusCode == Status.EXTENDED_TRACKED;

            if (isTracked)
            {
                string markerName = observer.TargetName;
                MarkerNode node   = navGraph.GetNodeByMarkerName(markerName);

                if (node == null)
                {
                    Debug.LogWarning($"[MarkerLocalizationManager] Detected marker '{markerName}' " +
                                     "has no matching MarkerNode in the NavGraph. " +
                                     "Check that markerName in the graph matches the Vuforia target name exactly.");
                    return;
                }

                CurrentNode         = node;
                HasLocalization     = true;
                IsCurrentlyTracking = true;

                Debug.Log($"[MarkerLocalizationManager] Detected: {markerName} → Node: {node.nodeID} ({node.displayLabel})");
                OnMarkerDetected?.Invoke(node);
            }
            else
            {
                // Tracking lost retain CurrentNode (last-known-position strategy)
                IsCurrentlyTracking = false;

                Debug.Log($"[MarkerLocalizationManager] Tracking lost for '{observer.TargetName}'. " +
                          "CurrentNode retained.");
                OnMarkerLost?.Invoke();
            }
        }

        private IEnumerator VisionLocalizationRoutine(Dictionary<string, string> descriptions)
        {
            // Briefly hide UI so Gemini doesn't see buttons/text overlaying the shelves
            if (uiCanvas != null) uiCanvas.enabled = false;

            // WaitForEndOfFrame is required so Vuforia has finished rendering its camera feed
            yield return new WaitForEndOfFrame();

            byte[] jpegBytes = CaptureFrameAsJpeg();
            
            // Restore UI immediately after capture
            if (uiCanvas != null) uiCanvas.enabled = true;

            if (jpegBytes == null)
            {
                _visionRequestInFlight = false;
                yield break;
            }

            Debug.Log($"[MarkerLocalizationManager] Vision frame: {jpegBytes.Length / 1024} KB → Gemini...");
            geminiClassifier.IdentifyLocation(descriptions, jpegBytes, OnVisionSuccess, OnVisionFailure);
        }

        private void OnVisionSuccess(string nodeID)
        {
            _visionRequestInFlight = false;

            if (string.Equals(nodeID, "UNKNOWN", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[MarkerLocalizationManager] Vision: UNKNOWN — no node update.");
                return; // retain last CurrentNode silently
            }

            // Defensive: take only the first word/line in case Gemini adds preamble despite instructions
            nodeID = nodeID.Split('\n')[0].Split(' ')[0];

            var node = navGraph.GetNode(nodeID);
            if (node == null)
            {
                Debug.LogWarning($"[MarkerLocalizationManager] Vision returned unknown nodeID '{nodeID}'.");
                return;
            }

            if (CurrentNode != null && CurrentNode.nodeID == node.nodeID) return; // no change

            CurrentNode     = node;
            HasLocalization = true;
            Debug.Log($"[MarkerLocalizationManager] Vision → Node: {node.nodeID} ({node.displayLabel})");
            OnMarkerDetected?.Invoke(node);
        }

        private void OnVisionFailure(string error)
        {
            _visionRequestInFlight = false;
            Debug.LogWarning($"[MarkerLocalizationManager] Vision failed: {error}");
        }

        private Dictionary<string, string> BuildNodeDescriptions()
        {
            var dict = new Dictionary<string, string>();
            foreach (string id in navGraph.AllNodeIDs())
            {
                var node = navGraph.GetNode(id);
                if (node != null && !string.IsNullOrWhiteSpace(node.visualDescription))
                    dict[node.nodeID] = node.visualDescription;
            }
            return dict;
        }

        /// <summary>
        /// Captures a downsampled JPEG of the current screen (includes Vuforia camera feed).
        /// Must be called from inside a coroutine that yielded WaitForEndOfFrame first.
        /// Downsampled to 512 px wide enough for Gemini scene recognition, keeps payload small.
        /// </summary>
        private byte[] CaptureFrameAsJpeg()
        {
            Texture2D screenTex = ScreenCapture.CaptureScreenshotAsTexture();
            if (screenTex == null) return null;

            try
            {
                const int maxDim = 512;
                int sw = screenTex.width, sh = screenTex.height;
                int w, h;
                if (sw >= sh) { w = maxDim; h = Mathf.RoundToInt(sh * maxDim / (float)sw); }
                else          { h = maxDim; w = Mathf.RoundToInt(sw * maxDim / (float)sh); }

                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(screenTex, rt);
                var resized = new Texture2D(w, h, TextureFormat.RGB24, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                resized.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                resized.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                byte[] jpeg = resized.EncodeToJPG(75);
                Destroy(screenTex);
                Destroy(resized);
                return jpeg;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MarkerLocalizationManager] Frame capture failed: {ex.Message}");
                Destroy(screenTex);
                return null;
            }
        }

        /// <summary>
        /// Debug helper right-click MarkerLocalizationManager in Inspector to run.
        /// Loads Assets/Resources/TestCapture.jpg and sends it to Gemini for identification.
        /// Remove before final build.
        /// </summary>
        [ContextMenu("Debug: Vision Test Static Image")]
        private void DebugVisionStaticImage()
        {
            if (!useVisionLocalization || geminiClassifier == null)
            {
                Debug.LogWarning("[MarkerLocalizationManager] Enable Vision Localization and wire GeminiClassifier first.");
                return;
            }

            var tex = Resources.Load<Texture2D>("TestCapture");
            if (tex == null)
            {
                Debug.LogError("[MarkerLocalizationManager] Put a JPEG named 'TestCapture.jpg' in Assets/Resources/");
                return;
            }

            var descriptions = BuildNodeDescriptions();
            if (descriptions.Count == 0)
            {
                Debug.LogWarning("[MarkerLocalizationManager] No visualDescriptions set on NavGraph nodes.");
                return;
            }

            _visionRequestInFlight = true;
            geminiClassifier.IdentifyLocation(descriptions, tex.EncodeToJPG(75), OnVisionSuccess, OnVisionFailure);
        }
    }
}
