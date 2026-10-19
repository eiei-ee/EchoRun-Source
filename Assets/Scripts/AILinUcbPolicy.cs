using System;
using UnityEngine;

[Serializable]
public sealed class AILinUcbState
{
    public int version = 1;
    public float[] designMatrices;
    public float[] rewardVectors;
    public int[] actionPulls;
}

public sealed class AILinUcbPolicy
{
    public const int FeatureCount = AITrackPolicy.FeatureCount;
    public const int ActionCount = AITrackPolicy.ActionCount;

    private const float PriorStrength = 2f;
    private const float MinDiagonal = 0.0001f;

    private readonly float[] _design =
        new float[ActionCount * FeatureCount * FeatureCount];
    private readonly float[] _reward =
        new float[ActionCount * FeatureCount];
    private readonly int[] _pulls = new int[ActionCount];

    public float LastSelectedMean { get; private set; }
    public float LastSelectedUncertainty { get; private set; }
    public float LastSelectedScore { get; private set; }

    public AILinUcbPolicy(float[] priorWeights = null,
        string savedStateJson = null)
    {
        InitializePrior(priorWeights);
        ImportState(savedStateJson);
    }

    public int Select(float[] context, float explorationStrength)
    {
        ValidateContext(context);
        float alpha = Mathf.Clamp(explorationStrength, 0f, 2f);
        int bestAction = 0;
        Evaluate(0, context, alpha,
            out float bestMean, out float bestUncertainty,
            out float bestScore);

        for (int action = 1; action < ActionCount; action++)
        {
            Evaluate(action, context, alpha,
                out float mean, out float uncertainty, out float score);
            if (score <= bestScore) continue;
            bestAction = action;
            bestMean = mean;
            bestUncertainty = uncertainty;
            bestScore = score;
        }

        LastSelectedMean = bestMean;
        LastSelectedUncertainty = bestUncertainty;
        LastSelectedScore = bestScore;
        return bestAction;
    }

    public void Update(int action, float[] context, float reward,
        float evidenceWeight = 1f)
    {
        ValidateAction(action);
        ValidateContext(context);
        float weight = Mathf.Clamp(evidenceWeight, 0.1f, 2f);
        float boundedReward = Mathf.Clamp(reward, -1f, 1f);
        int matrixBase = action * FeatureCount * FeatureCount;
        int vectorBase = action * FeatureCount;

        for (int row = 0; row < FeatureCount; row++)
        {
            _reward[vectorBase + row] +=
                weight * boundedReward * context[row];
            for (int column = 0; column < FeatureCount; column++)
            {
                _design[matrixBase + row * FeatureCount + column] +=
                    weight * context[row] * context[column];
            }
        }
        _pulls[action]++;
    }

    public float MeanScore(int action, float[] context)
    {
        ValidateAction(action);
        ValidateContext(context);
        float[] theta = SolveForAction(action,
            GetRewardVector(action));
        return Dot(theta, context);
    }

    public float Uncertainty(int action, float[] context)
    {
        ValidateAction(action);
        ValidateContext(context);
        float[] solved = SolveForAction(action, context);
        return Mathf.Sqrt(Mathf.Max(0f, Dot(context, solved)));
    }

    public float[] ExportWeights()
    {
        float[] weights = new float[ActionCount * FeatureCount];
        for (int action = 0; action < ActionCount; action++)
        {
            float[] theta = SolveForAction(
                action, GetRewardVector(action));
            Array.Copy(theta, 0, weights,
                action * FeatureCount, FeatureCount);
        }
        return weights;
    }

    public string ExportStateJson()
    {
        return JsonUtility.ToJson(new AILinUcbState
        {
            designMatrices = (float[])_design.Clone(),
            rewardVectors = (float[])_reward.Clone(),
            actionPulls = (int[])_pulls.Clone()
        });
    }

    private void Evaluate(int action, float[] context, float alpha,
        out float mean, out float uncertainty, out float score)
    {
        mean = MeanScore(action, context);
        uncertainty = Uncertainty(action, context);
        score = mean + alpha * uncertainty;
    }

