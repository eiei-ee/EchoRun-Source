using NUnit.Framework;
using UnityEngine;

public sealed class CityTransitLoopTests
{
    private GameObject _root;
    private CityTransitLoop _loop;
    private Transform _lead;
    private Transform _trailing;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("TransitTest");
        _loop = _root.AddComponent<CityTransitLoop>();
        _lead = new GameObject("Lead").transform;
        _lead.SetParent(_root.transform, false);
        _trailing = new GameObject("Trailing").transform;
        _trailing.SetParent(_root.transform, false);
        _loop.vehicles = new[] { _lead, _trailing };
        _loop.pathPoints = new[]
        {
            Vector3.zero, new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, 20f), new Vector3(0f, 0f, 20f)
        };
        _loop.speed = 5f;
        _loop.spacing = 4f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void UnequalSegmentsUseDistanceAndCloseWithoutTeleporting()
    {
        _loop.Sample(3f);
        AssertPosition(_lead.localPosition, new Vector3(10f, 0f, 5f));
        _loop.Sample(11.999f);
        Vector3 before = _lead.localPosition;
        Quaternion heading = _lead.localRotation;
        _loop.Sample(12.001f);
        Assert.That(Vector3.Distance(before, _lead.localPosition), Is.LessThan(0.011f));
        Assert.That(Quaternion.Angle(heading, _lead.localRotation), Is.LessThan(3f));
        _loop.Sample(12f);
        AssertPosition(_lead.localPosition, Vector3.zero);
    }

    [Test]
    public void SpacingAndPhaseStayLocalWhenCityBlockIsRecycled()
    {
        _loop.phaseOffset = 1f;
        _loop.Sample(3f);
        AssertPosition(_lead.localPosition, new Vector3(10f, 0f, 10f));
        AssertPosition(_trailing.localPosition, new Vector3(10f, 0f, 6f));
        _root.transform.SetPositionAndRotation(new Vector3(96f, -27f, 192f),
            Quaternion.Euler(0f, 90f, 0f));
        _loop.Advance(1f);
        AssertPosition(_lead.localPosition, new Vector3(10f, 0f, 15f));
        AssertPosition(_trailing.localPosition, new Vector3(10f, 0f, 11f));
    }

    [Test]
    public void ZeroDeltaAndReenablePreservePhaseWithoutAddingPhysics()
    {
        _loop.Sample(3f);
        Vector3 position = _lead.localPosition;
        Quaternion rotation = _lead.localRotation;
        _loop.Advance(0f);
        _root.SetActive(false);
        _root.SetActive(true);
        AssertPosition(_lead.localPosition, position);
        Assert.That(Quaternion.Angle(_lead.localRotation, rotation), Is.LessThan(0.001f));
        Assert.That(_root.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(_root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        _loop.Advance(1f);
        AssertPosition(_lead.localPosition, new Vector3(10f, 0f, 10f));
    }

    private static void AssertPosition(Vector3 actual, Vector3 expected)
    {
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
