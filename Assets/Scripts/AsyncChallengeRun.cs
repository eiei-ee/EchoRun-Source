using System;

public static class AsyncChallengeRules
{
    public const int CurrentVersion = 1;
    public const float CourseDurationSeconds = 95f;
    public const float StartSpeed = 10f;
    public const float MaximumSpeed = 24f;
    public const float Acceleration = 0.12f;

    public static bool IsSupported(int version) => version == CurrentVersion;
}

// Runtime metadata only. ActiveEchoIdentity remains the sole shadow payload.
public sealed class AsyncChallengeRunParameters
{
    public string challengeId { get; }
    public int rulesVersion { get; }
    public int runSeed { get; }

    public AsyncChallengeRunParameters(string challengeId, int rulesVersion, int runSeed)
    {
        this.challengeId = challengeId;
        this.rulesVersion = rulesVersion;
        this.runSeed = runSeed;
    }

    public bool TryValidate(out string error)
    {
        error = "";
        if (!AsyncChallengeRules.IsSupported(rulesVersion))
            error = "RULES_UNSUPPORTED";
        else if (runSeed <= 0)
            error = "INVALID_RUN_SEED";
        else if (string.IsNullOrEmpty(challengeId) || challengeId.Length > 128)
            error = "INVALID_CHALLENGE_ID";
        else
        {
            foreach (char c in challengeId)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '-' || c == '_') continue;
                error = "INVALID_CHALLENGE_ID";
                break;
            }
        }
        return error.Length == 0;
    }

    public AsyncChallengeRunParameters WithChallengeId(string id) =>
        new AsyncChallengeRunParameters(id, rulesVersion, runSeed);
}

public sealed class AsyncChallengeResult
{
    public string challengeId { get; }
    public int rulesVersion { get; }
    public string opponentIdentityId { get; }
    public int runSeed { get; }
    public float distanceMeters { get; }
    public float playerLeadMeters { get; }
    public RunEndReason reason { get; }
    public bool won { get; }

    public AsyncChallengeResult(AsyncChallengeRunParameters parameters,
        string opponentIdentityId, float distanceMeters, float playerLeadMeters,
        RunEndReason reason, bool won)
    {
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));
        challengeId = parameters.challengeId;
        rulesVersion = parameters.rulesVersion;
        runSeed = parameters.runSeed;
        this.opponentIdentityId = opponentIdentityId;
        this.distanceMeters = distanceMeters;
        this.playerLeadMeters = playerLeadMeters;
        this.reason = reason;
        this.won = won;
    }
}
