using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TrainingResetTests
{
    private const string LegacyShadowKey = "AIShadowProfileV1";

    private static readonly string[] StringKeys =
    {
        EchoRunSaveSystem.SaveKey,
        EchoRunSaveSystem.SaveSlotAKey,
        EchoRunSaveSystem.SaveSlotBKey,
        EchoRunSaveSystem.SingleContractSaveSlotAKey,
        EchoRunSaveSystem.SingleContractSaveSlotBKey,
        EchoRunSaveSystem.TelemetryKey,
        LegacyShadowKey
    };

    private static readonly string[] IntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey,
        EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
        EchoRunSaveSystem.TrainingResetPendingKey,
        "HighScore",
        "TotalCoins",
        "TargetFrameRate",
        "AudioMuted",
        "CharacterPreset",
        RunDifficultySettings.PreferenceKey
    };

    private static readonly string[] FloatKeys =
    {
        "MasterVolume",
        "MusicVolume",
        "SfxVolume"
    };

    private static readonly string[] StaticFields =
    {
        "_data", "_initialized", "_activeSlot", "_generation",
        "_singleContractData", "_singleContractInitialized",
        "_singleContractActiveSlot", "_singleContractGeneration",
        "_trainingResetInProgress",
        "_trainingWritesEnabled",
        "<LoadedExistingArchive>k__BackingField",
        "<MigratedLegacyData>k__BackingField",
        "<RecoveredFromBackup>k__BackingField",
        "<LoadedExistingSingleContractArchive>k__BackingField",
        "<MigratedLegacySingleContractIdentity>k__BackingField",
        "<RecoveredSingleContractFromBackup>k__BackingField"
    };

    private readonly Dictionary<string, string> _strings =
        new Dictionary<string, string>();
    private readonly Dictionary<string, int> _ints =
        new Dictionary<string, int>();
    private readonly Dictionary<string, float> _floats =
        new Dictionary<string, float>();
    private readonly Dictionary<string, object> _staticValues =
        new Dictionary<string, object>();

    [SetUp]
    public void SetUp()
    {
        foreach (string key in StringKeys)
        {
            if (PlayerPrefs.HasKey(key))
                _strings[key] = PlayerPrefs.GetString(key);
            PlayerPrefs.DeleteKey(key);
        }
        foreach (string key in IntKeys)
        {
            if (PlayerPrefs.HasKey(key))
                _ints[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(key);
        }
        foreach (string key in FloatKeys)
        {
            if (PlayerPrefs.HasKey(key))
                _floats[key] = PlayerPrefs.GetFloat(key);
            PlayerPrefs.DeleteKey(key);
        }
        foreach (string field in StaticFields)
            _staticValues[field] = SaveField(field).GetValue(null);

        PlayerPrefs.SetInt("HighScore", 123);
        PlayerPrefs.SetInt("TotalCoins", 456);
        PlayerPrefs.Save();
        InstallEmptyCaches();
        SeedTrainingData();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string key in StringKeys)
            PlayerPrefs.DeleteKey(key);
        foreach (string key in IntKeys)
            PlayerPrefs.DeleteKey(key);
        foreach (string key in FloatKeys)
            PlayerPrefs.DeleteKey(key);
        foreach (KeyValuePair<string, string> pair in _strings)
            PlayerPrefs.SetString(pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in _ints)
            PlayerPrefs.SetInt(pair.Key, pair.Value);
        foreach (KeyValuePair<string, float> pair in _floats)
            PlayerPrefs.SetFloat(pair.Key, pair.Value);
        PlayerPrefs.Save();

        foreach (KeyValuePair<string, object> pair in _staticValues)
            SaveField(pair.Key).SetValue(null, pair.Value);
    }

    [Test]
    public void CommitTrainingResetClearsBothArchivesAndPreservesProgress()
    {
        Assert.IsNotNull(EchoRunSaveSystem.GetActiveEchoIdentity());

        bool committed = EchoRunSaveSystem.CommitTrainingReset();

        Assert.IsTrue(committed);
        Assert.IsFalse(PlayerPrefs.HasKey(
            EchoRunSaveSystem.TrainingResetPendingKey));
        AssertAllTrainingEmpty();
        Assert.AreEqual(123, PlayerPrefs.GetInt("HighScore"));
        Assert.AreEqual(456, EchoRunSaveSystem.TotalCoins);
        AssertLegacySlotEmpty(EchoRunSaveSystem.SaveSlotAKey);
        AssertLegacySlotEmpty(EchoRunSaveSystem.SaveSlotBKey);
        AssertSingleContractSlotEmpty(
            EchoRunSaveSystem.SingleContractSaveSlotAKey);
        AssertSingleContractSlotEmpty(
            EchoRunSaveSystem.SingleContractSaveSlotBKey);

        EchoRunSaveSystem.SaveShadowProfile("late-shadow");
        EchoRunSaveSystem.SaveDirector(new[] { 9f }, 9, "late-director");
        EchoRunSaveSystem.SavePlayerStyle("late-style");
        EchoRunSaveSystem.SaveLastRunTelemetry("late-telemetry");
        AssertAllTrainingEmpty();

        ResetCachesForRecovery();
        Assert.AreEqual(456, EchoRunSaveSystem.TotalCoins);
        Assert.AreEqual(123, PlayerPrefs.GetInt("HighScore"));
        AssertAllTrainingEmpty();
    }

    [Test]
    public void PendingResetCompletesBeforeEitherArchiveCanBeRead()
    {
        Assert.IsNotNull(EchoRunSaveSystem.GetActiveEchoIdentity());
        Assert.IsNotEmpty(EchoRunSaveSystem.GetShadowProfileJson());
        PlayerPrefs.SetInt(EchoRunSaveSystem.TrainingResetPendingKey, 1);
        PlayerPrefs.Save();
        ResetCachesForRecovery();

        Assert.IsNull(EchoRunSaveSystem.GetActiveEchoIdentity());

        Assert.IsFalse(PlayerPrefs.HasKey(
            EchoRunSaveSystem.TrainingResetPendingKey));
        AssertAllTrainingEmpty();
        AssertLegacySlotEmpty(EchoRunSaveSystem.SaveSlotAKey);
        AssertLegacySlotEmpty(EchoRunSaveSystem.SaveSlotBKey);
        AssertSingleContractSlotEmpty(
            EchoRunSaveSystem.SingleContractSaveSlotAKey);
        AssertSingleContractSlotEmpty(
            EchoRunSaveSystem.SingleContractSaveSlotBKey);
    }

    [Test]
    public void PendingResetBlocksReadsWhileRecoveryCannotRun()
    {
        PlayerPrefs.SetInt(EchoRunSaveSystem.TrainingResetPendingKey, 1);
        PlayerPrefs.Save();
        SetSaveField("_trainingResetInProgress", true);

        Assert.Throws<System.InvalidOperationException>(
            () => EchoRunSaveSystem.GetShadowProfileJson());
        Assert.Throws<System.InvalidOperationException>(
            () => EchoRunSaveSystem.GetActiveEchoIdentity());

        SetSaveField("_trainingResetInProgress", false);
        Assert.IsTrue(EchoRunSaveSystem.CommitTrainingReset());
        AssertAllTrainingEmpty();
    }

    private static void SeedTrainingData()
    {
        EchoRunSaveSystem.SaveShadowProfile("{\"version\":5}");
        EchoRunSaveSystem.SaveDirector(new[] { 0.25f }, 3, "director");
        EchoRunSaveSystem.SaveSkillProfile("skill");
        EchoRunSaveSystem.SavePlayerStyle("style");
        EchoRunSaveSystem.SaveLastEchoContract("legacy-contract");
        EchoRunSaveSystem.SaveLastRunTelemetry("telemetry");

        ActiveEchoIdentity identity = CreateIdentity();
        SaveCommitResult result =
            EchoRunSaveSystem.TryCommitSingleContractSettlement(
                new RunSettlementCommit
                {
                    transactionId = "calibration-1",
                    runSequence = 1,
                    endReason = RunEndReason.FinishReached,
                    calibrationCompleted = true,
                    playerWon = false,
                    promotedIdentity = identity
                });
        Assert.IsTrue(result.succeeded, result.error);
    }

    private static ActiveEchoIdentity CreateIdentity()
    {
        AIShadowSequenceState sequence =
            new AIShadowSequencePolicy().ExportState();
        var identity = new ActiveEchoIdentity
        {
            generation = 1,
            sourceRunSequence = 1,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions,
            sequencePairCount = sequence.pairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(
                new PlayerStyleData()),
            pace = 13f,
            clarity = 1f,
            memoryContract = new EchoMemoryContract
            {
                contractId = "route-contract-1",
                preferredLane = 2,
                confidence = 1f,
                evidenceCount = 5
            }
        };
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        return identity;
    }

    private static void AssertAllTrainingEmpty()
    {
        Assert.IsEmpty(EchoRunSaveSystem.GetShadowProfileJson());
        Assert.IsNull(EchoRunSaveSystem.GetDirectorWeights());
        Assert.AreEqual(0, EchoRunSaveSystem.DirectorModelUpdateCount);
        Assert.IsEmpty(EchoRunSaveSystem.GetDirectorPolicyJson());
        Assert.IsEmpty(EchoRunSaveSystem.GetSkillProfileJson());
        Assert.IsEmpty(EchoRunSaveSystem.GetPlayerStyleJson());
        Assert.IsEmpty(EchoRunSaveSystem.GetLastEchoContractJson());
        Assert.IsEmpty(EchoRunSaveSystem.GetLastRunTelemetryJson());
        Assert.IsNull(EchoRunSaveSystem.GetActiveEchoIdentity());
        EchoSingleContractSaveData archive =
            EchoRunSaveSystem.GetSingleContractSaveData();
        Assert.IsNull(archive.lastResult);
        Assert.AreEqual(0, archive.lastCommittedRunSequence);
        Assert.IsEmpty(archive.lastTransactionId);
        Assert.AreEqual(0, archive.retryState.attemptCount);
    }

    private static void AssertLegacySlotEmpty(string key)
    {
        EchoRunSaveEnvelope envelope =
            JsonUtility.FromJson<EchoRunSaveEnvelope>(
                PlayerPrefs.GetString(key, ""));
        Assert.IsNotNull(envelope);
        Assert.AreEqual(StableHash.ComputeHex(envelope.payload),
            envelope.checksum);
        EchoRunSaveData data = JsonUtility.FromJson<EchoRunSaveData>(
            envelope.payload);
        Assert.IsNotNull(data);
        Assert.IsEmpty(data.shadowProfileJson);
        Assert.IsTrue(data.directorWeights == null
                      || data.directorWeights.Length == 0);
        Assert.AreEqual(0, data.directorModelUpdateCount);
        Assert.IsEmpty(data.directorPolicyJson);
        Assert.IsEmpty(data.skillProfileJson);
        Assert.IsEmpty(data.playerStyleJson);
        Assert.IsEmpty(data.lastEchoContractJson);
    }

    private static void AssertSingleContractSlotEmpty(string key)
    {
        EchoSingleContractSaveEnvelope envelope =
            JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(
                PlayerPrefs.GetString(key, ""));
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ComputeSingleContractEnvelopeChecksum(envelope),
            envelope.checksum);
        EchoSingleContractSaveData data =
            JsonUtility.FromJson<EchoSingleContractSaveData>(
                envelope.payload);
        data.Normalize();
        Assert.IsTrue(data.IsSemanticallyValid());
        Assert.IsNull(data.activeIdentity);
        Assert.IsNull(data.lastResult);
        Assert.AreEqual(0, data.retryState.attemptCount);
    }

    private static string ComputeSingleContractEnvelopeChecksum(
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

    private static void InstallEmptyCaches()
    {
        SetSaveField("_data", new EchoRunSaveData());
        SetSaveField("_initialized", true);
        SetSaveField("_activeSlot", -1);
        SetSaveField("_generation", 0L);
        ResetSingleContractCache();
        SetSaveField("_trainingResetInProgress", false);
        SetSaveField("_trainingWritesEnabled", true);
    }

    private static void ResetCachesForRecovery()
    {
        SetSaveField("_data", null);
        SetSaveField("_initialized", false);
        SetSaveField("_activeSlot", -1);
        SetSaveField("_generation", 0L);
        ResetSingleContractCache();
        SetSaveField("_trainingResetInProgress", false);
        SetSaveField("_trainingWritesEnabled", true);
    }

    private static void ResetSingleContractCache()
    {
        SetSaveField("_singleContractData", null);
        SetSaveField("_singleContractInitialized", false);
        SetSaveField("_singleContractActiveSlot", -1);
        SetSaveField("_singleContractGeneration", 0L);
    }

    private static void SetSaveField(string name, object value)
    {
        SaveField(name).SetValue(null, value);
    }

    private static FieldInfo SaveField(string name)
    {
        FieldInfo field = typeof(EchoRunSaveSystem).GetField(name,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing save field: " + name);
        return field;
    }
}
