// TEMPORARY
using UnityEngine;
using ARLibraryNav.Navigation;
using ARLibraryNav.AR;

public class PathfinderTest : MonoBehaviour
{
    public NavGraph navGraph;
    public MarkerLocalizationManager localizationManager;

    void Start()
    {
        localizationManager.ForceSetCurrentNode("L2_Entrance");
        var path = Pathfinder.FindPath(navGraph, "L2_Entrance", "L2_SectionB");
        Debug.Log($"Path found: {path.Count} nodes");
        foreach (var node in path)
            Debug.Log($"  ? {node.nodeID} ({node.displayLabel})");
    }
}
