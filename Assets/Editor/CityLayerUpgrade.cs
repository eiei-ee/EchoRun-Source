using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class CityLayerUpgrade
{
    const string Art="Assets/Art/CityLayers/";
    const string City="Assets/CityAfterimageV7/Materials/";
    const string Output="TestResults/CityLayers";
    static Mesh cube;
    static Material concrete,street,roof,glass,warm,trim;

    public static void InstallAndReview()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Art);AssetDatabase.Refresh();
        if(!File.Exists(Output+"/before-1-1.png"))Capture("before");
        var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
        concrete=Material("Concrete",new Color(.44f,.46f,.43f));
        street=Material("LowerStreets",new Color(.12f,.16f,.18f));
        roof=Material("RoofMetal",new Color(.25f,.34f,.35f));
        trim=Material("OxideTrim",new Color(.40f,.28f,.20f));
        glass=AssetDatabase.LoadAssetAtPath<Material>(City+"CityV7_Glass.mat");
        warm=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/ExperienceSlice/WarmWindow.mat");
        UpgradeFacades();
        CitySurfaceRepair.RepairChunks();
        BuildLowerBlocks();
        foreach(string segment in new[]{"TrackSegment","TurnSegment_Left","TurnSegment_Right"})BuildBridge(segment);
        LowerHorizonGround();
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Capture("after");
        Debug.Log("CITY_LAYERS_INSTALLED_AND_CAPTURED");
    }

    static Material Material(string name,Color tint)
    {
        string path=Art+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=tint;mat.SetFloat("_Glossiness",.22f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        go.AddComponent<MeshFilter>().sharedMesh=cube;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>(true);Bounds b=renderers[0].bounds;
        foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static void Remove(Transform root,string name)
    {var child=root.Find(name);if(child!=null)UnityEngine.Object.DestroyImmediate(child.gameObject);}

    static void BuildLowerBlocks()
    {
        var sources=new List<GameObject>();
        foreach(int index in new[]{0,1,2,4,7})
        foreach(Transform child in Resources.Load<GameObject>("CityV7/Chunk"+index).transform)
            if(child.name!="V7_GroundedTerrace"&&child.GetComponentsInChildren<MeshFilter>().Length>0)sources.Add(child.gameObject);
        for(int variant=0;variant<4;variant++)
        {
            var root=new GameObject("LowerBlock"+variant);
            Box(root.transform,"StreetBed",new Vector3(0,-27,0),new Vector3(96,.3f,96),street);
            for(int x=0;x<2;x++)for(int z=0;z<2;z++)
            {
                int index=x+z*2+variant;
                Vector3 center=new Vector3(x==0?-24:24,-26.8f,z==0?-24:24);
                Box(root.transform,"CityBlockPaving",center,new Vector3(36,.4f,36),concrete);
                var building=UnityEngine.Object.Instantiate(sources[index%sources.Count],root.transform);
                building.name="LowerAuthoredBuilding_"+index;building.SetActive(true);building.transform.localPosition=Vector3.zero;
                // Keep the FBX's up-axis conversion; only add a world-up yaw.
                building.transform.localRotation=Quaternion.Euler(0,((index+variant)%4)*90,0)*building.transform.localRotation;
                Bounds b=BoundsOf(building);
                float scale=Mathf.Min(31f/Mathf.Max(b.size.x,b.size.z),(13f+(index%3)*3)/Mathf.Max(1,b.size.y));
                building.transform.localScale*=scale;b=BoundsOf(building);
                building.transform.position=center+Vector3.up*.2f-new Vector3(b.center.x,b.min.y,b.center.z);
                if(index%3==0)foreach(var r in building.GetComponentsInChildren<Renderer>())
                    if(r.name.Contains("Scale"))r.sharedMaterial=trim;
                foreach(var collider in building.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                // Existing modeled roof equipment provides a scale cue from above.
                var equipment=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(WorldStyler.SideEnergyStationResourcePath),root.transform);
                equipment.name="RoofEquipment";equipment.transform.localPosition=Vector3.zero;
                Bounds e=BoundsOf(equipment);equipment.transform.localScale*=3f/Mathf.Max(e.size.x,e.size.z);e=BoundsOf(equipment);
                b=BoundsOf(building);equipment.transform.position=new Vector3(b.center.x,b.max.y,b.center.z)-new Vector3(e.center.x,e.min.y,e.center.z);
                foreach(var c in equipment.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var r in equipment.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>roof).ToArray();
            }
            // Restrained lane dashes and sidewalks make the lower level read as streets.
            for(int i=-4;i<=4;i++)
            {
                Box(root.transform,"StreetDash",new Vector3(i*10,-26.82f,0),new Vector3(4,.02f,.18f),concrete);
                Box(root.transform,"StreetDash",new Vector3(0,-26.82f,i*10),new Vector3(.18f,.02f,4),concrete);
            }
            Combine(root,"LowerBlock"+variant,false);
            CitySurfaceRepair.RepairLowerBlock(root);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/CityV7/LowerBlock"+variant+".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void UpgradeFacades()
    {
        var frame=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/ExperienceSlice/WindowFrame.mat");
        for(int i=0;i<9;i++)
        {
            string path="Assets/Resources/CityV7/Chunk"+i+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                CitySurfaceRepair.RestoreAuthoredMeshes(root);
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter==null)continue;
                    bool terrace=filter.name=="V7_GroundedTerrace";
                    if(!terrace&&!filter.name.EndsWith("__CityV7_Pale"))continue;
                    if(terrace)
                    {
                        var scale=filter.transform.localScale;scale.y=27f;filter.transform.localScale=scale;
                        var position=filter.transform.localPosition;position.y=.05f-scale.y*.5f;filter.transform.localPosition=position;
                        filter.GetComponent<Renderer>().sharedMaterial=concrete;
                    }
                    Remove(filter.transform,"CityLayerFacade");
                    var detail=new GameObject("CityLayerFacade");detail.transform.SetParent(filter.transform,false);
                    string name=filter.name;
                    filter.name="LayerFacade_"+i+"_"+name;
                    InstallExperienceSlice.BakeFacadeWindows(filter,detail.transform,frame,warm,true);
                    filter.name=name;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var pale=AssetDatabase.LoadAssetAtPath<Material>(City+"CityV7_Pale.mat");pale.color=new Color(.84f,.83f,.76f);EditorUtility.SetDirty(pale);
        glass.color=new Color(.13f,.28f,.30f);EditorUtility.SetDirty(glass);
    }

    static void BuildBridge(string name)
    {
        string path="Assets/Prefabs/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Remove(root.transform,"CityBridgeStructure");
            var pier=root.transform.Find("CityDeckPier");if(pier!=null)pier.gameObject.SetActive(false);
            var group=new GameObject("CityBridgeStructure");group.transform.SetParent(root.transform,false);
            foreach(var collider in root.GetComponentsInChildren<BoxCollider>())
            {
                Bounds b=collider.bounds;
                if(b.size.x<2||b.size.z<2||b.size.y>3||b.center.y>1)continue;
                Vector3 center=root.transform.InverseTransformPoint(b.center);
                Box(group.transform,"BridgeDeck",new Vector3(center.x,-.85f,center.z),new Vector3(b.size.x,1.35f,b.size.z),concrete);
                bool alongZ=b.size.z>=b.size.x;
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 beamPosition=new Vector3(center.x,-1.45f,center.z);
                    if(alongZ)beamPosition.x+=side*(b.extents.x-.3f);else beamPosition.z+=side*(b.extents.z-.3f);
                    Box(group.transform,"EdgeGirder",beamPosition,alongZ?new Vector3(.4f,.75f,b.size.z):new Vector3(b.size.x,.75f,.4f),roof);
                }
            }
            Vector3 support=name=="TrackSegment"?new Vector3(0,-14.4f,0):new Vector3(0,-14.4f,10);
            Box(group.transform,"PierCrosshead",support+Vector3.up*12.2f,new Vector3(9,1.2f,2.2f),roof);
            foreach(int side in new[]{-1,1})Box(group.transform,"Pier",support+Vector3.right*side*3.8f,new Vector3(1.5f,25.6f,2),concrete);
            Combine(group,name+"Bridge",true);
            CitySurfaceRepair.RepairBridgeStructure(group, name);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    static void LowerHorizonGround()
    {
        string path="Assets/Resources/CityV7/ExperienceSkyline.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ground=root.transform.Find("LowerCityGround");
            if(ground!=null){var p=ground.localPosition;p.y=-27.3f;ground.localPosition=p;ground.GetComponent<Renderer>().sharedMaterial=street;}
            var silhouette=root.transform.Find("DistantCitySilhouette");
            if(silhouette!=null)
            {
                var material=Material("HorizonHaze",new Color(.30f,.38f,.46f));material.shader=Shader.Find("EchoRun/DistantCity");
                silhouette.GetComponent<Renderer>().sharedMaterial=material;EditorUtility.SetDirty(material);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    static void Combine(GameObject root,string prefix,bool shadows)
    {
        var batches=new Dictionary<Material,List<CombineInstance>>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<Renderer>();if(renderer==null||!renderer.enabled)continue;
            var materials=renderer.sharedMaterials;
            for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
            {
                var material=materials[Mathf.Min(sub,materials.Length-1)];if(material==null)continue;
                if(!batches.ContainsKey(material))batches[material]=new List<CombineInstance>();
                batches[material].Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
        }
        var meshes=new List<(Mesh,Material)>();int index=0;
        foreach(var batch in batches)
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();
            string path=Art+prefix+"_"+index+++".asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(asset==null){AssetDatabase.CreateAsset(mesh,path);asset=mesh;}else{EditorUtility.CopySerialized(mesh,asset);UnityEngine.Object.DestroyImmediate(mesh);}
            meshes.Add((asset,batch.Key));
        }
        while(root.transform.childCount>0)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
        foreach(var item in meshes)
        {
            var go=new GameObject(item.Item2.name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=item.Item1;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=item.Item2;renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
        }
    }

    public static void Capture(string label)
    {
        ShaderUtil.allowAsyncCompilation=false;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
        foreach(var root in scene.GetRootGameObjects())root.SetActive(false);
        var camera=new GameObject("CityReviewCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=56;camera.farClipPlane=420;
        RenderSettings.skybox=Resources.Load<Material>("CityV7/ExperienceSky");camera.clearFlags=CameraClearFlags.Skybox;
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.shadows=LightShadows.Soft;
        var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(38,145,0);
        CityV7PlayableEnvironment.ApplyAtmosphere(key,fill);
        UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CityV7/ExperienceSkyline"));
        if(Resources.Load<GameObject>("CityV7/LowerBlock0")!=null)
            for(int x=-2;x<=2;x++)for(int z=-1;z<=3;z++)UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CityV7/LowerBlock"+((x+z+8)%4)),new Vector3((x+.5f)*96,0,(z+.5f)*96),Quaternion.identity);
        foreach(int direction in new[]{-1,1})
        {
            var route=new GameObject("ReviewRoute");
            for(int i=0;i<4;i++)Spawn("TrackSegment",new Vector3(0,0,i*20),Quaternion.identity,i*20,TrackSegmentType.Straight,route.transform);
            Spawn(direction<0?"TurnSegment_Left":"TurnSegment_Right",new Vector3(0,0,70),Quaternion.identity,80,direction<0?TrackSegmentType.TurnLeft:TrackSegmentType.TurnRight,route.transform);
            for(int i=1;i<=4;i++)Spawn("TrackSegment",new Vector3(direction*i*20,0,80),Quaternion.Euler(0,direction*90,0),80+i*20,TrackSegmentType.Straight,route.transform);
            CityV7PlayableEnvironment.RefreshClearance();
            foreach(int view in new[]{0,1,2})
            {
                if(view==0){camera.transform.position=new Vector3(0,3.85f,25);camera.transform.LookAt(new Vector3(0,1.25f,45));}
                if(view==1){camera.transform.position=new Vector3(0,3.85f,66);camera.transform.LookAt(new Vector3(direction*7,1,84));}
                if(view==2){camera.transform.position=new Vector3(direction*12,3.85f,80);camera.transform.LookAt(new Vector3(direction*34,1.25f,80));}
                var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+label+"-"+direction+"-"+view+".png",texture.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(route);
        }
    }
    static void Spawn(string name,Vector3 position,Quaternion rotation,float distance,TrackSegmentType type,Transform parent)
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab"),position,rotation,parent);go.SetActive(true);
        var data=go.GetComponent<TrackSegmentData>();data.segmentType=type;data.routeDistance=distance;
        CityV7PlayableEnvironment.Decorate(go,type);
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output+"/Windows");
        var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.scene"},Output+"/Windows/EchoRun.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development|BuildOptions.CompressWithLz4HC);
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("City layer build failed");
        Debug.Log("CITY_LAYERS_BUILD_OK");
    }
}
