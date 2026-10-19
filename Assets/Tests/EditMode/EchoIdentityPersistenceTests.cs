using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EchoIdentityPersistenceTests
{
    private static readonly string[] StaticFields =
    {
        "_data",
        "_initialized",
        "_singleContractData",
        "_singleContractInitialized",
        "_singleContractActiveSlot",
        "_singleContractGeneration",
        "_trainingResetInProgress",
        "_trainingWritesEnabled",
        "<LoadedExistingSingleContractArchive>k__BackingField",
        "<MigratedLegacySingleContractIdentity>k__BackingField",
        "<RecoveredSingleContractFromBackup>k__BackingField"
    };

    private readonly Dictionary<string, object> _previousStaticValues =
        new Dictionary<string, object>();
    private readonly Dictionary<string, string> _previousStrings =
        new Dictionary<string, string>();
    private bool _hadActiveSlot;
    private int _previousActiveSlot;
    private bool _hadResetPending;
    private int _previousResetPending;

    [SetUp]
    public void SetUp()
    {
        CaptureStaticState();
        CapturePreferences();
        ClearSingleContractPreferences();
        SetStatic("_data", new EchoRunSaveData());
        SetStatic("_initialized", true);
        ResetSingleContractCache();
    }

    [TearDown]
    public void TearDown()
    {
        ClearSingleContractPreferences();
        foreach (KeyValuePair<string, string> pair in _previousStrings)
            PlayerPrefs.SetString(pair.Key, pair.Value);
        if (_hadActiveSlot)
        {
            PlayerPrefs.SetInt(
                EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
                _previousActiveSlot);
        }
        if (_hadResetPending)
        {
            PlayerPrefs.SetInt(EchoRunSaveSystem.TrainingResetPendingKey,
                _previousResetPending);
        }
        PlayerPrefs.Save();
        RestoreStaticState();
    }

    [Test]
    public void FailedSettlementPreservesIdentityJsonAndIncrementsRetry()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        string identityBefore = EchoRunSaveSystem.GetActiveEchoIdentity().ToJson();
        string hashBefore = StableHash.ComputeHex(identityBefore);

        SaveCommitResult failed =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "failure-2",
                    runSequence = 2,
                    endReason = RunEndReason.Collision,
                    hasActiveOpponent = true,
                    playerWon = false,
                    playerLead = -3f
                });

        Assert.IsTrue(failed.succeeded, failed.error);
        EchoSingleContractSaveData archive =
            EchoRunSaveSystem.GetSingleContractSaveData();
        Assert.AreEqual(identityBefore, archive.activeIdentity.ToJson());
        Assert.AreEqual(hashBefore, archive.activeIdentity.ComputeHash());
        Assert.AreEqual(identity.identityId, archive.retryState.identityId);
        Assert.AreEqual(identity.memoryContract.contractId,
            archive.retryState.contractId);
        Assert.AreEqual(1, archive.retryState.attemptCount);
        Assert.AreEqual(1, archive.lastResult.generationBefore);
        Assert.AreEqual(1, archive.lastResult.generationAfter);
    }

    [Test]
    public void FailedRunCannotPromoteIdentity()
    {
        ActiveEchoIdentity parent = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(parent, "calibration-1", 1).succeeded);
        ActiveEchoIdentity promoted = CreateIdentity(
            2, parent.identityId, 2, 0);

        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "collision-2",
                    runSequence = 2,
                    endReason = RunEndReason.Collision,
                    hasActiveOpponent = true,
                    playerWon = false,
                    promotedIdentity = promoted
                });

        Assert.IsFalse(result.succeeded);
        Assert.AreEqual(parent.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
    }

    [Test]
    public void NegativeLeadCannotPromoteEvenWhenCallerClaimsVictory()
    {
        ActiveEchoIdentity parent = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(parent, "calibration-1", 1).succeeded);
        ActiveEchoIdentity promoted = CreateIdentity(
            2, parent.identityId, 2, 0);
        string identityBefore =
            EchoRunSaveSystem.GetActiveEchoIdentity().ToJson();

        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "fake-victory-2",
                    runSequence = 2,
                    endReason = RunEndReason.FinishReached,
                    hasActiveOpponent = true,
                    playerWon = true,
                    playerLead = -0.01f,
                    promotedIdentity = promoted
                });

        Assert.IsFalse(result.succeeded);
        Assert.AreEqual(identityBefore,
            EchoRunSaveSystem.GetActiveEchoIdentity().ToJson());
    }

    [Test]
    public void FormalRouteIdentityCannotClaimCalibrationPromotion()
    {
        ActiveEchoIdentity parent = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(parent, "calibration-1", 1).succeeded);
        ActiveEchoIdentity promoted = CreateIdentity(
            2, parent.identityId, 2, 1);

        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "fake-calibration-2",
                    runSequence = 2,
                    endReason = RunEndReason.FinishReached,
                    hasActiveOpponent = false,
                    calibrationCompleted = true,
                    playerWon = true,
                    playerLead = 2f,
                    promotedIdentity = promoted
                });

        Assert.IsFalse(result.succeeded);
        Assert.AreEqual(parent.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
    }

    [Test]
    public void CompatibilityIdentityMayPromoteThroughCalibration()
    {
        ActiveEchoIdentity compatibility =
            CreateCompatibilityIdentity(1, "", 1);
        InstallSingleContractArchive(compatibility);
        ActiveEchoIdentity promoted = CreateIdentity(
            2, compatibility.identityId, 2, 1);

        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "compatibility-calibration-2",
                    runSequence = 2,
                    endReason = RunEndReason.FinishReached,
                    hasActiveOpponent = false,
                    calibrationCompleted = true,
                    playerWon = false,
                    playerLead = -10f,
                    promotedIdentity = promoted
                });

        Assert.IsTrue(result.succeeded, result.error);
        Assert.IsTrue(result.identityPromoted);
        Assert.AreEqual(promoted.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
        Assert.IsFalse(EchoRunSaveSystem.GetSingleContractSaveData()
            .lastResult.playerWon,
            "Calibration arrival is not a challenge victory.");
    }

    [Test]
    public void LowConfidenceChoicesDoNotCreatePreciseRouteMemory()
    {
        var choices = new GateChoiceAccumulator();
        Assert.IsTrue(choices.Record(1, 0, true));
        Assert.IsTrue(choices.Record(2, 1, true));
        Assert.IsTrue(choices.Record(3, 0, true));
        Assert.IsTrue(choices.Record(4, 1, true));
        Assert.IsTrue(choices.Record(5, 2, true));

        Assert.IsFalse(choices.TryBuildMemoryContract(out _));
        var vague = new EchoMemoryContract
        {
            preferredLane = 2,
            confidence = 0.4f,
            evidenceCount = 2
        };
        StringAssert.Contains("记忆模糊", vague.BuildMemoryText());
        StringAssert.DoesNotContain("右侧", vague.BuildMemoryText());
    }

    [Test]
    public void PromotionRejectsWrongParentAndDuplicateTransactionIsIdempotent()
    {
        ActiveEchoIdentity parent = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(parent, "calibration-1", 1).succeeded);
        ActiveEchoIdentity wrong = CreateIdentity(2, "wrong-parent", 2, 0);

        SaveCommitResult rejected = CommitIdentity(wrong, "victory-2", 2);

        Assert.IsFalse(rejected.succeeded);
        Assert.AreEqual(parent.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);

        ActiveEchoIdentity promoted = CreateIdentity(
            2, parent.identityId, 2, 0);
        RunSettlementCommit settlement = Settlement(
            promoted, "victory-2", 2);
        SaveCommitResult committed =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(settlement);
        Assert.IsTrue(committed.succeeded, committed.error);
        Assert.IsTrue(committed.identityPromoted);
        Assert.AreEqual(2, committed.activeIdentity.generation);
        Assert.AreEqual(parent.identityId,
            committed.activeIdentity.parentIdentityId);
        Assert.AreEqual(committed.activeIdentity.identityId,
            committed.activeIdentity.memoryContract.identityId);

        string slotA = PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotAKey, "");
        string slotB = PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotBKey, "");
        int activeSlot = PlayerPrefs.GetInt(
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey, -1);
        SaveCommitResult duplicate =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(settlement);

        Assert.IsTrue(duplicate.succeeded, duplicate.error);
        Assert.IsTrue(duplicate.alreadyCommitted);
        Assert.IsTrue(duplicate.identityPromoted);
        Assert.AreEqual(slotA, PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotAKey, ""));
        Assert.AreEqual(slotB, PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotBKey, ""));
        Assert.AreEqual(activeSlot, PlayerPrefs.GetInt(
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey, -1));
    }

    [Test]
    public void ContradictoryOpponentOrVictoryClaimsCannotPromoteIdentity()
    {
        ActiveEchoIdentity parent = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(parent, "calibration-1", 1).succeeded);
        ActiveEchoIdentity promoted = CreateIdentity(
            2, parent.identityId, 2, 0);

        SaveCommitResult missingOpponent =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "missing-opponent-2",
                    runSequence = 2,
                    endReason = RunEndReason.FinishReached,
                    hasActiveOpponent = false,
                    playerWon = false,
                    playerLead = 2f,
                    promotedIdentity = promoted
                });
        Assert.IsFalse(missingOpponent.succeeded);
        StringAssert.Contains("active opponent", missingOpponent.error);

        SaveCommitResult deniedPhysicalVictory =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "denied-victory-2",
                    runSequence = 2,
                    endReason = RunEndReason.FinishReached,
                    hasActiveOpponent = true,
                    playerWon = false,
                    playerLead = 2f,
                    promotedIdentity = promoted
                });
        Assert.IsFalse(deniedPhysicalVictory.succeeded);
        StringAssert.Contains("Claimed victory",
            deniedPhysicalVictory.error);
        Assert.AreEqual(parent.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
    }

    [Test]
    public void CorruptNewestSlotFallsBackToPreviousVerifiedIdentity()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        Assert.IsTrue(EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "failure-2",
                runSequence = 2,
                endReason = RunEndReason.Collision,
                hasActiveOpponent = true
            }).succeeded);
        string activeKey = GetActiveSlotKey();
        PlayerPrefs.SetString(activeKey, "{\"schemaVersion\":1");
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        EchoSingleContractSaveData recovered =
            EchoRunSaveSystem.GetSingleContractSaveData();
        Assert.AreEqual(identity.identityId,
            recovered.activeIdentity.identityId);
        Assert.AreEqual(1, recovered.lastCommittedRunSequence);
    }

    [Test]
    public void HigherGenerationInactiveCandidateIsNotPublished()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        string inactiveKey = GetInactiveSlotKey();
        EchoSingleContractSaveEnvelope inactive =
            JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(
                PlayerPrefs.GetString(inactiveKey));
        inactive.generation += 1000;
        inactive.checksum = ComputeEnvelopeChecksum(inactive);
        PlayerPrefs.SetString(inactiveKey, JsonUtility.ToJson(inactive));
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsFalse(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        Assert.AreEqual(identity.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId,
            "An inactive candidate is not committed until ActiveSlot publishes it.");
    }

    [Test]
    public void GenerationMutationInvalidatesV2EnvelopeAndUsesBackup()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        Assert.IsTrue(EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "failure-2",
                runSequence = 2,
                endReason = RunEndReason.Collision,
                hasActiveOpponent = true,
                playerWon = false,
                playerLead = -1f
            }).succeeded);
        string activeKey = GetActiveSlotKey();
        EchoSingleContractSaveEnvelope envelope =
            JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(
                PlayerPrefs.GetString(activeKey));
        Assert.AreEqual(2, envelope.schemaVersion);
        envelope.generation++;
        PlayerPrefs.SetString(activeKey, JsonUtility.ToJson(envelope));
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        Assert.AreEqual(identity.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
        Assert.AreEqual(1, EchoRunSaveSystem.GetSingleContractSaveData()
            .lastCommittedRunSequence);
    }

    [Test]
    public void ChecksumValidIdentityContractMismatchFallsBackToBackup()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        Assert.IsTrue(EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "failure-2",
                runSequence = 2,
                endReason = RunEndReason.Collision,
                hasActiveOpponent = true
            }).succeeded);

        string activeKey = GetActiveSlotKey();
        EchoSingleContractSaveEnvelope envelope =
            JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(
                PlayerPrefs.GetString(activeKey));
        EchoSingleContractSaveData corrupted =
            JsonUtility.FromJson<EchoSingleContractSaveData>(envelope.payload);
        corrupted.retryState.identityId = "wrong-identity";
        envelope.payload = JsonUtility.ToJson(corrupted);
        envelope.checksum = ComputeEnvelopeChecksum(envelope);
        PlayerPrefs.SetString(activeKey, JsonUtility.ToJson(envelope));
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        EchoSingleContractSaveData recovered =
            EchoRunSaveSystem.GetSingleContractSaveData();
        Assert.AreEqual(identity.identityId,
            recovered.activeIdentity.identityId);
        Assert.AreEqual(1, recovered.lastCommittedRunSequence);
    }

    [Test]
    public void ChecksumValidIdentityContentIdMismatchFallsBackToBackup()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        Assert.IsTrue(CommitIdentity(identity, "calibration-1", 1).succeeded);
        Assert.IsTrue(EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "failure-2",
                runSequence = 2,
                endReason = RunEndReason.Collision,
                hasActiveOpponent = true,
                playerWon = false,
                playerLead = -1f
            }).succeeded);

        string activeKey = GetActiveSlotKey();
        EchoSingleContractSaveEnvelope envelope =
            JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(
                PlayerPrefs.GetString(activeKey));
        EchoSingleContractSaveData corrupted =
            JsonUtility.FromJson<EchoSingleContractSaveData>(envelope.payload);
        string staleIdentityId = corrupted.activeIdentity.identityId;
        corrupted.activeIdentity.pace += 3f;
        Assert.AreEqual(staleIdentityId,
            corrupted.activeIdentity.identityId);
        envelope.payload = JsonUtility.ToJson(corrupted);
        envelope.checksum = ComputeEnvelopeChecksum(envelope);
        PlayerPrefs.SetString(activeKey, JsonUtility.ToJson(envelope));
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        Assert.AreEqual(identity.identityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId);
        Assert.AreEqual(1, EchoRunSaveSystem.GetSingleContractSaveData()
            .lastCommittedRunSequence);
    }

    [Test]
    public void MaximumGenerationRejectsFurtherCommitWithoutChangingIdentity()
    {
        ActiveEchoIdentity identity = CreateIdentity(1, "", 1, 2);
        InstallSingleContractArchive(identity);
        SetStatic("_singleContractGeneration", long.MaxValue);
        string identityBefore =
            EchoRunSaveSystem.GetActiveEchoIdentity().ToJson();

        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "overflow-2",
                    runSequence = 2,
                    endReason = RunEndReason.Collision,
                    hasActiveOpponent = true,
                    playerWon = false,
                    playerLead = -1f
                });

        Assert.IsFalse(result.succeeded);
        StringAssert.Contains("generation", result.error.ToLowerInvariant());
        Assert.AreEqual(identityBefore,
            EchoRunSaveSystem.GetActiveEchoIdentity().ToJson());
    }

    [Test]
    public void V9MigrationUsesOnlyFrozenSnapshotAndRunsOnce()
    {
        AIShadowSequenceState sequence =
            new AIShadowSequencePolicy().ExportState();
        var frozenStyle = new PlayerStyleData
        {
            lanePreference = 0.75f,
            laneSamples = 12,
            rhythmStability = 0.8f,
            rhythmSamples = 6
        };
        var frozen = new EchoGenerationSnapshot
        {
            generation = 3,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions,
            sequencePairCount = sequence.pairCount,
            styleJson = JsonUtility.ToJson(frozenStyle),
            pace = 13.5f,
            clarity = 0.8f
        };
        string frozenJson = frozen.ToJson();
        var legacy = new LegacyShadowProfileV5MigrationData
        {
            generation = 99,
            weights = new[] { 999f },
            sequenceTransitions = new[] { 999f },
            pace = 99f,
            clarity = 0.1f,
            activeGenerationJson = frozenJson
        };
        SetStatic("_data", new EchoRunSaveData
        {
            runSequence = 17,
            shadowProfileJson = JsonUtility.ToJson(legacy),
            playerStyleJson = JsonUtility.ToJson(new PlayerStyleData
            {
                lanePreference = -1f,
                laneSamples = 99
            }),
            lastEchoContractJson = "{\"contractId\":\"wrong\"}"
        });
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.MigratedLegacySingleContractIdentity);
        EchoSingleContractSaveData migrated =
            EchoRunSaveSystem.GetSingleContractSaveData();
        Assert.AreEqual(3, migrated.activeIdentity.generation);
        Assert.AreEqual(13.5f, migrated.activeIdentity.pace, 0.001f);
        Assert.AreEqual(0.75f,
            migrated.activeIdentity.GetPlayerStyle().lanePreference, 0.001f);
        CollectionAssert.AreEqual(frozen.policyWeights,
            migrated.activeIdentity.policyWeights);
        Assert.IsNull(migrated.activeIdentity.memoryContract,
            "A six-phase contract must not migrate into route memory.");
        Assert.AreEqual("v9:" + StableHash.ComputeHex(frozenJson)
                .ToLowerInvariant(),
            migrated.importedLegacyFingerprint);
        string migratedIdentityId = migrated.activeIdentity.identityId;

        string migratedActiveKey = GetActiveSlotKey();
        Assert.IsFalse(string.IsNullOrEmpty(PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotAKey, "")));
        Assert.IsFalse(string.IsNullOrEmpty(PlayerPrefs.GetString(
            EchoRunSaveSystem.SingleContractSaveSlotBKey, "")));
        PlayerPrefs.SetString(migratedActiveKey, "{\"schemaVersion\":2");
        PlayerPrefs.Save();
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredSingleContractFromBackup);
        Assert.AreEqual(migratedIdentityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId,
            "The first migration must seed a recoverable second slot.");

        var replacement = new EchoGenerationSnapshot
        {
            generation = 8,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions,
            pace = 25f,
            clarity = 1f
        };
        SetStatic("_data", new EchoRunSaveData
        {
            shadowProfileJson = JsonUtility.ToJson(
                new LegacyShadowProfileV5MigrationData
                {
                    activeGenerationJson = replacement.ToJson()
                })
        });
        ResetSingleContractCache();

        EchoRunSaveSystem.EnsureSingleContractInitialized();

        Assert.IsTrue(EchoRunSaveSystem.LoadedExistingSingleContractArchive);
        Assert.IsFalse(EchoRunSaveSystem.MigratedLegacySingleContractIdentity);
        Assert.AreEqual(migratedIdentityId,
            EchoRunSaveSystem.GetActiveEchoIdentity().identityId,
            "The migration tombstone must prevent a second import.");
    }

    private static SaveCommitResult CommitIdentity(ActiveEchoIdentity identity,
        string transactionId, int runSequence)
    {
        return EchoRunSaveSystem.TryCommitSingleContractSettlement(
            Settlement(identity, transactionId, runSequence));
    }

    private static RunSettlementCommit Settlement(ActiveEchoIdentity identity,
        string transactionId, int runSequence)
    {
        return new RunSettlementCommit
        {
            transactionId = transactionId,
            runSequence = runSequence,
            endReason = RunEndReason.FinishReached,
            hasActiveOpponent = identity.generation > 1,
            calibrationCompleted = identity.generation == 1,
            playerWon = identity.generation > 1,
            playerLead = 2f,
            promotedIdentity = identity
        };
    }

    private static ActiveEchoIdentity CreateIdentity(int generation,
        string parentIdentityId, int runSequence, int preferredLane)
    {
        AIShadowSequenceState sequence =
            new AIShadowSequencePolicy().ExportState();
        var identity = new ActiveEchoIdentity
        {
            generation = generation,
            parentIdentityId = parentIdentityId,
            sourceRunSequence = runSequence,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions,
            sequencePairCount = sequence.pairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(
                new PlayerStyleData
                {
                    lanePreference = preferredLane - 1f,
                    laneSamples = 10
                }),
            pace = 12f + generation,
            clarity = 1f,
            memoryContract = new EchoMemoryContract
            {
                contractId = "route-contract-" + generation,
                preferredLane = preferredLane,
                confidence = 1f,
                evidenceCount = 5
            }
        };
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        identity.Normalize();
        Assert.IsTrue(identity.IsSemanticallyValid());
        return identity;
    }

    private static ActiveEchoIdentity CreateCompatibilityIdentity(
        int generation, string parentIdentityId, int runSequence)
    {
        ActiveEchoIdentity identity = CreateIdentity(
            generation, parentIdentityId, runSequence, 1);
        identity.memoryContract = null;
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.Normalize();
        Assert.IsTrue(identity.RequiresCompatibilityCalibration);
        Assert.IsTrue(identity.IsSemanticallyValid());
        return identity;
    }

    private static void InstallSingleContractArchive(
        ActiveEchoIdentity identity)
    {
        var archive = new EchoSingleContractSaveData
        {
            activeIdentity = identity,
            importedLegacyFingerprint = "compatibility-test"
        };
        archive.Normalize();
        Assert.IsTrue(archive.IsSemanticallyValid());
        SetStatic("_singleContractData", archive);
        SetStatic("_singleContractInitialized", true);
        SetStatic("_singleContractActiveSlot", -1);
        SetStatic("_singleContractGeneration", 0L);
    }

    private static string GetActiveSlotKey()
    {
        return PlayerPrefs.GetInt(
                   EchoRunSaveSystem.SingleContractActiveSaveSlotKey, 0) == 0
            ? EchoRunSaveSystem.SingleContractSaveSlotAKey
            : EchoRunSaveSystem.SingleContractSaveSlotBKey;
    }

    private static string GetInactiveSlotKey()
    {
        return PlayerPrefs.GetInt(
                   EchoRunSaveSystem.SingleContractActiveSaveSlotKey, 0) == 0
            ? EchoRunSaveSystem.SingleContractSaveSlotBKey
            : EchoRunSaveSystem.SingleContractSaveSlotAKey;
    }

    private static string ComputeEnvelopeChecksum(
        EchoSingleContractSaveEnvelope envelope)
    {
        if (envelope.schemaVersion == 1)
            return StableHash.ComputeHex(envelope.payload ?? "");
        return StableHash.ComputeHex(envelope.schemaVersion.ToString()
                                     + "\n"
                                     + envelope.generation.ToString()
                                     + "\n"
                                     + (envelope.payload ?? ""));
    }

    private void CapturePreferences()
    {
        CaptureString(EchoRunSaveSystem.SingleContractSaveSlotAKey);
        CaptureString(EchoRunSaveSystem.SingleContractSaveSlotBKey);
        _hadActiveSlot = PlayerPrefs.HasKey(
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey);
        _previousActiveSlot = PlayerPrefs.GetInt(
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey, -1);
        _hadResetPending = PlayerPrefs.HasKey(
            EchoRunSaveSystem.TrainingResetPendingKey);
        _previousResetPending = PlayerPrefs.GetInt(
            EchoRunSaveSystem.TrainingResetPendingKey, 0);
    }

    private void CaptureString(string key)
    {
        if (PlayerPrefs.HasKey(key))
            _previousStrings[key] = PlayerPrefs.GetString(key);
    }

    private static void ClearSingleContractPreferences()
    {
        PlayerPrefs.DeleteKey(EchoRunSaveSystem.SingleContractSaveSlotAKey);
        PlayerPrefs.DeleteKey(EchoRunSaveSystem.SingleContractSaveSlotBKey);
        PlayerPrefs.DeleteKey(
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey);
        PlayerPrefs.DeleteKey(EchoRunSaveSystem.TrainingResetPendingKey);
        PlayerPrefs.Save();
    }

    private void CaptureStaticState()
    {
        foreach (string name in StaticFields)
            _previousStaticValues[name] = GetStaticField(name).GetValue(null);
    }

    private void RestoreStaticState()
    {
        foreach (KeyValuePair<string, object> pair in _previousStaticValues)
            GetStaticField(pair.Key).SetValue(null, pair.Value);
    }

    private static void ResetSingleContractCache()
    {
        SetStatic("_singleContractData", null);
        SetStatic("_singleContractInitialized", false);
        SetStatic("_singleContractActiveSlot", -1);
        SetStatic("_singleContractGeneration", 0L);
        SetStatic("_trainingResetInProgress", false);
        SetStatic("_trainingWritesEnabled", true);
        SetStatic("<LoadedExistingSingleContractArchive>k__BackingField", false);
        SetStatic("<MigratedLegacySingleContractIdentity>k__BackingField", false);
        SetStatic("<RecoveredSingleContractFromBackup>k__BackingField", false);
    }

    private static void SetStatic(string name, object value)
    {
        GetStaticField(name).SetValue(null, value);
    }

    private static FieldInfo GetStaticField(string name)
    {
        FieldInfo field = typeof(EchoRunSaveSystem).GetField(
            name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing save-system field: " + name);
        return field;
    }
}
