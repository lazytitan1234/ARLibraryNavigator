// TEMPORARY TEST SCRIPT
using UnityEngine;
using ARLibraryNav.Navigation;
using ARLibraryNav.AR;

public class PathfinderTest : MonoBehaviour
{
    [Header("Wire these in the Inspector")]
    public NavGraph navGraph;
    public MarkerLocalizationManager localizationManager;

    private void Start()
    {
        if (navGraph == null || localizationManager == null)
        {
            Debug.LogError("[PathfinderTest] navGraph or localizationManager not assigned in Inspector.");
            return;
        }

        // Simulate scanning the Entrance marker (no physical marker needed)
        localizationManager.ForceSetCurrentNode("L2_Entrance");

        // Run Dijkstra from Entrance to Section B
        var path = Pathfinder.FindPath(navGraph, "L2_Entrance", "L2_SectionB");

        Debug.Log($"[PathfinderTest] Path found: {path.Count} nodes");
        foreach (var node in path)
            Debug.Log($"  → {node.nodeID} ({node.displayLabel})");

        // Expected output:
        //   Path found: 3 nodes
        //   -> L2_Entrance (Level 2 – Entrance)
        //   -> L2_SectionA (Level 2 – Section A (Computing))
        //   -> L2_SectionB (Level 2 – Section B (History))
    }
}
