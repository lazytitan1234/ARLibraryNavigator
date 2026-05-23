using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARLibraryNav.Navigation;
using ARLibraryNav.Logging;

namespace ARLibraryNav.UI
{
    public class FloorPlanViewController : MonoBehaviour
    {
        // Inspector
        [Header("Data")]
        [SerializeField] private NavGraph      navGraph;
        [SerializeField] private SessionLogger sessionLogger;

        [Header("Floor Plan Textures (assign in Inspector after running WireSceneReferences)")]
        [SerializeField] private Texture2D floorPlanLevel2;
        [SerializeField] private Texture2D floorPlanLevel3;

        [Header("UI References")]
        [SerializeField] private GameObject    floorPlanPanel;
        [SerializeField] private RawImage      floorPlanImage;
        [SerializeField] private RectTransform overlayRoot;   // Same size as floorPlanImage — holds dots/lines
        [SerializeField] private TextMeshProUGUI stepsText;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Map World Bounds  (update after library visit)")]
        [Tooltip("World-space (X, Z) that maps to the bottom-left pixel of the floor plan image.")]
        [SerializeField] private Vector2 mapWorldMin = Vector2.zero;
        [Tooltip("World-space (X, Z) that maps to the top-right pixel of the floor plan image.")]
        [SerializeField] private Vector2 mapWorldMax = new Vector2(50f, 40f);
        [Tooltip("Auto-recalculate map bounds from non-zero NavGraph node positions on every ShowPath() call. "
               + "Disable once you have manually set mapWorldMin / mapWorldMax.")]
        [SerializeField] private bool autoBounds = true;

        [Header("Visual Style")]
        [SerializeField] private float dotRadius     = 18f;
        [SerializeField] private float lineThickness = 6f;
        [SerializeField] private Color colorCurrent  = new Color(0.18f, 0.85f, 0.30f, 1f); // green
        [SerializeField] private Color colorTarget   = new Color(1.00f, 0.35f, 0.10f, 1f); // orange-red
        [SerializeField] private Color colorWaypoint = new Color(1.00f, 1.00f, 1.00f, 0.85f); // white
        [SerializeField] private Color colorLine     = new Color(0.30f, 0.75f, 1.00f, 0.80f); // sky blue

        // Runtime State
        private readonly List<GameObject> _overlayElements = new List<GameObject>();

        /// <summary>
        /// Shows the floor plan panel with the path drawn on it.
        /// </summary>
        /// <param name="orderedNodeIDs">Ordered list of nodeIDs from Pathfinder (current→target).</param>
        /// <param name="currentNodeID">Where the user currently is.</param>
        /// <param name="targetNodeID">Where the user wants to go.</param>
        public void ShowPath(List<string> orderedNodeIDs, string currentNodeID, string targetNodeID)
        {
            if (floorPlanPanel == null) { Debug.LogError("[FloorPlanVC] floorPlanPanel not assigned."); return; }
            if (navGraph       == null) { Debug.LogError("[FloorPlanVC] navGraph not assigned.");       return; }

            // Resolve nodeIDs → MarkerNode objects
            var path = new List<MarkerNode>();
            foreach (var id in orderedNodeIDs)
            {
                var node = navGraph.GetNode(id);
                if (node != null) path.Add(node);
                else Debug.LogWarning($"[FloorPlanVC] Node not found in NavGraph: '{id}'");
            }

            // Determine display floor from starting node
            var currentNode  = navGraph.GetNode(currentNodeID);
            var targetNode   = navGraph.GetNode(targetNodeID);
            int displayFloor = currentNode != null ? currentNode.floor : 2;

            // Texture selection
            Texture2D tex = displayFloor >= 3 ? floorPlanLevel3 : floorPlanLevel2;
            if (floorPlanImage != null)
            {
                floorPlanImage.texture = tex != null ? tex : Texture2D.whiteTexture;
                floorPlanImage.color   = Color.white;
            }

            // Update map bounds if requested
            if (autoBounds) RecalculateBoundsFromGraph();

            // Title
            if (titleText != null)
            {
                string toLbl = targetNode != null ? targetNode.displayLabel : targetNodeID;
                titleText.text = $"Level {displayFloor}  —  Route to: {toLbl}";
            }

            // Force layout update so overlayRoot.rect is valid before we place dots
            Canvas.ForceUpdateCanvases();

            // Draw overlay
            ClearOverlay();
            DrawPath(path, currentNodeID, targetNodeID);

            // Steps text
            if (stepsText != null)
                stepsText.text = BuildStepsText(path, currentNodeID, targetNodeID);

            sessionLogger?.LogEvent("FLOOR_PLAN_SHOWN",
                $"from={currentNodeID} to={targetNodeID} steps={path.Count} floor={displayFloor}");

            floorPlanPanel.SetActive(true);
        }

