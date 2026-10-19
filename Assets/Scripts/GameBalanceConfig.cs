using System;
using UnityEngine;

public enum PowerUpId
{
    None = -1,
    Shield = 0,
    Magnet = 1,
    ScoreBoost = 2,
    TurboStart = 3
}

public enum RunDifficultyLevel
{
    Relaxed = 0,
    Standard = 1,
    Intense = 2
}

public static class RunDifficultySettings
{
    public const string PreferenceKey = "RunDifficulty";
    public const RunDifficultyLevel DefaultLevel = RunDifficultyLevel.Standard;

    public static RunDifficultyLevel Current => Normalize(
        PlayerPrefs.GetInt(PreferenceKey, (int)DefaultLevel));

    public static void Set(RunDifficultyLevel level)
    {
        PlayerPrefs.SetInt(PreferenceKey, (int)Normalize((int)level));
        PlayerPrefs.Save();
    }

    public static RunDifficultyLevel Normalize(int value)
    {
        return (RunDifficultyLevel)Mathf.Clamp(value,
            (int)RunDifficultyLevel.Relaxed,
            (int)RunDifficultyLevel.Intense);
    }

    public static float AdjustDifficulty(float baseDifficulty,
        RunDifficultyLevel level)
    {
        float offset = level == RunDifficultyLevel.Relaxed ? -0.15f
            : level == RunDifficultyLevel.Intense ? 0.22f : 0.08f;
        return Mathf.Clamp01(baseDifficulty + offset);
    }

    public static float AdjustObstacleChance(float baseChance,
        RunDifficultyLevel level)
    {
        float multiplier = level == RunDifficultyLevel.Relaxed ? 0.8f
            : level == RunDifficultyLevel.Intense ? 1.35f : 1.15f;
        float bonus = level == RunDifficultyLevel.Relaxed ? 0f
            : level == RunDifficultyLevel.Intense ? 0.05f : 0.03f;
        return Mathf.Clamp01(baseChance * multiplier + bonus);
    }

    public static int ResolveMaxFreeSegments(int configuredMaximum,
        RunDifficultyLevel level)
    {
        if (level == RunDifficultyLevel.Relaxed)
            return Mathf.Max(3, configuredMaximum);
        return 1;
    }

    public static float ObstacleRecoverySeconds(RunDifficultyLevel level)
    {
        return level == RunDifficultyLevel.Relaxed ? 0.45f
            : level == RunDifficultyLevel.Intense ? 0.15f : 0.3f;
    }

    public static string DisplayName(RunDifficultyLevel level)
    {
        switch (level)
        {
            case RunDifficultyLevel.Relaxed: return "休闲";
            case RunDifficultyLevel.Intense: return "高压";
            default: return "标准";
        }
    }

    public static string Description(RunDifficultyLevel level)
    {
        switch (level)
        {
            case RunDifficultyLevel.Relaxed: return "障碍较少 · 单道为主";
            case RunDifficultyLevel.Intense: return "高密度 · 更短恢复窗";
            default: return "障碍密集 · 保留反应窗";
        }
    }
}

[Serializable]
public sealed class PowerUpBalance
{
    public string id;
    public string displayName;
    public string description;
    public int cost;
    public float duration;
    public float value;
}

[Serializable]
public sealed class GameplayBalance
{
    public float startSpeed = 10f;
    public float maxSpeed = 24f;
    public float speedIncreaseRate = 0.12f;
    public int coinScore = 10;
    public float magnetRadius = 7f;
    public float calibrationCourseDistance = 450f;
    public float challengeCourseDistance = 700f;
    public float calibrationDuration = 75f;
    public float challengeDuration = 190f;
}

[Serializable]
public sealed class TrackBalance
{
    public float obstacleChance = 0.7f;
    public float coinChance = 0.6f;
    public float turnChance = 0.24f;
}

