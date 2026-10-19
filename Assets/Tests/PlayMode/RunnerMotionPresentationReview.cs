using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class RuntimeSmokeTests
{
    // Review the real scene, input, physics and animation together. Close views
    // supplement the unchanged gameplay camera; they are not gameplay framing.
    [UnityTest]
    public IEnumerator RunnerMotionPresentationReview()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        GameManager game = null;
        Camera detail = null;
        RunnerMotionLateCapture lateCapture = null;
        RunnerMotionReviewInput input = null;
        float previousCaptureDeltaTime = Time.captureDeltaTime;
        string phase = Environment.GetEnvironmentVariable("ECHORUN_MOTION_REVIEW") ?? "Before";
        string output = Path.GetFullPath("TestResults/RunnerMotion-20260926/" + phase);
        Directory.CreateDirectory(output);
        var report = new List<string>();
        var frameReport = new List<string>();
        try
        {
            // Rendering three views can be slow. Advance simulation by exactly
            // one 60 Hz step per frame, independently of that wall-clock cost.
            Time.captureDeltaTime = 1f / 60f;
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            yield return new WaitForSeconds(.8f);
            game = GameManager.Instance;
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            Animator animator = player.characterModel.GetComponent<Animator>();
            detail = new GameObject("RunnerMotionDetailCamera").AddComponent<Camera>();
            detail.CopyFrom(Camera.main);
            detail.enabled = false;
            detail.fieldOfView = 34f;
            lateCapture = new GameObject("RunnerMotionLateCapture")
                .AddComponent<RunnerMotionLateCapture>();
            input = new GameObject("RunnerMotionEarlyInput").AddComponent<RunnerMotionReviewInput>();
            lateCapture.FrameObserved = () => frameReport.Add(
                RunnerMotionFrameDiagnostics("frame", player, animator));
            yield return QueueRunnerReviewCapture(lateCapture, player, detail,
                output, "idle", report);
            game.StartGame();
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(GameState.Playing, game.State);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(.08f);
                yield return QueueRunnerReviewCapture(lateCapture, player, detail,
                    output, "run-" + i, report);
            }
            float runForwardOffset = RunnerBodyForwardOffset(player, animator);
            input.Request(SwipeDirection.Up);
            float timeout = Time.realtimeSinceStartup + 3f;
            while (!player.IsJumping && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(player.IsJumping, "Real jump input must be accepted.");
            foreach (float sample in new[] { .06f, .18f, .34f, .50f, .68f, .84f, .96f })
            {
                while (player.IsJumping && player.MotionSnapshot.Jump01 < sample) yield return null;
                yield return QueueRunnerReviewCapture(lateCapture, player, detail,
                    output, "jump-" + Mathf.RoundToInt(sample * 100f).ToString("D2"), report);
                Assert.That(Mathf.Abs(RunnerBodyForwardOffset(player, animator) - runForwardOffset),
                    Is.LessThan(.35f), "Jump must preserve the running body's forward origin.");
            }
            while (player.IsJumping) yield return null;
            for (int i = 0; i < 4; i++)
            {
                yield return QueueRunnerReviewCapture(lateCapture, player, detail,
                    output, "land-" + i, report);
                Assert.That(Mathf.Abs(RunnerBodyForwardOffset(player, animator) - runForwardOffset),
                    Is.LessThan(.35f), "Landing must preserve the running body's forward origin.");
                yield return new WaitForSeconds(.045f);
            }
            Assert.AreEqual(GameState.Playing, game.State);
            input.Request(SwipeDirection.Down);
            timeout = Time.realtimeSinceStartup + 3f;
            while (!player.IsSliding && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(player.IsSliding);
            yield return new WaitForSeconds(.3f);
            yield return QueueRunnerReviewCapture(lateCapture, player, detail,
                output, "slide-preserved", report);
            Assert.IsFalse(animator.applyRootMotion);
            File.WriteAllLines(Path.Combine(output, "capture.txt"), report);
            File.WriteAllLines(Path.Combine(output, "motion-frames.txt"), frameReport);
        }
        finally
        {
            Time.captureDeltaTime = previousCaptureDeltaTime;
            if (lateCapture != null)
            {
                lateCapture.Cancel();
                lateCapture.enabled = false;
                Object.Destroy(lateCapture.gameObject);
            }
            if (detail != null) Object.Destroy(detail.gameObject);
            if (input != null) Object.Destroy(input.gameObject);
            if (game != null) game.ReturnToMenu();
            RestoreSavePreferences(saved);
        }
    }

    private static IEnumerator QueueRunnerReviewCapture(
        RunnerMotionLateCapture lateCapture, PlayerController player, Camera detail,
        string folder, string label, List<string> report)
    {
        lateCapture.Queue(() => CaptureRunnerReview(player, detail, folder, label, report));
        while (lateCapture.HasPending) yield return null;
        if (lateCapture.Failure != null)
            throw new InvalidOperationException(
                "Runner review capture failed in LateUpdate: " + label,
                lateCapture.Failure);
    }

    private static string RunnerMotionFrameDiagnostics(
        string label, PlayerController player, Animator animator)
    {
        bool transitioning = animator.IsInTransition(0);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = transitioning
            ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
        AnimatorTransitionInfo transition = transitioning
            ? animator.GetAnimatorTransitionInfo(0) : default(AnimatorTransitionInfo);
        return label + " frame=" + Time.frameCount
            + " time=" + RunnerMotionNumber(Time.time)
            + " dt=" + RunnerMotionNumber(Time.deltaTime)
            + " jump01=" + RunnerMotionNumber(player.MotionSnapshot.Jump01)
            + " jumping=" + player.IsJumping + " sliding=" + player.IsSliding
            + " state=" + RunnerMotionStateName(current)
            + " stateNT=" + RunnerMotionNumber(current.normalizedTime)
            + " next=" + (transitioning ? RunnerMotionStateName(next) : "none")
            + " nextNT=" + RunnerMotionNumber(next.normalizedTime)
            + " transition=" + transitioning
            + " transitionNT=" + RunnerMotionNumber(transition.normalizedTime)
            + " transitionDuration=" + RunnerMotionNumber(transition.duration)
            + " animatorSpeed=" + RunnerMotionNumber(animator.speed)
            // World heights are deliberately not called ground clearance.
            // The runtime road plane has not been measured by this probe.
            + " rootY=" + RunnerMotionNumber(player.transform.position.y)
            + " modelY=" + RunnerMotionNumber(player.characterModel.position.y)
            + " hipsOffset=" + (animator.GetBoneTransform(HumanBodyBones.Hips).position - player.characterModel.position).ToString("F4")
            + " leftFootWorldY=" + RunnerMotionBoneWorldY(animator, HumanBodyBones.LeftFoot)
            + " rightFootWorldY=" + RunnerMotionBoneWorldY(animator, HumanBodyBones.RightFoot)
            + " leftToesWorldY=" + RunnerMotionBoneWorldY(animator, HumanBodyBones.LeftToes)
            + " rightToesWorldY=" + RunnerMotionBoneWorldY(animator, HumanBodyBones.RightToes);
    }

    private static float RunnerBodyForwardOffset(PlayerController player, Animator animator)
    {
        return Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.Hips).position
            - player.characterModel.position, player.ForwardDirection.normalized);
    }

    private static string RunnerMotionStateName(AnimatorStateInfo state)
    {
        foreach (string name in new[] { "Idle", "Run", "Jump", "Land", "Slide" })
            if (state.IsName(name)) return name;
        return "hash:" + state.shortNameHash;
    }

    private static string RunnerMotionBoneWorldY(Animator animator, HumanBodyBones bone)
    {
        Transform target = animator.GetBoneTransform(bone);
        return target != null ? RunnerMotionNumber(target.position.y) : "missing";
    }

    private static string RunnerMotionNumber(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void CaptureRunnerReview(PlayerController player, Camera detail,
        string folder, string label, List<string> report)
    {
        Vector3 origin = player.characterModel.position;
        Vector3 forward = player.ForwardDirection.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        CaptureRunnerCamera(Camera.main, Path.Combine(folder, label + "-game.png"), 540, 1080);
        Vector3 hips = player.characterModel.GetComponent<Animator>()
            .GetBoneTransform(HumanBodyBones.Hips).position;
        Vector3 detailOrigin = new Vector3(hips.x, origin.y, hips.z);
        detail.transform.position = detailOrigin - forward * 3.7f + Vector3.up * 1.75f;
        detail.transform.LookAt(detailOrigin + Vector3.up * .95f);
        CaptureRunnerCamera(detail, Path.Combine(folder, label + "-rear.png"), 600, 760);
        detail.transform.position = detailOrigin + right * 3.7f + Vector3.up * 1.25f;
        detail.transform.LookAt(detailOrigin + Vector3.up * .95f);
        CaptureRunnerCamera(detail, Path.Combine(folder, label + "-side.png"), 600, 760);
        report.Add(label + " jump01=" + player.MotionSnapshot.Jump01
            + " sliding=" + player.IsSliding + " root=" + player.transform.position
            + " model=" + origin + " scale=" + player.characterModel.localScale);
        report.Add(RunnerMotionFrameDiagnostics(label + "-pose", player,
            player.characterModel.GetComponent<Animator>()));
        report.Add(label + "-contact " + RunnerContactDiagnostics(player, out _));
    }

    private static string RunnerContactDiagnostics(PlayerController player, out float clearance)
    {
        clearance = float.NaN;
        SkinnedMeshRenderer clothing = player.characterModel.GetComponentsInChildren<SkinnedMeshRenderer>()
            .FirstOrDefault(skin => skin.sharedMesh != null && skin.sharedMesh.name.Contains("MemoryCourierClothing"));
        if (clothing != null)
        {
            Mesh source = clothing.sharedMesh;
            Vector3[] rest = source.vertices;
            int[] soles = source.GetTriangles(3).Distinct().Where(i => rest[i].y < .15f).ToArray();
            var baked = new Mesh();
            clothing.BakeMesh(baked);
            Vector3[] vertices = baked.vertices;
            float minSoleY = soles.Min(i => clothing.transform.TransformPoint(vertices[i]).y);
            RaycastHit[] hits = Physics.RaycastAll(player.transform.position + Vector3.up,
                Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit road = hits.Where(h => !h.transform.IsChildOf(player.transform)
                && h.point.y < player.transform.position.y).OrderByDescending(h => h.point.y).FirstOrDefault();
            clearance = road.collider != null ? minSoleY - road.point.y : float.NaN;
            string diagnostics = "soleY=" + RunnerMotionNumber(minSoleY)
                + " ground=" + (road.collider != null ? road.collider.name : "missing")
                + " groundY=" + RunnerMotionNumber(road.point.y)
                + " clearance=" + RunnerMotionNumber(clearance);
            Object.Destroy(baked);
            return diagnostics;
        }
        return "clothing=missing";
    }

    private static void CaptureRunnerCamera(Camera camera, string path, int width, int height)
    {
        RenderTexture before = camera.targetTexture;
        RenderTexture active = RenderTexture.active;
        float aspect = camera.aspect;
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = before;
            camera.aspect = aspect;
            RenderTexture.active = active;
            Object.Destroy(pixels);
            target.Release();
            Object.Destroy(target);
        }
    }
}

// The production CharacterAnimator runs at the default execution order. Capture
// only after its LateUpdate has applied the authored transition and pose layer.
[DefaultExecutionOrder(32000)]
public sealed class RunnerMotionLateCapture : MonoBehaviour
{
    private Action _pending;
    public Action FrameObserved { private get; set; }
    public Exception Failure { get; private set; }
    public bool HasPending => _pending != null;

    public void Queue(Action capture)
    {
        if (Failure != null)
            throw new InvalidOperationException("Runner frame probe already failed.", Failure);
        if (_pending != null)
            throw new InvalidOperationException("A runner capture is already pending.");
        if (capture == null) throw new ArgumentNullException(nameof(capture));
        _pending = capture;
    }

    public void Cancel()
    {
        _pending = null;
        FrameObserved = null;
    }

    private void LateUpdate()
    {
        if (Failure != null) return;
        try
        {
            FrameObserved?.Invoke();
            if (_pending == null) return;
            Action capture = _pending;
            capture();
            _pending = null;
        }
        catch (Exception exception)
        {
            Failure = exception;
            Cancel();
        }
    }
}
