using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerFeedbackControllerTests
{
    [TestCase(PlayerActionEdge.JumpStarted, PlayerFeedbackCue.JumpStart)]
    [TestCase(PlayerActionEdge.Landed, PlayerFeedbackCue.Land)]
    [TestCase(PlayerActionEdge.SlideStarted, PlayerFeedbackCue.SlideStart)]
    [TestCase(PlayerActionEdge.SlideEnded, PlayerFeedbackCue.SlideEnd)]
    [TestCase(PlayerActionEdge.ImpactAbsorbed,
        PlayerFeedbackCue.RecoverableImpact)]
    [TestCase(PlayerActionEdge.ImpactRecovered,
        PlayerFeedbackCue.RecoverableImpact)]
    [TestCase(PlayerActionEdge.FatalImpact, PlayerFeedbackCue.FatalImpact)]
    public void AuthorityEdgesMapToOneSemanticCue(
        PlayerActionEdge edge, PlayerFeedbackCue expected)
    {
        Assert.AreEqual(expected, PlayerFeedbackController.CueFor(edge));
    }

    [Test]
    public void LaneEdgesStayContinuousAndDoNotCreateDuplicateBursts()
    {
        Assert.AreEqual(PlayerFeedbackCue.None,
            PlayerFeedbackController.CueFor(
                PlayerActionEdge.LaneChangeStarted));
        Assert.AreEqual(PlayerFeedbackCue.None,
            PlayerFeedbackController.CueFor(
                PlayerActionEdge.LaneChangeCompleted));
    }

    [Test]
    public void SequenceGuardRejectsDuplicatesAndOutOfOrderSignals()
    {
        Assert.IsTrue(PlayerFeedbackController.ShouldHandleSequence(0, 1));
        Assert.IsTrue(PlayerFeedbackController.ShouldHandleSequence(4, 5));
        Assert.IsFalse(PlayerFeedbackController.ShouldHandleSequence(5, 5));
        Assert.IsFalse(PlayerFeedbackController.ShouldHandleSequence(5, 4));
        Assert.IsFalse(PlayerFeedbackController.ShouldHandleSequence(0, 0));
    }

    [Test]
    public void ContactAndTrailMappingsAreClampedAndDeterministic()
    {
        Assert.AreEqual(new Vector3(2f, 0f, 5f),
            PlayerFeedbackController.ResolveGroundContactPosition(
                new Vector3(2f, 1f, 5f), 1f));
        var capsuleBounds = new Bounds(
            new Vector3(3f, 1f, 7f), new Vector3(1f, 2.2f, 1f));
        Vector3 colliderContact =
            PlayerFeedbackController.ResolveColliderGroundContactPosition(
                new Vector3(2f, 4f, 5f), capsuleBounds);
        Assert.AreEqual(2f, colliderContact.x, 0.0001f);
        Assert.AreEqual(-0.1f, colliderContact.y, 0.0001f);
        Assert.AreEqual(5f, colliderContact.z, 0.0001f);
        Vector3 left =
            PlayerFeedbackController.ResolveAlternatingFootfallPosition(
                Vector3.zero, Vector3.forward, 0);
        Vector3 right =
            PlayerFeedbackController.ResolveAlternatingFootfallPosition(
                Vector3.zero, Vector3.forward, 1);
        Assert.AreEqual(-0.14f, left.x, 0.0001f);
        Assert.AreEqual(0.14f, right.x, 0.0001f);
        Assert.AreEqual(0.012f, left.y, 0.0001f);
        Assert.AreEqual(left.z, right.z, 0.0001f);
    }

    [Test]
    public void ImpactsKeepTheAuthoritativeClosestPoint()
    {
        Assert.IsTrue(PlayerFeedbackController.UsesGroundContact(
            PlayerFeedbackCue.Land));
        Assert.IsTrue(PlayerFeedbackController.UsesGroundContact(
            PlayerFeedbackCue.SlideStart));
        Assert.IsFalse(PlayerFeedbackController.UsesGroundContact(
            PlayerFeedbackCue.RecoverableImpact));
        Assert.IsFalse(PlayerFeedbackController.UsesGroundContact(
            PlayerFeedbackCue.FatalImpact));
    }

    [Test]
    public void EnableAndDisableCallbacksWireAuthorityEdgesExactlyOnce()
    {
        GameObject gameObject = new GameObject("FeedbackLifecycle_Test");
        try
        {
            PlayerController player =
                gameObject.AddComponent<PlayerController>();
            PlayerFeedbackController feedback =
                gameObject.AddComponent<PlayerFeedbackController>();

            InvokePrivate(feedback, "OnEnable");
            InvokePrivate(player, "BeginJump");
            int handledBeforeDisable = feedback.LastHandledSequence;
            Assert.Greater(handledBeforeDisable, 0);

            InvokePrivate(feedback, "OnDisable");
            InvokePrivate(player, "CompleteJump");
            Assert.AreEqual(handledBeforeDisable,
                feedback.LastHandledSequence);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SampleSceneHasExactlyOneEnabledFeedbackBridge()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/SampleScene.scene", OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Assert.IsNotNull(player, scene.path);
        PlayerFeedbackController[] bridges =
            player.GetComponents<PlayerFeedbackController>();
        Assert.AreEqual(1, bridges.Length);
        Assert.IsTrue(bridges[0].enabled);
        Assert.IsNotNull(player.GetComponent<PlayerController>());
    }

    private static object InvokePrivate(object target, string name,
        params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, name);
        return method.Invoke(target, args);
    }
}
