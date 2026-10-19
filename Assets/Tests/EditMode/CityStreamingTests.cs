using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Exercise the production pool update without starting a run, loading a scene,
// or invoking GameManager.Awake (which initializes the player's save).
public sealed class CityStreamingTests
{
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    readonly List<GameObject> owned = new List<GameObject>();
    static readonly Vector3 IsolatedPosition = new Vector3(10000f, 0f, 10000f);

    [TearDown]
    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [TestCase(20f, 12)]
    [TestCase(20f, 16)]
    [TestCase(5f, 12)]
    [TestCase(50f, 12)]
    [TestCase(0f, 12)]
    [TestCase(-20f, 12)]
    public void GeometryIsPreparedBeyondTheFogWithRoomForTwoSegments(
        float segmentLength, int planningPoolSize)
    {
        float distance = TrackManager.GeometryLookaheadDistance(
            segmentLength, planningPoolSize);
        float safeLength = Mathf.Max(1f, segmentLength);
        Assert.That(distance, Is.GreaterThanOrEqualTo(
            CityV7PlayableEnvironment.FogEndDistance + safeLength * 2f),
            "City geometry must exist before it emerges from the fog.");
        Assert.That(distance, Is.GreaterThanOrEqualTo(
            TrackManager.ContentLookaheadDistance(segmentLength, planningPoolSize)),
            "Preparing a content segment must never precede its road shell.");
    }

    [Test]
    public void EarlierCityGeometryDoesNotExtendTheExistingContentWindow()
    {
        Assert.That(TrackManager.GeometryLookaheadDistance(20f),
            Is.GreaterThan(TrackManager.ContentLookaheadDistance(20f)));
        Assert.That(TrackManager.ContentLookaheadDistance(20f), Is.EqualTo(120f));
        Assert.That(TrackManager.ContentLookaheadDistance(20f, 16), Is.EqualTo(160f));
        Assert.That(TrackManager.ShouldPrepareSegmentContent(119.9f, 0f, 20f), Is.True);
        Assert.That(TrackManager.ShouldPrepareSegmentContent(120f, 0f, 20f), Is.False);
        Assert.That(TrackManager.ShouldPrepareSegmentContent(159.9f, 0f, 20f, 16), Is.True);
        Assert.That(TrackManager.ShouldPrepareSegmentContent(160f, 0f, 20f, 16), Is.False);
    }

    [Test]
    public void RecyclingTheOnlyBlockingRoadRestoresBuildingInTheSameUpdateWithoutSpawning()
    {
        GameManager previousManager = GameManager.Instance;
        AIShadowRunner previousShadow = AIShadowRunner.Instance;
        GameManager manager = null;
        try
        {
            // Inactive hosts prevent Awake/Start from installing runtime services
            // or touching saves. Only TrackManager.Update is explicitly invoked.
            manager = Create("Streaming test manager", false).AddComponent<GameManager>();
            SetStaticInstance(typeof(GameManager), manager);
            SetStaticInstance(typeof(AIShadowRunner), null);
            SetField(manager, "<State>k__BackingField", GameState.Playing);
            SetField(manager, "<Distance>k__BackingField", 101f);
            SetField(manager, "<ActiveGameplayFlowMode>k__BackingField",
                GameplayFlowMode.SixPhaseLegacy);

            TrackManager track = Create("Streaming test track", false).AddComponent<TrackManager>();
            track.useAITrackDirector = false;
            track.segmentLength = 20f;
            track.poolSize = 10;
            track.trackSegmentPrefab = Create("Unused shell prefab", false);
            SetField(track, "_player", Create("Streaming test player").transform);
            SetField(track, "_plannedDistance", 1000f);

            GameObject retiredRoad = Road("Road leaving the pool", IsolatedPosition, 0f);
            GameObject retainedRoad = Road("Road still in use",
                IsolatedPosition + Vector3.right * 200f, 100f);
            var active = (List<GameObject>)GetField(track, "_activeSegments");
            active.Add(retiredRoad);
            active.Add(retainedRoad);

            GameObject building = Building(IsolatedPosition + Vector3.up * 3f);
            CityV7PlayableEnvironment.RefreshClearance();
            Assert.That(building.activeSelf, Is.False,
                "The old road must actually block the building before recycling.");
            Assert.That(track.PlannedRouteDistance - manager.Distance,
                Is.GreaterThan(TrackManager.GeometryLookaheadDistance(20f)),
                "This fixture must reach the recycle-only branch, without a new spawn.");

            MethodInfo update = typeof(TrackManager).GetMethod("Update", InstanceFields);
            Assert.That(update, Is.Not.Null);
            update.Invoke(track, null);

            Assert.That(retiredRoad.activeSelf, Is.False);
            Assert.That(retainedRoad.activeSelf, Is.True);
            Assert.That(track.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(track.PlannedRouteDistance, Is.EqualTo(1000f),
                "A new segment must not conceal a missing recycle notification.");
            Assert.That(building.activeSelf, Is.True,
                "The building must not wait for the next 20 m spawn to reappear.");
        }
        finally
        {
            // Avoid an artificial Playing state surviving into object cleanup.
            if (manager != null) SetField(manager, "<State>k__BackingField", GameState.Menu);
            SetStaticInstance(typeof(GameManager), previousManager);
            SetStaticInstance(typeof(AIShadowRunner), previousShadow);
        }
    }

    [Test]
    public void StandaloneDecorationStillRefreshesClearanceImmediatelyByDefault()
    {
        GameObject road = Road("Standalone decoration road", IsolatedPosition, 0f);
        road.GetComponent<TrackSegmentData>().segmentType = TrackSegmentType.TurnLeft;
        GameObject building = Building(IsolatedPosition + Vector3.up * 3f);
        Assert.That(building.activeSelf, Is.True);

        // A bare turn isolates the clearance notification from expensive authored
        // decoration. Its route data still participates in the real corridor scan.
        CityV7PlayableEnvironment.Decorate(road, TrackSegmentType.TurnLeft);
        Assert.That(building.activeSelf, Is.False);

        road.transform.position += Vector3.right * 200f;
        CityV7PlayableEnvironment.Decorate(road, TrackSegmentType.TurnLeft);
        Assert.That(building.activeSelf, Is.True,
            "Editor previews and other direct callers retain immediate refresh.");
    }

    GameObject Create(string name, bool active = true)
    {
        var go = new GameObject(name);
        go.SetActive(active);
        owned.Add(go);
        return go;
    }

    GameObject Road(string name, Vector3 position, float distance)
    {
        GameObject road = Create(name);
        road.transform.position = position;
        TrackSegmentData data = road.AddComponent<TrackSegmentData>();
        data.segmentType = TrackSegmentType.Straight;
        data.routeDistance = distance;
        data.contentSpawned = true;
        return road;
    }

    GameObject Building(Vector3 position)
    {
        GameObject city = Create("Isolated streaming city");
        city.AddComponent<CityV7ChunkIdentity>();
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Building previously hidden by a retired road";
        building.transform.SetParent(city.transform, false);
        building.transform.position = position;
        building.transform.localScale = Vector3.one * 2f;
        Object.DestroyImmediate(building.GetComponent<Collider>());
        return building;
    }

    static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceFields);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(target);
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceFields);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    static void SetStaticInstance(System.Type type, object value)
    {
        FieldInfo field = type.GetField("<Instance>k__BackingField", StaticFields);
        Assert.That(field, Is.Not.Null, type.Name + ".Instance");
        field.SetValue(null, value);
    }
}
