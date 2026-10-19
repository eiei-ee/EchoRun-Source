using UnityEngine;

public enum PlayerActionEdge
{
    JumpStarted = 1,
    Landed = 2,
    SlideStarted = 3,
    SlideEnded = 4,
    LaneChangeStarted = 5,
    LaneChangeCompleted = 6,
    ImpactAbsorbed = 7,
    ImpactRecovered = 8,
    FatalImpact = 9
}

public readonly struct PlayerMotionSnapshot
{
    public int Lane { get; }
    public bool IsJumping { get; }
    public bool IsSliding { get; }
    public float Jump01 { get; }
    public float Slide01 { get; }
    public float LateralVelocity { get; }
    public float Speed01 { get; }
    public Vector3 Forward { get; }
    public float LateralOffset { get; }

    public PlayerMotionSnapshot(int lane, bool isJumping, bool isSliding,
        float jump01, float slide01, float lateralVelocity, float speed01,
        Vector3 forward, float lateralOffset)
    {
        Lane = lane;
        IsJumping = isJumping;
        IsSliding = isSliding;
        Jump01 = jump01;
        Slide01 = slide01;
        LateralVelocity = lateralVelocity;
        Speed01 = speed01;
        Forward = forward;
        LateralOffset = lateralOffset;
    }
}

public readonly struct PlayerActionSignal
{
    public PlayerActionEdge Edge { get; }
    public int Sequence { get; }
    public int ActionId { get; }
    public float GameTime { get; }
    public float Duration { get; }
    public Vector3 Position { get; }
    public Vector3 Forward { get; }
    public int FromLane { get; }
    public int ToLane { get; }
    public PlayerMotionSnapshot Motion { get; }

    public PlayerActionSignal(PlayerActionEdge edge, int sequence,
        int actionId, float gameTime, float duration, Vector3 position,
        Vector3 forward, int fromLane, int toLane,
        PlayerMotionSnapshot motion)
    {
        Edge = edge;
        Sequence = sequence;
        ActionId = actionId;
        GameTime = gameTime;
        Duration = duration;
        Position = position;
        Forward = forward;
        FromLane = fromLane;
        ToLane = toLane;
        Motion = motion;
    }
}

public static class PlayerMotionFeedback
{
    public static PlayerMotionSnapshot Project(int lane, bool isJumping,
        float jumpTimer, float jumpDuration, bool isSliding,
        float slideTimer, float slideDuration, float lateralVelocity,
        float currentSpeed, float startSpeed, float maxSpeed,
        Vector3 forward, float lateralOffset)
    {
        float jump01 = isJumping
            ? NormalizeTimer(jumpTimer, jumpDuration)
            : 0f;
        float slide01 = isSliding
            ? NormalizeTimer(slideTimer, slideDuration)
            : 0f;
        float speed01 = maxSpeed > startSpeed
            ? Mathf.InverseLerp(startSpeed, maxSpeed, currentSpeed)
            : 0f;
        Vector3 normalizedForward = Vector3.ProjectOnPlane(
            forward, Vector3.up);
        if (normalizedForward.sqrMagnitude < 0.0001f)
            normalizedForward = Vector3.forward;
        else
            normalizedForward.Normalize();

        return new PlayerMotionSnapshot(lane, isJumping, isSliding,
            jump01, slide01, lateralVelocity, speed01,
            normalizedForward, lateralOffset);
    }

    private static float NormalizeTimer(float timer, float duration)
    {
        return Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
    }
}
