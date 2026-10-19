using System;
using UnityEngine;

[Serializable]
public sealed class RunResultSummary
{
    public string transactionId = "";
    public int runSequence;
    public RunEndReason endReason;
    public bool playerWon;
    public float playerLead;
    public int generationBefore;
    public int generationAfter;
    public string activeIdentityId = "";
    public string message = "";

    public RunResultSummary Clone()
    {
        return JsonUtility.FromJson<RunResultSummary>(JsonUtility.ToJson(this));
    }

    public void Normalize()
    {
        transactionId = transactionId ?? "";
        runSequence = Mathf.Max(0, runSequence);
        playerLead = IsFinite(playerLead) ? playerLead : 0f;
        generationBefore = Mathf.Max(0, generationBefore);
        generationAfter = Mathf.Max(0, generationAfter);
        activeIdentityId = activeIdentityId ?? "";
        message = message ?? "";
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class RunSettlementCommit
{
    public string transactionId = "";
    public int runSequence;
    public RunEndReason endReason;
    public bool hasActiveOpponent;
    public bool calibrationCompleted;
    public bool playerWon;
    public float playerLead;
    public string resultMessage = "";
    public ActiveEchoIdentity promotedIdentity;
}

public sealed class SaveCommitResult
{
    public bool succeeded;
    public bool alreadyCommitted;
    public bool identityPromoted;
    public string error = "";
    public ActiveEchoIdentity activeIdentity;

    public static SaveCommitResult Committed(ActiveEchoIdentity identity,
        bool idempotent, bool promoted)
    {
        return new SaveCommitResult
        {
            succeeded = true,
            alreadyCommitted = idempotent,
            identityPromoted = promoted,
            activeIdentity = identity != null ? identity.Clone() : null
        };
    }

    public static SaveCommitResult Failed(string message)
    {
        return new SaveCommitResult
        {
            succeeded = false,
            error = message ?? "Single-contract save could not be committed."
        };
    }
}

[Serializable]
public sealed class EchoSingleContractSaveData
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentGameplayModeVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public int gameplayModeVersion = CurrentGameplayModeVersion;
    public ActiveEchoIdentity activeIdentity;
    public EchoRetryState retryState = new EchoRetryState();
    public RunResultSummary lastResult;
    public string lastTransactionId = "";
    public int lastCommittedRunSequence;
    public string importedLegacyFingerprint = "";

    public EchoSingleContractSaveData Clone()
    {
        EchoSingleContractSaveData clone = JsonUtility.FromJson<
            EchoSingleContractSaveData>(JsonUtility.ToJson(this));
        if (clone == null) clone = new EchoSingleContractSaveData();
        clone.Normalize();
        return clone;
    }

    public void Normalize()
    {
        schemaVersion = CurrentSchemaVersion;
        gameplayModeVersion = CurrentGameplayModeVersion;
        if (IsEmptyIdentityPlaceholder(activeIdentity))
        {
            activeIdentity = null;
        }
        else if (activeIdentity != null)
        {
            activeIdentity = activeIdentity.Clone();
            activeIdentity.Normalize();
        }
        retryState = retryState != null
            ? retryState.Clone() : new EchoRetryState();
        retryState.Normalize();
        if (lastResult != null)
        {
            lastResult = lastResult.Clone();
            lastResult.Normalize();
            if (lastResult.runSequence == 0
                && string.IsNullOrEmpty(lastResult.transactionId)
                && string.IsNullOrEmpty(lastResult.activeIdentityId)
                && string.IsNullOrEmpty(lastResult.message))
                lastResult = null;
        }
        lastTransactionId = lastTransactionId ?? "";
        lastCommittedRunSequence = Mathf.Max(0, lastCommittedRunSequence);
        importedLegacyFingerprint = importedLegacyFingerprint ?? "";
    }

    private static bool IsEmptyIdentityPlaceholder(ActiveEchoIdentity identity)
    {
        return identity != null
               && identity.generation == 0
               && string.IsNullOrEmpty(identity.identityId);
    }

    public bool IsSemanticallyValid()
    {
        return TryValidateSemantics(out _);
    }

    public bool TryValidateSemantics(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion
            || gameplayModeVersion != CurrentGameplayModeVersion)
        {
            error = "Archive version is not supported.";
            return false;
        }
        if (string.IsNullOrEmpty(importedLegacyFingerprint))
        {
            error = "Legacy migration fingerprint is missing.";
            return false;
        }

        if (lastCommittedRunSequence == 0)
        {
            if (!string.IsNullOrEmpty(lastTransactionId)
                || lastResult != null)
            {
                error = "Uncommitted archive contains transaction state.";
                return false;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(lastTransactionId)
                || lastResult == null
                || lastResult.runSequence != lastCommittedRunSequence
                || !string.Equals(lastResult.transactionId,
                    lastTransactionId, StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(RunEndReason), lastResult.endReason))
            {
                error = "Last settlement transaction is inconsistent.";
                return false;
            }
        }

        bool retryEmpty = retryState == null
                          || string.IsNullOrEmpty(retryState.identityId)
                          && string.IsNullOrEmpty(retryState.contractId)
                          && retryState.attemptCount == 0;
        if (activeIdentity == null)
        {
            if (lastResult != null
                && (lastResult.generationAfter != 0
                    || !string.IsNullOrEmpty(lastResult.activeIdentityId)))
            {
                error = "Last result references a missing active identity.";
                return false;
            }
            error = retryEmpty ? "" : "Retry state has no active identity.";
            return retryEmpty;
        }
        if (!ValidateIdentity(activeIdentity))
        {
            error = "Active identity is invalid.";
            return false;
        }
        if (lastResult != null
            && (lastResult.generationAfter != activeIdentity.generation
                || !string.Equals(lastResult.activeIdentityId,
                    activeIdentity.identityId, StringComparison.Ordinal)
                || lastResult.generationBefore > lastResult.generationAfter
                || lastResult.generationAfter - lastResult.generationBefore
                > 1))
        {
            error = "Last result does not match the active identity.";
            return false;
        }

        EchoMemoryContract contract = activeIdentity.memoryContract;
        if (contract == null)
        {
            error = retryEmpty ? "" : "Retry state references missing contract.";
            return retryEmpty;
        }
        if (retryEmpty)
        {
            error = "";
            return true;
        }
        bool retryMatches = string.Equals(retryState.identityId,
                                activeIdentity.identityId,
                                StringComparison.Ordinal)
                            && string.Equals(retryState.contractId,
                                contract.contractId,
                                StringComparison.Ordinal);
        error = retryMatches ? "" : "Retry state references another identity or contract.";
        return retryMatches;
    }

