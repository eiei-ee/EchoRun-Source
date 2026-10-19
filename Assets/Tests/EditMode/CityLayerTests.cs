using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public class CityLayerTests
{
    [TestCase("Chunk0")][TestCase("Chunk1")][TestCase("Chunk2")]
    [TestCase("Chunk3")][TestCase("Chunk4")][TestCase("Chunk5")]
    [TestCase("Chunk6")][TestCase("Chunk7")][TestCase("Chunk8")]
    [TestCase("LowerBlock0")][TestCase("LowerBlock1")][TestCase("LowerBlock2")][TestCase("LowerBlock3")]
    [TestCase("CornerQuarterLeft")][TestCase("CornerQuarterRight")]
    public void ExposedCityFacesDoNotHaveCompetingCoplanarMaterials(string resource)
    {
        var type=System.Type.GetType("CitySurfaceReview, TempleRun.Editor",true);
        var report=new List<string>();
        int conflicts=(int)type.GetMethod("AuditPrefab").Invoke(null,new object[]{"CityV7/"+resource,report});
        Assert.That(conflicts,Is.Zero,string.Join("\n",report));
    }
    [Test]
    public void LowerCityKeepsNearbyBlocksStationaryAcrossRecycling()
    {
        var camera=new GameObject("GridTestCamera");var root=new GameObject("GridTest");
        try
        {
            camera.transform.position=new Vector3(95,4,10);
            var grid=root.AddComponent<CityLowerDistrict>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CityLowerDistrict).GetField("viewer",flags).SetValue(grid,camera.transform);
            typeof(CityLowerDistrict).GetMethod("Start",flags).Invoke(grid,null);
            var nearby=new Dictionary<Transform,Vector3>();
            foreach(Transform child in root.transform)if(Vector3.Distance(child.position,camera.transform.position)<180)nearby.Add(child,child.position);
            camera.transform.position+=Vector3.right*2;
            typeof(CityLowerDistrict).GetMethod("LateUpdate",flags).Invoke(grid,null);
            Assert.That(root.transform.childCount,Is.EqualTo(25));
            foreach(var pair in nearby)Assert.That(pair.Key.position,Is.EqualTo(pair.Value),"Visible rooftop moved with camera");
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(camera);}
    }
    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
    public void LowerCityStaysBelowGameplayAndHasNoCollision(int variant)
    {
        var prefab=Resources.Load<GameObject>("CityV7/LowerBlock"+variant);
        Assert.That(prefab,Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
        int vertices=0;
        foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            Assert.That(filter.sharedMesh.bounds.max.y,Is.LessThan(-3f),filter.name);
            vertices+=filter.sharedMesh.vertexCount;
            var material=filter.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);
        }
        Assert.That(vertices,Is.GreaterThan(1000));
        Assert.That(prefab.GetComponentsInChildren<Renderer>().Length,Is.LessThanOrEqualTo(18));
    }
    [TestCase("TrackSegment")][TestCase("TurnSegment_Left")][TestCase("TurnSegment_Right")]
    public void BridgeIsVisualOnlyAndBelowTrack(string name)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");
        var bridge=prefab.transform.Find("CityBridgeStructure");Assert.That(bridge,Is.Not.Null);
        Assert.That(bridge.GetComponentsInChildren<Collider>(true),Is.Empty);
        bool deck=false;
        foreach(var filter in bridge.GetComponentsInChildren<MeshFilter>())
        {
            var b=filter.sharedMesh.bounds;
            Assert.That(b.max.y,Is.LessThan(-.1f),filter.name);
            if(b.size.x>=8&&b.size.z>=8)deck=true;
        }
        Assert.That(deck,Is.True,"A support pillar alone does not provide a bridge deck");
    }
    [Test]
    public void FacadeBakingDoesNotLeaveTemporaryColliders()
    {
        for(int i=0;i<9;i++)
        {
            var prefab=Resources.Load<GameObject>("CityV7/Chunk"+i);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty,"Chunk"+i);
        }
    }
}
