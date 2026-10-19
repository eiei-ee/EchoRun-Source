using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerVerticalActionTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject gameObject in _objects)
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
        _objects.Clear();
    }

    [TestCase(true, false, SwipeDirection.Down)]
    [TestCase(false, true, SwipeDirection.Up)]
    public void OppositeVerticalInputCannotOverlapActiveAction(
        bool jumping, bool sliding, SwipeDirection direction)
    {
        InputManager input = CreateInput();
        PlayerController player = CreateGroundedPlayer(input);
        SetField(player, "<IsJumping>k__BackingField", jumping);
        SetField(player, "<IsSliding>k__BackingField", sliding);
        input.QueueSwipe(direction, InputIntentSource.Replay, Time.unscaledTime);

        Invoke(player, "HandleInput");

        Assert.AreEqual(jumping, player.IsJumping);
        Assert.AreEqual(sliding, player.IsSliding);
        Assert.AreEqual(1, input.PendingInputCount,
            "Blocked input may stay buffered briefly, but must not overlap actions.");
    }

    [TestCase(SwipeDirection.Up)]
    [TestCase(SwipeDirection.Down)]
    public void GroundedVerticalInputStartsExactlyOneAction(
        SwipeDirection direction)
    {
        InputManager input = CreateInput();
        PlayerController player = CreateGroundedPlayer(input);
        input.QueueSwipe(direction, InputIntentSource.Replay, Time.unscaledTime);

        Invoke(player, "HandleInput");

        Assert.AreEqual(direction == SwipeDirection.Up, player.IsJumping);
        Assert.AreEqual(direction == SwipeDirection.Down, player.IsSliding);
        Assert.AreEqual(0, input.PendingInputCount);
        Assert.IsFalse(player.IsJumping && player.IsSliding);
    }

    private InputManager CreateInput()
    {
        GameObject gameObject = Track(new GameObject("InputManager"));
        return gameObject.AddComponent<InputManager>();
    }

    private PlayerController CreateGroundedPlayer(InputManager input)
    {
        GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.1f, 0f);
        ground.transform.localScale = new Vector3(10f, 0.2f, 10f);

        GameObject playerObject = Track(new GameObject("Player"));
        Rigidbody body = playerObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        CapsuleCollider capsule = playerObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.height = 2f;
        capsule.radius = 0.4f;
        PlayerController player = playerObject.AddComponent<PlayerController>();
        player.groundLayer = 1 << ground.layer;

        SetField(player, "_input", input);
        SetField(player, "_rb", body);
        SetField(player, "_capsuleCollider", capsule);
        SetField(player, "_originalColliderHeight", capsule.height);
        SetField(player, "_originalColliderCenter", capsule.center);
        Physics.SyncTransforms();
        return player;
    }

    private GameObject Track(GameObject gameObject)
    {
        _objects.Add(gameObject);
        return gameObject;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing field: " + name);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing method: " + methodName);
        method.Invoke(target, null);
    }
}
