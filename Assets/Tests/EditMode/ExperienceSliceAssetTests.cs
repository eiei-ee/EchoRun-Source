using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ExperienceSliceAssetTests
{
    [Test]
    public void DistantBackdropCannotCollideWithTheRoute()
    {
        var prefab = Resources.Load<GameObject>("CityV7/ExperienceSkyline");
        Assert.NotNull(prefab);
        Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true));
        Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true));
        Assert.IsEmpty(prefab.GetComponentsInChildren<TrackSegmentData>(true));
        Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(0));
    }

    [Test]
    public void NewFacadeMeshesHaveMaterialsAndNoGameplayComponents()
    {
        int details = 0;
        for (int i = 0; i < 9; i++)
        {
            var chunk = Resources.Load<GameObject>("CityV7/Chunk" + i);
            foreach (var transform in chunk.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != "AuthoredWindowDetail" && transform.name != "AuthoredFacadeWindows") continue;
                details++;
                Assert.IsEmpty(transform.GetComponentsInChildren<Collider>(true));
                foreach (var renderer in transform.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.NotNull(renderer.GetComponent<MeshFilter>().sharedMesh);
                    Assert.NotNull(renderer.sharedMaterial);
                    Assert.NotNull(renderer.sharedMaterial.shader);
                    Assert.IsFalse(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader));
                }
            }
        }
        Assert.That(details, Is.GreaterThan(0));
        Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("EchoRun/MineralCladding")));
    }
}
