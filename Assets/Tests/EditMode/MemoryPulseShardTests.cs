using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MemoryPulseShardTests
{
    private const string VisualPrefabPath =
        "Assets/Resources/Art/Pickups/MemoryPulseShard_B.prefab";
    private const string BlenderModelPath =
        "Assets/Art/Pickups/MemoryPulseShard/Models/MemoryPulseShard_B.fbx";
    private const string CoinPrefabPath = "Assets/Prefabs/Coin.prefab";

    [Test]
    public void FormalShardUsesOneColliderFreeVertexColoredRenderer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            VisualPrefabPath);
        Assert.IsNotNull(prefab);
        Assert.AreEqual(0,
            prefab.GetComponentsInChildren<Collider>(true).Length);

        MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
        Assert.IsNotNull(filter);
        Assert.IsNotNull(filter.sharedMesh);
        Assert.IsNotNull(renderer);
        Assert.AreEqual(1, renderer.sharedMaterials.Length);
        Assert.AreEqual("EchoRun/Collectible", renderer.sharedMaterial.shader.name);

        GameObject authoredModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            BlenderModelPath);
        MeshFilter authored = authoredModel.GetComponentInChildren<MeshFilter>(true);
        Assert.IsNotNull(authored);
        Assert.AreSame(authored.sharedMesh, filter.sharedMesh,
            "Runtime prefab must bind the Blender-authored mesh.");
        Assert.AreEqual(1, filter.sharedMesh.subMeshCount);
        Assert.LessOrEqual(filter.sharedMesh.triangles.Length / 3, 4500);

        Vector3 size = TransformBounds(filter.sharedMesh.bounds,
            filter.transform.localToWorldMatrix).size;
        Assert.That(size.x, Is.InRange(0.85f, 1.05f));
        Assert.That(size.y, Is.InRange(0.95f, 1.12f));
        Assert.That(size.z, Is.InRange(0.12f, 0.22f));

        Color[] colors = filter.sharedMesh.colors;
        Assert.AreEqual(filter.sharedMesh.vertexCount, colors.Length);
        int frame = 0;
        int core = 0;
        int accent = 0;
        foreach (Color color in colors)
        {
            if (color.r > 0.9f) frame++;
            if (color.g > 0.9f) core++;
            if (color.b > 0.9f) accent++;
        }
        Assert.Greater(frame, 0);
        Assert.Greater(core, 0);
        Assert.Greater(accent, 0);

        Vector3[] vertices = filter.sharedMesh.vertices;
        int[] triangles = filter.sharedMesh.triangles;
        float totalArea = 0f;
        float accentArea = 0f;
        for (int index = 0; index < triangles.Length; index += 3)
        {
            int a = triangles[index];
            int b = triangles[index + 1];
            int c = triangles[index + 2];
            float area = Vector3.Cross(vertices[b] - vertices[a],
                vertices[c] - vertices[a]).magnitude * 0.5f;
            totalArea += area;
            if (colors[a].b > 0.9f) accentArea += area;
        }
        Assert.That(accentArea / totalArea, Is.LessThan(0.10f));
    }

    [Test]
    public void PickupMotionAndSpacingStayWithinMemoryPathBudget()
    {
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CoinPrefabPath);
        BoxCollider trigger = coinPrefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(trigger);
        Assert.IsTrue(trigger.isTrigger);
        Assert.AreEqual(new Vector3(1f, 1f, 0.4f), trigger.size);

        GameObject runtimeCoin = new GameObject("RuntimeCoinValidation");
        try
        {
            Coin coin = Coin.EnsureRuntimeContract(runtimeCoin);
            Assert.IsNotNull(coin);
            Assert.AreSame(coin, Coin.EnsureRuntimeContract(runtimeCoin),
                "Runtime repair must be idempotent for pooled pickups.");
            BoxCollider runtimeTrigger = runtimeCoin.GetComponent<BoxCollider>();
            Assert.IsNotNull(runtimeTrigger);
            Assert.IsTrue(runtimeTrigger.isTrigger);
            Assert.AreEqual(new Vector3(1f, 1f, 0.4f), runtimeTrigger.size);
            Assert.That(coin.rotateSpeed, Is.InRange(200f, 260f));
            Assert.That(coin.yawAmplitude, Is.InRange(8f, 14f));
            Assert.That(coin.bobHeight, Is.InRange(0.05f, 0.08f));
        }
        finally
        {
            Object.DestroyImmediate(runtimeCoin);
        }

        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(
            VisualPrefabPath);
        MeshFilter visualFilter = visual.GetComponentInChildren<MeshFilter>();
        float width = TransformBounds(visualFilter.sharedMesh.bounds,
            visualFilter.transform.localToWorldMatrix).size.x;
        Assert.GreaterOrEqual(TrackSpawnRules.CoinSpacing, width * 1.3f);
    }

    [Test]
    public void PickupVisualFacesViewerAcrossStraightAndTurnedRoutes()
    {
        Vector3 coinPosition = new Vector3(0f, 1f, 0f);
        Vector3[] viewerPositions =
        {
            new Vector3(0f, 3f, -8f),
            new Vector3(-8f, 3f, 0f),
            new Vector3(8f, 3f, 0f)
        };

        foreach (Vector3 viewerPosition in viewerPositions)
        {
            Vector3 expectedForward = coinPosition - viewerPosition;
            expectedForward.y = 0f;
            expectedForward.Normalize();

            Quaternion aligned = Coin.ResolveViewFacingRotation(
                coinPosition, viewerPosition, Quaternion.identity, 0f);
            Assert.Less(Vector3.Angle(
                aligned * Vector3.forward, expectedForward), 0.01f);

            Quaternion swayed = Coin.ResolveViewFacingRotation(
                coinPosition, viewerPosition, Quaternion.identity, 12f);
            Assert.That(Vector3.Angle(
                swayed * Vector3.forward, expectedForward),
                Is.InRange(11.9f, 12.1f));
        }
    }

    [Test]
    public void CollectibleMaterialKeepsWarmReadabilityWithoutBloom()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Resources/Materials/EchoCollectible.mat");
        Assert.IsNotNull(material);
        Assert.IsTrue(material.enableInstancing);
        Assert.IsTrue(material.HasProperty("_FrameEmissionStrength"));
        Assert.IsTrue(material.HasProperty("_AccentEmissionStrength"));
        Assert.That(material.GetFloat("_EmissionStrength"),
            Is.InRange(1.8f, 2.2f));
        Assert.That(material.GetFloat("_FrameEmissionStrength"),
            Is.InRange(0.55f, 0.8f));
        Assert.That(material.GetFloat("_AccentEmissionStrength"),
            Is.InRange(0.5f, 0.8f));

        Color core = material.GetColor("_CoreColor");
        Color frameHighlight = material.GetColor("_FrameHighlight");
        Color accent = material.GetColor("_AccentColor");
        Assert.Greater(core.r, core.g,
            "The collectible core must read as amber, not scene cyan.");
        Assert.Greater(frameHighlight.grayscale, 0.85f,
            "The outer frame needs a warm-white distance highlight.");
        Assert.Greater(accent.g, accent.r * 2f,
            "Cyan is reserved for the small data accent.");
    }

    private static Bounds TransformBounds(Bounds localBounds,
        Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = matrix.MultiplyVector(
            new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(
            new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(
            new Vector3(0f, 0f, extents.z));
        extents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, extents * 2f);
    }
}
