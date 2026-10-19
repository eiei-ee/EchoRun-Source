using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BackpackFitReview
{
    const string ScenePath="Assets/Scenes/SampleScene.scene";
    const string Output="TestResults/BackpackFit";

    public static void ApplyAndReview()
    {
        Directory.CreateDirectory(Output);
        ShaderUtil.allowAsyncCompilation=false;
        Capture("before");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var model=GameObject.Find("player").transform.Find("CharacterModel");
        var spine=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="EchoMemorySpine");
        InstallEchoRunnerPhaseOne.FitMemorySpine(model,spine);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spine);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save backpack fit");
        Capture("after");
        Debug.Log("BACKPACK_FIT_REVIEW_OK");
    }

    static void Capture(string label)
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var player=GameObject.Find("player");
        var model=player.transform.Find("CharacterModel");
        var animator=model.GetComponent<Animator>();
        var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
        var report=new StringBuilder();
        foreach(var clip in clips)report.AppendLine(clip.name+" "+clip.length);
        foreach(var root in scene.GetRootGameObjects())if(root!=player)root.SetActive(false);
        var camera=new GameObject("BackpackReviewCamera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.32f,.39f,.46f);
        camera.fieldOfView=40;camera.allowHDR=false;
        var key=new GameObject("ReviewKey").AddComponent<Light>();key.type=LightType.Directional;
        var fill=new GameObject("ReviewFill").AddComponent<Light>();fill.type=LightType.Directional;
        fill.transform.rotation=Quaternion.Euler(38,145,0);
        CityV7PlayableEnvironment.ApplyAtmosphere(key,fill);RenderSettings.fog=false;
        var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;
        var selected=clips.Where(c=>c.name.IndexOf("run",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("slide",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
        if(selected.Length==0)throw new InvalidOperationException("No run/slide clips found: "+report);
        foreach(var clip in selected)
        foreach(float phase in new[]{.15f,.5f,.85f})
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(model.gameObject,clip,clip.length*phase);AnimationMode.EndSampling();
            foreach(int view in new[]{0,60})
            {
                Vector3 anchor=player.transform.position;
                camera.transform.position=anchor+Quaternion.Euler(0,view,0)*new Vector3(0,2.5f,-5f);
                camera.transform.LookAt(anchor+Vector3.up*1.1f);
                camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(768,768,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,768,768),0,0);texture.Apply();
                string name=string.Concat(clip.name.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
                File.WriteAllBytes(Output+"/"+label+"-"+name+"-"+(int)(phase*100)+"-"+view+".png",texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AnimationMode.StopAnimationMode();
        }
        RenderTexture.active=null;camera.targetTexture=null;rt.Release();
        File.WriteAllText(Output+"/"+label+"-clips.txt",report.ToString());
    }

    public static void Build()
    {
        Directory.CreateDirectory(Output+"/Windows");
        var result=BuildPipeline.BuildPlayer(new[]{ScenePath},Output+"/Windows/EchoRun.exe",BuildTarget.StandaloneWindows64,
            BuildOptions.Development|BuildOptions.CompressWithLz4HC);
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Backpack fit build failed");
        Debug.Log("BACKPACK_FIT_BUILD_OK");
    }
}
