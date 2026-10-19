using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class RuntimeSmokeTests
{
    // A continuous sequence is required here: isolated apex screenshots hid
    // the arm hold/reversed timing in the rejected jump candidate.
    [UnityTest]
    public IEnumerator RunnerJumpContinuousReview()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        float previousStep = Time.captureDeltaTime;
        GameManager game = null;
        Camera detail = null;
        RunnerMotionLateCapture capture = null;
        RunnerMotionReviewInput input = null;
        AnimatorOverrideController candidate = null;
        string version = Environment.GetEnvironmentVariable("ECHORUN_JUMP_REVIEW") ?? "Rejected";
        string output = Path.GetFullPath("TestResults/RunnerJumpRevision-20260926/" + version);
        Directory.CreateDirectory(output);
        var report = new List<string>();
        int renderFrame = 0;
        int captured = 0;
        bool sawJump = false, sawLand = false;
        float minimumSoleClearance = float.PositiveInfinity;
        try
        {
            Time.captureDeltaTime = 1f / 60f;
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            yield return new WaitForSeconds(.2f);
            game = GameManager.Instance;
            PlayerController player = Object.FindObjectOfType<PlayerController>();
#if UNITY_EDITOR
            string jumpPath = Environment.GetEnvironmentVariable("ECHORUN_JUMP_CLIP");
            string landPath = Environment.GetEnvironmentVariable("ECHORUN_LAND_CLIP");
            if (!string.IsNullOrEmpty(jumpPath))
            {
                Animator animator = player.characterModel.GetComponent<Animator>();
                var baseController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                Assert.IsNotNull(baseController, "Candidate review requires the actual base controller.");
                var states = baseController.layers[0].stateMachine.states;
                var jumpKey = Array.Find(states, entry => entry.state.name == "Jump").state.motion as AnimationClip;
                var landKey = Array.Find(states, entry => entry.state.name == "Land").state.motion as AnimationClip;
                candidate = new AnimatorOverrideController(animator.runtimeAnimatorController);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                candidate.GetOverrides(pairs);
                int replaced = 0;
                for (int i = 0; i < pairs.Count; i++)
                {
                    string path = pairs[i].Key == jumpKey ? jumpPath
                        : pairs[i].Key == landKey ? landPath : null;
                    if (string.IsNullOrEmpty(path)) continue;
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    Assert.IsNotNull(clip, "Review candidate must exist: " + path);
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
                    replaced++;
                }
                Assert.AreEqual(string.IsNullOrEmpty(landPath) ? 1 : 2, replaced,
                    "Every requested candidate must replace an actual controller clip.");
                candidate.ApplyOverrides(pairs);
                animator.runtimeAnimatorController = candidate;
                animator.Rebind();
            }
#endif
            detail = new GameObject("JumpSequenceDetailCamera").AddComponent<Camera>();
            detail.CopyFrom(Camera.main);
            detail.enabled = false;
            detail.fieldOfView = 34f;
            capture = new GameObject("JumpSequenceLateCapture").AddComponent<RunnerMotionLateCapture>();
            input = new GameObject("JumpSequenceEarlyInput").AddComponent<RunnerMotionReviewInput>();
            game.StartGame();
            yield return null;
            capture.FrameObserved = () =>
            {
                Animator animator = player.characterModel.GetComponent<Animator>();
                sawJump |= player.IsJumping && animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
                sawLand |= animator.GetCurrentAnimatorStateInfo(0).IsName("Land")
                    || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Land"));
                // Include both entry/exit blends, but stop measuring after Run
                // fully takes over. Its existing gait is outside this review.
                bool reviewingAirMotion = player.IsJumping
                    || animator.GetCurrentAnimatorStateInfo(0).IsName("Jump")
                    || animator.GetCurrentAnimatorStateInfo(0).IsName("Land")
                    || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Land"));
                if (reviewingAirMotion)
                {
                    RunnerContactDiagnostics(player, out float clearance);
                    Assert.IsFalse(float.IsNaN(clearance), "Actual shoes and road must be measurable.");
                    minimumSoleClearance = Mathf.Min(minimumSoleClearance, clearance);
                }
                if ((renderFrame++ % 2) != 0) return;
                CaptureRunnerReview(player, detail, output, captured.ToString("D3"), report);
                captured++;
            };
            for (int frame = 0; frame < 132; frame++)
            {
                if (frame == 24)
                    input.Request(SwipeDirection.Up);
                yield return null;
                if (capture.Failure != null) throw capture.Failure;
            }
            capture.Cancel();
            report.Add("minimumSoleClearance=" + RunnerMotionNumber(minimumSoleClearance));
            // Retain diagnostics even when an assertion fails. A sequence that
            // never entered Jump must never be mistaken for jump evidence.
            File.WriteAllLines(Path.Combine(output, "capture.txt"), report);
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.That(captured, Is.InRange(65, 67));
            Assert.IsTrue(sawJump, "Real input must enter Jump during the captured sequence.");
            Assert.IsTrue(sawLand, "The captured sequence must include landing recovery.");
            Assert.That(minimumSoleClearance, Is.GreaterThanOrEqualTo(-.015f),
                "Actual skinned shoes must stay above the road through Jump, landing entry and recovery.");
            File.WriteAllLines(Path.Combine(output, "capture.txt"), report);
            File.WriteAllText(Path.Combine(output, "sequence.json"),
                "{\"frames\":" + captured + ",\"fps\":30,\"simulationFps\":60,\"jumpInputFrame\":12}");
        }
        finally
        {
            Time.captureDeltaTime = previousStep;
            if (capture != null) { capture.Cancel(); capture.enabled = false; Object.Destroy(capture.gameObject); }
            if (input != null) Object.Destroy(input.gameObject);
            if (detail != null) Object.Destroy(detail.gameObject);
            if (game != null) game.ReturnToMenu();
            if (candidate != null) Object.Destroy(candidate);
            RestoreSavePreferences(saved);
        }
    }
}

// Screenshot IO runs in LateUpdate and can exceed the real-time input lifetime.
// Queue on the next frame before PlayerController.Update, using that frame's
// actual clock; keep the production queue, ground check and expiry unchanged.
[DefaultExecutionOrder(-10000)]
public sealed class RunnerMotionReviewInput : MonoBehaviour
{
    private SwipeDirection pending;
    public void Request(SwipeDirection direction) { pending = direction; }
    private void Update()
    {
        if (pending == SwipeDirection.None || InputManager.Instance == null) return;
        InputManager.Instance.QueueSwipe(pending, InputIntentSource.Keyboard, Time.unscaledTime);
        pending = SwipeDirection.None;
    }
}
