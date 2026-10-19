using System;
using UnityEngine;

[Serializable]
public sealed class AIShadowSequenceState
{
    public float[] transitions;
    public int pairCount;
}

// A small first-order action model. It keeps the shadow's learned habits
// without requiring a neural-network runtime in WebGL.
public sealed class AIShadowSequencePolicy
{
    public const int ActionCount = 5;

    private const float PriorCount = 0.25f;
    private readonly float[,] _transitions = new float[ActionCount, ActionCount];
    private int _pairCount;

    public int PairCount => _pairCount;

    public AIShadowSequencePolicy(float[] savedTransitions = null,
        int savedPairCount = 0)
    {
        int index = 0;
        bool hasSavedState = savedTransitions != null
                             && savedTransitions.Length == ActionCount * ActionCount;
        for (int previous = 0; previous < ActionCount; previous++)
        {
            for (int next = 0; next < ActionCount; next++)
            {
                float value = hasSavedState ? savedTransitions[index] : PriorCount;
                _transitions[previous, next] = IsFinite(value) && value >= 0f
                    ? value
                    : PriorCount;
                index++;
            }
        }

        _pairCount = Mathf.Max(0, savedPairCount);
    }

    public void Learn(int previousAction, int nextAction)
    {
        if (!IsAction(previousAction) || !IsAction(nextAction)) return;
        _transitions[previousAction, nextAction] += 1f;
        _pairCount++;
    }

    public int Predict(float[] immediateProbabilities, int previousAction,
        out float sequenceConfidence, out float sequenceInfluence)
    {
        ValidateProbabilities(immediateProbabilities);

        int immediateAction = BestAction(immediateProbabilities);
        float immediateConfidence = Mathf.Clamp01(
            immediateProbabilities[immediateAction]);
        sequenceConfidence = 0f;
        sequenceInfluence = 0f;

        if (!IsAction(previousAction) || _pairCount == 0)
            return immediateAction;

        float total = 0f;
        for (int action = 0; action < ActionCount; action++)
            total += _transitions[previousAction, action];

        if (total <= 0.0001f) return immediateAction;

        float evidence = Mathf.Clamp01((_pairCount - 2f) / 18f);
        float ambiguity = Mathf.Clamp01((0.72f - immediateConfidence) / 0.42f);
        sequenceInfluence = evidence * ambiguity * 0.7f;
        if (sequenceInfluence <= 0.0001f) return immediateAction;

        int selectedAction = immediateAction;
        float selectedScore = float.MinValue;
        for (int action = 0; action < ActionCount; action++)
        {
            float transitionProbability = _transitions[previousAction, action] / total;
            float score = Mathf.Lerp(immediateProbabilities[action],
                transitionProbability, sequenceInfluence);
            if (score > selectedScore)
            {
                selectedAction = action;
                selectedScore = score;
            }
        }

        sequenceConfidence = _transitions[previousAction, selectedAction] / total;
        return selectedAction;
    }

    public AIShadowSequenceState ExportState()
    {
        float[] transitions = new float[ActionCount * ActionCount];
        int index = 0;
        for (int previous = 0; previous < ActionCount; previous++)
            for (int next = 0; next < ActionCount; next++)
                transitions[index++] = _transitions[previous, next];

        return new AIShadowSequenceState
        {
            transitions = transitions,
            pairCount = _pairCount
        };
    }

    private static int BestAction(float[] probabilities)
    {
        int best = 0;
        for (int action = 1; action < ActionCount; action++)
            if (probabilities[action] > probabilities[best]) best = action;
        return best;
    }

    private static bool IsAction(int action)
    {
        return action >= 0 && action < ActionCount;
    }

    private static void ValidateProbabilities(float[] probabilities)
    {
        if (probabilities == null || probabilities.Length != ActionCount)
            throw new ArgumentException(
                "AI shadow probabilities must contain five actions.",
                nameof(probabilities));
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