    private static bool ValidateIdentity(ActiveEchoIdentity identity)
    {
        if (identity == null || !identity.IsSemanticallyValid()) return false;
        if (identity.policyWeights == null
            || identity.policyWeights.Length != AIShadowPolicy.ActionCount
                                             * AIShadowPolicy.FeatureCount
            || !AllFinite(identity.policyWeights, false))
            return false;
        if (identity.sequenceTransitions == null
            || identity.sequenceTransitions.Length
            != AIShadowSequencePolicy.ActionCount
               * AIShadowSequencePolicy.ActionCount
            || !AllFinite(identity.sequenceTransitions, true))
            return false;
        if (identity.style == null
            || !IsFinite(identity.style.aggressiveness)
            || !IsFinite(identity.style.jumpTiming)
            || !IsFinite(identity.style.slideFrequency)
            || !IsFinite(identity.style.slideOpportunitySuccess)
            || !IsFinite(identity.style.lanePreference)
            || !IsFinite(identity.style.rhythmStability)
            || !IsFinite(identity.style.recoveryStyle))
            return false;
        bool legacyIdentity = identity.identityId.StartsWith("legacy-",
            StringComparison.Ordinal);
        if (!legacyIdentity
            && !string.Equals(identity.identityId,
                ActiveEchoIdentity.CreateIdentityId(identity),
                StringComparison.Ordinal))
            return false;
        return identity.sourceRunSequence > 0 || legacyIdentity;
    }

    private static bool AllFinite(float[] values, bool requireNonNegative)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (!IsFinite(values[index])
                || requireNonNegative && values[index] < 0f)
                return false;
        }
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class EchoSingleContractSaveEnvelope
{
    public int schemaVersion = 1;
    public long generation;
    public string payload = "";
    public string checksum = "";
}

// Read-only DTO for the nested v5 shadow profile stored inside the v9 archive.
// Migration deliberately consumes only activeGenerationJson.
[Serializable]
public sealed class LegacyShadowProfileV5MigrationData
{
    public int version;
    public int generation;
    public int sampleCount;
    public int activeSampleCount;
    public int[] actionCounts;
    public float pace;
    public float bestProgress;
    public float[] weights;
    public float[] sequenceTransitions;
    public int sequencePairCount;
    public float clarity;
    public string activeGenerationJson = "";
}
