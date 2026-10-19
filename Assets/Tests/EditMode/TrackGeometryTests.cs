using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TrackGeometryTests
{
    [Test]
    public void LaneCentersStayFixedAtMinusThreeZeroAndThree()
    {
        Assert.AreEqual(-3f, TrackGeometryStandards.GetLaneCenter(0));
        Assert.AreEqual(0f, TrackGeometryStandards.GetLaneCenter(1));
        Assert.AreEqual(3f, TrackGeometryStandards.GetLaneCenter(2));
    }

    [Test]
    public void StraightPrefabUsesSharedRoadWidthAndCenteredRails()
    {
        GameObject prefab = LoadPrefab("Assets/Prefabs/TrackSegment.prefab");

        AssertRoadRootAndLanes(prefab);
        AssertRoadSurface(prefab.transform.Find("GroundPlane"));
        Transform leftRail = TryFindDescendant(prefab.transform, "LeftRail");
        Transform rightRail = TryFindDescendant(prefab.transform, "RightRail");
        if (leftRail != null && rightRail != null)
        {
            Assert.AreEqual(-TrackGeometryStandards.EdgeRailOffset,
                leftRail.localPosition.x, 0.001f);
            Assert.AreEqual(TrackGeometryStandards.EdgeRailOffset,
                rightRail.localPosition.x, 0.001f);
        }
        else
        {
            AssertColdWhiteRoadEdge(prefab);
        }
    }

    [TestCase("Assets/Prefabs/TurnSegment_Left.prefab")]
    [TestCase("Assets/Prefabs/TurnSegment_Right.prefab")]
    public void TurnPrefabsUseTheSameEntryAndExitWidth(string path)
    {
        GameObject prefab = LoadPrefab(path);

        AssertRoadRootAndLanes(prefab);
        Transform entry = prefab.transform.Find("EntryStrip");
        Transform exit = prefab.transform.Find("ExitStrip");
        AssertRoadWidth(entry);
        AssertRoadWidth(exit);
        AssertTurnJoinBounds(entry, exit);
        AssertTurnCornerSupport(prefab,
            path.Contains("Right") ? 1 : -1);
        Transform entryRail = TryFindDescendant(prefab.transform,
            "TurnEntryRail");
        Transform exitRail = TryFindDescendant(prefab.transform,
            "TurnExitRail");
        if (entryRail != null && exitRail != null)
        {
            Assert.AreEqual(TrackGeometryStandards.EdgeRailOffset,
                Mathf.Abs(entryRail.localPosition.x), 0.001f);
            Assert.AreEqual(10f + TrackGeometryStandards.EdgeRailOffset,
                exitRail.localPosition.z, 0.001f);
        }
        else
        {
            AssertColdWhiteRoadEdge(prefab);
        }
    }

    [TestCase(-1, -7.75f)]
    [TestCase(1, 7.75f)]
    public void TurnCornerGeometryClosesTheMirroredInnerSquare(
        int turnDirection, float expectedCapX)
    {
        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        Vector3 cap = TrackGeometryStandards.TurnInnerCornerCenter(
            segmentLength, turnDirection);
        Vector3 bridge = TrackGeometryStandards.TurnWalkableBridgeCenter(
            segmentLength, turnDirection);

        Assert.AreEqual(4.5f,
            TrackGeometryStandards.TurnInnerCornerSize(segmentLength),
            0.001f);
        Assert.AreEqual(expectedCapX, cap.x, 0.001f);
        Assert.AreEqual(2.25f, cap.z, 0.001f);
        Assert.AreEqual(turnDirection * 5f, bridge.x, 0.001f);
        Assert.AreEqual(10f, bridge.z, 0.001f);
        Assert.AreEqual(1f,
            TrackGeometryStandards.TurnWalkableBridgeWidth, 0.001f);
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.NotNull(prefab, path);
        return prefab;
    }

    private static void AssertRoadRootAndLanes(GameObject prefab)
    {
        Assert.AreEqual(Vector3.one, prefab.transform.localScale,
            $"{prefab.name} root scale must remain 1,1,1.");
        for (int lane = 0; lane < 3; lane++)
        {
            Transform laneTransform = prefab.transform.Find($"Lane_{lane}");
            Assert.NotNull(laneTransform, $"{prefab.name}/Lane_{lane}");
            Assert.AreEqual(TrackGeometryStandards.GetLaneCenter(lane),
                laneTransform.localPosition.x, 0.001f);
        }
    }

    private static void AssertRoadSurface(Transform surface)
    {
        AssertRoadWidth(surface);
        Assert.AreEqual(TrackGeometryStandards.StandardSegmentLength,
            surface.localScale.z * 10f, 0.001f);

        BoxCollider collider = surface.GetComponent<BoxCollider>();
        Assert.NotNull(collider);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            collider.size.x * surface.localScale.x, 0.001f);
        Assert.AreEqual(TrackGeometryStandards.StandardSegmentLength,
            collider.size.z * surface.localScale.z, 0.001f);
    }

    private static void AssertRoadWidth(Transform surface)
    {
        Assert.NotNull(surface);
        Assert.AreEqual(TrackGeometryStandards.VisualRoadWidth,
            surface.localScale.x * 10f, 0.001f);

        BoxCollider collider = surface.GetComponent<BoxCollider>();
        Assert.NotNull(collider);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            collider.size.x * surface.localScale.x, 0.001f);
    }

    private static void AssertTurnJoinBounds(Transform entry, Transform exit)
    {
        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        float entryLength = entry.localScale.z * 10f;
        float entryNear = entry.localPosition.z - entryLength * 0.5f;
        float entryFar = entry.localPosition.z + entryLength * 0.5f;
        Assert.AreEqual(0f, entryNear, 0.001f,
            "The turn entry must touch the previous straight without overlap.");
        Assert.AreEqual(segmentLength * 0.5f
                        + TrackGeometryStandards.VisualRoadHalfWidth,
            entryFar, 0.001f,
            "The entry may cover only the corner square, not continue as a road arm.");

        float exitLength = exit.localScale.z * 10f;
        float exitCenter = Mathf.Abs(exit.localPosition.x);
        Assert.AreEqual(TrackGeometryStandards.VisualRoadHalfWidth,
            exitCenter - exitLength * 0.5f, 0.001f,
            "The exit must begin at the corner square's outer edge.");
        Assert.AreEqual(segmentLength * 0.5f,
            exitCenter + exitLength * 0.5f, 0.001f,
            "The exit must stop at the following straight's near edge.");

        BoxCollider entryCollider = entry.GetComponent<BoxCollider>();
        BoxCollider exitCollider = exit.GetComponent<BoxCollider>();
        Assert.AreEqual(entryLength,
            entryCollider.size.z * entry.localScale.z, 0.001f);
        Assert.AreEqual(exitLength,
            exitCollider.size.z * exit.localScale.z, 0.001f);
    }

    private static void AssertTurnCornerSupport(GameObject prefab,
        int turnDirection)
    {
        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        Transform cap = prefab.transform.Find(
            TrackManager.TurnInnerCornerCapName);
        Assert.NotNull(cap, prefab.name + " corner cap");
        Vector3 expectedCap = TrackGeometryStandards.TurnInnerCornerCenter(
            segmentLength, turnDirection);
        Assert.AreEqual(expectedCap.x, cap.localPosition.x, 0.001f);
        Assert.AreEqual(expectedCap.z, cap.localPosition.z, 0.001f);
        float capSize = TrackGeometryStandards.TurnInnerCornerSize(
            segmentLength);
        Assert.AreEqual(capSize, cap.localScale.x * 10f, 0.001f);
        Assert.AreEqual(capSize, cap.localScale.z * 10f, 0.001f);
        Assert.IsNull(cap.GetComponent<Collider>(),
            "The visual cap must not expand the formal walkable boundary.");
        Renderer capRenderer = cap.GetComponent<Renderer>();
        Assert.NotNull(capRenderer);
        Assert.AreEqual(turnDirection < 0, capRenderer.enabled,
            "Only the legacy left turn needs the separate visible cap; the formal right mesh contains it.");

        Transform bridge = prefab.transform.Find(
            TrackManager.TurnWalkableBridgeName);
        Assert.NotNull(bridge, prefab.name + " walkable bridge");
        Vector3 expectedBridge =
            TrackGeometryStandards.TurnWalkableBridgeCenter(
                segmentLength, turnDirection);
        Assert.AreEqual(expectedBridge.x, bridge.localPosition.x, 0.001f);
        Assert.AreEqual(expectedBridge.z, bridge.localPosition.z, 0.001f);
        Assert.IsNull(bridge.GetComponent<Renderer>(),
            "The collision bridge must remain invisible.");
        BoxCollider bridgeCollider = bridge.GetComponent<BoxCollider>();
        Assert.NotNull(bridgeCollider);
        Assert.IsTrue(bridgeCollider.enabled);
        Assert.AreEqual(TrackGeometryStandards.TurnWalkableBridgeWidth,
            bridgeCollider.size.x, 0.001f);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            bridgeCollider.size.z, 0.001f);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform descendant = TryFindDescendant(root, name);
        if (descendant != null) return descendant;

        Assert.Fail($"Missing {root.name}/{name}");
        return null;
    }

    private static Transform TryFindDescendant(Transform root, string name)
    {
        foreach (Transform descendant in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (descendant.name == name) return descendant;
        }
        return null;
    }

    private static void AssertColdWhiteRoadEdge(GameObject prefab)
    {
        int edgeRendererCount = 0;
        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.name.IndexOf("RoadEdgeWhite",
                    System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            edgeRendererCount++;
            Assert.IsTrue(renderer.enabled,
                renderer.name + " must remain visible.");
            Assert.NotNull(renderer.sharedMaterial, renderer.name);
            Assert.AreEqual("ColdWhiteFortress_Ceramic",
                renderer.sharedMaterial.name,
                "The authored road edge replaces the legacy rail with a neutral ceramic guard.");
        }

        Assert.Greater(edgeRendererCount, 0,
            prefab.name + " must provide either legacy rails or an authored ceramic road edge.");
    }
}
