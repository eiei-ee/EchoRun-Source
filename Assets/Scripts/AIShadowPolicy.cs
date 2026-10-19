using System;
using UnityEngine;

public enum ShadowAction
{
    Keep,
    Left,
    Right,
    Jump,
    Slide
}

// Small online softmax classifier used for runtime behavior cloning.
// The model is intentionally pure C# so it runs identically in WebGL.
public sealed class AIShadowPolicy
{
    public const int FeatureCount = 8;
    public const int ActionCount = 5;

    private const float MinimumWeight = -4f;
    private const float MaximumWeight = 4f;

    private readonly float[,] _weights = new float[ActionCount, FeatureCount];

    public AIShadowPolicy(float[] savedWeights = null)
    {
        if (AIModelWeightRules.TrySanitize(savedWeights,
                ActionCount * FeatureCount, MinimumWeight, MaximumWeight,
                out float[] sanitized))
        {
            int index = 0;
            for (int action = 0; action < ActionCount; action++)
                for (int feature = 0; feature < FeatureCount; feature++)
                    _weights[action, feature] = sanitized[index++];
        }
        else
        {
            _weights[(int)ShadowAction.Keep, 0] = 0.25f;
            if (savedWeights != null)
                Debug.LogWarning(
                    "AI shadow weights were invalid and were reset to defaults.");
        }
    }

    public int Predict(float[] features)
    {
        Validate(features);
        int bestAction = 0;
        float bestScore = Score(0, features);
        for (int action = 1; action < ActionCount; action++)
        {
            float score = Score(action, features);
            if (score <= bestScore) continue;
            bestAction = action;
            bestScore = score;
        }
        return bestAction;
    }

    public float Confidence(float[] features)
    {
        float[] probabilities = Probabilities(features);
        float best = 0f;
        for (int i = 0; i < probabilities.Length; i++)
            best = Mathf.Max(best, probabilities[i]);
        return best;
    }

    public float[] GetProbabilities(float[] features) => Probabilities(features);

    public void Learn(int label, float[] features, float learningRate)
    {
        Validate(features);
        if (label < 0 || label >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(label));

        float[] probabilities = Probabilities(features);
        float rate = Mathf.Clamp(learningRate, 0.001f, 0.5f);
        for (int action = 0; action < ActionCount; action++)
        {
            float target = action == label ? 1f : 0f;
            float error = target - probabilities[action];
            for (int feature = 0; feature < FeatureCount; feature++)
            {
                float updated = _weights[action, feature]
                                + rate * error * features[feature];
                _weights[action, feature] = Mathf.Clamp(
                    updated, MinimumWeight, MaximumWeight);
            }
        }
    }

    public float Score(int action, float[] features)
    {
        Validate(features);
        if (action < 0 || action >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(action));

        float score = 0f;
        for (int feature = 0; feature < FeatureCount; feature++)
            score += _weights[action, feature] * features[feature];
        return score;
    }

    public float[] ExportWeights()
    {
        float[] result = new float[ActionCount * FeatureCount];
        int index = 0;
        for (int action = 0; action < ActionCount; action++)
            for (int feature = 0; feature < FeatureCount; feature++)
                result[index++] = _weights[action, feature];
        return result;
    }

    private float[] Probabilities(float[] features)
    {
        Validate(features);
        float[] result = new float[ActionCount];
        float maxScore = Score(0, features);
        for (int action = 1; action < ActionCount; action++)
            maxScore = Mathf.Max(maxScore, Score(action, features));

        float total = 0f;
        for (int action = 0; action < ActionCount; action++)
        {
            result[action] = Mathf.Exp(Score(action, features) - maxScore);
            total += result[action];
        }

        for (int action = 0; action < ActionCount; action++)
            result[action] /= Mathf.Max(0.0001f, total);
        return result;
    }

    private static void Validate(float[] features)
    {
        if (features == null || features.Length != FeatureCount)
            throw new ArgumentException(
                "AI shadow input must contain eight features.", nameof(features));
    }
}
