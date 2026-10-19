using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StackedCityWallDetailTests
{
    [TestCase("WallDistrictA", 2.4f, 6.8f)]
    [TestCase("WallDistrictB", 2.4f, 6.8f)]
    [TestCase("WallTransit", 3.2f, 1.8f)]
    [TestCase("WallService", 1.4f, 1.6f)]
    [TestCase("WallMemory", 2.6f, 2.2f)]
    [TestCase("WallGround", 2.6f, 2.2f)]
    [TestCase("WallCat", .9f, 1f)]
    public void ImportedWallModuleFitsItsAuthoredMountAndGeometryBudget(string name, float width, float height)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/StackedCity/Models/" + name + ".fbx");
        Assert.That(asset, Is.Not.Null, "Missing formal wall module: " + name);
        var instance = Object.Instantiate(asset);
        try
        {
            instance.transform.position = Vector3.zero;
            Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(instance.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.InRange(1, 4), "Shared-material authoring budget: " + name);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.x, Is.EqualTo(width).Within(.12f), "Imported X footprint: " + name);
            Assert.That(bounds.size.y, Is.EqualTo(height).Within(.12f), "Imported +Y height: " + name);
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-.01f), "Rear mount must be at Z=0: " + name);
            Assert.That(bounds.max.z, Is.InRange(.01f, .30f), "Outward projection budget: " + name);
            Assert.That(Triangles(instance), Is.InRange(30L, 3000L), "One small wall prop must not become dense text geometry: " + name);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void PlayableChunksBindPersistentWallStoriesWithoutPhysicsOrRuntimeScripts()
    {
        int decoratedChunks = 0;
        for (int index = 0; index < 9; index++)
        {
            var chunk = Resources.Load<GameObject>("CityV7/Chunk" + index);
            Assert.That(chunk, Is.Not.Null);
            bool hasStories = false;
            foreach (Transform node in chunk.GetComponentsInChildren<Transform>(true))
            {
                if (node.name != "StackedWallStories") continue;
                hasStories = true;
                Assert.That(node.GetComponentsInChildren<Collider>(true), Is.Empty, node.parent.name);
                Assert.That(node.GetComponentsInChildren<Rigidbody>(true), Is.Empty, node.parent.name);
                Assert.That(node.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, node.parent.name);
                Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.InRange(1, 5), "Wall stories should be merged by shared material.");
                Assert.That(Triangles(node.gameObject), Is.InRange(30L, 15000L), node.parent.name);
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty, renderer.name);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, renderer.name);
                        Assert.That(AssetDatabase.Contains(material), Is.True, "Temporary story material: " + renderer.name);
                        Assert.That(material.shader, Is.Not.Null, material.name);
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name);
                    }
                }
                foreach (MeshFilter filter in node.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                    Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, "Unsaved story mesh: " + filter.name);
                }
            }
            if (hasStories) decoratedChunks++;
        }
        Assert.That(decoratedChunks, Is.GreaterThanOrEqualTo(2), "Wall stories must reach the playable route, not only source assets.");
    }

    [Test]
    public void InspectionAnchorsAreEmptyAndStrippedFromThePlayer()
    {
        int checkedAnchors = 0;
        for (int index = 0; index < 9; index++)
        {
            var chunk = Resources.Load<GameObject>("CityV7/Chunk" + index);
            Assert.That(chunk, Is.Not.Null);
            foreach (Transform node in chunk.GetComponentsInChildren<Transform>(true))
            {
                if (!node.name.StartsWith("Story_Wall", StringComparison.Ordinal)) continue;
                checkedAnchors++;
                Assert.That(node.CompareTag("EditorOnly"), Is.True, "Inspection markers must be stripped at build time.");
                Assert.That(node.GetComponents<Component>().Length, Is.EqualTo(1), "Inspection markers must remain empty transforms.");
                Assert.That(node.childCount, Is.Zero, "Gameplay art must not be parented under stripped inspection markers.");
            }
        }
        Assert.That(checkedAnchors, Is.GreaterThan(0));
    }

    private static long Triangles(GameObject root)
    {
        long triangles = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                triangles += filter.sharedMesh.GetIndexCount(submesh) / 3;
        }
        return triangles;
    }
}
