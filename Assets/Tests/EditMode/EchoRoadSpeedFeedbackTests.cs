using NUnit.Framework;
using UnityEngine;

public sealed class EchoRoadSpeedFeedbackTests
{
    private GameObject _controllerObject;
    private GameObject _roadObject;

    [TearDown]
    public void TearDown()
    {
        if (_roadObject != null) Object.DestroyImmediate(_roadObject);
        if (_controllerObject != null)
            Object.DestroyImmediate(_controllerObject);
    }

    [Test]
    public void FlowSpeedMappingIsContinuousClampedAndReturnsToBase()
    {
        Assert.AreEqual(EchoRoadVisualController.BaseFlowSpeed,
            EchoRoadVisualController.ResolveFlowSpeed(-1f), 0.0001f);
        Assert.AreEqual(0.20f,
            EchoRoadVisualController.ResolveFlowSpeed(0.5f), 0.0001f);
        Assert.AreEqual(0.32f,
            EchoRoadVisualController.ResolveFlowSpeed(1f), 0.0001f);
        Assert.AreEqual(0.32f,
            EchoRoadVisualController.ResolveFlowSpeed(2f), 0.0001f);
        Assert.IsFalse(EchoRoadVisualController.ShouldApplyFlowSpeed(
            0.20f, 0.201f));
        Assert.IsTrue(EchoRoadVisualController.ShouldApplyFlowSpeed(
            0.20f, 0.203f));
    }

    [Test]
    public void RepeatedSpeedFeedbackPreservesExistingPropertyBlockAndMaterial()
    {
        EchoRoadVisualController controller = CreateController();
        Renderer renderer = CreateRoadRenderer();
        controller.ApplyTo(renderer, RoadSurfaceRole.Main);
        Material sharedMaterial = renderer.sharedMaterial;

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetFloat("_RoadSafeLaneHint", 0.73f);
        properties.SetFloat("_FeedbackSentinel", 0.41f);
        renderer.SetPropertyBlock(properties);

        controller.SetSpeedFeedback(0.5f);
        controller.SetSpeedFeedback(0.5f);

        properties.Clear();
        renderer.GetPropertyBlock(properties);
        Assert.AreEqual(0.20f, properties.GetFloat("_FlowSpeed"), 0.0001f);
        Assert.AreEqual(0.73f,
            properties.GetFloat("_RoadSafeLaneHint"), 0.0001f,
            "Speed feedback must not erase the low-quality lane hint.");
        Assert.AreEqual(0.41f,
            properties.GetFloat("_FeedbackSentinel"), 0.0001f);
        Assert.AreSame(sharedMaterial, renderer.sharedMaterial,
            "Continuous feedback must keep the shared road material.");
    }

    [Test]
    public void NewlyBoundRoadUsesCurrentSpeedAndExplicitResetRestoresBase()
    {
        EchoRoadVisualController controller = CreateController();
        controller.SetSpeedFeedback(1f);
        Renderer renderer = CreateRoadRenderer();
        controller.ApplyTo(renderer, RoadSurfaceRole.Main);

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        Assert.AreEqual(0.32f,
            properties.GetFloat("_FlowSpeed"), 0.0001f);

        controller.ResetSpeedFeedback();
        properties.Clear();
        renderer.GetPropertyBlock(properties);
        Assert.AreEqual(EchoRoadVisualController.BaseFlowSpeed,
            properties.GetFloat("_FlowSpeed"), 0.0001f);
    }

    private EchoRoadVisualController CreateController()
    {
        _controllerObject = new GameObject("EchoRoadSpeedFeedback_Test");
        return _controllerObject.AddComponent<EchoRoadVisualController>();
    }

    private Renderer CreateRoadRenderer()
    {
        _roadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _roadObject.name = "GroundPlane";
        return _roadObject.GetComponent<Renderer>();
    }
}
