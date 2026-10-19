using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CityV7IntegrationTests
{
    readonly List<GameObject> owned=new List<GameObject>();
    GameObject Straight(Vector3 position,Quaternion rotation,float distance)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrackSegment.prefab");
        var instance=Object.Instantiate(asset);owned.Add(instance);instance.SetActive(true);instance.transform.SetPositionAndRotation(position,rotation);
        var data=instance.GetComponent<TrackSegmentData>()??instance.AddComponent<TrackSegmentData>();data.segmentType=TrackSegmentType.Straight;data.routeDistance=distance;
        CityV7PlayableEnvironment.Decorate(instance,TrackSegmentType.Straight);return instance;
    }
    [TearDown] public void Cleanup(){foreach(var go in owned)if(go!=null)Object.DestroyImmediate(go);owned.Clear();}
    [Test] public void AuthoredStraightKeepsVisibleRoadWhenOldEnvironmentIsHidden()
    {
        var segment=Straight(Vector3.zero,Quaternion.identity,0);
        Assert.That(segment.transform.Find("EchoEnvironment").gameObject.activeSelf,Is.False);
        Assert.That(segment.transform.Find("GroundPlane").GetComponent<Renderer>().enabled,Is.True,"Collision-only ground is not a visible road");
        foreach(Transform child in segment.transform)if(child.name.StartsWith("LaneLine_"))Assert.That(child.GetComponent<Renderer>().enabled,Is.True,child.name);
    }
    [Test] public void ReusedCityChunkRestoresRoadEvenOnSameVariantFastPath()
    {
        var segment=Straight(Vector3.zero,Quaternion.identity,0);var renderer=segment.transform.Find("GroundPlane").GetComponent<Renderer>();renderer.enabled=false;
        segment.GetComponent<TrackSegmentData>().routeDistance=180;
        CityV7PlayableEnvironment.Decorate(segment,TrackSegmentType.Straight);Assert.That(renderer.enabled,Is.True);
    }
    [TestCase(TrackSegmentType.TurnLeft)]
    [TestCase(TrackSegmentType.TurnRight)]
    public void TurnDecorationRemovesLegacyCityWithoutChangingRoadOrColliders(TrackSegmentType type)
    {
        string side=type==TrackSegmentType.TurnLeft?"Left":"Right";
        var segment=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_"+side+".prefab"));
        owned.Add(segment);segment.SetActive(true);
        var colliders=segment.GetComponentsInChildren<Collider>(true);
        var states=new bool[colliders.Length];
        for(int i=0;i<colliders.Length;i++)states[i]=colliders[i].gameObject.activeInHierarchy&&colliders[i].enabled;
        for(int reuse=0;reuse<3;reuse++)
        {
            segment.SetActive(false);segment.SetActive(true);
            var variants=segment.GetComponentInChildren<EchoEnvironmentVariantSet>();
            variants?.SelectFor(42,reuse*180);
            CityV7PlayableEnvironment.Decorate(segment,type);
            foreach(var child in segment.GetComponentsInChildren<Transform>())
                Assert.That(child.name,Does.Not.StartWith("MegacityDistrict"));
            for(int i=0;i<colliders.Length;i++)
                Assert.That(colliders[i].gameObject.activeInHierarchy&&colliders[i].enabled,Is.EqualTo(states[i]),colliders[i].name);
        }
        if(type==TrackSegmentType.TurnLeft)
        {
            Assert.That(segment.GetComponentsInChildren<CityV7ChunkIdentity>(true).Length,Is.EqualTo(1));
            Assert.That(segment.transform.Find("CityV7Environment").GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
        }
    }
    [TestCase(-1,0)] [TestCase(-1,1)] [TestCase(-1,2)] [TestCase(-1,3)] [TestCase(-1,4)] [TestCase(-1,5)] [TestCase(-1,6)] [TestCase(-1,7)] [TestCase(-1,8)]
    [TestCase(1,0)] [TestCase(1,1)] [TestCase(1,2)] [TestCase(1,3)] [TestCase(1,4)] [TestCase(1,5)] [TestCase(1,6)] [TestCase(1,7)] [TestCase(1,8)]
    public void CityDoesNotOccludeCameraSweepAtTurn(int direction,int phase)
    {
        for(int i=0;i<3;i++)Straight(new Vector3(0,0,i*20),Quaternion.identity,(i+phase)*20);
        var turn=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_"+(direction<0?"Left":"Right")+".prefab"));owned.Add(turn);turn.SetActive(true);turn.transform.position=new Vector3(0,0,50);
        var data=turn.GetComponent<TrackSegmentData>();data.segmentType=direction<0?TrackSegmentType.TurnLeft:TrackSegmentType.TurnRight;data.routeDistance=60;data.turnPointWorld=new Vector3(0,0,60);
        CityV7PlayableEnvironment.Decorate(turn,data.segmentType);
        for(int i=1;i<=6;i++)Straight(new Vector3(direction*i*20,0,60),Quaternion.Euler(0,direction*90,0),60+(i+phase)*20);
        // The production spawn notification must protect already-existing city
        // pieces too, when a new adjacent segment completes a bend.
        typeof(CityV7PlayableEnvironment).GetMethod("RefreshClearance")?.Invoke(null,null);
        foreach(var identity in Object.FindObjectsOfType<CityV7ChunkIdentity>())
        foreach(var filter in identity.GetComponentsInChildren<MeshFilter>())
        {if(!filter.GetComponent<Renderer>().enabled)continue;filter.gameObject.layer=31;var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
        Physics.SyncTransforms();int hits=0;
        bool old=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        try
        {
            for(int lane=-1;lane<=1;lane++)for(int i=0;i<=36;i++)
            {
                float angle=direction*i*2.5f;var forward=Quaternion.Euler(0,angle,0)*Vector3.forward;var right=Vector3.Cross(Vector3.up,forward);
                var anchor=new Vector3(0,0,60)+right*lane*3;
                var camera=anchor-forward*6.83f+Vector3.up*3.91f;
                foreach(var offset in new[]{Vector3.zero,right*.4f,-right*.4f,Vector3.up*.3f})
                    if(Physics.Linecast(anchor+Vector3.up,camera+offset,1<<31,QueryTriggerInteraction.Ignore))hits++;
            }
            // Also advance through the exit arm. A stationary sweep around the
            // corner alone misses a building that blocks the road after turning.
            for(int lane=-1;lane<=1;lane++)
            {
                Vector3 velocity=Vector3.zero;
                Vector3 camera=new Vector3(lane*3,3.91f,60-20-6.83f);
                for(int tick=0;tick<=360;tick++)
                {
                    float distance=-20+tick/6f;
                    Vector3 forward=distance<=0?Vector3.forward:Vector3.right*direction;
                    Vector3 right=Vector3.Cross(Vector3.up,forward);
                    Vector3 anchor=new Vector3(0,0,60)+forward*distance+right*lane*3;
                    camera=Vector3.SmoothDamp(camera,anchor-forward*6.83f+Vector3.up*3.91f,ref velocity,1f/8,float.PositiveInfinity,1f/60);
                    if(Physics.Linecast(anchor+Vector3.up,camera,1<<31,QueryTriggerInteraction.Ignore))hits++;
                }
            }
        }
        finally{Physics.queriesHitBackfaces=old;}
        Assert.That(hits,Is.Zero,"City geometry intersects the three-lane turn camera sweep");
    }
}
