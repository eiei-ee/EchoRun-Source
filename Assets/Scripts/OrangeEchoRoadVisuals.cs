using UnityEngine;

// Attaches authored art to the existing pool. Collision and route coordinates
// remain owned by TrackManager, including its fallback coverage colliders.
public static class OrangeEchoRoadVisuals
{
    public const string RootName = "OrangeEchoRoad";

    public static void Apply(GameObject segment, TrackSegmentType type)
    {
        if (segment == null) return;
        Transform art = segment.transform.Find(RootName);
        if (art == null)
        {
            string asset = type == TrackSegmentType.Straight ? "OrangeRoadStraight"
                : type == TrackSegmentType.TurnRight ? "OrangeRoadRight" : "OrangeRoadLeft";
            GameObject prefab = Resources.Load<GameObject>("OrangeEcho/" + asset);
            if (prefab == null) return; // Retain the existing road if art is unavailable.
            art = Object.Instantiate(prefab, segment.transform, false).transform;
            art.name = RootName;
        }
        foreach (Renderer renderer in segment.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(art)) continue;
            string name = renderer.name;
            bool replaced = name == "GroundPlane" || name == "EntryStrip"
                || name == "ExitStrip" || name == "EntryCoverage" || name == "ExitCoverage"
                || name == TrackManager.TurnInnerCornerCapName
                || name.StartsWith("LaneLine_") || name.StartsWith("DataSeam")
                || name == "LeftRail" || name == "RightRail"
                || name == "TurnEntryRail" || name == "TurnExitRail";
            for (Transform parent = renderer.transform; parent != segment.transform && parent != null;
                 parent = parent.parent)
                if (parent.name == "RoadVisual") { replaced = true; break; }
            if (replaced) renderer.enabled = false;
        }
    }
}
