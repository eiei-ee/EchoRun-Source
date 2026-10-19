// Staging source: root copies this script into Assets/Editor before executing.
// Imports one privately staged whole-body Jump source and samples the existing character.
// No production controller, scene or gameplay data is saved. Root supplies FBX/metas/dependencies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RunnerBasicMotionReview
{
    public const string Source = "Assets/Animations/HumanMotion/KevinBasicPrivate/HumanM@Jump01.fbx";
    public const string Output = "TestResults/RunnerJumpRevision-20260927/BasicSource";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const int Width = 600, Height = 760;
    private static readonly string[] ClipNames = { "HumanM@Jump01" };
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [MenuItem("Tools/Echo Runner/Athletic Clips/Review Basic Whole Body Source Jump")]
    public static void InspectCandidates()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before reviewing source clips.");
        Require(File.Exists(Source), "Copy the private Jump FBX with original meta and Avatar/mask dependencies to " + Source + " first.");
        AnimationClip[] clips = ImportWholeJump();
        Directory.CreateDirectory(Output);
        var report = new List<string>
        {
            "Source=" + Source,
            "Original in-place source; preserve original RootY, heightFromFeet=false; no procedural limb edits.",
            "Actual scene character clone. One temporary state directly binds each source clip; Foot IK and root motion disabled.",
            "Full source phases=0,10,20,30,40,50,60,70,80,90,99; full body keeps one continuous source timeline.",
            "Camera follows hips XZ; fixed Y, distance and scale fitted to all sampled poses. Isolated source poses, not gameplay acceptance."
        };
        report.AddRange(clips.Select(c => c.name + " length=" + Number(c.length) + " human=" + c.isHumanMotion));
        WriteRootCurves(clips[0], report);

        var preview = new PreviewRenderUtility();
        AnimatorController controller = null;
        AnimatorStateMachine machine = null;
        AnimatorState state = null;
        Scene sourceScene = default(Scene);
        bool openedSourceScene = false;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            sourceScene = SceneManager.GetSceneByPath(ScenePath);
            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                sourceScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedSourceScene = true;
            }
            GameObject player = sourceScene.GetRootGameObjects().SingleOrDefault(g => g.name == "player");
            Require(player != null, "Actual scene player root missing.");
            Transform original = player.transform.Find("CharacterModel");
            Require(original != null, "Actual CharacterModel missing.");
            // Instantiate directly in the utility's preview scene. Read the
            // real scene only; never replace its Animator or move its objects.
            GameObject model = Object.Instantiate(original.gameObject);
            preview.AddSingleGO(model);
            model.name = "BasicActualRunnerReview";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = original.lossyScale;
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            if (openedSourceScene)
            {
                EditorSceneManager.CloseScene(sourceScene, true);
                openedSourceScene = false;
            }
            model.SetActive(true);

            Animator animator = model.GetComponent<Animator>();
            Require(animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                "Existing character must have its actual valid Humanoid Avatar.");
            controller = new AnimatorController { name = "BasicReviewOnly", hideFlags = HideFlags.HideAndDontSave };
            machine = new AnimatorStateMachine { name = "BasicReviewLayer", hideFlags = HideFlags.HideAndDontSave };
            state = new AnimatorState { name = "SourcePose", hideFlags = HideFlags.HideAndDontSave, iKOnFeet = false, writeDefaultValues = true };
            state.motion = clips[0];
            machine.states = new[] { new ChildAnimatorState { state = state, position = Vector3.zero } };
            machine.defaultState = state;
            controller.AddLayer(new AnimatorControllerLayer { name = "BasicReview", defaultWeight = 1f, stateMachine = machine });
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.13f, .16f, .20f);
            preview.camera.fieldOfView = 33f;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100f;
            preview.ambientColor = new Color(.42f, .44f, .48f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            preview.lights[1].intensity = .7f;
            preview.lights[1].transform.rotation = Quaternion.Euler(25f, 140f, 0f);

            float low = float.PositiveInfinity, high = float.NegativeInfinity, radius = 0f;
            foreach (AnimationClip clip in clips.Where(c => !c.name.EndsWith("_Loop", StringComparison.Ordinal)))
            {
                Bind(animator, state, clip);
                foreach (float phase in Phases(clip))
                {
                    Sample(animator, phase);
                    Bounds bounds = ActualBounds(model);
                    Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    low = Mathf.Min(low, bounds.min.y);
                    high = Mathf.Max(high, bounds.max.y);
                    radius = Mathf.Max(radius, Mathf.Abs(bounds.min.x - hips.x), Mathf.Abs(bounds.max.x - hips.x),
                        Mathf.Abs(bounds.min.z - hips.z), Mathf.Abs(bounds.max.z - hips.z));
                }
            }
            Require(high > low && radius > 0f, "Cannot frame actual skinned poses.");
            float centerY = (low + high) * .5f;
            float tangent = Mathf.Tan(preview.camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float distance = (Mathf.Max((high - low) * .5f / tangent, radius / (tangent * Width / Height)) + radius) * 1.12f;
            report.Add("Framing centerY=" + Number(centerY) + " distance=" + Number(distance) + " minY=" + Number(low) + " maxY=" + Number(high));
            foreach (AnimationClip clip in clips.Where(c => !c.name.EndsWith("_Loop", StringComparison.Ordinal)))
            {
                Bind(animator, state, clip);
                foreach (float phase in Phases(clip))
                {
                    Sample(animator, phase);
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 look = new Vector3(hips.position.x, centerY, hips.position.z);
                    string label = clip.name.Replace('|', '-') + "-" + Mathf.RoundToInt(phase * 100f).ToString("D2");
                    Capture(preview, look + new Vector3(0f, .10f, -distance), look, label + "-rear.png");
                    Capture(preview, look + new Vector3(distance, .10f, 0f), look, label + "-side.png");
                    report.Add(label + " sourceTime=" + Number(phase * clip.length)
                        + " stateNT=" + Number(animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
                        + " hips=" + Vector(hips.position)
                        + " leftFoot=" + Vector(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)
                        + " rightFoot=" + Vector(animator.GetBoneTransform(HumanBodyBones.RightFoot).position));
                }
            }
            File.WriteAllLines(Output + "/candidate-clips.txt", report);
            Debug.Log("BASIC_SOURCE_REVIEW_CAPTURED " + Output + " images=22");
        }
        finally
        {
            preview.Cleanup();
            if (openedSourceScene && sourceScene.IsValid()) EditorSceneManager.CloseScene(sourceScene, true);
            if (controller != null) Object.DestroyImmediate(controller);
            if (state != null) Object.DestroyImmediate(state);
            if (machine != null) Object.DestroyImmediate(machine);
            ShaderUtil.allowAsyncCompilation = previousAsync;
        }
    }

    private static AnimationClip[] ImportWholeJump()
    {
        AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
        Require(importer != null, "Source is not a ModelImporter.");
        // Preserve the author's complete humanDescription and original Avatar
        // reference. The root task imports the source model dependency first.
        Require(importer.animationType == ModelImporterAnimationType.Human,
            "Original source meta must configure Humanoid; do not auto-map a different rig.");
        Require(importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther,
            "Original source meta must retain Copy From Other Avatar.");
        Require(importer.sourceAvatar != null && importer.sourceAvatar.isValid && importer.sourceAvatar.isHuman,
            "Original HumanM_Model Avatar dependency missing or invalid.");
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.optimizeGameObjects = false;
        ModelImporterClipAnimation[] chosen = importer.clipAnimations;
        Require(chosen.Length == 1 && chosen[0].name == ClipNames[0]
            && chosen[0].takeName == ClipNames[0], "Expected one original complete HumanM@Jump01 clip/take.");
        foreach (ModelImporterClipAnimation clip in chosen)
        {
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
        }
        importer.clipAnimations = chosen;
        importer.SaveAndReimport();
        AnimationClip[] animations = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        Require(animations.Length == 1, "Expected only one non-preview imported source clip.");
        return ClipNames.Select(name => animations.Single(c => c.isHumanMotion && c.name == name)).ToArray();
    }

    private static void WriteRootCurves(AnimationClip clip, List<string> report)
    {
        var rootBindings = AnimationUtility.GetCurveBindings(clip).Where(b =>
            b.type == typeof(Animator) && (b.propertyName.StartsWith("RootT.", StringComparison.Ordinal)
                || b.propertyName.StartsWith("RootQ.", StringComparison.Ordinal))).ToArray();
        var curves = rootBindings.ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b));
        string[] properties = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        foreach (string name in properties)
            Require(curves.ContainsKey(name) && curves[name] != null && curves[name].length > 0,
                "Imported Humanoid source is missing " + name);
        var rows = new List<string> { "phase,seconds," + string.Join(",", properties) };
        foreach (float phase in Phases(clip))
        {
            float time = phase * clip.length;
            rows.Add(Number(phase) + "," + Number(time) + "," + string.Join(",",
                properties.Select(name => Number(curves[name].Evaluate(time)))));
        }
        File.WriteAllLines(Output + "/source-root-curves.csv", rows);
        foreach (string name in properties)
        {
            float[] values = Enumerable.Range(0, 101).Select(i => curves[name].Evaluate(clip.length * i / 100f)).ToArray();
            report.Add(name + " keys=" + curves[name].length + " min=" + Number(values.Min())
                + " max=" + Number(values.Max()) + " first=" + Number(values[0]) + " last=" + Number(values[100]));
        }
    }

    private static void Bind(Animator animator, AnimatorState state, AnimationClip clip)
    {
        state.motion = clip;
        animator.Rebind();
        animator.Update(0f);
        AnimatorClipInfo[] bound = animator.GetCurrentAnimatorClipInfo(0);
        Require(bound.Length == 1 && bound[0].clip == clip, "Temporary state did not bind the requested source clip: " + clip.name);
    }

    private static void Sample(Animator animator, float phase)
    {
        animator.Play(0, 0, phase);
        animator.Update(0f);
        Require(!animator.IsInTransition(0), "Source review must not blend states.");
        Require(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - phase) < .001f,
            "Temporary source state did not sample the requested normalized time.");
        Require(animator.GetBoneTransform(HumanBodyBones.Hips) != null, "Retargeted hips missing.");
    }

    private static float[] Phases(AnimationClip clip)
    {
        return new[] { 0f, .10f, .20f, .30f, .40f, .50f, .60f, .70f, .80f, .90f, .99f };
    }

    private static Bounds ActualBounds(GameObject model)
    {
        Bounds bounds = default(Bounds);
        bool found = false;
        var baked = new Mesh();
        try
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin == null)
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                    continue;
                }
                skin.BakeMesh(baked);
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 world = skin.transform.TransformPoint(vertex);
                    if (!found) { bounds = new Bounds(world, Vector3.zero); found = true; }
                    else bounds.Encapsulate(world);
                }
                baked.Clear();
            }
        }
        finally { Object.DestroyImmediate(baked); }
        Require(found, "No visible actual character geometry.");
        return bounds;
    }

    private static void Capture(PreviewRenderUtility preview, Vector3 position, Vector3 look, string filename)
    {
        preview.camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
        preview.BeginStaticPreview(new Rect(0f, 0f, Width, Height));
        preview.Render(true);
        Texture2D image = preview.EndStaticPreview();
        try { File.WriteAllBytes(Output + "/" + filename, image.EncodeToPNG()); }
        finally { Object.DestroyImmediate(image); }
    }

    private static string Number(float value) { return value.ToString("R", Invariant); }
    private static string Vector(Vector3 value) { return "(" + Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + ")"; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
