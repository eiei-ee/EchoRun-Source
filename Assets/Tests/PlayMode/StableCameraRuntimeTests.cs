using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator StableCameraIgnoresRapidLaneReversalsAndEchoApproachingCorner()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        GameManager game = null;
        Camera camera = null;
        RenderTexture previousTarget = null, target = null;
        RunnerMotionLateCapture capture = null;
        float previousStep = Time.captureDeltaTime;
        string output = System.Environment.GetEnvironmentVariable("ECHORUN_CAMERA_REVIEW")
            ?? "TestResults/CameraStable-20261012/Runtime";
        Directory.CreateDirectory(output);
        int checkedFrames = 0;
        float maximumPositionChange = 0f, maximumAngleChange = 0f, maximumFovChange = 0f;
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
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            GameObject echo = (GameObject)typeof(AIShadowRunner).GetField("_ghost", flags).GetValue(shadow);
            Assert.IsNotNull(echo);
            game.enabled = player.enabled = shadow.enabled = TrackManager.Instance.enabled = false;
            Rigidbody body = player.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            Vector3 center = body.position - Vector3.right * player.RenderedLateralOffset;
            float echoY = echo.transform.position.y;
            camera = Camera.main;
            previousTarget = camera.targetTexture;
            CameraFollow follow = camera.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow);
            FieldInfo lateral = typeof(PlayerController).GetField("_laneOffset", flags);
            capture = new GameObject("StableCameraLateCapture").AddComponent<RunnerMotionLateCapture>();
            foreach (float aspect in new[] { .5f, 1.777778f })
            {
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(aspect < 1f ? 540 : 960, aspect < 1f ? 1080 : 540, 24);
                target.Create();
                camera.targetTexture = target;
                camera.aspect = aspect;
                camera.fieldOfView = WorldStyler.GetCameraFieldOfView(aspect < 1f);
                follow.offset = WorldStyler.GetCameraOffset(aspect < 1f);
                PlayerPrefs.SetInt(CameraViewSettings.PreferenceKey, (int)CameraViewHeight.Low);
                follow.ResetMotionFeedback();
                lateral.SetValue(player, 0f);
                body.position = player.transform.position = center;
                for (int settle = 0; settle < 90; settle++) yield return null;
                Vector3 stablePosition = camera.transform.position;
                Quaternion stableRotation = camera.transform.rotation;
                float stableFov = camera.fieldOfView;
                for (int frame = 0; frame < 240; frame++)
                {
                    float lane = Mathf.Sin(frame * Mathf.PI / 6f) * player.laneDistance;
                    lateral.SetValue(player, lane);
                    body.position = player.transform.position = center + Vector3.right * lane;
                    // First exercise opposite lanes. Then let the echo enter
                    // a perpendicular road before the player reaches the bend.
                    echo.transform.position = frame < 120
                        ? center - Vector3.right * lane + Vector3.forward * 3.2f
                        : center + Vector3.forward * 16f + Vector3.right * Mathf.Sin(frame * .12f) * 16f;
                    Vector3 ghostPosition = echo.transform.position;
                    ghostPosition.y = echoY;
                    echo.transform.position = ghostPosition;
                    int sampledFrame = frame;
                    capture.Queue(() =>
                    {
                        float positionChange = Vector3.Distance(stablePosition, camera.transform.position);
                        float angleChange = Quaternion.Angle(stableRotation, camera.transform.rotation);
                        float fovChange = Mathf.Abs(stableFov - camera.fieldOfView);
                        maximumPositionChange = Mathf.Max(maximumPositionChange, positionChange);
                        maximumAngleChange = Mathf.Max(maximumAngleChange, angleChange);
                        maximumFovChange = Mathf.Max(maximumFovChange, fovChange);
                        Assert.Less(positionChange, .003f, "Lane reversals must not drag the camera sideways.");
                        Assert.Less(angleChange, .05f, "Echo approaching a bend must not pan the player's camera.");
                        Assert.Less(fovChange, .001f, "Lane and echo changes must not pump the lens.");
                        if (sampledFrame < 120)
                        {
                            AssertSkinnedBodyInFrame(camera, player.gameObject, "rapid reversals player");
                            AssertSkinnedBodyInFrame(camera, echo, "rapid reversals echo");
                        }
                        if (sampledFrame == 30 || sampledFrame == 210)
                            CapturePortraitWorld(camera, target, Path.Combine(output, aspect.ToString("F2") + "-" + sampledFrame + ".png"));
                        checkedFrames++;
                    });
                    yield return null;
                    if (capture.Failure != null) throw capture.Failure;
                }
            }
            game.Pause();
            Vector3 pausedPosition = camera.transform.position;
            Quaternion pausedRotation = camera.transform.rotation;
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.AreEqual(pausedPosition, camera.transform.position);
            Assert.AreEqual(pausedRotation, camera.transform.rotation);
            File.WriteAllText(Path.Combine(output, "stability-result.txt"),
                "frames=" + checkedFrames + " maxPositionChange=" + maximumPositionChange
                + " maxAngleChange=" + maximumAngleChange + " maxFovChange=" + maximumFovChange
                + "\nControlled camera regression, not human hand-feel acceptance.\n");
        }
        finally
        {
            Time.captureDeltaTime = previousStep;
            if (capture != null) { capture.Cancel(); Object.Destroy(capture.gameObject); }
            if (camera != null) camera.targetTexture = previousTarget;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (game != null) { game.enabled = true; game.ReturnToMenu(); }
            RestoreSavePreferences(saved);
        }
    }
}