    private void InitializePrior(float[] priorWeights)
    {
        float[] weights;
        if (!AIModelWeightRules.TrySanitize(priorWeights,
                ActionCount * FeatureCount, -3f, 3f, out weights))
        {
            if (priorWeights != null)
                Debug.LogWarning(
                    "AI director prior weights were invalid and were reset to defaults.");
            weights = new AITrackPolicy(1337).ExportWeights();
        }

        for (int action = 0; action < ActionCount; action++)
        {
            int matrixBase = action * FeatureCount * FeatureCount;
            int vectorBase = action * FeatureCount;
            for (int feature = 0; feature < FeatureCount; feature++)
            {
                _design[matrixBase + feature * FeatureCount + feature] =
                    PriorStrength;
                _reward[vectorBase + feature] =
                    PriorStrength * weights[vectorBase + feature];
            }
        }
    }

    private void ImportState(string savedStateJson)
    {
        if (string.IsNullOrEmpty(savedStateJson)) return;
        try
        {
            AILinUcbState state =
                JsonUtility.FromJson<AILinUcbState>(savedStateJson);
            if (state == null
                || state.designMatrices == null
                || state.rewardVectors == null
                || state.actionPulls == null
                || state.designMatrices.Length != _design.Length
                || state.rewardVectors.Length != _reward.Length
                || state.actionPulls.Length != _pulls.Length)
                return;

            for (int i = 0; i < state.designMatrices.Length; i++)
                if (!IsFinite(state.designMatrices[i])) return;
            for (int i = 0; i < state.rewardVectors.Length; i++)
                if (!IsFinite(state.rewardVectors[i])) return;
            for (int i = 0; i < state.actionPulls.Length; i++)
                if (state.actionPulls[i] < 0) return;

            for (int action = 0; action < ActionCount; action++)
            {
                int diagonal = action * FeatureCount * FeatureCount;
                for (int feature = 0; feature < FeatureCount; feature++)
                {
                    float value = state.designMatrices[
                        diagonal + feature * FeatureCount + feature];
                    if (!IsFinite(value) || value < MinDiagonal) return;
                }
            }

            Array.Copy(state.designMatrices, _design, _design.Length);
            Array.Copy(state.rewardVectors, _reward, _reward.Length);
            Array.Copy(state.actionPulls, _pulls, _pulls.Length);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "LinUCB director state could not be loaded: "
                + exception.Message);
        }
    }

    private float[] SolveForAction(int action, float[] rightHandSide)
    {
        float[,] lower = new float[FeatureCount, FeatureCount];
        int matrixBase = action * FeatureCount * FeatureCount;
        for (int row = 0; row < FeatureCount; row++)
        {
            for (int column = 0; column <= row; column++)
            {
                float sum = _design[
                    matrixBase + row * FeatureCount + column];
                for (int k = 0; k < column; k++)
                    sum -= lower[row, k] * lower[column, k];

                if (row == column)
                    lower[row, column] =
                        Mathf.Sqrt(Mathf.Max(MinDiagonal, sum));
                else
                    lower[row, column] =
                        sum / Mathf.Max(MinDiagonal, lower[column, column]);
            }
        }

        float[] intermediate = new float[FeatureCount];
        for (int row = 0; row < FeatureCount; row++)
        {
            float sum = rightHandSide[row];
            for (int column = 0; column < row; column++)
                sum -= lower[row, column] * intermediate[column];
            intermediate[row] =
                sum / Mathf.Max(MinDiagonal, lower[row, row]);
        }

        float[] result = new float[FeatureCount];
        for (int row = FeatureCount - 1; row >= 0; row--)
        {
            float sum = intermediate[row];
            for (int column = row + 1; column < FeatureCount; column++)
                sum -= lower[column, row] * result[column];
            result[row] =
                sum / Mathf.Max(MinDiagonal, lower[row, row]);
        }
        return result;
    }

    private float[] GetRewardVector(int action)
    {
        float[] result = new float[FeatureCount];
        Array.Copy(_reward, action * FeatureCount,
            result, 0, FeatureCount);
        return result;
    }

    private static float Dot(float[] left, float[] right)
    {
        float result = 0f;
        for (int i = 0; i < FeatureCount; i++)
            result += left[i] * right[i];
        return result;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void ValidateAction(int action)
    {
        if (action < 0 || action >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(action));
    }

    private static void ValidateContext(float[] context)
    {
        if (context == null || context.Length != FeatureCount)
            throw new ArgumentException(
                "LinUCB context must contain five features.",
                nameof(context));
    }
}
