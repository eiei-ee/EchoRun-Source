using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Authoring evidence, not a gameplay or release entry point.
public static class LayeredMemoryRunnerReview
{
    public const string Output = "TestResults/LayeredMemoryRunnerForm-20260924";
    public const string ScenePath = "Assets/Scenes/SampleScene.scene";

    public static void CaptureBefore() => Capture("Before");
    public static void CaptureAfter() => Capture("After");

    public static void BuildWindows()
    {
        StackedCityReview.BuildInto(Output, "EchoRun-LayeredMemory-RunnerFormReview");
        Debug.Log("LAYERED_MEMORY_RUNNER_BUILD_OK");
    }

    private static void Capture(string phase)
    {
        string folder = Output + "/" + phase;
        Directory.CreateDirectory(folder);
        ShaderUtil.allowAsyncCompilation = false;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        GameObject player = GameObject.Find("player");
        if (player == null) throw new InvalidOperationException("Scene runner missing.");
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != player) root.SetActive(false);
        player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
        Transform model = player.transform.Find("CharacterModel");
        Animator animator = model.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        var camera = new GameObject("RunnerReviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .22f);
        camera.fieldOfView = 28f;
        var key = new GameObject("RunnerReviewKey").AddComponent<Light>();
        key.type = LightType.Directional;
        var fill = new GameObject("RunnerReviewFill").AddComponent<Light>();
        fill.type = LightType.Directional;
        CityV7PlayableEnvironment.ApplyAtmosphere(key, fill);
        RenderSettings.fog = false;
        var report = new List<string>
        {
            "Art review of the actual scene binding; sampled clips are not runtime action acceptance.",
            "avatar=" + AssetDatabase.GetAssetPath(animator.avatar),
            "controller=" + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
            "modelScale=" + model.localScale,
            "rootMotion=" + animator.applyRootMotion
        };
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            renderer.forceMatrixRecalculationPerRender = true;
            long triangles = 0;
            for (int sub = 0; sub < renderer.sharedMesh.subMeshCount; sub++)
                triangles += renderer.sharedMesh.GetIndexCount(sub) / 3;
            report.Add(renderer.name + " mesh=" + AssetDatabase.GetAssetPath(renderer.sharedMesh)
                + " vertices=" + renderer.sharedMesh.vertexCount + " triangles=" + triangles
                + " materialSlots=" + renderer.sharedMaterials.Length + " bounds=" + renderer.sharedMesh.bounds);
        }
        File.WriteAllLines(folder + "/binding.txt", report);
        CaptureView(camera, folder, "front", new Vector3(0f, 1.05f, 4.3f));
        CaptureView(camera, folder, "back", new Vector3(0f, 1.05f, -4.3f));
        CaptureView(camera, folder, "three-quarter", new Vector3(3f, 1.15f, -3.2f));
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("Expected the existing authored controller.");
        var motionReport = new List<string>();
        foreach (string action in new[] { "Run", "Jump", "Slide" })
        {
            AnimationClip clip = controller.layers[0].stateMachine.states
                .First(s => s.state.name.Equals(action, StringComparison.OrdinalIgnoreCase)).state.motion as AnimationClip;
            if (clip == null) throw new InvalidOperationException("Missing existing motion: " + action);
            foreach (float normalized in new[] { .15f, .45f, .75f })
            {
                animator.Play(action, 0, normalized);
                animator.Update(0f);
                string name = action.ToLowerInvariant() + "-" + Mathf.RoundToInt(normalized * 100f);
                motionReport.Add(name + " leftLeg=" + animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation
                    + " state=" + animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                Bounds pose = VisiblePoseBounds(model);
                motionReport.Add(name + " bakedWorldBounds=" + pose);
                CaptureView(camera, folder, name + "-rear", pose.center + new Vector3(2.8f, .25f, -4.6f), pose.center);
                CaptureView(camera, folder, name + "-side", pose.center + new Vector3(5.4f, .25f, 0f), pose.center);
            }
        }
        File.WriteAllLines(folder + "/motion-samples.txt", motionReport);
        Debug.Log("LAYERED_MEMORY_RUNNER_CAPTURE_OK " + phase);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static Bounds VisiblePoseBounds(Transform model)
    {
        var baked = new Mesh();
        Bounds result = default;
        bool first = true;
        try
        {
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                skin.BakeMesh(baked);
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 point = skin.transform.TransformPoint(vertex);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }
        finally { Object.DestroyImmediate(baked); }
    }

    private static void CaptureView(Camera camera, string folder, string name, Vector3 position, Vector3? lookAt = null)
    {
        camera.transform.position = position;
        camera.transform.LookAt(lookAt ?? new Vector3(0f, .94f, 0f));
        var target = new RenderTexture(720, 960, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(720, 960, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 720, 960), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(folder + "/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
