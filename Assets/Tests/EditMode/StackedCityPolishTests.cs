using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StackedCityPolishTests
{
    [TestCase("V7_SteppedTower")]
    [TestCase("V7_SideGallery")]
    [TestCase("V7_SkylineMid")]
    public void PrimaryWhiteBuildingsContainPersistentFacadeDetails(string buildingName)
    {
        int found = 0;
        for (int i = 0; i < 9; i++)
        {
            var prefab = Resources.Load<GameObject>("CityV7/Chunk" + i);
            var building = prefab.transform.Find(buildingName);
            if (building == null) continue;
            found++;
            var detail = building.Find("StackedArchitecturalDetail");
            Assert.That(detail, Is.Not.Null, buildingName + " still has an unprocessed primary facade.");
            var meshes = detail.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(meshes, Is.Not.Empty, buildingName);
            foreach (var filter in meshes)
            {
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, "Temporary detail mesh: " + filter.name);
                Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(3));
            }
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        Assert.That(found, Is.GreaterThan(0), "No primary building binding found: " + buildingName);
    }

    [Test]
    public void BakedCladdingRetainsTheTangentFramesNeededForSurfaceRelief()
    {
        int checkedMeshes = 0;
        var resources = Enumerable.Range(0,4).Select(i => "StackedBlock"+i)
            .Concat(Enumerable.Range(0,9).Select(i => "Chunk"+i)).Concat(new[]{"UpperTransit"});
        foreach (string resource in resources)
        {
            var prefab = Resources.Load<GameObject>("CityV7/" + resource);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (!renderer.sharedMaterials.Any(m => m.shader.name == "EchoRun/MineralCladding")) continue;
                var tangents = filter.sharedMesh.tangents;
                Assert.That(tangents.Length, Is.EqualTo(filter.sharedMesh.vertexCount), filter.name);
                Assert.That(tangents.All(t => new Vector3(t.x,t.y,t.z).sqrMagnitude > .5f),
                    Is.True, "Zero tangent hides authored normal relief: " + filter.name);
                checkedMeshes++;
            }
        }
        Assert.That(checkedMeshes, Is.GreaterThan(0));
    }

    [Test]
    public void SkyReflectionContainsLitFacesAndRoughnessMips()
    {
        var reflection = Resources.Load<Cubemap>("CityV7/StackedCityReflection");
        Assert.That(reflection, Is.Not.Null);
        Assert.That(reflection.mipmapCount, Is.GreaterThanOrEqualTo(7));
        for (int face = 0; face < 6; face++)
        {
            Color[] sharp = reflection.GetPixels((CubemapFace)face,0);
            Color[] rough = reflection.GetPixels((CubemapFace)face,reflection.mipmapCount-1);
            Assert.That(sharp.Average(c => c.grayscale), Is.GreaterThan(.005f), "Black reflection face " + face);
            Assert.That(rough.All(c => !float.IsNaN(c.r) && !float.IsInfinity(c.r)), Is.True);
        }
    }
}
