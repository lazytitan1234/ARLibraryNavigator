using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARLibraryNav.Navigation;
using ARLibraryNav.Logging;

namespace ARLibraryNav.UI
{
    public class BookSearchController : MonoBehaviour
    {
        // Inspector References
        [Header("Data")]
        [SerializeField] private NavGraph      navGraph;
        [SerializeField] private SessionLogger sessionLogger;

        [Header("UI – Controls")]
        [SerializeField] private TMP_Dropdown   topicDropdown;
        [SerializeField] private TMP_InputField currentShelfInput;
        [SerializeField] private Button         findButton;
        [SerializeField] private Button         clearButton;

        [Header("UI – Output")]
        [SerializeField] private TextMeshProUGUI directionsText;
        [SerializeField] private TextMeshProUGUI statusText;

        // State
        private readonly List<MarkerNode> _shelfNodes = new List<MarkerNode>();

        // Unity Lifecycle
        private void Start()
        {
            navGraph?.BuildLookups(); // ensure dictionary is rebuilt after any external asset edits
            BuildShelfNodeList();
            PopulateDropdown();
            ClearDirections();
        }

        // Private Setup

        private void BuildShelfNodeList()
        {
            _shelfNodes.Clear();
            if (navGraph == null)
            {
                Debug.LogWarning("[BookSearchController] NavGraph not assigned.");
                return;
            }

            foreach (var node in navGraph.nodes)
            {
                if (node.nodeID.StartsWith("SHELF_"))
                    _shelfNodes.Add(node);
            }

            // Sort alphabetically by displayLabel for a tidy dropdown
            _shelfNodes.Sort((a, b) =>
                string.Compare(a.displayLabel, b.displayLabel,
                               System.StringComparison.OrdinalIgnoreCase));

            Debug.Log($"[BookSearchController] Loaded {_shelfNodes.Count} shelf nodes into dropdown.");
        }

        private void PopulateDropdown()
        {
            if (topicDropdown == null) return;

            var options = new List<string> { "-- Select a topic --" };
            foreach (var node in _shelfNodes)
                options.Add(node.displayLabel);

            topicDropdown.ClearOptions();
            topicDropdown.AddOptions(options);
            topicDropdown.value = 0;
        }

        // Public (wired to Buttons)

        /// <summary>Called by the "Get Directions" button.</summary>
        public void OnFindClicked()
        {
            // Validate topic selection (index 0 = placeholder "-- Select a topic --")
            int selectedIndex = topicDropdown != null ? topicDropdown.value - 1 : -1;

            if (selectedIndex < 0 || selectedIndex >= _shelfNodes.Count)
            {
                ShowStatus("Please select a topic first.");
                ClearDirections();
                return;
            }

            MarkerNode targetNode    = _shelfNodes[selectedIndex];
            string     rawInput      = currentShelfInput != null ? currentShelfInput.text.Trim() : string.Empty;
            string     currentNodeID = NormalizeShelfInput(rawInput);
            MarkerNode currentNode   = navGraph != null ? navGraph.GetNode(currentNodeID) : null;

            string directions = BuildDirections(currentNode, targetNode, rawInput);
            ShowDirections(directions);
            ShowStatus(string.Empty);

            sessionLogger?.LogEvent("BOOK_SEARCH",
                $"target={targetNode.nodeID} current={currentNodeID} floor={targetNode.floor}");
        }

        /// <summary>Called by the "Clear" / "Search Again" button.</summary>
        public void OnClearClicked()
        {
            if (topicDropdown     != null) topicDropdown.value    = 0;
            if (currentShelfInput != null) currentShelfInput.text = string.Empty;
            ClearDirections();
            ShowStatus(string.Empty);
        }

        // Core Logic

        /// <summary>
        /// Converts user-typed shelf input into a NavGraph nodeID.
        /// Accepts formats: "B2-15", "B2_15", "b2_15", "SHELF_B2_15".
        /// </summary>
        private string NormalizeShelfInput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string normalized = raw.Trim().ToUpper()
                                   .Replace("-", "_")
                                   .Replace(" ", "_");

            if (!normalized.StartsWith("SHELF_"))
                normalized = "SHELF_" + normalized;

            return normalized;
        }

        /// <summary>
        /// Builds a plain-English direction string based on floor comparison.
        /// </summary>
        private string BuildDirections(MarkerNode currentNode, MarkerNode targetNode, string rawInput)
        {
            // Already at the destination
            if (currentNode != null && currentNode.nodeID == targetNode.nodeID)
                return $"You are already at your destination!\n\n{targetNode.displayLabel}";

            // Unknown current location (blank or unrecognised shelf ID)
            if (currentNode == null)
            {
                string targetInfo = $"{targetNode.displayLabel}\n(Level {targetNode.floor})";

                if (!string.IsNullOrWhiteSpace(rawInput))
                    return $"Shelf \"{rawInput}\" was not found in the library map.\n\n" +
                           $"Your destination is:\n{targetInfo}";

                // No location provided — just tell them where to go
                return $"Your destination is:\n{targetInfo}";
            }

            // Same floor
            if (currentNode.floor == targetNode.floor)
                return $"You are on Level {currentNode.floor}.\n\n" +
                       $"Your destination is also on Level {targetNode.floor}.\n\n" +
                       $"Head to:\n{targetNode.displayLabel}";

            // Different floor
            string stairDirection = targetNode.floor > currentNode.floor ? "up" : "down";
            return $"You are currently on Level {currentNode.floor}.\n\n" +
                   $"Take the stairs {stairDirection} to Level {targetNode.floor},\n" +
                   $"then look for:\n{targetNode.displayLabel}";
        }

        // UI Helpers

        private void ShowDirections(string text)
        {
            if (directionsText != null)
            {
                directionsText.text = text;
                directionsText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void ClearDirections()
        {
            if (directionsText != null)
            {
                directionsText.text = string.Empty;
                directionsText.gameObject.SetActive(false);
            }
        }

        private void ShowStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }
    }
}