        /// <summary>
        /// Shows the floor plan with only the target node marked (no current position known).
        /// Used when the user skips the shelf scan step.
        /// </summary>
        public void ShowTargetOnly(string targetNodeID)
        {
            ShowPath(new List<string> { targetNodeID }, targetNodeID, targetNodeID);
        }

        /// <summary>Hides the panel and clears all overlay elements.</summary>
        public void Hide()
        {
            if (floorPlanPanel != null) floorPlanPanel.SetActive(false);
            ClearOverlay();
        }

        /// <summary>Called by the Close button on the floor plan panel.</summary>
        public void OnCloseClicked() => Hide();

        // Path Drawing

        private void DrawPath(List<MarkerNode> path, string currentID, string targetID)
        {
            if (overlayRoot == null) return;

            if (AllPositionsZero(path))
            {
                // worldPositions haven't been set yet — draw a placeholder linear layout
                DrawPlaceholderDots(path, currentID, targetID);
                return;
            }

            // Draw connecting lines first (so dots render on top)
            for (int i = 0; i < path.Count - 1; i++)
            {
                CreateLine(
                    WorldToOverlay(path[i].worldPosition),
                    WorldToOverlay(path[i + 1].worldPosition),
                    colorLine);
            }

            // Draw node dots
            for (int i = 0; i < path.Count; i++)
            {
                var     node      = path[i];
                bool    isCurrent = node.nodeID == currentID;
                bool    isTarget  = node.nodeID == targetID;
                Color   dotColor  = isCurrent ? colorCurrent  : isTarget ? colorTarget  : colorWaypoint;
                float   radius    = (isCurrent || isTarget)   ? dotRadius * 1.35f        : dotRadius;
                string  dotLabel  = isCurrent ? "YOU"         : isTarget ? "TARGET"      : "";
                CreateDot(WorldToOverlay(node.worldPosition), dotColor, radius, dotLabel);
            }
        }

        /// <summary>
        /// Draws nodes in a horizontal line when worldPositions are all zero.
        /// Gives a visual preview of the route even before the library visit.
        /// </summary>
        private void DrawPlaceholderDots(List<MarkerNode> path, string currentID, string targetID)
        {
            if (overlayRoot == null || path.Count == 0) return;

            float ow = overlayRoot.rect.width;
            float oh = overlayRoot.rect.height;
            if (ow < 10f) ow = 800f;  // fallback if layout not yet resolved
            if (oh < 10f) oh = 600f;

            float margin  = ow * 0.1f;
            float usable  = ow - margin * 2f;
            float spacing = path.Count > 1 ? usable / (path.Count - 1) : 0f;
            float y       = 0f; // horizontal row through the centre

            for (int i = 0; i < path.Count; i++)
            {
                var    node      = path[i];
                float  x        = -ow / 2f + margin + spacing * i;
                bool   isCurrent = node.nodeID == currentID;
                bool   isTarget  = node.nodeID == targetID;
                Color  dotColor  = isCurrent ? colorCurrent : isTarget ? colorTarget : colorWaypoint;
                float  radius    = (isCurrent || isTarget)  ? dotRadius * 1.35f      : dotRadius;
                string dotLabel  = isCurrent ? "START"      : isTarget ? "END"       : $"{i}";

                CreateDot(new Vector2(x, y), dotColor, radius, dotLabel);

                if (i < path.Count - 1)
                    CreateLine(new Vector2(x, y), new Vector2(x + spacing, y), colorLine);
            }
        }

        // Coordinate Mapping

        /// <summary>
        /// Converts a NavGraph worldPosition (X, Z metres) to local coords inside overlayRoot,
        /// where (0, 0) = centre of the overlay rectangle.
        /// </summary>
        private Vector2 WorldToOverlay(Vector2 worldPos)
        {
            if (overlayRoot == null) return Vector2.zero;

            float ow = overlayRoot.rect.width;
            float oh = overlayRoot.rect.height;
            if (ow < 10f) ow = 800f;
            if (oh < 10f) oh = 600f;

            float tx = Mathf.InverseLerp(mapWorldMin.x, mapWorldMax.x, worldPos.x);
            float ty = Mathf.InverseLerp(mapWorldMin.y, mapWorldMax.y, worldPos.y);

            return new Vector2(
                Mathf.Lerp(-ow / 2f, ow / 2f, tx),
                Mathf.Lerp(-oh / 2f, oh / 2f, ty));
        }

