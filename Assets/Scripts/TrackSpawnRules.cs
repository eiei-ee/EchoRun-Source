using System.Collections.Generic;
using UnityEngine;

public static class TrackSpawnRules
{
    public const float CoinSpacing = 1.8f;
    public const float GroundCoinHeight = 1f;
    public const int JumpRewardCoinCount = 7;
    public const float CoinSegmentMargin = 1f;
    public const int TurnGuideCoinsPerArm = 3;
    public const int TurnGuideCoinCount = TurnGuideCoinsPerArm * 2 + 1;

    public static bool NeedsSegment(float plannedRouteDistance,
        float playerRouteDistance, float segmentLength, int poolSize)
    {
        float safeLength = Mathf.Max(1f, segmentLength);
        float lookAheadDistance = safeLength * Mathf.Max(2, poolSize / 2);
        return plannedRouteDistance - playerRouteDistance < lookAheadDistance;
    }

    public static bool CanRecycleSegment(float segmentRouteDistance,
        float playerRouteDistance, float segmentLength, float recycleMultiplier)
    {
        float recycleDistance = Mathf.Max(1f, segmentLength)
                                * Mathf.Max(1f, recycleMultiplier);
        return playerRouteDistance - segmentRouteDistance > recycleDistance;
    }

    public static bool ShouldSpawnObstacleRow(int straightSegmentsSpawned,
        int obstacleFreeSegments, int warmupSegments, int maxFreeSegments,
        float chance, float chanceRoll)
    {
        if (straightSegmentsSpawned <= Mathf.Max(0, warmupSegments)) return false;
        if (obstacleFreeSegments >= Mathf.Max(1, maxFreeSegments)) return true;
        return chanceRoll < Mathf.Clamp01(chance);
    }

    public static float MinimumObstacleRowSpacing(float speed,
        float jumpDuration, float segmentLength)
    {
        return MinimumObstacleRowSpacing(speed, jumpDuration, segmentLength,
            0.3f);
    }

    public static float MinimumObstacleRowSpacing(float speed,
        float jumpDuration, float segmentLength, float recoverySeconds)
    {
        float actionRecoveryDistance = Mathf.Max(1f, speed)
                                       * (Mathf.Max(0.2f, jumpDuration)
                                          + Mathf.Max(0f, recoverySeconds));
        return Mathf.Max(Mathf.Max(1f, segmentLength),
            actionRecoveryDistance);
    }

    public static bool CanSpawnObstacleRow(float routeDistance,
        float previousRouteDistance, float minimumSpacing)
    {
        if (float.IsNegativeInfinity(previousRouteDistance)) return true;
        return routeDistance - previousRouteDistance
               >= Mathf.Max(0f, minimumSpacing);
    }

    public static float CoinTrailEndZ(float startZ, int count, float spacing)
    {
        return startZ + Mathf.Max(0, count - 1) * Mathf.Max(0f, spacing);
    }

    public static Vector3 TurnGuideCoinLocalPosition(float segmentLength,
        int turnDirection, int coinIndex)
    {
        float corner = Mathf.Max(0f, segmentLength) * 0.5f;
        int index = Mathf.Clamp(coinIndex, 0, TurnGuideCoinCount - 1);
        int stepFromCorner = index - TurnGuideCoinsPerArm;
        if (stepFromCorner <= 0)
        {
            return new Vector3(0f, GroundCoinHeight,
                corner + stepFromCorner * CoinSpacing);
        }

        float direction = turnDirection >= 0 ? 1f : -1f;
        return new Vector3(direction * stepFromCorner * CoinSpacing,
            GroundCoinHeight, corner);
    }

    public static Vector3 TurnGuideCoinLocalTangent(float segmentLength,
        int turnDirection, int coinIndex)
    {
        int index = Mathf.Clamp(coinIndex, 0, TurnGuideCoinCount - 1);
        int previousIndex = Mathf.Max(0, index - 1);
        int nextIndex = Mathf.Min(TurnGuideCoinCount - 1, index + 1);
        Vector3 tangent = TurnGuideCoinLocalPosition(
                              segmentLength, turnDirection, nextIndex)
                          - TurnGuideCoinLocalPosition(
                              segmentLength, turnDirection, previousIndex);
        tangent.y = 0f;
        return tangent.sqrMagnitude > 0.0001f
            ? tangent.normalized
            : Vector3.forward;
    }

