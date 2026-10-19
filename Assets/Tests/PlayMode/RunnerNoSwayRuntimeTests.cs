using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator PlayerAndEchoRunWithoutCyclicBodySway()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        GameManager game = null;
        RunnerMotionLateCapture capture = null;
        float previousStep = Time.captureDeltaTime;
        string output = System.Environment.GetEnvironmentVariable("ECHORUN_CAMERA_REVIEW")
            ?? "TestResults/RunnerNoSway-20261012/Runtime";
        Directory.CreateDirectory(output);
        var firstPositions = new Dictionary<Transform, Vector3>();
        var firstRotations = new Dictionary<Transform, Quaternion>();
        int samples = 0;
        float maximumSideTravel = 0f, maximumYaw = 0f, maximumRoll = 0f;
        try
        {
            Time.captureDeltaTime = 1f / 60f;
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            game = GameManager.Instance;
            Assert.IsTrue(game.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract,
                new SingleContractValidationConfig { enabled = true, useFixedIdentity = true,
                    fixedSeed = 1337, freezeDirector = true, disablePowerUps = true,
                    forceStandardDifficulty = true }));
            game.StartGame();
            yield return new WaitForSeconds(2f);
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            AIShadowRunner shadow = AIShadowRunner.Instance;
            GameObject echo = (GameObject)typeof(AIShadowRunner).GetField("_ghost",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shadow);
            Assert.IsNotNull(echo);
            CharacterAnimator echoMotion = echo.GetComponentInChildren<CharacterAnimator>();
            game.enabled = player.enabled = shadow.enabled = TrackManager.Instance.enabled = false;
            Rigidbody body = player.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            Animator[] animators = { player.characterModel.GetComponent<Animator>(),
                echo.GetComponentInChildren<Animator>() };
            var coreBones = new List<Transform>();
            foreach (Animator animator in animators)
            {
                Assert.IsTrue(animator.isHuman);
                foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine,
                    HumanBodyBones.Chest, HumanBodyBones.UpperChest })
                {
                    Transform found = animator.GetBoneTransform(bone);
                    if (found != null) coreBones.Add(found);
                }
            }
            capture = new GameObject("NoSwayLateCapture").AddComponent<RunnerMotionLateCapture>();
            // Freeze movement only. Keep real Humanoid evaluation and the
            // player's LateUpdate; drive the echo through its production API.
            capture.FrameObserved = () => echoMotion.ApplyExternalMotion(false, false,
                Vector3.forward, 10f, Time.deltaTime, 0f, 0f, 0f);
            for (int warm = 0; warm < 60; warm++) yield return null;
            for (int frame = 0; frame < 120; frame++)
            {
                capture.Queue(() =>
                {
                    foreach (Transform bone in coreBones)
                    {
                        if (!firstPositions.ContainsKey(bone))
                        {
                            firstPositions.Add(bone, bone.localPosition);
                            firstRotations.Add(bone, bone.localRotation);
                        }
                        float travel = Mathf.Abs(bone.localPosition.x - firstPositions[bone].x)
                            * bone.parent.TransformVector(Vector3.right).magnitude;
                        Quaternion relative = Quaternion.Inverse(firstRotations[bone]) * bone.localRotation;
                        float yaw = Mathf.Abs(Mathf.DeltaAngle(0f, relative.eulerAngles.y));
                        float roll = Mathf.Abs(Mathf.DeltaAngle(0f, relative.eulerAngles.z));
                        maximumSideTravel = Mathf.Max(maximumSideTravel, travel);
                        maximumYaw = Mathf.Max(maximumYaw, yaw);
                        maximumRoll = Mathf.Max(maximumRoll, roll);
                        Assert.Less(travel, .0001f, bone.name + " sideways travel");
                        Assert.Less(yaw, .05f, bone.name + " cyclic yaw");
                        Assert.Less(roll, .05f, bone.name + " cyclic roll");
                        samples++;
                    }
                });
                yield return null;
                if (capture.Failure != null) throw capture.Failure;
            }
            File.WriteAllText(Path.Combine(output, "no-sway-result.txt"),
                "playerAndEchoCoreSamples=" + samples + " maximumSideTravel=" + maximumSideTravel
                + " maximumYaw=" + maximumYaw + " maximumRoll=" + maximumRoll
                + "\nReal scene and animated rigs; controlled movement fixture.\n");
        }
        finally
        {
            Time.captureDeltaTime = previousStep;
            if (capture != null) { capture.Cancel(); Object.Destroy(capture.gameObject); }
            if (game != null) { game.enabled = true; game.ReturnToMenu(); }
            RestoreSavePreferences(saved);
        }
    }
}
