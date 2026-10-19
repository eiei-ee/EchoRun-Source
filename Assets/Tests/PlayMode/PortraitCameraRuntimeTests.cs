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
    public IEnumerator HomeCityDeckMeetsRenderedFeetAndHidesDuringRun()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        GameManager game = null;
        try
        {
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            yield return new WaitForSeconds(.75f);
            game = GameManager.Instance;
            Assert.AreEqual(GameState.Menu, game.State);
            GameObject preview = GameObject.Find("HomeCityPreview");
            Assert.IsNotNull(preview);
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            float soleY = float.PositiveInfinity;
            var pose = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer renderer in player.characterModel
                    .GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.enabled) continue;
                    renderer.BakeMesh(pose);
                    foreach (Vector3 vertex in pose.vertices)
                        soleY = Mathf.Min(soleY, renderer.transform.TransformPoint(vertex).y);
                }
            }
            finally { Object.Destroy(pose); }
            Assert.IsFalse(float.IsInfinity(soleY), "Measure the visible character, not proxy bones.");
            float surfaceY = preview.transform.position.y + .10f;
            Assert.That(soleY - surfaceY, Is.InRange(-.06f, .15f),
                "Home deck must meet the rendered soles. sole=" + soleY + " deck=" + surfaceY);
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(collider.enabled, "The home preview must not add gameplay collision.");
            game.StartGame();
            yield return null;
            yield return null;
            Assert.IsFalse(preview.activeSelf, "The real route owns gameplay scenery.");
        }
        finally
        {
            if (game != null) game.ReturnToMenu();
            RestoreSavePreferences(saved);
        }
    }

    [UnityTest]
    public IEnumerator PortraitCameraKeepsActualRunnerAndEchoVisibleOnAllLanePairs()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        RenderTexture target = null;
        Camera camera = null;
        RenderTexture previousTarget = null;
        GameManager game = null;
        string output = Path.GetFullPath("TestResults/PortraitPolish-20260925/Framing");
        Directory.CreateDirectory(output);
        try
        {
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
            Assert.AreEqual(GameState.Playing, game.State);
            AIShadowRunner shadow = AIShadowRunner.Instance;
            Assert.IsTrue(shadow.HasActiveOpponent);
            var echo = (GameObject)typeof(AIShadowRunner).GetField("_ghost",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shadow);
            Assert.IsNotNull(echo);
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            Rigidbody body = player.GetComponent<Rigidbody>();
            // Controlled poses isolate the framing regression. This is not a
            // collision or ordinary-play acceptance test.
            game.enabled = false;
            player.enabled = false;
            shadow.enabled = false;
            TrackManager.Instance.enabled = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            Vector3 forward = player.ForwardDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = player.transform.position - right * player.RenderedLateralOffset;
            float echoY = echo.transform.position.y;
            camera = Camera.main;
            previousTarget = camera.targetTexture;
            target = new RenderTexture(540, 1080, 24);
            target.Create();
            camera.targetTexture = target;
            camera.aspect = 0.5f;
            CameraFollow follow = camera.GetComponent<CameraFollow>();
            follow.offset = WorldStyler.GetCameraOffset(true);
            camera.fieldOfView = WorldStyler.GetCameraFieldOfView(true);
            follow.ResetMotionFeedback();
            foreach (CameraViewHeight view in new[] { CameraViewHeight.Low, CameraViewHeight.High })
            {
                PlayerPrefs.SetInt(CameraViewSettings.PreferenceKey, (int)view);
                foreach (float gap in new[] { -2.5f, 0f, 3.2f })
                foreach (int playerLane in new[] { 0, 1, 2 })
                foreach (int echoLane in new[] { 0, 1, 2 })
                {
                    float lateral = (playerLane - 1) * player.laneDistance;
                    typeof(PlayerController).GetField("_laneOffset", BindingFlags.Instance
                        | BindingFlags.NonPublic).SetValue(player, lateral);
                    body.position = center + right * lateral;
                    player.transform.position = body.position;
                    echo.transform.position = center + right * ((echoLane - 1) * player.laneDistance)
                        + forward * gap;
                    Vector3 ghostPosition = echo.transform.position;
                    ghostPosition.y = echoY;
                    echo.transform.position = ghostPosition;
                    float look = CameraFollow.ResolveLookAhead(view, camera.aspect);
                    Vector3 fit = CameraFollow.ResolveLaneFramingOffset(
                        CameraFollow.ResolveViewOffset(follow.offset, view), camera.aspect,
                        WorldStyler.GetCameraFieldOfView(true), player.laneDistance, look);
                    camera.transform.position = center + forward * fit.z + Vector3.up * fit.y;
                    camera.transform.LookAt(center + forward * look);
                    for (int frame = 0; frame < 8; frame++) yield return null;
                    string label = view + "-p" + playerLane + "-e" + echoLane + "-gap" + gap;
                    if (Mathf.Abs(playerLane - echoLane) == 2 || playerLane == echoLane)
                        CapturePortraitWorld(camera, target, Path.Combine(output, label + ".png"));
                    AssertSkinnedBodyInFrame(camera, player.gameObject, label + " player");
                    AssertSkinnedBodyInFrame(camera, echo, label + " echo");
                }
            }
            File.WriteAllText(Path.Combine(output, "result.txt"),
                "54 controlled actual-model lane-pair states passed at 540x1080.\n"
                + "Both views; echo gaps -2.5, 0, 3.2; real CameraFollow, scene and models.\n"
                + "World-only captures. Simulation frozen for controlled poses; no human-play or collision claim.\n");
        }
        finally
        {
            if (camera != null) camera.targetTexture = previousTarget;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (game != null) { game.enabled = true; game.ReturnToMenu(); }
            RestoreSavePreferences(saved);
        }
    }

    private static void AssertSkinnedBodyInFrame(Camera camera, GameObject actor, string context)
    {
        SkinnedMeshRenderer[] renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        Assert.IsNotEmpty(renderers, context + " must use the real character mesh");
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            // Imported animation bounds include poses that are not being drawn.
            // Measure the actual skinned pose instead of that culling envelope.
            var pose = new Mesh();
            try
            {
                renderer.BakeMesh(pose);
                Vector3 minimum = Vector3.one * float.PositiveInfinity;
                Vector3 maximum = Vector3.one * float.NegativeInfinity;
                foreach (Vector3 vertex in pose.vertices)
                {
                    Vector3 point = camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                    minimum = Vector3.Min(minimum, point);
                    maximum = Vector3.Max(maximum, point);
                }
                string detail = context + " " + renderer.name + " min=" + minimum
                    + " max=" + maximum + " camera=" + camera.transform.position;
                Assert.Greater(minimum.z, 0f, detail);
                Assert.That(minimum.x, Is.InRange(0.015f, 0.985f), detail);
                Assert.That(maximum.x, Is.InRange(0.015f, 0.985f), detail);
                Assert.That(minimum.y, Is.InRange(0.01f, 0.98f), detail);
                Assert.That(maximum.y, Is.InRange(0.01f, 0.98f), detail);
            }
            finally { Object.Destroy(pose); }
        }
    }

    private static void CapturePortraitWorld(Camera camera, RenderTexture target, string path)
    {
        RenderTexture active = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = active; Object.Destroy(pixels); }
    }
}