    public static Quaternion CoinRouteRotation(Quaternion segmentRotation,
        Vector3 localTangent)
    {
        Vector3 worldTangent = segmentRotation * localTangent;
        worldTangent.y = 0f;
        if (worldTangent.sqrMagnitude <= 0.0001f)
            worldTangent = segmentRotation * Vector3.forward;
        return Quaternion.LookRotation(worldTangent.normalized, Vector3.up);
    }

    public static bool CoinTrailOverlapsObstacle(float startZ, int count,
        float spacing, float obstacleZ, float obstacleHalfDepth)
    {
        if (count <= 0) return false;
        float safeSpacing = Mathf.Max(0f, spacing);
        float halfCoinStep = safeSpacing * 0.5f;
        float endZ = CoinTrailEndZ(startZ, count, safeSpacing);
        float halfDepth = Mathf.Max(0f, obstacleHalfDepth);
        return obstacleZ + halfDepth >= startZ - halfCoinStep
               && obstacleZ - halfDepth <= endZ + halfCoinStep;
    }

    public static float ClampJumpRewardCenter(float centerZ,
        float segmentLength, int coinCount, float spacing, float margin)
    {
        float safeLength = Mathf.Max(0f, segmentLength);
        float safeMargin = Mathf.Max(0f, margin);
        float halfSpan = Mathf.Max(0, coinCount - 1)
                         * Mathf.Max(0f, spacing) * 0.5f;
        float minimum = safeMargin + halfSpan;
        float maximum = safeLength - safeMargin - halfSpan;
        if (maximum < minimum) return safeLength * 0.5f;
        return Mathf.Clamp(centerZ, minimum, maximum);
    }

    public static float JumpCoinHeight(float normalizedProgress,
        float groundHeight, float jumpHeight)
    {
        return groundHeight + PlayerController.EvaluateJumpArc(
                   normalizedProgress) * Mathf.Max(0f, jumpHeight);
    }

    public static int SelectObstaclePrefabIndex(float difficulty, float typeRoll)
    {
        float normalizedDifficulty = Mathf.Clamp01(difficulty);
        if (normalizedDifficulty < 0.3f) return 0;

        float highObstacleChance = normalizedDifficulty < 0.6f ? 0.35f : 0.5f;
        return Mathf.Clamp01(typeRoll) < highObstacleChance ? 1 : 0;
    }

    public static int ChooseFairSafeLane(int proposedLane, int previousSafeLane,
        int[] laneObstacleDrought)
    {
        int proposed = Mathf.Clamp(proposedLane, 0, 2);
        int previous = Mathf.Clamp(previousSafeLane, 0, 2);
        int minLane = Mathf.Max(0, previous - 1);
        int maxLane = Mathf.Min(2, previous + 1);
        int bestLane = Mathf.Clamp(proposed, minLane, maxLane);
        int bestDrought = GetLaneDrought(laneObstacleDrought, bestLane);

        for (int lane = minLane; lane <= maxLane; lane++)
        {
            int drought = GetLaneDrought(laneObstacleDrought, lane);
            if (drought < bestDrought
                || (drought == bestDrought
                    && Mathf.Abs(lane - proposed) < Mathf.Abs(bestLane - proposed)))
            {
                bestLane = lane;
                bestDrought = drought;
            }
        }
        return bestLane;
    }

    public static int[] SelectBlockedLanes(int safeLane, int blockedLaneCount,
        int[] laneObstacleDrought)
    {
        int safe = Mathf.Clamp(safeLane, 0, 2);
        var candidates = new List<int>(2);
        for (int lane = 0; lane < 3; lane++)
            if (lane != safe) candidates.Add(lane);

        candidates.Sort((left, right) =>
        {
            int droughtOrder = GetLaneDrought(laneObstacleDrought, right)
                .CompareTo(GetLaneDrought(laneObstacleDrought, left));
            return droughtOrder != 0 ? droughtOrder : left.CompareTo(right);
        });

        int count = Mathf.Clamp(blockedLaneCount, 1, 2);
        return candidates.GetRange(0, Mathf.Min(count, candidates.Count)).ToArray();
    }

    private static int GetLaneDrought(int[] laneObstacleDrought, int lane)
    {
        return laneObstacleDrought != null && lane >= 0
               && lane < laneObstacleDrought.Length
            ? Mathf.Max(0, laneObstacleDrought[lane])
            : 0;
    }
}
