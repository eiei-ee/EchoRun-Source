using NUnit.Framework;
using UnityEngine;

public sealed class PortraitCameraFramingTests
{
    [TestCase(0.45f)]
    [TestCase(0.5f)]
    [TestCase(0.5625f)]
    [TestCase(1.777778f)]
    public void AllLanePairsKeepRunningAndJumpingBodiesInsideTheFrame(float aspect)
    {
        var owner = new GameObject("FramingRegressionCamera");
        try
        {
            Camera camera = owner.AddComponent<Camera>();
            camera.aspect = aspect;
            camera.fieldOfView = WorldStyler.GetCameraFieldOfView(aspect < 1f);
            foreach (CameraViewHeight view in new[] { CameraViewHeight.Low, CameraViewHeight.High })
            {
                float lookAhead = CameraFollow.ResolveLookAhead(view, aspect);
                Vector3 offset = CameraFollow.ResolveLaneFramingOffset(
                    CameraFollow.ResolveViewOffset(WorldStyler.GetCameraOffset(aspect < 1f), view),
                    aspect, camera.fieldOfView, 3f, lookAhead);
                foreach (float heading in new[] { 0f, 90f, 180f, 270f })
                {
                    Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
                    Vector3 forward = rotation * Vector3.forward;
                    Vector3 right = rotation * Vector3.right;
                    foreach (int playerLane in new[] { 0, 1, 2 })
                    {
                        Vector3 player = right * ((playerLane - 1) * 3f);
                        Vector3 anchor = CameraFollow.ResolveTrackAnchor(player, forward,
                            (playerLane - 1) * 3f);
                        camera.transform.position = anchor + rotation * offset;
                        camera.transform.LookAt(anchor + forward * lookAhead);
                        CheckBody(camera, player, rotation, "player " + playerLane);
                        foreach (int echoLane in new[] { 0, 1, 2 })
                        foreach (float gap in new[] { -2.5f, 0f, 3.2f, 8f, 16f })
                        foreach (float jump in new[] { 0f, 3f })
                            CheckBody(camera, right * ((echoLane - 1) * 3f)
                                + forward * gap + Vector3.up * jump, rotation,
                                view + " player=" + playerLane + " echo=" + echoLane
                                + " gap=" + gap + " jump=" + jump + " aspect=" + aspect);
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private static void CheckBody(Camera camera, Vector3 root, Quaternion rotation, string context)
    {
        foreach (float x in new[] { -0.6f, 0.6f })
        foreach (float y in new[] { -1f, 1.9f })
        foreach (float z in new[] { -0.5f, 0.5f })
        {
            Vector3 viewport = camera.WorldToViewportPoint(root + rotation * new Vector3(x, y, z));
            Assert.Greater(viewport.z, camera.nearClipPlane, context);
            Assert.That(viewport.x, Is.InRange(0.025f, 0.975f), context);
            Assert.That(viewport.y, Is.InRange(0.02f, 0.96f), context);
        }
    }

    [Test]
    public void LaneFramingDoesNotRaiseTheCameraIntoTheUpperTransit()
    {
        foreach (float aspect in new[] { 0.45f, 0.5f, 0.5625f })
        foreach (CameraViewHeight view in new[] { CameraViewHeight.Low, CameraViewHeight.High })
        {
            Vector3 configured = CameraFollow.ResolveViewOffset(WorldStyler.GetCameraOffset(true), view);
            Vector3 fitted = CameraFollow.ResolveLaneFramingOffset(configured, aspect, 62f, 3f,
                CameraFollow.ResolveLookAhead(view, aspect));
            Assert.AreEqual(configured.y, fitted.y, 0.0001f,
                "Narrower viewports must retreat horizontally, not raise into overhead geometry.");
            Assert.Less(fitted.y + 1f + 3f * 0.12f, 8.4f);
        }
    }
}
