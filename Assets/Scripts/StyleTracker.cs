using System;
using UnityEngine;

public static class StyleTracker
{
    private const float LaneSampleInterval = 0.5f;
    private const float RecoveryWindowDuration = 10f;

    private static PlayerStyleData _profile;
    private static bool _initialized;
    private static bool _runActive;
    private static float _laneSampleTimer;
    private static float _rhythmProximityMean;
    private static float _rhythmProximityM2;
    private static int _rhythmSamples;
    private static float _recoveryTimeRemaining;
    private static int _recoveryActions;
    private static float _recoveryRiskTotal;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        string json = EchoRunSaveSystem.GetPlayerStyleJson();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _profile = JsonUtility.FromJson<PlayerStyleData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player style profile could not be loaded: "
                                 + exception.Message);
            }
        }

        _profile = _profile ?? new PlayerStyleData();
        _profile.Normalize();
    }

    public static void BeginRun(bool trainingEnabled = true)
    {
        EnsureInitialized();
        _runActive = trainingEnabled;
        _laneSampleTimer = 0f;
        _rhythmProximityMean = 0f;
        _rhythmProximityM2 = 0f;
        _rhythmSamples = 0;
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public static void TickLane(int lane, float deltaTime)
    {
        TickLane(lane, deltaTime, 1f);
    }

    public static void TickLane(int lane, float deltaTime,
        float offeredLaneCenter)
    {
        if (!_runActive) return;
        float elapsed = Mathf.Max(0f, deltaTime);
        _laneSampleTimer += elapsed;

        while (_laneSampleTimer >= LaneSampleInterval)
        {
            _laneSampleTimer -= LaneSampleInterval;
            _profile.ObserveLaneChoice(lane, offeredLaneCenter);
        }

        if (_recoveryTimeRemaining <= 0f) return;
        _recoveryTimeRemaining -= elapsed;
        if (_recoveryTimeRemaining <= 0f)
            CommitRecoveryObservation();
    }

    public static void RecordAction(ShadowAction action, float threatProximity,
        float jumpTimingOffset, bool airLaneChange = false,
        bool matchedActionObstacle = false)
    {
        if (!_runActive || action == ShadowAction.Keep) return;
        float proximity = Mathf.Clamp01(threatProximity);

        if (proximity > 0.05f || airLaneChange)
            _profile.ObserveAggressiveness(
                airLaneChange
                    ? 1f
                    : Mathf.InverseLerp(0.35f, 0.95f, proximity));
        if (action == ShadowAction.Jump && proximity > 0.05f)
            _profile.ObserveJumpTiming(jumpTimingOffset);
        if (ShouldObserveVerticalAction(action, matchedActionObstacle))
            _profile.ObserveVerticalAction(action);

        if ((action == ShadowAction.Jump || action == ShadowAction.Slide)
            && proximity > 0.05f)
        {
            _rhythmSamples++;
            float delta = proximity - _rhythmProximityMean;
            _rhythmProximityMean += delta / _rhythmSamples;
            _rhythmProximityM2 += delta
                                  * (proximity - _rhythmProximityMean);
            if (_rhythmSamples >= 2)
            {
                float deviation = Mathf.Sqrt(
                    _rhythmProximityM2 / Mathf.Max(1, _rhythmSamples - 1));
                _profile.ObserveRhythm(
                    1f - Mathf.Clamp01(deviation / 0.25f));
            }
        }

        if (_recoveryTimeRemaining > 0f)
        {
            _recoveryActions++;
            _recoveryRiskTotal += proximity;
        }
    }

    public static bool ShouldObserveVerticalAction(ShadowAction action,
        bool matchedActionObstacle)
    {
        return matchedActionObstacle
               && (action == ShadowAction.Jump || action == ShadowAction.Slide);
    }

    public static void RecordObstacleOpportunity(ObstacleType type,
        bool usedRequiredAction)
    {
        if (!_runActive) return;
        if (type == ObstacleType.Low)
            _profile.ObserveSlideOpportunity(usedRequiredAction);
    }

    public static void RecordMistake()
    {
        if (!_runActive) return;
        _recoveryTimeRemaining = RecoveryWindowDuration;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public static void EndRun()
    {
        if (!_runActive) return;
        _runActive = false;
        if (_recoveryTimeRemaining > 0f && _recoveryActions > 0)
            CommitRecoveryObservation();
        _profile.Normalize();
        EchoRunSaveSystem.SavePlayerStyle(JsonUtility.ToJson(_profile));
    }

    public static PlayerStyleData GetSnapshot()
    {
        EnsureInitialized();
        return _profile.Clone();
    }

    public static void ResetTraining()
    {
        ResetTrainingInternal(true);
    }

    public static void ResetTrainingInMemory()
    {
        ResetTrainingInternal(false);
    }

    private static void ResetTrainingInternal(bool persist)
    {
        _profile = new PlayerStyleData();
        _profile.Normalize();
        _initialized = true;
        _runActive = false;
        _laneSampleTimer = 0f;
        _rhythmProximityMean = 0f;
        _rhythmProximityM2 = 0f;
        _rhythmSamples = 0;
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
        if (persist) EchoRunSaveSystem.SavePlayerStyle("");
    }

    private static void CommitRecoveryObservation()
    {
        float actionUrgency = Mathf.Clamp01(_recoveryActions / 6f);
        float averageRisk = _recoveryActions > 0
            ? _recoveryRiskTotal / _recoveryActions
            : 0f;
        _profile.ObserveRecovery(actionUrgency * 0.65f
                                 + averageRisk * 0.35f);
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }
}

public class ObstacleOpportunityTracker
{
    private const int MaxRememberedObstacles = 256;
    private readonly System.Collections.Generic.HashSet<int> _resolvedIds =
        new System.Collections.Generic.HashSet<int>();
    private readonly System.Collections.Generic.Queue<int> _resolvedOrder =
        new System.Collections.Generic.Queue<int>();
    private int _pendingId;
    private int _pendingLane;
    private readonly ObstacleType _targetType;
    private bool _usedAction;

    public ObstacleOpportunityTracker(ObstacleType targetType)
    {
        _targetType = targetType;
    }

    public bool HasPending => _pendingId != 0;
    public int PendingId => _pendingId;
    public int LastResolvedId { get; private set; }
    public int LastResolvedLane { get; private set; } = 1;
    public bool LastResolvedByPass { get; private set; }
    public System.Collections.Generic.ISet<int> ResolvedIds => _resolvedIds;

    public void Reset()
    {
        _pendingId = 0;
        _pendingLane = 1;
        _usedAction = false;
        LastResolvedId = 0;
        LastResolvedLane = 1;
        LastResolvedByPass = false;
        _resolvedIds.Clear();
        _resolvedOrder.Clear();
    }

    public bool Update(int playerLane, bool isUsingAction, bool hasObstacle,
        float obstacleDistance, ObstacleType obstacleType, int obstacleId,
        float detectionDistance, out bool usedAction)
    {
        usedAction = false;
        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (HasPending)
        {
            if (lane != _pendingLane)
                return Resolve(out usedAction);
            if (isUsingAction) _usedAction = true;
            if (!hasObstacle || obstacleId != _pendingId)
                return Resolve(out usedAction, true);
            return false;
        }

        if (!hasObstacle || obstacleType != _targetType
            || obstacleId == 0 || _resolvedIds.Contains(obstacleId)
            || obstacleDistance > Mathf.Max(0f, detectionDistance))
            return false;

        _pendingId = obstacleId;
        _pendingLane = lane;
        _usedAction = isUsingAction;
        return false;
    }

    public void MarkAction(int playerLane)
    {
        if (HasPending && Mathf.Clamp(playerLane, 0, 2) == _pendingLane)
            _usedAction = true;
    }

    public bool Resolve(out bool usedAction, bool passedInLane = false)
    {
        usedAction = _usedAction;
        if (!HasPending) return false;
        LastResolvedId = _pendingId;
        LastResolvedLane = _pendingLane;
        LastResolvedByPass = passedInLane;
        if (_resolvedIds.Add(_pendingId))
        {
            _resolvedOrder.Enqueue(_pendingId);
            while (_resolvedOrder.Count > MaxRememberedObstacles)
                _resolvedIds.Remove(_resolvedOrder.Dequeue());
        }
        _pendingId = 0;
        _usedAction = false;
        return true;
    }
}

public sealed class SlideOpportunityTracker : ObstacleOpportunityTracker
{
    public SlideOpportunityTracker() : base(ObstacleType.Low)
    {
    }

    public void MarkSlide(int playerLane)
    {
        MarkAction(playerLane);
    }
}