[Serializable]
public sealed class AIBalance
{
    public float shadowLearningRate = 0.08f;
    public int minimumTrainingSamples = 24;
    public int minimumActiveSamples = 6;
    public int minimumJumpSamples = 2;
    public int minimumSlideSamples = 2;
    public float directorExploration = 0.35f;
}

[Serializable]
public sealed class GameBalanceData
{
    public GameplayBalance gameplay = new GameplayBalance();
    public TrackBalance track = new TrackBalance();
    public AIBalance ai = new AIBalance();
    public PowerUpBalance[] powerUps;
}

public static class GameBalanceConfig
{
    private static GameBalanceData _current;

    public static GameBalanceData Current
    {
        get
        {
            if (_current == null) _current = Load();
            return _current;
        }
    }

    public static PowerUpBalance GetPowerUp(PowerUpId id)
    {
        int index = (int)id;
        PowerUpBalance[] definitions = Current.powerUps;
        return definitions != null && index >= 0 && index < definitions.Length
            ? definitions[index]
            : null;
    }

    public static void ReloadForTests()
    {
        _current = null;
    }

    private static GameBalanceData Load()
    {
        TextAsset asset = Resources.Load<TextAsset>("Config/game-balance");
        GameBalanceData data = null;
        if (asset != null)
        {
            try
            {
                data = JsonUtility.FromJson<GameBalanceData>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Game balance config is invalid; using safe defaults. "
                                 + exception.Message);
            }
        }
        if (data == null)
        {
            Debug.LogWarning("Game balance config is missing; using safe defaults.");
            data = new GameBalanceData();
        }
        return Normalize(data);
    }

    private static GameBalanceData Normalize(GameBalanceData data)
    {
        data = data ?? new GameBalanceData();
        data.gameplay = data.gameplay ?? new GameplayBalance();
        data.track = data.track ?? new TrackBalance();
        data.ai = data.ai ?? new AIBalance();
        NormalizeGameplay(data.gameplay);
        NormalizeTrack(data.track);
        NormalizeAI(data.ai);
        data.powerUps = NormalizePowerUps(data.powerUps);
        return data;
    }

    private static PowerUpBalance[] NormalizePowerUps(PowerUpBalance[] source)
    {
        PowerUpBalance[] fallback = CreateDefaultPowerUps();
        var normalized = new PowerUpBalance[fallback.Length];
        for (int expectedIndex = 0; expectedIndex < fallback.Length; expectedIndex++)
        {
            PowerUpBalance expected = fallback[expectedIndex];
            PowerUpBalance candidate = FindById(source, expected.id);
            normalized[expectedIndex] = NormalizePowerUp(candidate, expected,
                expectedIndex == (int)PowerUpId.Shield);
        }
        return normalized;
    }

    private static PowerUpBalance[] CreateDefaultPowerUps()
    {
        return new[]
        {
            new PowerUpBalance { id = "shield", displayName = "相位护盾", description = "抵消一次碰撞", cost = 60, value = 1f },
            new PowerUpBalance { id = "magnet", displayName = "磁轨吸附", description = "自动吸取附近金币", cost = 45, duration = 20f, value = 1f },
            new PowerUpBalance { id = "scoreBoost", displayName = "双倍协议", description = "积分翻倍", cost = 75, duration = 25f, value = 2f },
            new PowerUpBalance { id = "turboStart", displayName = "涡轮起步", description = "以更高速度起跑", cost = 50, duration = 12f, value = 7f }
        };
    }

    private static PowerUpBalance FindById(PowerUpBalance[] source, string id)
    {
        if (source == null) return null;
        foreach (PowerUpBalance candidate in source)
        {
            if (candidate != null && string.Equals(candidate.id, id,
                    StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    private static PowerUpBalance NormalizePowerUp(PowerUpBalance candidate,
        PowerUpBalance fallback, bool instant)
    {
        candidate = candidate ?? fallback;
        return new PowerUpBalance
        {
            id = fallback.id,
            displayName = string.IsNullOrWhiteSpace(candidate.displayName)
                ? fallback.displayName
                : candidate.displayName,
            description = string.IsNullOrWhiteSpace(candidate.description)
                ? fallback.description
                : candidate.description,
            cost = candidate.cost > 0 ? candidate.cost : fallback.cost,
            duration = instant
                ? 0f
                : PositiveOrDefault(candidate.duration, fallback.duration),
            value = PositiveOrDefault(candidate.value, fallback.value)
        };
    }

    private static void NormalizeGameplay(GameplayBalance gameplay)
    {
        gameplay.startSpeed = Mathf.Clamp(
            PositiveOrDefault(gameplay.startSpeed, 10f), 1f, 50f);
        gameplay.maxSpeed = Mathf.Clamp(
            PositiveOrDefault(gameplay.maxSpeed, 40f),
            gameplay.startSpeed, 80f);
        gameplay.speedIncreaseRate = Mathf.Clamp(
            PositiveOrDefault(gameplay.speedIncreaseRate, 0.5f), 0.01f, 10f);
        gameplay.coinScore = Mathf.Clamp(gameplay.coinScore > 0
            ? gameplay.coinScore
            : 10, 1, 1000);
        gameplay.magnetRadius = Mathf.Clamp(
            PositiveOrDefault(gameplay.magnetRadius, 7f), 1f, 30f);
        gameplay.calibrationCourseDistance = Mathf.Clamp(
            PositiveOrDefault(gameplay.calibrationCourseDistance, 450f),
            100f, 5000f);
        gameplay.challengeCourseDistance = Mathf.Clamp(
            PositiveOrDefault(gameplay.challengeCourseDistance, 700f),
            gameplay.calibrationCourseDistance, 10000f);
        gameplay.calibrationDuration = Mathf.Clamp(
            PositiveOrDefault(gameplay.calibrationDuration, 75f), 45f, 120f);
        gameplay.challengeDuration = Mathf.Clamp(
            PositiveOrDefault(gameplay.challengeDuration, 190f),
            120f, 240f);
    }

    private static void NormalizeTrack(TrackBalance track)
    {
        track.obstacleChance = ProbabilityOrDefault(track.obstacleChance, 0.7f);
        track.coinChance = ProbabilityOrDefault(track.coinChance, 0.6f);
        track.turnChance = ProbabilityOrDefault(track.turnChance, 0.24f);
    }

    private static void NormalizeAI(AIBalance ai)
    {
        ai.shadowLearningRate = Mathf.Clamp(
            PositiveOrDefault(ai.shadowLearningRate, 0.08f), 0.001f, 0.5f);
        ai.minimumTrainingSamples = Mathf.Clamp(
            ai.minimumTrainingSamples > 0 ? ai.minimumTrainingSamples : 24,
            1, 10000);
        ai.minimumActiveSamples = Mathf.Clamp(
            ai.minimumActiveSamples > 0 ? ai.minimumActiveSamples : 6,
            1, ai.minimumTrainingSamples);
        ai.minimumJumpSamples = Mathf.Clamp(
            ai.minimumJumpSamples > 0 ? ai.minimumJumpSamples : 2,
            1, ai.minimumActiveSamples);
        ai.minimumSlideSamples = Mathf.Clamp(
            ai.minimumSlideSamples > 0 ? ai.minimumSlideSamples : 2,
            1, ai.minimumActiveSamples);
        ai.directorExploration = ProbabilityOrDefault(
            ai.directorExploration, 0.35f);
    }

    private static float ProbabilityOrDefault(float value, float fallback)
    {
        return IsFinite(value) ? Mathf.Clamp01(value) : fallback;
    }

    private static float PositiveOrDefault(float value, float fallback)
    {
        return IsFinite(value) && value > 0f ? value : fallback;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
