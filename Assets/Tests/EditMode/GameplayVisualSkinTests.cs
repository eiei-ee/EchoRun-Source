using NUnit.Framework;
using UnityEngine;

public class GameplayVisualSkinTests
{
    private GameObject _stylerObject;
    private GameObject _subject;

    [TearDown]
    public void TearDown()
    {
        if (_subject != null) Object.DestroyImmediate(_subject);
        if (_stylerObject != null) Object.DestroyImmediate(_stylerObject);
    }

    [Test]
    public void CoinUsesOneCombinedRendererAndContractPropertyBlock()
    {
        WorldStyler styler = CreateStyler();
        _subject = new GameObject("CoinVisualTest");
        BoxCollider gameplayCollider = _subject.AddComponent<BoxCollider>();
        gameplayCollider.isTrigger = true;
        Coin coin = _subject.AddComponent<Coin>();

        styler.StyleCoin(_subject);
        coin.ConfigureEchoContractMarker(true);

        Transform visual = _subject.transform.Find("StreamlinedVisual");
        Assert.NotNull(visual);
        Assert.AreEqual(1,
            visual.GetComponentsInChildren<Renderer>(true).Length);
        MeshFilter filter = visual.GetComponent<MeshFilter>();
        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        Assert.NotNull(filter);
        Assert.NotNull(filter.sharedMesh);
        Assert.Greater(filter.sharedMesh.vertexCount, 100);
        Assert.NotNull(renderer.sharedMaterial);
        Assert.AreEqual("EchoRun/Collectible", renderer.sharedMaterial.shader.name);

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        Assert.AreEqual(1f, properties.GetFloat("_ContractMarker"));
        Assert.AreSame(gameplayCollider, _subject.GetComponent<BoxCollider>());
        Assert.IsTrue(gameplayCollider.isTrigger);
    }

    [TestCase(ObstacleType.Low)]
    [TestCase(ObstacleType.High)]
    [TestCase(ObstacleType.Barrier)]
    public void ObstacleSkinKeepsColliderAndQualityGate(ObstacleType type)
    {
        WorldStyler styler = CreateStyler();
        _subject = new GameObject("ObstacleVisualTest");
        BoxCollider gameplayCollider = _subject.AddComponent<BoxCollider>();
        gameplayCollider.isTrigger = true;
        _subject.AddComponent<Obstacle>().type = type;

        styler.StyleObstacle(_subject);
        Transform visual = _subject.transform.Find("StreamlinedVisual");
        Assert.NotNull(visual);
        EchoQualityGate gate = visual.GetComponent<EchoQualityGate>();
        Assert.NotNull(gate);
        Transform highOnly = visual.Find("HighQualityOnly");
        Assert.NotNull(highOnly);

        gate.ApplyQuality(VisualQuality.Low);
        Assert.IsFalse(highOnly.gameObject.activeSelf);
        gate.ApplyQuality(VisualQuality.High);
        Assert.IsTrue(highOnly.gameObject.activeSelf);
        Assert.AreSame(gameplayCollider, _subject.GetComponent<BoxCollider>());
        Assert.IsTrue(gameplayCollider.isTrigger);
    }

    private WorldStyler CreateStyler()
    {
        _stylerObject = new GameObject("WorldStyler_GameplaySkinTest");
        return _stylerObject.AddComponent<WorldStyler>();
    }
}
