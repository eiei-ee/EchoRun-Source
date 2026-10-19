using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LayeredMemoryArtTests
{
    [TestCase("LayeredTerraceQuarter")]
    [TestCase("LayeredGalleryQuarter")]
    public void AuthoredQuartersRemainPersistentRenderableVisualOnlyAssets(string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/LayeredMemory/Models/" + name + ".fbx");
        Assert.IsNotNull(model);
        Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
        Assert.IsEmpty(model.GetComponentsInChildren<MonoBehaviour>(true));
        long triangles = 0;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            Assert.IsTrue(AssetDatabase.Contains(mesh));
            Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length, filter.name);
            for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetIndexCount(sub) / 3;
        }
        Assert.That(triangles, Is.InRange(1000L, 22000L));
        Assert.LessOrEqual(model.GetComponentsInChildren<Renderer>(true).Length, 7);
    }

    [Test]
    public void AuthoredBridgeKeepsTheCameraClearAndRailsUnderTheMovingTrain()
    {
        var root = Object.Instantiate(Resources.Load<GameObject>("CityV7/UpperTransit"));
        try
        {
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            Transform structure = root.transform.Find("StaticRoot");
            CityTransitLoop loop = root.GetComponentInChildren<CityTransitLoop>(true);
            var colliders = new List<MeshCollider>();
            long triangles = 0;
            foreach (MeshFilter filter in structure.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                Assert.Greater(renderer.bounds.min.y, 8.4f, filter.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader));
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    triangles += filter.sharedMesh.GetIndexCount(sub) / 3;
                // Test-only collider to check the final baked surfaces, not a
                // parallel mathematical copy of the generator's rail profile.
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                colliders.Add(collider);
            }
            Assert.Less(triangles, 65000L);
            Assert.LessOrEqual(colliders.Count, 4);
            Physics.SyncTransforms();
            Vector3[] points = loop.pathPoints;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % points.Length];
                Vector3 right = Vector3.Cross(Vector3.up, (b - a).normalized);
                foreach (float side in new[] { -.85f, .85f })
                {
                    Vector3 wheel = Vector3.Lerp(a, b, .5f) + right * side;
                    bool supported = false;
                    var ray = new Ray(wheel + Vector3.up * .25f, Vector3.down);
                    foreach (MeshCollider collider in colliders)
                        if (collider.Raycast(ray, out RaycastHit hit, .30f)) supported = true;
                    Assert.IsTrue(supported, "Rail missing below wheel at chord " + i + " side " + side);
                }
            }
        }
        finally { Object.DestroyImmediate(root); }
    }
}
