using System;
using UnityEngine;

[Serializable]
public struct ShadowAIDirective
{
    [Range(0f, 1f)] public float styleInfluence;
    [Range(-1f, 1f)] public float riskBias;
    [Range(0f, 1f)] public float decisionNoise;

    public static ShadowAIDirective Neutral => new ShadowAIDirective
    {
        styleInfluence = 1f,
        riskBias = 0f,
        decisionNoise = 0.12f
    };

    public ShadowAIDirective Normalized()
    {
        styleInfluence = Mathf.Clamp01(styleInfluence);
        riskBias = Mathf.Clamp(riskBias, -1f, 1f);
        decisionNoise = Mathf.Clamp01(decisionNoise);
        return this;
    }
}

public interface IShadowDirectiveSource
{
    ShadowAIDirective CurrentShadowDirective { get; }
}

public struct ShadowDecisionContext
{
    public int lane;
    public float threatProximity;
    public int relativeThreatLane;
    public ObstacleType threatType;
    public bool hasThreat;
    public bool isJumping;
    public bool isSliding;
    public bool isStumbling;
    public bool isRecovering;
}

[Serializable]
public sealed class ShadowDecisionTrace
{
    public ShadowAction originalPrediction;
    public ShadowAction selectedAction;
    public float[] baseScores;
    public float[] styleAdjustedScores;
    public float[] finalScores;
    public bool[] feasibleActions;
    public bool safetyAdjusted;
    public ShadowAIDirective directive;
}

public sealed class ShadowDecisionMaker
{
    public ShadowAction Select(float[] baseProbabilities,
        PlayerStyleData style, ShadowDecisionContext context,
        ShadowAIDirective directive, float random01)
    {
        return Select(baseProbabilities, style, context, directive,
            random01, out _);
    }

    public ShadowAction Select(float[] baseProbabilities,
        PlayerStyleData style, ShadowDecisionContext context,
        ShadowAIDirective directive, float random01,
        out ShadowDecisionTrace trace)
    {
        if (baseProbabilities == null
            || baseProbabilities.Length != AIShadowPolicy.ActionCount)
            throw new ArgumentException("Shadow decision requires five base scores.",
                nameof(baseProbabilities));

        style = style ?? new PlayerStyleData();
        style.Normalize();
        directive = directive.Normalized();
        float[] scores = (float[])baseProbabilities.Clone();
        float influence = style.Confidence * directive.styleInfluence;

        ApplyLanePreference(scores, style, context, influence);
        ApplyVerticalPreference(scores, style, context, influence);
        ApplyRiskPreference(scores, style, context, directive, influence);
        float[] styleAdjustedScores = (float[])scores.Clone();
        bool safetyAdjusted = ApplyFeasibility(scores, context);

        ShadowAction selected = SelectWeighted(scores, style.rhythmStability,
            directive.decisionNoise, random01);
        trace = new ShadowDecisionTrace
        {
            originalPrediction = (ShadowAction)FindBest(baseProbabilities),
            selectedAction = selected,
            baseScores = (float[])baseProbabilities.Clone(),
            styleAdjustedScores = styleAdjustedScores,
            finalScores = SanitizeScores(scores),
            feasibleActions = BuildFeasibility(scores),
            safetyAdjusted = safetyAdjusted,
            directive = directive
        };
        return selected;
    }

    public static float ReactionDistanceMultiplier(PlayerStyleData style,
        ShadowAIDirective directive)
    {
        style = style ?? new PlayerStyleData();
        directive = directive.Normalized();
        float confidence = style.Confidence * directive.styleInfluence;
        float timing = Mathf.Lerp(0f, style.jumpTiming, confidence);
        float aggression = Mathf.Lerp(0.5f, style.aggressiveness, confidence);

        // Negative timing means early. Positive timing and aggression both wait longer.
        float multiplier = 1f - timing * 0.28f
                           - (aggression - 0.5f) * 0.22f
                           - directive.riskBias * 0.15f;
        return Mathf.Clamp(multiplier, 0.62f, 1.42f);
    }

    private static void ApplyLanePreference(float[] scores,
        PlayerStyleData style, ShadowDecisionContext context, float influence)
    {
        float currentLane = Mathf.Clamp(context.lane, 0, 2) - 1f;
        float delta = style.lanePreference - currentLane;
        scores[(int)ShadowAction.Left] += Mathf.Max(0f, -delta) * 0.34f * influence;
        scores[(int)ShadowAction.Right] += Mathf.Max(0f, delta) * 0.34f * influence;
        scores[(int)ShadowAction.Keep] += (1f - Mathf.Abs(delta) * 0.5f)
                                          * 0.10f * influence;
    }