        private void RecalculateBoundsFromGraph()
        {
            if (navGraph == null || navGraph.nodes == null) return;

            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;
            bool  any  = false;

            foreach (var node in navGraph.nodes)
            {
                if (node.worldPosition == Vector2.zero) continue;
                any  = true;
                minX = Mathf.Min(minX, node.worldPosition.x);
                minZ = Mathf.Min(minZ, node.worldPosition.y);
                maxX = Mathf.Max(maxX, node.worldPosition.x);
                maxZ = Mathf.Max(maxZ, node.worldPosition.y);
            }

            if (!any) return;

            float padX = Mathf.Max((maxX - minX) * 0.12f, 1f);
            float padZ = Mathf.Max((maxZ - minZ) * 0.12f, 1f);
            mapWorldMin = new Vector2(minX - padX, minZ - padZ);
            mapWorldMax = new Vector2(maxX + padX, maxZ + padZ);
        }

        // UI Element Factories

        /// <summary>Creates a square dot with optional label above it inside overlayRoot.</summary>
        private void CreateDot(Vector2 localPos, Color color, float radius, string label)
        {
            if (overlayRoot == null) return;

            // Dot image
            var go  = new GameObject("Dot", typeof(Image));
            go.transform.SetParent(overlayRoot, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta        = new Vector2(radius * 2f, radius * 2f);
            go.GetComponent<Image>().color = color;
            _overlayElements.Add(go);

            // Optional label
            if (!string.IsNullOrEmpty(label))
            {
                var lGO = new GameObject("DotLabel", typeof(TextMeshProUGUI));
                lGO.transform.SetParent(overlayRoot, false);
                var lRT = lGO.GetComponent<RectTransform>();
                lRT.anchorMin        = lRT.anchorMax = new Vector2(0.5f, 0.5f);
                lRT.pivot            = new Vector2(0.5f, 0f);
                lRT.anchoredPosition = new Vector2(localPos.x, localPos.y + radius + 4f);
                lRT.sizeDelta        = new Vector2(120f, 28f);
                var tmp = lGO.GetComponent<TextMeshProUGUI>();
                tmp.text      = label;
                tmp.fontSize  = 17;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color     = color;
                tmp.alignment = TextAlignmentOptions.Center;
                _overlayElements.Add(lGO);
            }
        }

        /// <summary>Creates a rotated rectangle Image strip connecting two overlay positions.</summary>
        private void CreateLine(Vector2 from, Vector2 to, Color color)
        {
            if (overlayRoot == null) return;

            var go  = new GameObject("Line", typeof(Image));
            go.transform.SetParent(overlayRoot, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin    = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot        = new Vector2(0.5f, 0.5f);
            go.GetComponent<Image>().color = color;

            Vector2 dir    = to - from;
            float   length = dir.magnitude;
            float   angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = (from + to) * 0.5f;
            rt.sizeDelta        = new Vector2(length, lineThickness);
            rt.localRotation    = Quaternion.Euler(0f, 0f, angle);
            _overlayElements.Add(go);
        }

        private void ClearOverlay()
        {
            foreach (var go in _overlayElements)
                if (go != null) Destroy(go);
            _overlayElements.Clear();
        }

        // Step Text Builder

        private string BuildStepsText(List<MarkerNode> path, string currentID, string targetID)
        {
            if (path.Count == 0)
                return "No route found.\nTry scanning your shelf label again, or ask a librarian.";

            if (path.Count == 1)
                return $"<b>You are already there!</b>\n{path[0].displayLabel}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Your route:</b>\n");

            for (int i = 0; i < path.Count; i++)
            {
                var    n   = path[i];
                string sfx = "";
                if (n.isStairNode && i + 1 < path.Count)
                    sfx = $"  →  Take stairs to Level {path[i + 1].floor}";

                if (i == 0)
                    sb.AppendLine($"<color=#33DD55>● You are here: {n.displayLabel}</color>{sfx}");
                else if (i == path.Count - 1)
                    sb.AppendLine($"<color=#FF7733>★ Destination: {n.displayLabel}</color>{sfx}");
                else
                    sb.AppendLine($"  → {n.displayLabel}{sfx}");
            }

            return sb.ToString().TrimEnd();
        }

        // Helpers

        private bool AllPositionsZero(List<MarkerNode> path)
        {
            foreach (var n in path)
                if (n.worldPosition != Vector2.zero) return false;
            return true;
        }
    }
}
