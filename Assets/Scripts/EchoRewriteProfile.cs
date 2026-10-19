using System;
using UnityEngine;

[Serializable]
public sealed class EchoRewriteSnapshot
{
    [Range(0f, 1f)] public float routeVariation01;
    [Range(0f, 1f)] public float actionMix01;
    [Range(0f, 1f)] public float rhythmNovelty01;
    [Range(0f, 1f)] public float execution01;
    [Range(0f, 1f)] public float sampleCoverage01;
    [Range(0f, 1f)] public float profileChange01;
    [Range(1f, 2f)] public float writeStrength = 1f;
    public int effectiveRouteChoices;
    public int effectiveVerticalActions;
    public int successfulExecutions;
    public int mistakes;
    public string styleJson = "";

    public PlayerStyleData GetStyle()
    {
        if (string.IsNullOrEmpty(styleJson)) return new PlayerStyleData();
        try
        {
            PlayerStyleData style = JsonUtility.FromJson<PlayerStyleData>(
                styleJson);
            style = style ?? new PlayerStyleData();
            style.Normalize();
            return style;
        }
        catch (Exception)
        {
            return new PlayerStyleData();
        }
    }

    public EchoRewriteSnapshot Clone()
    {
        return JsonUtility.FromJson<EchoRewriteSnapshot>(
            JsonUtility.ToJson(this));
    }

    public string BuildHudSummary()
    {
        return "样本 " + Percent(sampleCoverage01)
               + " · 清晰 " + Percent(execution01)
               + " · 变化 " + Percent(profileChange01)
               + " · 写入×" + Mathf.Clamp(writeStrength, 1f, 2f)
                   .ToString("0.00");
    }

    public string BuildProfileSummary()
    {
        string route = routeVariation01 >= 0.55f
            ? "多路线" : routeVariation01 >= 0.25f
                ? "双路线" : "路线稳定";
        string action = actionMix01 >= 0.55f
            ? "混合动作" : actionMix01 >= 0.2f
                ? "动作变化" : "单一动作";
        string rhythm = rhythmNovelty01 >= 0.55f
            ? "非固定节奏" : rhythmNovelty01 >= 0.2f
                ? "节奏变化" : "节奏稳定";
        return route + " · " + action + " · " + rhythm
               + " · 画像变化 " + Percent(profileChange01)
               + " · 写入×" + Mathf.Clamp(writeStrength, 1f, 2f)
                   .ToString("0.00");
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }
}

/// <summary>
/// Measures deliberate, successfully executed variation during the fixed
/// rewrite window. Raw input spam is deliberately ignored: route evidence
/// comes from collected route trails and vertical evidence must match a real
/// obstacle opportunity.
/// </summary>
public sealed class EchoRewriteTracker
{
    public const float MinimumRouteSampleSpacing = 14f;
    private const int MistakeExecutionWeight = 4;

    private PlayerStyleData _baselineStyle;
    private readonly bool[] _visitedLanes = new bool[3];
    private int _visitedLaneCount;
    private int _routeSamples;
    private int _laneSwitches;
    private float _laneTotal;
    private float _routeWeight;
    private float _laneWeightTotal;
    private float _laneSwitchWeight;
    private int _lastLane = -1;
    private float _lastRouteDistance = float.NegativeInfinity;
    private int _jumpActions;
    private int _slideActions;
    private float _jumpWeight;
    private float _slideWeight;
    private float _lastVerticalActionTime = -1f;
    private int _rhythmIntervals;
    private float _rhythmMean;
    private float _rhythmM2;
    private int _successes;
    private int _mistakes;
    private float _successWeight;
    private float _mistakeWeight;

    public EchoRewriteTracker(PlayerStyleData baselineStyle = null)
    {
        if (baselineStyle == null) return;
        _baselineStyle = baselineStyle.Clone();
        _baselineStyle.Normalize();
    }

    public void RecordRouteChoice(int lane, float routeDistance,
        float sampleWeight = 1f)
    {
        int clampedLane = Mathf.Clamp(lane, 0, 2);
        float distance = Mathf.Max(0f, routeDistance);
        if (!float.IsNegativeInfinity(_lastRouteDistance)
            && distance - _lastRouteDistance < MinimumRouteSampleSpacing)
            return;

        _lastRouteDistance = distance;
        float weight = Mathf.Clamp(sampleWeight, 0.1f, 2f);
        _routeSamples++;
        _laneTotal += clampedLane;
        _routeWeight += weight;
        _laneWeightTotal += clampedLane * weight;
        _successes++;
        _successWeight += weight;
        if (!_visitedLanes[clampedLane])
        {
            _visitedLanes[clampedLane] = true;
            _visitedLaneCount++;
        }
        if (_lastLane >= 0 && _lastLane != clampedLane)
        {
            _laneSwitches++;
            _laneSwitchWeight += weight;
        }
        _lastLane = clampedLane;
    }

    public void RecordVerticalAction(ShadowAction action,
        bool matchedObstacle, float eventTime, float sampleWeight = 1f)
    {
        if (!matchedObstacle
            || (action != ShadowAction.Jump
                && action != ShadowAction.Slide))
            return;

        float weight = Mathf.Clamp(sampleWeight, 0.1f, 2f);
        if (action == ShadowAction.Jump)
        {
            _jumpActions++;
            _jumpWeight += weight;
        }
        else
        {
            _slideActions++;
            _slideWeight += weight;
        }

        float time = Mathf.Max(0f, eventTime);
        if (_lastVerticalActionTime >= 0f)
        {
            float interval = time - _lastVerticalActionTime;
            if (interval >= 0.15f && interval <= 12f)
            {
                _rhythmIntervals++;
                float delta = interval - _rhythmMean;
                _rhythmMean += delta / _rhythmIntervals;
                _rhythmM2 += delta * (interval - _rhythmMean);
            }
        }
        _lastVerticalActionTime = time;
    }

