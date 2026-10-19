using System;
using UnityEngine;

[Serializable]
public sealed class BayesianAbilityEstimate
{
    public float alpha = 2f;
    public float beta = 2f;

    public float Mean => alpha / Mathf.Max(0.0001f, alpha + beta);

    public float Confidence
    {
        get
        {
            float total = Mathf.Max(0.0001f, alpha + beta);
            float variance = alpha * beta
                             / (total * total * (total + 1f));
            return 1f - Mathf.Clamp01(Mathf.Sqrt(variance) / 0.225f);
        }
    }

    public void Observe(bool success, float weight = 1f)
    {
        float evidence = Mathf.Clamp(weight, 0.05f, 4f);
        if (success) alpha += evidence;
        else beta += evidence;
        Normalize();
    }

    public void Normalize()
    {
        alpha = Mathf.Max(0.1f, alpha);
        beta = Mathf.Max(0.1f, beta);
    }
}

[Serializable]
public sealed class AIPlayerSkillProfile
{
    public int version = 1;
    public int completedRuns;
    public float bestDistance;
    public int totalObstacleOutcomes;
    public BayesianAbilityEstimate survival = new BayesianAbilityEstimate();
    public BayesianAbilityEstimate jumping = new BayesianAbilityEstimate();
    public BayesianAbilityEstimate sliding = new BayesianAbilityEstimate();
    public float reactionProximityMean = 0.55f;
    public float reactionProximityM2;
    public int reactionSamples;

    public float OverallSkill =>
        Mathf.Clamp01(survival.Mean * 0.5f
                      + jumping.Mean * 0.25f
                      + sliding.Mean * 0.25f);

    public float Confidence =>
        Mathf.Clamp01(survival.Confidence * 0.5f
                      + jumping.Confidence * 0.25f
                      + sliding.Confidence * 0.25f);

    public float Uncertainty => 1f - Confidence;

    public void Normalize()
    {
        version = 1;
        completedRuns = Mathf.Max(0, completedRuns);
        bestDistance = Mathf.Max(0f, bestDistance);
        totalObstacleOutcomes = Mathf.Max(0, totalObstacleOutcomes);
        survival = survival ?? new BayesianAbilityEstimate();
        jumping = jumping ?? new BayesianAbilityEstimate();
        sliding = sliding ?? new BayesianAbilityEstimate();
        survival.Normalize();
        jumping.Normalize();
        sliding.Normalize();
        reactionProximityMean = Mathf.Clamp01(reactionProximityMean);
        reactionProximityM2 = Mathf.Max(0f, reactionProximityM2);
        reactionSamples = Mathf.Max(0, reactionSamples);
    }

    public void RecordObstacle(ObstacleType type, bool avoided,
        float actionProximity)
    {
        totalObstacleOutcomes++;
        if (type == ObstacleType.High)
            jumping.Observe(avoided);
        else if (type == ObstacleType.Low)
            sliding.Observe(avoided);

        if (avoided && type != ObstacleType.Barrier
            && actionProximity >= 0f)
        {
            reactionSamples++;
            float delta = actionProximity - reactionProximityMean;
            reactionProximityMean += delta / reactionSamples;
            float deltaAfter = actionProximity - reactionProximityMean;
            reactionProximityM2 += delta * deltaAfter;
        }
    }

    public void RecordSegment(bool survived, float distanceGain)
    {
        float weight = Mathf.Clamp(distanceGain / 25f, 0.25f, 1f);
        survival.Observe(survived, weight);
    }

    public void RecordRunEnd(float distance, bool completed)
    {
        if (!completed) return;
        completedRuns++;
        bestDistance = Mathf.Max(bestDistance, distance);
    }
}

public static class AIPlayerSkillEstimator
{
    private static AIPlayerSkillProfile _profile;
    private static bool _initialized;
    private static bool _runActive;
    private static float _lastJumpProximity = -1f;
    private static float _lastSlideProximity = -1f;

    public static float Skill
    {
        get
        {
            EnsureInitialized();
            return _profile.OverallSkill;
        }
    }

    public static float Confidence
    {
        get
        {
            EnsureInitialized();
            return _profile.Confidence;
        }
    }

    public static float Uncertainty => 1f - Confidence;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        string json = EchoRunSaveSystem.GetSkillProfileJson();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _profile = JsonUtility.FromJson<AIPlayerSkillProfile>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "AI player skill profile could not be loaded: "
                    + exception.Message);
            }
        }
        _profile = _profile ?? new AIPlayerSkillProfile();
        _profile.Normalize();
    }

    public static void BeginRun(bool trainingEnabled = true)
    {
        EnsureInitialized();
        _runActive = trainingEnabled;
        _lastJumpProximity = -1f;
        _lastSlideProximity = -1f;
    }

    public static void RecordAction(ShadowAction action, float[] features)
    {
        if (!_runActive || features == null || features.Length < 4) return;
        float proximity = Mathf.Clamp01(features[3]);
        if (action == ShadowAction.Jump)
            _lastJumpProximity = proximity;
        else if (action == ShadowAction.Slide)
            _lastSlideProximity = proximity;
    }

    public static void RecordObstacleOutcome(ObstacleType type, bool avoided)
    {
        if (!_runActive) return;
        float proximity = type == ObstacleType.High
            ? _lastJumpProximity
            : (type == ObstacleType.Low ? _lastSlideProximity : -1f);
        _profile.RecordObstacle(type, avoided, proximity);
        if (type == ObstacleType.High) _lastJumpProximity = -1f;
        else if (type == ObstacleType.Low) _lastSlideProximity = -1f;
    }

    public static void RecordSegmentOutcome(bool survived, float distanceGain)
    {
        if (!_runActive) return;
        _profile.RecordSegment(survived, distanceGain);
    }

    public static void EndRun(float distance, bool completed)
    {
        if (!_runActive) return;
        _runActive = false;
        _profile.RecordRunEnd(distance, completed);
        EchoRunSaveSystem.SaveSkillProfile(JsonUtility.ToJson(_profile));
    }

    public static AIPlayerSkillProfile GetSnapshot()
    {
        EnsureInitialized();
        return JsonUtility.FromJson<AIPlayerSkillProfile>(
            JsonUtility.ToJson(_profile));
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
        _profile = new AIPlayerSkillProfile();
        _profile.Normalize();
        _initialized = true;
        _runActive = false;
        _lastJumpProximity = -1f;
        _lastSlideProximity = -1f;
        if (persist) EchoRunSaveSystem.SaveSkillProfile("");
    }
}