    private static void ApplyVerticalPreference(float[] scores,
        PlayerStyleData style, ShadowDecisionContext context, float influence)
    {
        if (!context.hasThreat || context.relativeThreatLane != 0) return;
        float slideBias = (style.slideFrequency - 0.5f) * 0.34f * influence;
        scores[(int)ShadowAction.Slide] += slideBias;
        scores[(int)ShadowAction.Jump] -= slideBias * 0.35f;
    }

    private static void ApplyRiskPreference(float[] scores,
        PlayerStyleData style, ShadowDecisionContext context,
        ShadowAIDirective directive, float influence)
    {
        float risk = Mathf.Lerp(0f, style.aggressiveness - 0.5f, influence)
                     + directive.riskBias * 0.5f;
        if (context.isRecovering)
            risk += Mathf.Lerp(0f, style.recoveryStyle - 0.5f, influence);

        float pressure = Mathf.Clamp01(context.threatProximity);
        scores[(int)ShadowAction.Keep] += risk * pressure * 0.24f;
        float activeBias = -risk * pressure * 0.08f;
        scores[(int)ShadowAction.Left] += activeBias;
        scores[(int)ShadowAction.Right] += activeBias;
        scores[(int)ShadowAction.Jump] += activeBias;
        scores[(int)ShadowAction.Slide] += activeBias;
    }

    private static bool ApplyFeasibility(float[] scores,
        ShadowDecisionContext context)
    {
        if (context.lane <= 0) scores[(int)ShadowAction.Left] = float.NegativeInfinity;
        if (context.lane >= 2) scores[(int)ShadowAction.Right] = float.NegativeInfinity;
        if (context.isJumping || context.isSliding || context.isStumbling)
        {
            scores[(int)ShadowAction.Jump] = float.NegativeInfinity;
            scores[(int)ShadowAction.Slide] = float.NegativeInfinity;
        }

        if (!context.hasThreat || context.relativeThreatLane != 0) return false;
        if (context.threatType == ObstacleType.Barrier)
        {
            scores[(int)ShadowAction.Jump] = float.NegativeInfinity;
            scores[(int)ShadowAction.Slide] = float.NegativeInfinity;
        }

        if (context.threatProximity < 0.86f) return false;
        ShadowAction required = AIShadowRules.RequiredActionForObstacle(
            context.threatType);
        if (required != ShadowAction.Keep
            && !float.IsNegativeInfinity(scores[(int)required]))
        {
            // Emergency safety is stronger than style imitation.
            for (int i = 0; i < scores.Length; i++)
                if (i != (int)required) scores[i] = float.NegativeInfinity;
            return true;
        }
        else if (context.threatType == ObstacleType.Barrier)
        {
            scores[(int)ShadowAction.Keep] = float.NegativeInfinity;
            return true;
        }
        return false;
    }

    private static ShadowAction SelectWeighted(float[] scores,
        float rhythmStability, float decisionNoise, float random01)
    {
        int best = FindBest(scores);
        float noise = Mathf.Clamp01(decisionNoise)
                      * (1f - Mathf.Clamp01(rhythmStability));
        if (noise <= 0.001f) return (ShadowAction)best;

        float temperature = Mathf.Lerp(0.05f, 0.35f, noise);
        float maxScore = scores[best];
        float total = 0f;
        float[] weights = new float[scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            if (float.IsNegativeInfinity(scores[i])) continue;
            weights[i] = Mathf.Exp((scores[i] - maxScore) / temperature);
            total += weights[i];
        }

        float cursor = Mathf.Clamp01(random01) * total;
        for (int i = 0; i < weights.Length; i++)
        {
            cursor -= weights[i];
            if (cursor <= 0f && weights[i] > 0f) return (ShadowAction)i;
        }
        return (ShadowAction)best;
    }

    private static int FindBest(float[] scores)
    {
        int best = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[best]) best = i;
        return best;
    }

    private static bool[] BuildFeasibility(float[] scores)
    {
        bool[] result = new bool[scores.Length];
        for (int i = 0; i < scores.Length; i++)
            result[i] = !float.IsNegativeInfinity(scores[i]);
        return result;
    }

    private static float[] SanitizeScores(float[] scores)
    {
        float[] result = new float[scores.Length];
        for (int i = 0; i < scores.Length; i++)
            result[i] = float.IsNegativeInfinity(scores[i]) ? -999f : scores[i];
        return result;
    }
}
