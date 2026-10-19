using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StackedCityContinuityTests
{
    readonly List<GameObject> owned = new List<GameObject>();

    [TearDown]
    public void Cleanup()
    {
        foreach (GameObject instance in owned)
            if (instance != null) Object.DestroyImmediate(instance);
        owned.Clear();
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void CornerQuarterHasFullHeightAndKeepsAllThreeLaneCorridorsClear(int direction)
    {
        GameObject quarter = LoadCity(direction < 0 ? "CornerQuarterLeft" : "CornerQuarterRight");
        Assert.That(quarter.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(quarter.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        Assert.That(BoundsIn(quarter.transform, quarter.transform).max.y, Is.GreaterThan(60f));
        Assert.That(BoundsIn(quarter.transform, quarter.transform).min.y, Is.LessThan(-90f));

        Transform[] towers = Children(quarter.transform, "CornerTower_");
        Transform[] foundations = Children(quarter.transform, "CornerFoundation_");
        Assert.That(towers.Length, Is.EqualTo(4));
        Assert.That(foundations.Length, Is.EqualTo(towers.Length));
        Assert.That(towers.Concat(foundations).Select(t => t.name).Distinct().Count(),
            Is.EqualTo(quarter.transform.childCount), "Duplicate authored footprint roots.");

        // The complete three-lane road and camera safety width, including the
        // approach, turn sweep and long exit arm; not just the centre lane.
        Bounds[] corridors = {
            new Bounds(new Vector3(0f, 3f, -10f), new Vector3(11.2f, 6.2f, 40f)),
            new Bounds(new Vector3(0f, 3f, 10f), new Vector3(28f, 6.2f, 28f)),
            new Bounds(new Vector3(direction * 40f, 3f, 10f), new Vector3(80f, 6.2f, 11.2f))
        };
        foreach (Transform footprint in quarter.transform)
        {
            Bounds bounds = BoundsIn(footprint, quarter.transform);
            foreach (Bounds corridor in corridors)
                Assert.That(bounds.Intersects(corridor), Is.False,
                    footprint.name + " enters the authored road/camera corridor.");
        }
        foreach (Transform foundation in foundations)
            Assert.That(BoundsIn(foundation, quarter.transform).max.y, Is.LessThan(-.1f));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)]
    public void EveryStreetChunkHasPersistentDeepFootingsBelowTheRoad(int index)
    {
        GameObject chunk = LoadCity("Chunk" + index);
        Transform[] terraces = chunk.transform.Cast<Transform>()
            .Where(t => t.name == "V7_GroundedTerrace").ToArray();
        Transform[] foundations = Children(chunk.transform, "StackedDeepFooting_");
        Assert.That(terraces.Length, Is.GreaterThan(0), chunk.name);
        Assert.That(foundations.Length, Is.EqualTo(terraces.Length));
        Assert.That(foundations.Select(t => t.name).Distinct().Count(), Is.EqualTo(foundations.Length));
        foreach (Transform foundation in foundations)
        {
            Assert.That(foundation.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(foundation.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Bounds bounds = BoundsIn(foundation, chunk.transform);
            Assert.That(bounds.max.y, Is.LessThan(-.1f), foundation.name);
            Assert.That(bounds.min.y, Is.LessThan(-90f), foundation.name);
            Assert.That(bounds.size.y, Is.GreaterThan(90f), "Foundation was vertically shortened.");
            Assert.That(terraces.Any(terrace =>
            {
                Bounds above = BoundsIn(terrace, chunk.transform);
                return Mathf.Abs(bounds.center.x - above.center.x) < .05f
                    && Mathf.Abs(bounds.center.z - above.center.z) < .05f
                    && Mathf.Abs(bounds.max.y - above.min.y) < .15f;
            }), Is.True, foundation.name + " must meet its building instead of floating beneath it.");
        }
    }

    [TestCase(TrackSegmentType.TurnLeft)]
    [TestCase(TrackSegmentType.TurnRight)]
    public void PooledTurnReusesOneCornerQuarterAndPreservesItsFootings(TrackSegmentType type)
    {
        string side = type == TrackSegmentType.TurnLeft ? "Left" : "Right";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/TurnSegment_" + side + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject turn = Object.Instantiate(prefab);
        owned.Add(turn);
        turn.SetActive(true);
        turn.GetComponent<TrackSegmentData>().segmentType = type;
        int colliderCount = turn.GetComponentsInChildren<Collider>(true).Length;
        Transform originalQuarter = null;
        for (int reuse = 0; reuse < 3; reuse++)
        {
            turn.SetActive(false);
            turn.SetActive(true);
            turn.GetComponentInChildren<EchoEnvironmentVariantSet>(true)?.SelectFor(1337, reuse * 180f);
            CityV7PlayableEnvironment.Decorate(turn, type);
            Transform quarter = turn.transform.Find("CityV7Environment");
            Assert.That(quarter, Is.Not.Null);
            if (reuse == 0) originalQuarter = quarter;
            else Assert.That(quarter, Is.SameAs(originalQuarter));
            Assert.That(turn.GetComponentsInChildren<CityV7ChunkIdentity>(true).Length, Is.EqualTo(1));
            Assert.That(Children(quarter, "CornerTower_").Length, Is.EqualTo(4));
            Transform[] foundations = Children(quarter, "CornerFoundation_");
            Assert.That(foundations.Length, Is.EqualTo(4));
            Assert.That(foundations.All(t => t.gameObject.activeInHierarchy), Is.True);
            Assert.That(turn.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));
        }
    }

    GameObject LoadCity(string name)
    {
        GameObject prefab = Resources.Load<GameObject>("CityV7/" + name);
        Assert.That(prefab, Is.Not.Null, "Install authored continuity resources first: " + name);
        GameObject instance = Object.Instantiate(prefab);
        owned.Add(instance);
        return instance;
    }

    static Transform[] Children(Transform root, string prefix)
    {
        return root.Cast<Transform>().Where(t => t.name.StartsWith(prefix,
            System.StringComparison.Ordinal)).ToArray();
    }

    static Bounds BoundsIn(Transform root, Transform basis)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers, Is.Not.Empty, root.name);
        Bounds result = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = basis.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(
                    x == 0 ? local.min.x : local.max.x,
                    y == 0 ? local.min.y : local.max.y,
                    z == 0 ? local.min.z : local.max.z));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }
}
