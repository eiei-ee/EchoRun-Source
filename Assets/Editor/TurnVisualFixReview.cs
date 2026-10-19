using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TurnVisualFixReview
{
    const string Output="TestResults/TurnVisualFix";
    public static void Capture()
    {
        ShaderUtil.allowAsyncCompilation=false;
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
        var player=GameObject.Find("player");
        if(player==null)throw new InvalidOperationException("Player missing");
        foreach(var root in scene.GetRootGameObjects())if(root!=player)root.SetActive(false);
        player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var camera=new GameObject("ReviewCamera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.fieldOfView=32;camera.allowHDR=false;
        var key=new GameObject("ReviewKey").AddComponent<Light>();key.type=LightType.Directional;
        var fill=new GameObject("ReviewFill").AddComponent<Light>();fill.type=LightType.Directional;
        fill.transform.rotation=Quaternion.Euler(38,145,0);
        CityV7PlayableEnvironment.ApplyAtmosphere(key,fill);RenderSettings.fog=false;
        var report=new StringBuilder();
        var renderers=player.GetComponentsInChildren<Renderer>();
        var originalMaterials=new Material[renderers.Length][];
        for(int i=0;i<renderers.Length;i++)originalMaterials[i]=renderers[i].sharedMaterials;
        var baselineShader=ShaderUtil.CreateShaderAsset(File.ReadAllText(Output+"/original.shader").Replace("EchoRun/ExoGrayBlueTech","Hidden/RunnerBeforeTurnFix"));
        Bounds playerBounds=renderers[0].bounds;
        foreach(var renderer in renderers)playerBounds.Encapsulate(renderer.bounds);
        float lookHeight=playerBounds.center.y;
        var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
        camera.targetTexture=target;
        for(int pass=0;pass<2;pass++)
        {
            float minimum=float.MaxValue,maximum=0;
            for(int rendererIndex=0;rendererIndex<renderers.Length;rendererIndex++)
            {
                var renderer=renderers[rendererIndex];
                var materials=(Material[])originalMaterials[rendererIndex].Clone();
                for(int slot=0;slot<materials.Length;slot++)
                {
                    if(pass==0)report.AppendLine("ALL "+renderer.name+" submeshes="+(renderer is SkinnedMeshRenderer skin?skin.sharedMesh.subMeshCount:0)+" -> "+(materials[slot]==null?"null":materials[slot].name+" / "+materials[slot].shader.name+" / "+AssetDatabase.GetAssetPath(materials[slot])));
                    if(materials[slot]==null||materials[slot].shader.name!="EchoRun/ExoGrayBlueTech")continue;
                    if(pass==0){materials[slot]=new Material(materials[slot]);materials[slot].shader=baselineShader;}
                    if(pass==0)report.AppendLine(renderer.name+" -> "+materials[slot].name+" / "+materials[slot].shader.name);
                }
                renderer.sharedMaterials=materials;
            }
            for(int angle=0;angle<360;angle+=90)
            {
                var rotation=Quaternion.Euler(0,angle,0);player.transform.rotation=rotation;
                camera.transform.position=rotation*new Vector3(0,lookHeight+.35f,-7f);
                camera.transform.LookAt(new Vector3(0,lookHeight,0));
                camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);
                texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();
                string label=(pass==0?"before":"after")+"-"+angle;
                File.WriteAllBytes(Output+"/"+label+".png",texture.EncodeToPNG());
                float sum=0;int count=0;
                foreach(var pixel in texture.GetPixels())if(pixel.a>.5f){sum+=pixel.grayscale;count++;}
                float mean=sum/Mathf.Max(1,count);minimum=Mathf.Min(minimum,mean);maximum=Mathf.Max(maximum,mean);
                report.AppendLine(label+" mean="+mean+" pixels="+count);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            report.AppendLine((pass==0?"before":"after")+" brightness ratio="+maximum/Mathf.Max(.001f,minimum));
            if(pass==1&&maximum/Mathf.Max(.001f,minimum)>1.5f)
                throw new InvalidOperationException("Runner orientation brightness regression: "+report);
        }
        File.WriteAllText(Output+"/orientation-report.txt",report.ToString());
        player.SetActive(false);
        camera.fieldOfView=65;
        camera.transform.position=new Vector3(0,9,-15);
        camera.transform.LookAt(new Vector3(7,2,10));
        foreach(string side in new[]{"Left","Right"})
        {
            var turn=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_"+side+".prefab"));
            turn.SetActive(true);turn.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            for(int pass=0;pass<2;pass++)
            {
                if(pass==1)CityV7PlayableEnvironment.Decorate(turn,side=="Left"?TrackSegmentType.TurnLeft:TrackSegmentType.TurnRight);
                camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);
                texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/turn-"+side+"-"+(pass==0?"before":"after")+".png",texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            UnityEngine.Object.DestroyImmediate(turn);
        }
        RenderTexture.active=null;camera.targetTexture=null;target.Release();
        Debug.Log("TURN_VISUAL_CAPTURE_OK\n"+report);
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output+"/Windows");
        var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.scene"},Output+"/Windows/EchoRun.exe",
            BuildTarget.StandaloneWindows64,BuildOptions.Development|BuildOptions.CompressWithLz4HC);
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Turn visual fix build failed");
        Debug.Log("TURN_VISUAL_BUILD_OK");
    }
}
