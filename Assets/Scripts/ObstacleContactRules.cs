using System;
using UnityEngine;

public enum ObstacleContactSource
{
    Trigger,
    Overlap,
    Sweep
}

public enum ObstacleContactOutcome
{
    Pass,
    Hit,
    AlreadyResolved
}

public enum ObstacleContactReason
{
    LowClearance,
    LowClearanceWithoutSlideState,
    LowInsufficientClearance,
    HighClearance,
    HighPastFrontDuringJump,
    HighInsufficientClearance,
    BarrierRequiresLaneChange,
    PreviouslyResolved
}

public readonly struct ObstacleContactEvaluation
{
    public readonly ObstacleContactOutcome outcome;
    public readonly ObstacleContactReason reason;
    public readonly float verticalClearance;

    public ObstacleContactEvaluation(ObstacleContactOutcome outcome,
        ObstacleContactReason reason, float verticalClearance)
    {
        this.outcome = outcome;
        this.reason = reason;
        this.verticalClearance = verticalClearance;
    }

    public bool Passed => outcome == ObstacleContactOutcome.Pass;
}

[Serializable]
public sealed class ObstacleContactDiagnostic
{
    public ObstacleContactSource source;
    public int obstacleId;
    public ObstacleType type;
    public int seed;
    public float speed;
    public int lane;
    public bool jumping;
    public bool sliding;
    public float verticalClearance;
    public ObstacleContactOutcome outcome;
    public ObstacleContactReason reason;

    public string ToDisplayString()
    {
        return source + " #" + obstacleId + " " + type
               + " · " + outcome + " / " + reason
               + " · v=" + speed.ToString("0.0")
               + " lane=" + lane
               + " jump=" + jumping + " slide=" + sliding
               + " clear=" + verticalClearance.ToString("0.00")
               + " seed=" + seed;
    }
}

public static class ObstacleContactRules
{
    public const float LowClearanceTolerance = 0.05f;
    public const float HighClearanceTolerance = 0.3f;

    public static ObstacleContactEvaluation Evaluate(ObstacleType type,
        Bounds playerBounds, Bounds obstacleBounds, bool isJumping,
        bool isSliding, Vector3 forward)
    {
        if (type == ObstacleType.Low)
        {
            float clearance = obstacleBounds.min.y - playerBounds.max.y;
            if (clearance >= -LowClearanceTolerance)
            {
                return new ObstacleContactEvaluation(
                    ObstacleContactOutcome.Pass,
                    isSliding
                        ? ObstacleContactReason.LowClearance
                        : ObstacleContactReason.LowClearanceWithoutSlideState,
                    clearance);
            }

            return new ObstacleContactEvaluation(ObstacleContactOutcome.Hit,
                ObstacleContactReason.LowInsufficientClearance, clearance);
        }

        if (type == ObstacleType.High)
        {
            float clearance = playerBounds.min.y - obstacleBounds.max.y;
            if (isJumping && clearance >= -HighClearanceTolerance)
            {
                return new ObstacleContactEvaluation(
                    ObstacleContactOutcome.Pass,
                    ObstacleContactReason.HighClearance, clearance);
            }

            if (isJumping && HasCenterPassedFront(
                    playerBounds.center, obstacleBounds, forward))
            {
                return new ObstacleContactEvaluation(
                    ObstacleContactOutcome.Pass,
                    ObstacleContactReason.HighPastFrontDuringJump, clearance);
            }

            return new ObstacleContactEvaluation(ObstacleContactOutcome.Hit,
                ObstacleContactReason.HighInsufficientClearance, clearance);
        }

        return new ObstacleContactEvaluation(ObstacleContactOutcome.Hit,
            ObstacleContactReason.BarrierRequiresLaneChange,
            obstacleBounds.min.y - playerBounds.max.y);
    }

    private static bool HasCenterPassedFront(Vector3 playerCenter,
        Bounds obstacleBounds, Vector3 forward)
    {
        Vector3 direction = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
        float obstacleHalfDepth = Mathf.Abs(direction.x) * obstacleBounds.extents.x
                                  + Mathf.Abs(direction.y) * obstacleBounds.extents.y
                                  + Mathf.Abs(direction.z) * obstacleBounds.extents.z;
        float obstacleFront = Vector3.Dot(obstacleBounds.center, direction)
                              - obstacleHalfDepth;
        return Vector3.Dot(playerCenter, direction) >= obstacleFront;
    }
}

public static class ObstacleGeometryRules
{
    public static Vector3 ColliderSize(ObstacleType type)
    {
        if (type == ObstacleType.Low) return new Vector3(3.1f, 0.82f, 1.2f);
        if (type == ObstacleType.High) return new Vector3(3.2f, 0.9f, 0.7f);
        return new Vector3(3.4f, 2.7f, 0.9f);
    }

    public static Vector3 ColliderCenter(ObstacleType type)
    {
        if (type == ObstacleType.Low) return new Vector3(0f, 0.95f, 0f);
        if (type == ObstacleType.High) return new Vector3(0f, -0.45f, 0f);
        return new Vector3(0f, 0.25f, 0f);
    }
}