    public void RecordSuccessfulExecution(float sampleWeight = 1f)
    {
        _successes++;
        _successWeight += Mathf.Clamp(sampleWeight, 0.1f, 2f);
    }

    public void RecordMistake(float sampleWeight = 1f)
    {
        _mistakes++;
        _mistakeWeight += Mathf.Clamp(sampleWeight, 0.1f, 2f);
    }

    public EchoRewriteSnapshot BuildSnapshot(PlayerStyleData style)
    {
        float routeParticipation = Mathf.Clamp01(_routeWeight / 4f);
        float distinctRoutes = Mathf.Clamp01((_visitedLaneCount - 1f) / 2f);
        float switchRatio = _routeWeight > 1f
            ? Mathf.Clamp01(_laneSwitchWeight / Mathf.Max(1f,
                _routeWeight - 1f))
            : 0f;
        float routeVariation = routeParticipation
                               * (distinctRoutes * 0.65f
                                  + switchRatio * 0.35f);

        int verticalActions = _jumpActions + _slideActions;
        float verticalWeight = _jumpWeight + _slideWeight;
        float actionBalance = verticalWeight >= 1.5f
            ? 1f - Mathf.Abs(_jumpWeight - _slideWeight)
              / verticalWeight
            : 0f;
        float actionMix = actionBalance
                          * Mathf.Clamp01(verticalWeight / 4f);

        float rhythmNovelty = 0f;
        if (_rhythmIntervals >= 2 && _rhythmMean > 0.001f)
        {
            float deviation = Mathf.Sqrt(
                _rhythmM2 / Mathf.Max(1, _rhythmIntervals - 1));
            float coefficient = deviation / _rhythmMean;
            rhythmNovelty = Mathf.Clamp01(coefficient / 0.35f)
                            * Mathf.Clamp01(_rhythmIntervals / 3f);
        }

        float weightedMistakes = _mistakeWeight * MistakeExecutionWeight;
        float executionSamples = _successWeight + weightedMistakes;
        float execution = executionSamples > 0f
            ? _successWeight / executionSamples
              * Mathf.Clamp01(executionSamples / 4f)
            : 0f;
        float actionParticipation = Mathf.Clamp01(verticalWeight / 4f);
        float rhythmParticipation = Mathf.Clamp01(_rhythmIntervals / 3f);
        float sampleCoverage = routeParticipation * 0.40f
                               + actionParticipation * 0.35f
                               + rhythmParticipation * 0.25f;
        float strength = Mathf.Clamp(
            1f + sampleCoverage * execution, 1f, 2f);

        if (_baselineStyle == null)
        {
            _baselineStyle = style != null
                ? style.Clone() : new PlayerStyleData();
            _baselineStyle.Normalize();
        }
        PlayerStyleData frozenStyle = _baselineStyle.Clone();
        float baselineLane = frozenStyle.lanePreference;
        float baselineSlide = frozenStyle.slideFrequency;
        float baselineRhythm = frozenStyle.rhythmStability;
        if (_routeSamples > 0)
        {
            float rewriteLanePreference = Mathf.Clamp(
                _laneWeightTotal / Mathf.Max(0.1f, _routeWeight) - 1f,
                -1f, 1f);
            frozenStyle.lanePreference = Mathf.Lerp(
                frozenStyle.lanePreference, rewriteLanePreference,
                Mathf.Clamp01(routeParticipation * execution));
            frozenStyle.laneSamples += _routeSamples;
        }
        if (verticalActions > 0)
        {
            float rewriteSlideFrequency = (_slideWeight + 1f)
                                          / (verticalWeight + 2f);
            frozenStyle.slideFrequency = Mathf.Lerp(
                frozenStyle.slideFrequency, rewriteSlideFrequency,
                Mathf.Clamp01(actionParticipation * execution));
            frozenStyle.jumpActionSamples += _jumpActions;
            frozenStyle.slideActionSamples += _slideActions;
        }
        if (_rhythmIntervals >= 2)
        {
            float observedRhythmStability = 1f - rhythmNovelty;
            frozenStyle.rhythmStability = Mathf.Lerp(
                frozenStyle.rhythmStability, observedRhythmStability,
                Mathf.Clamp01(rhythmParticipation * execution));
            frozenStyle.rhythmSamples += _rhythmIntervals;
        }
        frozenStyle.Normalize();
        float laneChange = Mathf.Abs(frozenStyle.lanePreference - baselineLane)
                           * 0.5f;
        float actionChange = Mathf.Abs(frozenStyle.slideFrequency
                                       - baselineSlide);
        float rhythmChange = Mathf.Abs(frozenStyle.rhythmStability
                                       - baselineRhythm);
        float profileChange = Mathf.Clamp01(laneChange * 0.40f
                                            + actionChange * 0.35f
                                            + rhythmChange * 0.25f);
        return new EchoRewriteSnapshot
        {
            routeVariation01 = Mathf.Clamp01(routeVariation),
            actionMix01 = Mathf.Clamp01(actionMix),
            rhythmNovelty01 = Mathf.Clamp01(rhythmNovelty),
            execution01 = Mathf.Clamp01(execution),
            sampleCoverage01 = Mathf.Clamp01(sampleCoverage),
            profileChange01 = profileChange,
            writeStrength = strength,
            effectiveRouteChoices = _routeSamples,
            effectiveVerticalActions = verticalActions,
            successfulExecutions = _successes,
            mistakes = _mistakes,
            styleJson = JsonUtility.ToJson(frozenStyle)
        };
    }
}
