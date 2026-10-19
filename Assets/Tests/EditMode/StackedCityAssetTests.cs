using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StackedCityAssetTests
{
    private readonly List<GameObject> owned = new List<GameObject>();

    [TearDown]
    public void Cleanup()
    {
        foreach (GameObject instance in owned)
            if (instance != null) Object.DestroyImmediate(instance);
        owned.Clear();
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void StackedDistrictHasDepthAndCannotEnterGameplay(int variant)
    {
        GameObject block = LoadInstance("StackedBlock" + variant);
        Assert.That(block.GetComponentsInChildren<Collider>(true), Is.Empty,
            "City dressing must not participate in runner collision.");
        Assert.That(block.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        AssertAssetBindingsAndBudget(block, 20);
        foreach (float time in new[] { 0f, 3f, 10f, 31f, 89f })
        {
            SampleTransit(block, time);
            Bounds bounds = RendererBounds(block);
            Assert.That(bounds.max.y, Is.LessThan(-3f), "Lower streets enter the player/camera corridor at t=" + time);
            Assert.That(bounds.size.y, Is.GreaterThan(20f), "The stacked city must show more than one shallow street plane.");
        }
    }

    [Test]
    public void ElevatedRailAndMovingTrainsStayAboveTheCameraCorridor()
    {
        GameObject transit = LoadInstance("UpperTransit");
        Assert.That(transit.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(transit.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        AssertAssetBindingsAndBudget(transit, 8);
        bool hasTraffic = false;
        foreach (float time in new[] { 0f, 2f, 5f, 13f, 27f, 53f, 89f })
        {
            hasTraffic |= SampleTransit(transit, time) > 0;
            Assert.That(RendererBounds(transit).min.y, Is.GreaterThan(8f),
                "Rail or moving vehicle intrudes into gameplay at t=" + time);
        }
        Assert.That(hasTraffic, Is.True, "An elevated rail prefab must bind the moving train presentation.");
    }

    [Test]
    public void UpperRailRecyclingKeepsVisibleTrackStationary()
    {
        var viewer = new GameObject("UpperRailTestViewer");
        var host = new GameObject("UpperRailGridTest");
        owned.Add(viewer);
        owned.Add(host);
        viewer.transform.position = new Vector3(95f, 4f, 64f);
        var grid = host.AddComponent<CityUpperTransit>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CityUpperTransit).GetField("viewer", flags).SetValue(grid, viewer.transform);
        typeof(CityUpperTransit).GetMethod("Start", flags).Invoke(grid, null);
        var original = new Dictionary<Transform, Vector3>();
        foreach (Transform block in host.transform) original.Add(block, block.position);
        viewer.transform.position += Vector3.right * 2f;
        typeof(CityUpperTransit).GetMethod("LateUpdate", flags).Invoke(grid, null);
        Assert.That(host.transform.childCount, Is.EqualTo(9));
        foreach (var pair in original)
        {
            if (pair.Key.position == pair.Value) continue;
            Bounds local = RendererBounds(pair.Key.gameObject);
            Bounds before = new Bounds(pair.Value + local.center, local.size);
            Bounds after = new Bounds(pair.Key.position + local.center, local.size);
            Assert.That(Vector3.Distance(before.ClosestPoint(viewer.transform.position), viewer.transform.position), Is.GreaterThan(185f));
            Assert.That(Vector3.Distance(after.ClosestPoint(viewer.transform.position), viewer.transform.position), Is.GreaterThan(185f));
        }
    }

    [TestCase("TrackSegment", TrackSegmentType.Straight)]
    [TestCase("TurnSegment_Left", TrackSegmentType.TurnLeft)]
    [TestCase("TurnSegment_Right", TrackSegmentType.TurnRight)]
    public void CityDecorationPreservesEveryRoadColliderAndLane(string name, TrackSegmentType type)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject segment = Object.Instantiate(prefab);
        owned.Add(segment);
        // The full suite may leave the gameplay scene open. Keep its start
        // deck out of these ownership-sensitive raycasts without disabling it.
        segment.transform.position = new Vector3(31000f, 0f, -27000f);
        segment.SetActive(true);
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
        data.segmentType = type;
        data.routeDistance = 40f;
        Collider[] colliders = segment.GetComponentsInChildren<Collider>(true);
        Assert.That(colliders.Length, Is.GreaterThan(0));
        var snapshots = new Dictionary<Collider, string>();
        foreach (Collider collider in colliders) snapshots.Add(collider, CollisionState(collider));
        CityV7PlayableEnvironment.Decorate(segment, type);
        CityV7PlayableEnvironment.RefreshClearance();
        Assert.That(segment.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(segment.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders.Length));
        foreach (var pair in snapshots)
            Assert.That(CollisionState(pair.Key), Is.EqualTo(pair.Value), pair.Key.name);
        Physics.SyncTransforms();
        Assert.That(TrackGeometryStandards.LaneSpacing, Is.EqualTo(3f));
        for (int lane = 0; lane < 3; lane++)
        {
            float lateral = TrackGeometryStandards.GetLaneCenter(lane);
            foreach (float distance in new[] { 1f, 4f, 8f })
                AssertRoadUnder(segment, new Vector3(lateral, 4f, distance));
            if (type == TrackSegmentType.Straight) continue;
            int direction = type == TrackSegmentType.TurnRight ? 1 : -1;
            foreach (float distance in new[] { 2f, 5f, 8f })
                AssertRoadUnder(segment, new Vector3(direction * distance, 4f, 10f - direction * lateral));
        }
    }

    private static void AssertRoadUnder(GameObject segment, Vector3 localPoint)
    {
        Vector3 origin = segment.transform.TransformPoint(localPoint);
        Assert.That(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, 1 << 8,
            QueryTriggerInteraction.Ignore), Is.True, "Missing road at lane sample " + localPoint);
        Assert.That(hit.collider.transform.IsChildOf(segment.transform), Is.True,
            "An unrelated scene collider " + hit.collider.name + " hid a missing road at " + localPoint);
    }

    private static string CollisionState(Collider collider)
    {
        return EditorJsonUtility.ToJson(collider) + "|" + collider.transform.localToWorldMatrix
            + "|active=" + collider.gameObject.activeInHierarchy;
    }

    private GameObject LoadInstance(string resource)
    {
        GameObject prefab = Resources.Load<GameObject>("CityV7/" + resource);
        Assert.That(prefab, Is.Not.Null, "Missing authored city resource " + resource);
        GameObject instance = Object.Instantiate(prefab);
        instance.SetActive(true);
        owned.Add(instance);
        return instance;
    }

    private static void AssertAssetBindingsAndBudget(GameObject root, int staticRendererLimit)
    {
        int staticRenderers = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!UnderTransitLoop(renderer.transform)) staticRenderers++;
            Assert.That(renderer.sharedMaterials, Is.Not.Empty, renderer.name);
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null, renderer.name);
                Assert.That(material.shader, Is.Not.Null, material.name);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name);
            }
        }
        Assert.That(staticRenderers, Is.InRange(1, staticRendererLimit), root.name);
        int vertices = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
            vertices += filter.sharedMesh.vertexCount;
            Assert.That(filter.sharedMesh.subMeshCount, Is.GreaterThan(0), filter.name);
        }
        Assert.That(vertices, Is.GreaterThan(1000), "Missing city geometry: " + root.name);
    }

    private static bool UnderTransitLoop(Transform node)
    {
        Transform rendererNode = node;
        while (node != null)
        {
            foreach (MonoBehaviour component in node.GetComponents<MonoBehaviour>())
            {
                if (component == null || component.GetType().Name != "CityTransitLoop") continue;
                var vehicles = component.GetType().GetField("vehicles").GetValue(component) as Transform[];
                if (vehicles == null) continue;
                foreach (Transform vehicle in vehicles)
                    if (vehicle != null && rendererNode.IsChildOf(vehicle)) return true;
            }
            node = node.parent;
        }
        return false;
    }

    private static int SampleTransit(GameObject root, float seconds)
    {
        int sampled = 0;
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component.GetType().Name != "CityTransitLoop") continue;
            MethodInfo method = component.GetType().GetMethod("Sample", new[] { typeof(float) });
            Assert.That(method, Is.Not.Null, "Traffic needs deterministic presentation sampling.");
            method.Invoke(component, new object[] { seconds });
            sampled++;
        }
        return sampled;
    }

    private static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers, Is.Not.Empty);
        Bounds result = new Bounds();
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(x == 0 ? local.min.x : local.max.x,
                    y == 0 ? local.min.y : local.max.y, z == 0 ? local.min.z : local.max.z));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }
}
