using System;
using UnityEngine;

[Serializable]
public sealed class AITrainingSimulationConfig
{
    public int seed = 20260727;
    public int episodes = 250;
    public int segmentsPerEpisode = 80;
    [Range(0f, 1f)] public float initialPlayerSkill = 0.5f;
    [Range(0f, 0.5f)] public float playerVolatility = 0.08f;
    [Range(0f, 0.5f)] public float explorationRate = 0.12f;
    [Range(0f, 1f)] public float ucbExploration = 0.35f;
    [Range(0.001f, 0.5f)] public float learningRate = 0.06f;
    public bool useLinUcb = true;
}

[Serializable]
public sealed class AITrainingSimulationResult
{
    public int schemaVersion = 1;
    public int seed;
    public int episodes;
    public int totalSegments;
    public string policyType;
    public float meanReward;
    public float survivalRate;
    public float meanDifficulty;
    public float meanPolicyUncertainty;
    public int[] actionCounts;
    public float[] finalWeights;
    public string policyStateJson;
}

[Serializable]
public sealed class AITrainingComparisonResult
{
    public int schemaVersion = 1;
    public AITrainingSimulationResult baseline;
    public AITrainingSimulationResult linUcb;
    public float rewardLift;
    public float survivalLift;
}

public static class AITrainingSimulator
{
    public static AITrainingSimulationResult Run(
        AITrainingSimulationConfig config, float[] initialWeights = null,
        string linUcbStateJson = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        int episodes = Mathf.Max(1, config.episodes);
        int segmentsPerEpisode = Mathf.Max(1, config.segmentsPerEpisode);
        var random = new System.Random(config.seed);
        AITrackPolicy baselinePolicy = config.useLinUcb
            ? null
            : new AITrackPolicy(
                unchecked(config.seed ^ 0x2C9277B5), initialWeights);
        AILinUcbPolicy linUcbPolicy = config.useLinUcb
            ? new AILinUcbPolicy(initialWeights, linUcbStateJson)
            : null;
        int[] actionCounts = new int[AITrackPolicy.ActionCount];
        float totalReward = 0f;
        float totalDifficulty = 0f;
        float totalUncertainty = 0f;
        int survivedSegments = 0;
        int totalSegments = episodes * segmentsPerEpisode;

        for (int episode = 0; episode < episodes; episode++)
        {
            float skill = Mathf.Clamp01(config.initialPlayerSkill
                          + NextGaussian(random) * config.playerVolatility);
            float strain = 0.15f;
            float engagement = 0.55f;

            for (int segment = 0; segment < segmentsPerEpisode; segment++)
            {
                float recordPressure = Mathf.Clamp01(
                    (float)segment / Mathf.Max(1, segmentsPerEpisode - 1));
                float[] context =
                {
                    1f, skill, strain, recordPressure, engagement
                };
                int action;
                if (config.useLinUcb)
                {
                    action = linUcbPolicy.Select(
                        context, config.ucbExploration);
                    totalUncertainty +=
                        linUcbPolicy.LastSelectedUncertainty;
                }
                else
                {
                    action = baselinePolicy.Select(
                        context, true, config.explorationRate);
                }
                actionCounts[action]++;

                float difficulty = DifficultyForAction(action);
                float successChance = Mathf.Clamp01(
                    0.78f + (skill - difficulty) * 0.55f
                    - strain * 0.12f);
                bool survived = random.NextDouble() < successChance;
                if (survived) survivedSegments++;

                float flowMatch = 1f - Mathf.Abs(
                    difficulty - Mathf.Clamp01(skill + 0.08f));
                float boredomPenalty = Mathf.Max(
                    0f, skill - difficulty - 0.1f) * 0.8f;
                float reward = survived
                    ? 0.25f + flowMatch * 0.55f
                      + engagement * 0.15f - boredomPenalty
                    : -1f;
                reward = Mathf.Clamp(reward, -1f, 1f);
                if (config.useLinUcb)
                {
                    linUcbPolicy.Update(action, context, reward,
                        config.learningRate * 12.5f);
                }
                else
                {
                    baselinePolicy.Update(
                        action, context, reward, config.learningRate);
                }

                totalReward += reward;
                totalDifficulty += difficulty;
                strain = Mathf.Clamp01(strain
                         + (survived ? difficulty * 0.04f - 0.03f : 0.3f));
                engagement = Mathf.Clamp01(engagement
                             + (flowMatch - 0.55f) * 0.08f
                             - (survived ? 0f : 0.12f));
                skill = Mathf.Clamp01(skill
                        + (survived ? difficulty * 0.0025f : -0.001f));
            }
        }

        return new AITrainingSimulationResult
        {
            seed = config.seed,
            episodes = episodes,
            totalSegments = totalSegments,
            policyType = config.useLinUcb ? "LinUCB" : "EpsilonGreedy",
            meanReward = totalReward / totalSegments,
            survivalRate = (float)survivedSegments / totalSegments,
            meanDifficulty = totalDifficulty / totalSegments,
            meanPolicyUncertainty = config.useLinUcb
                ? totalUncertainty / totalSegments
                : 0f,
            actionCounts = actionCounts,
            finalWeights = config.useLinUcb
                ? linUcbPolicy.ExportWeights()
                : baselinePolicy.ExportWeights(),
            policyStateJson = config.useLinUcb
                ? linUcbPolicy.ExportStateJson()
                : ""
        };
    }

    public static AITrainingComparisonResult Compare(
        AITrainingSimulationConfig config, float[] initialWeights = null,
        string linUcbStateJson = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        AITrainingSimulationConfig baselineConfig = CloneConfig(config);
        baselineConfig.useLinUcb = false;
        AITrainingSimulationConfig linUcbConfig = CloneConfig(config);
        linUcbConfig.useLinUcb = true;

        AITrainingSimulationResult baseline =
            Run(baselineConfig, initialWeights);
        AITrainingSimulationResult linUcb =
            Run(linUcbConfig, initialWeights, linUcbStateJson);
        return new AITrainingComparisonResult
        {
            baseline = baseline,
            linUcb = linUcb,
            rewardLift = linUcb.meanReward - baseline.meanReward,
            survivalLift = linUcb.survivalRate - baseline.survivalRate
        };
    }

    public static float DifficultyForAction(int action)
    {
        switch (action)
        {
            case 0: return 0.25f;
            case 1: return 0.48f;
            case 2: return 0.72f;
            case 3: return 0.86f;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private static float NextGaussian(System.Random random)
    {
        double u1 = Math.Max(double.Epsilon, random.NextDouble());
        double u2 = random.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1))
                       * Math.Cos(2.0 * Math.PI * u2));
    }

    private static AITrainingSimulationConfig CloneConfig(
        AITrainingSimulationConfig source)
    {
        return new AITrainingSimulationConfig
        {
            seed = source.seed,
            episodes = source.episodes,
            segmentsPerEpisode = source.segmentsPerEpisode,
            initialPlayerSkill = source.initialPlayerSkill,
            playerVolatility = source.playerVolatility,
            explorationRate = source.explorationRate,
            ucbExploration = source.ucbExploration,
            learningRate = source.learningRate,
            useLinUcb = source.useLinUcb
        };
    }
}
