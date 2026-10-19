using System;
using UnityEngine;

[Serializable]
public sealed class EchoRunSaveData
{
    public int version = 10;
    public int highScore;
    public int totalCoins;
    public int targetFrameRate = 60;
    public float masterVolume = 1f;
    public float musicVolume = 0.5f;
    public float sfxVolume = 1f;
    public bool audioMuted;
    public int characterPreset;
    public int runDifficulty = (int)RunDifficultyLevel.Standard;
    public int cameraViewHeight = (int)CameraViewHeight.Low;
    public string shadowProfileJson = "";
    public float[] directorWeights;
    public int directorModelUpdateCount;
    public string directorPolicyJson = "";
    public int runSequence;
    public string lastRunTelemetryJson = "";
    public string skillProfileJson = "";
    public string playerStyleJson = "";
    public string lastEchoContractJson = "";
    public int[] powerUpInventory = new int[4];
    public int selectedPowerUp = -1;
    public long savedAtUtcTicks;
}

[Serializable]
public sealed class EchoRunSaveEnvelope
{
    public int schemaVersion = 1;
    public long generation;
    public string payload = "";
    public string checksum = "";
}

public static class EchoRunSaveSystem
{
    public const string SaveKey = "EchoRunSaveV1";
    public const string SaveSlotAKey = "EchoRunSaveV1.A";
    public const string SaveSlotBKey = "EchoRunSaveV1.B";
    public const string ActiveSaveSlotKey = "EchoRunSaveV1.ActiveSlot";
    public const string SingleContractSaveSlotAKey =
        "EchoRunSingleContractV1.A";
    public const string SingleContractSaveSlotBKey =
        "EchoRunSingleContractV1.B";
    public const string SingleContractActiveSaveSlotKey =
        "EchoRunSingleContractV1.ActiveSlot";
    public const string TrainingResetPendingKey =
        "EchoRunTrainingResetV1.Pending";
    public const string TelemetryKey = "EchoRunLastTelemetryV1";
    public const int CurrentVersion = 10;

    private const string ShadowProfileKey = "AIShadowProfileV1";
    private const int SaveEnvelopeVersion = 1;
    private const int SingleContractSaveEnvelopeVersion = 2;
    private const int LegacySingleContractSaveEnvelopeVersion = 1;
    private const string NoLegacyIdentityTombstone = "v9:none";
    private const string CorruptSingleContractArchiveTombstone =
        "v9:single-contract-archive-corrupt";
    private const string TrainingResetFingerprint = "v9:training-reset";

    private static EchoRunSaveData _data;
    private static bool _initialized;
    private static int _activeSlot = -1;
    private static long _generation;
    private static EchoSingleContractSaveData _singleContractData;
    private static bool _singleContractInitialized;
    private static int _singleContractActiveSlot = -1;
    private static long _singleContractGeneration;
    private static bool _trainingResetInProgress;
    private static bool _trainingWritesEnabled = true;

    public static bool LoadedExistingArchive { get; private set; }
    public static bool MigratedLegacyData { get; private set; }
    public static bool RecoveredFromBackup { get; private set; }
    public static bool LoadedExistingSingleContractArchive { get; private set; }
    public static bool MigratedLegacySingleContractIdentity { get; private set; }
    public static bool RecoveredSingleContractFromBackup { get; private set; }

    public static int DirectorModelUpdateCount
    {
        get
        {
            EnsureInitialized();
            return _data.directorModelUpdateCount;
        }
    }

    public static int TotalCoins
    {
        get
        {
            EnsureInitialized();
            return _data.totalCoins;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();

        if (GameObject.Find("EchoRun Save System") != null) return;
        GameObject host = new GameObject("EchoRun Save System");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<EchoRunSaveLifecycle>();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            EnsurePendingTrainingResetCompleted();
            return;
        }
        _initialized = true;
        LoadedExistingArchive = false;
        MigratedLegacyData = false;
        RecoveredFromBackup = false;
        _activeSlot = -1;
        _generation = 0;

        if (TryLoadBestSlot(out EchoRunSaveData slotData,
                out int slot, out long generation))
        {
            _data = slotData;
            _activeSlot = slot;
            _generation = generation;
            LoadedExistingArchive = true;
            Normalize();
            RestoreLegacyKeys();
            bool movedTelemetry = MoveTelemetryOutOfArchive();
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSlot);
            if (movedTelemetry) WriteArchive(true);
            else PlayerPrefs.Save();
            EnsurePendingTrainingResetCompleted();
            return;
        }

        if (TryLoadLegacyArchive(out EchoRunSaveData legacyData))
        {
            _data = legacyData;
            LoadedExistingArchive = true;
            Normalize();
            RestoreLegacyKeys();
            MoveTelemetryOutOfArchive();
            WriteArchive(true);
            EnsurePendingTrainingResetCompleted();
            return;
        }

        MigratedLegacyData = HasLegacyData();
        _data = new EchoRunSaveData();
        CaptureLegacyKeys();
        WriteArchive(true);
        EnsurePendingTrainingResetCompleted();
    }

    public static string GetShadowProfileJson()
    {
        EnsureInitialized();
        return _data.shadowProfileJson ?? "";
    }

    public static void SaveShadowProfile(string profileJson)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.shadowProfileJson = profileJson ?? "";
        PlayerPrefs.SetString(ShadowProfileKey, _data.shadowProfileJson);
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static float[] GetDirectorWeights()
    {
        EnsureInitialized();
        return _data.directorWeights == null
               || _data.directorWeights.Length == 0
            ? null : Clone(_data.directorWeights);
    }

    public static string GetDirectorPolicyJson()
    {
        EnsureInitialized();
        return _data.directorPolicyJson ?? "";
    }

    public static void SaveDirector(float[] weights, int modelUpdateCount,
        string policyJson = null)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.directorWeights = Clone(weights);
        _data.directorModelUpdateCount = Mathf.Max(0, modelUpdateCount);
        if (policyJson != null)
            _data.directorPolicyJson = policyJson;
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static int ReserveRunSequence()
    {
        EnsureInitialized();
        _trainingWritesEnabled = true;
        _data.runSequence = Mathf.Max(0, _data.runSequence) + 1;
        WriteArchive(true);
        return _data.runSequence;
    }

    public static string GetLastRunTelemetryJson()
    {
        EnsureInitialized();
        return PlayerPrefs.GetString(
            TelemetryKey, _data.lastRunTelemetryJson ?? "");
    }

    public static void SaveLastRunTelemetry(string telemetryJson)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.lastRunTelemetryJson = "";
        string normalized = telemetryJson ?? "";
        if (string.IsNullOrEmpty(normalized))
            PlayerPrefs.DeleteKey(TelemetryKey);
        else
            PlayerPrefs.SetString(TelemetryKey, normalized);
        PlayerPrefs.Save();
    }

    public static string GetSkillProfileJson()
    {
        EnsureInitialized();
        return _data.skillProfileJson ?? "";
    }

    public static void SaveSkillProfile(string profileJson)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.skillProfileJson = profileJson ?? "";
        WriteArchive(true);
    }

    public static string GetPlayerStyleJson()
    {
        EnsureInitialized();
        return _data.playerStyleJson ?? "";
    }

    public static void SavePlayerStyle(string profileJson)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.playerStyleJson = profileJson ?? "";
        WriteArchive(true);
    }

    public static string GetLastEchoContractJson()
    {
        EnsureInitialized();
        return _data.lastEchoContractJson ?? "";
    }

    public static void SaveLastEchoContract(string contractJson)
    {
        EnsureInitialized();
        if (!_trainingWritesEnabled) return;
        _data.lastEchoContractJson = contractJson ?? "";
        WriteArchive(true);
    }

    public static void EnsureSingleContractInitialized()
    {
        EnsureInitialized();
        if (_singleContractInitialized) return;
        _singleContractInitialized = true;
        LoadedExistingSingleContractArchive = false;
        MigratedLegacySingleContractIdentity = false;
        RecoveredSingleContractFromBackup = false;
        _singleContractActiveSlot = -1;
        _singleContractGeneration = 0;

        if (TryLoadBestSingleContractSlot(
                out EchoSingleContractSaveData slotData,
                out int slot, out long generation, out bool recovered))
        {
            _singleContractData = slotData;
            _singleContractActiveSlot = slot;
            _singleContractGeneration = generation;
            LoadedExistingSingleContractArchive = true;
            RecoveredSingleContractFromBackup = recovered;
            PlayerPrefs.SetInt(SingleContractActiveSaveSlotKey, slot);
            PlayerPrefs.Save();
            return;
        }

        bool hadSingleContractArchive =
            PlayerPrefs.HasKey(SingleContractSaveSlotAKey)
            || PlayerPrefs.HasKey(SingleContractSaveSlotBKey);
        EchoSingleContractSaveData initial =
            CreateInitialSingleContractData(!hadSingleContractArchive);
        if (!TrySeedBothSingleContractSlots(initial, out string error))
        {
            _singleContractData = initial;
            Debug.LogError("Single-contract archive could not be initialized: "
                           + error);
        }
    }

    public static EchoSingleContractSaveData GetSingleContractSaveData()
    {
        EnsureSingleContractInitialized();
        return _singleContractData != null
            ? _singleContractData.Clone() : new EchoSingleContractSaveData();
    }

    public static ActiveEchoIdentity GetActiveEchoIdentity()
    {
        EnsureSingleContractInitialized();
        return _singleContractData != null
               && _singleContractData.activeIdentity != null
            ? _singleContractData.activeIdentity.Clone() : null;
    }

    public static SaveCommitResult TryCommitSingleContractSettlement(
        RunSettlementCommit settlement)
    {
        EnsureSingleContractInitialized();
        if (!_trainingWritesEnabled)
            return SaveCommitResult.Failed(
                "Training writes are closed until the next run begins.");
        if (settlement == null)
            return SaveCommitResult.Failed("Settlement is required.");
        if (string.IsNullOrEmpty(settlement.transactionId))
            return SaveCommitResult.Failed("Transaction id is required.");
        if (settlement.runSequence <= 0)
            return SaveCommitResult.Failed("Run sequence must be positive.");
        if (!Enum.IsDefined(typeof(RunEndReason), settlement.endReason)
            || settlement.endReason == RunEndReason.None)
            return SaveCommitResult.Failed("Run end reason is invalid.");
        if (float.IsNaN(settlement.playerLead)
            || float.IsInfinity(settlement.playerLead))
            return SaveCommitResult.Failed("Player lead must be finite.");
        if (_singleContractData == null)
            return SaveCommitResult.Failed(
                "Single-contract archive is not available.");

        if (_singleContractData.lastCommittedRunSequence
            == settlement.runSequence)
        {
            if (string.Equals(_singleContractData.lastTransactionId,
                    settlement.transactionId, StringComparison.Ordinal))
            {
                bool originalCommitPromoted =
                    _singleContractData.lastResult != null
                    && _singleContractData.lastResult.generationAfter
                    > _singleContractData.lastResult.generationBefore;
                return SaveCommitResult.Committed(
                    _singleContractData.activeIdentity, true,
                    originalCommitPromoted);
            }
            return SaveCommitResult.Failed(
                "Run sequence was already committed by another transaction.");
        }
        if (settlement.runSequence
            < _singleContractData.lastCommittedRunSequence)
            return SaveCommitResult.Failed("Run sequence is stale.");
        if (string.Equals(_singleContractData.lastTransactionId,
                settlement.transactionId, StringComparison.Ordinal))
            return SaveCommitResult.Failed(
                "Transaction id was already used by another run.");

        EchoSingleContractSaveData candidate = _singleContractData.Clone();
        string identityBefore = candidate.activeIdentity != null
            ? candidate.activeIdentity.ToJson() : "";
        int generationBefore = candidate.activeIdentity != null
            ? candidate.activeIdentity.generation : 0;
        bool promotesIdentity = settlement.promotedIdentity != null;
        bool requiresCalibration = candidate.activeIdentity == null
                                   || candidate.activeIdentity
                                       .RequiresRouteCalibration;
        bool expectedActiveOpponent = !requiresCalibration;
        if (settlement.hasActiveOpponent != expectedActiveOpponent)
        {
            return SaveCommitResult.Failed(expectedActiveOpponent
                ? "A formal identity settlement requires an active opponent."
                : "Calibration settlement cannot claim an active opponent.");
        }
        bool reachedFinish = settlement.endReason
                             == RunEndReason.FinishReached;
        bool completedCalibration = requiresCalibration && reachedFinish
                                    && settlement.calibrationCompleted;
        bool wonChallenge = !requiresCalibration && reachedFinish
                            && settlement.playerLead >= 0f;
        if (settlement.playerWon != wonChallenge)
        {
            return SaveCommitResult.Failed(
                "Claimed victory does not match opponent, finish, and physical lead.");
        }
        bool earnedPromotion = completedCalibration || wonChallenge;
        if (settlement.calibrationCompleted && !requiresCalibration)
        {
            return SaveCommitResult.Failed(
                "Calibration promotion requires an identity without formal route memory.");
        }
        if (promotesIdentity != earnedPromotion)
        {
            return SaveCommitResult.Failed(earnedPromotion
                ? "A completed winning run must promote exactly one identity."
                : "Only a completed calibration or winning challenge may promote identity.");
        }

        if (promotesIdentity)
        {
            ActiveEchoIdentity promoted = settlement.promotedIdentity.Clone();
            if (!ValidatePromotion(candidate.activeIdentity, promoted,
                    settlement.runSequence, out string promotionError))
                return SaveCommitResult.Failed(promotionError);
            candidate.activeIdentity = promoted;
            candidate.retryState = new EchoRetryState();
        }
        else
        {
            ApplyFailedAttempt(candidate);
        }

        int generationAfter = candidate.activeIdentity != null
            ? candidate.activeIdentity.generation : 0;
        candidate.lastTransactionId = settlement.transactionId;
        candidate.lastCommittedRunSequence = settlement.runSequence;
        candidate.lastResult = new RunResultSummary
        {
            transactionId = settlement.transactionId,
            runSequence = settlement.runSequence,
            endReason = settlement.endReason,
            playerWon = wonChallenge,
            playerLead = settlement.playerLead,
            generationBefore = generationBefore,
            generationAfter = generationAfter,
            activeIdentityId = candidate.activeIdentity != null
                ? candidate.activeIdentity.identityId : "",
            message = settlement.resultMessage ?? ""
        };
        candidate.Normalize();

        if (!candidate.IsSemanticallyValid())
            return SaveCommitResult.Failed(
                "Candidate archive failed semantic validation.");
        if (!promotesIdentity)
        {
            string identityAfter = candidate.activeIdentity != null
                ? candidate.activeIdentity.ToJson() : "";
            if (!string.Equals(identityBefore, identityAfter,
                    StringComparison.Ordinal))
                return SaveCommitResult.Failed(
                    "Failed settlement attempted to mutate active identity.");
        }
        if (!TryWriteSingleContractCandidate(candidate, out string writeError))
            return SaveCommitResult.Failed(writeError);

        return SaveCommitResult.Committed(
            _singleContractData.activeIdentity, false, promotesIdentity);
    }

    public static int[] GetPowerUpInventory()
    {
        EnsureInitialized();
        return CloneInventory(_data.powerUpInventory);
    }

    public static int GetPowerUpCount(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        return index >= 0 && index < _data.powerUpInventory.Length
            ? _data.powerUpInventory[index]
            : 0;
    }

    public static PowerUpId GetSelectedPowerUp()
    {
        EnsureInitialized();
        return Enum.IsDefined(typeof(PowerUpId), _data.selectedPowerUp)
            ? (PowerUpId)_data.selectedPowerUp
            : PowerUpId.None;
    }

    public static bool TryPurchasePowerUp(PowerUpId id, int cost)
    {
        EnsureInitialized();
        int index = (int)id;
        int normalizedCost = Mathf.Max(0, cost);
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.totalCoins < normalizedCost)
            return false;

        _data.totalCoins -= normalizedCost;
        _data.powerUpInventory[index]++;
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        WriteArchive(true);
        return true;
    }

    public static bool SelectPowerUp(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.powerUpInventory[index] <= 0)
            return false;
        _data.selectedPowerUp = index;
        WriteArchive(true);
        return true;
    }

    public static bool ConsumePowerUp(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.powerUpInventory[index] <= 0)
            return false;
        _data.powerUpInventory[index]--;
        _data.selectedPowerUp = -1;
        WriteArchive(true);
        return true;
    }

    public static bool CommitTrainingReset()
    {
        bool wasPending = PlayerPrefs.GetInt(
            TrainingResetPendingKey, 0) != 0;
        try
        {
            EnsureInitialized();
            if (wasPending)
                return PlayerPrefs.GetInt(TrainingResetPendingKey, 0) == 0;
            EnsureSingleContractInitialized();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        PlayerPrefs.SetInt(TrainingResetPendingKey, 1);
        PlayerPrefs.Save();
        return TryCompletePendingTrainingReset();
    }

    public static void ResetAITraining()
    {
        CommitTrainingReset();
    }

    public static void SaveProgress(int highScore, int totalCoins)
    {
        EnsureInitialized();
        _data.highScore = Mathf.Max(0, highScore);
        _data.totalCoins = Mathf.Max(0, totalCoins);
        PlayerPrefs.SetInt("HighScore", _data.highScore);
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static void SaveFrameRate(int targetFrameRate)
    {
        EnsureInitialized();
        _data.targetFrameRate = Mathf.Max(1, targetFrameRate);
        PlayerPrefs.SetInt("TargetFrameRate", _data.targetFrameRate);
        WriteArchive(true);
    }

    public static void SaveAudio(float musicVolume, float sfxVolume, bool flush)
    {
        EnsureInitialized();
        SaveAudio(_data.masterVolume, musicVolume, sfxVolume,
            _data.audioMuted, flush);
    }

    public static void SaveAudio(float masterVolume, float musicVolume,
        float sfxVolume, bool muted, bool flush)
    {
        EnsureInitialized();
        _data.masterVolume = Mathf.Clamp01(masterVolume);
        _data.musicVolume = Mathf.Clamp01(musicVolume);
        _data.sfxVolume = Mathf.Clamp01(sfxVolume);
        _data.audioMuted = muted;
        PlayerPrefs.SetFloat("MasterVolume", _data.masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", _data.musicVolume);
        PlayerPrefs.SetFloat("SfxVolume", _data.sfxVolume);
        PlayerPrefs.SetInt("AudioMuted", _data.audioMuted ? 1 : 0);
        WriteArchive(flush);
    }

    public static void SaveCharacterPreset(int preset)
    {
        EnsureInitialized();
        _data.characterPreset = Mathf.Max(0, preset);
        PlayerPrefs.SetInt("CharacterPreset", _data.characterPreset);
        WriteArchive(true);
    }

    public static void SaveCameraViewHeight(CameraViewHeight height)
    {
        EnsureInitialized();
        _data.cameraViewHeight = (int)CameraViewSettings.Normalize((int)height);
        PlayerPrefs.SetInt(CameraViewSettings.PreferenceKey,
            _data.cameraViewHeight);
        WriteArchive(true);
    }

    public static void SaveLegacyState()
    {
        EnsureInitialized();
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    private static void Normalize()
    {
        int sourceVersion = _data.version;
        _data.version = CurrentVersion;
        _data.highScore = Mathf.Max(0, _data.highScore);
        _data.totalCoins = Mathf.Max(0, _data.totalCoins);
        _data.targetFrameRate = _data.targetFrameRate > 0
            ? _data.targetFrameRate
            : 60;
        if (sourceVersion < 9)
        {
            _data.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            _data.audioMuted = PlayerPrefs.GetInt("AudioMuted", 0) != 0;
        }
        _data.masterVolume = Mathf.Clamp01(_data.masterVolume);
        _data.musicVolume = Mathf.Clamp01(_data.musicVolume);
        _data.sfxVolume = Mathf.Clamp01(_data.sfxVolume);
        _data.characterPreset = Mathf.Max(0, _data.characterPreset);
        if (sourceVersion < 8
            && !PlayerPrefs.HasKey(RunDifficultySettings.PreferenceKey))
            _data.runDifficulty = (int)RunDifficultyLevel.Standard;
        _data.runDifficulty = (int)RunDifficultySettings.Normalize(
            _data.runDifficulty);
        _data.cameraViewHeight = (int)CameraViewSettings.Normalize(
            _data.cameraViewHeight);
        _data.shadowProfileJson = _data.shadowProfileJson ?? "";
        _data.directorWeights = Clone(_data.directorWeights);
        if (_data.directorWeights != null
            && _data.directorWeights.Length == 0)
            _data.directorWeights = null;
        _data.directorModelUpdateCount = Mathf.Max(0, _data.directorModelUpdateCount);
        _data.directorPolicyJson = _data.directorPolicyJson ?? "";
        _data.runSequence = Mathf.Max(0, _data.runSequence);
        _data.lastRunTelemetryJson = _data.lastRunTelemetryJson ?? "";
        _data.skillProfileJson = _data.skillProfileJson ?? "";
        _data.playerStyleJson = _data.playerStyleJson ?? "";
        _data.lastEchoContractJson = _data.lastEchoContractJson ?? "";
        _data.powerUpInventory = CloneInventory(_data.powerUpInventory);
        _data.selectedPowerUp = _data.selectedPowerUp >= 0
                                && _data.selectedPowerUp < _data.powerUpInventory.Length
                                && _data.powerUpInventory[_data.selectedPowerUp] > 0
            ? _data.selectedPowerUp
            : -1;
    }

    private static bool HasLegacyData()
    {
        return PlayerPrefs.HasKey("HighScore")
               || PlayerPrefs.HasKey("TotalCoins")
               || PlayerPrefs.HasKey(ShadowProfileKey)
               || PlayerPrefs.HasKey("TargetFrameRate")
               || PlayerPrefs.HasKey("MasterVolume")
               || PlayerPrefs.HasKey("MusicVolume")
               || PlayerPrefs.HasKey("SfxVolume")
               || PlayerPrefs.HasKey("AudioMuted")
               || PlayerPrefs.HasKey("CharacterPreset")
               || PlayerPrefs.HasKey(RunDifficultySettings.PreferenceKey)
               || PlayerPrefs.HasKey(CameraViewSettings.PreferenceKey);
    }

    private static void CaptureLegacyKeys()
    {
        _data.version = CurrentVersion;
        _data.highScore = Mathf.Max(0,
            PlayerPrefs.GetInt("HighScore", _data.highScore));
        _data.totalCoins = Mathf.Max(0,
            PlayerPrefs.GetInt("TotalCoins", _data.totalCoins));
        _data.targetFrameRate = Mathf.Max(1,
            PlayerPrefs.GetInt("TargetFrameRate", _data.targetFrameRate));
        _data.masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat("MasterVolume", _data.masterVolume));
        _data.musicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat("MusicVolume", _data.musicVolume));
        _data.sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat("SfxVolume", _data.sfxVolume));
        _data.audioMuted = PlayerPrefs.GetInt("AudioMuted",
            _data.audioMuted ? 1 : 0) != 0;
        _data.characterPreset = Mathf.Max(0,
            PlayerPrefs.GetInt("CharacterPreset", _data.characterPreset));
        _data.runDifficulty = (int)RunDifficultySettings.Normalize(
            PlayerPrefs.GetInt(RunDifficultySettings.PreferenceKey,
                _data.runDifficulty));
        _data.cameraViewHeight = (int)CameraViewSettings.Normalize(
            PlayerPrefs.GetInt(CameraViewSettings.PreferenceKey,
                _data.cameraViewHeight));
        _data.shadowProfileJson = PlayerPrefs.GetString(
            ShadowProfileKey, _data.shadowProfileJson ?? "");
    }

    private static void RestoreLegacyKeys()
    {
        PlayerPrefs.SetInt("HighScore", _data.highScore);
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        PlayerPrefs.SetInt("TargetFrameRate", _data.targetFrameRate);
        PlayerPrefs.SetFloat("MasterVolume", _data.masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", _data.musicVolume);
        PlayerPrefs.SetFloat("SfxVolume", _data.sfxVolume);
        PlayerPrefs.SetInt("AudioMuted", _data.audioMuted ? 1 : 0);
        PlayerPrefs.SetInt("CharacterPreset", _data.characterPreset);
        PlayerPrefs.SetInt(RunDifficultySettings.PreferenceKey,
            _data.runDifficulty);
        PlayerPrefs.SetInt(CameraViewSettings.PreferenceKey,
            _data.cameraViewHeight);
        if (!string.IsNullOrEmpty(_data.shadowProfileJson))
            PlayerPrefs.SetString(ShadowProfileKey, _data.shadowProfileJson);
        else
            PlayerPrefs.DeleteKey(ShadowProfileKey);
    }

    private static bool TryLoadBestSlot(out EchoRunSaveData data,
        out int slot, out long generation)
    {
        bool validA = TryReadSlot(SaveSlotAKey,
            out EchoRunSaveData dataA, out long generationA);
        bool validB = TryReadSlot(SaveSlotBKey,
            out EchoRunSaveData dataB, out long generationB);
        int preferred = PlayerPrefs.GetInt(ActiveSaveSlotKey, -1);

        if (!validA && !validB)
        {
            data = null;
            slot = -1;
            generation = 0;
            return false;
        }

        if (validA && validB)
        {
            if (generationA == generationB)
                slot = preferred == 1 ? 1 : 0;
            else
                slot = generationA > generationB ? 0 : 1;
        }
        else
        {
            slot = validA ? 0 : 1;
        }

        data = slot == 0 ? dataA : dataB;
        generation = slot == 0 ? generationA : generationB;
        RecoveredFromBackup = preferred >= 0 && preferred != slot;
        return true;
    }

    private static bool TryReadSlot(string key, out EchoRunSaveData data,
        out long generation)
    {
        data = null;
        generation = 0;
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            EchoRunSaveEnvelope envelope =
                JsonUtility.FromJson<EchoRunSaveEnvelope>(json);
            if (envelope == null
                || envelope.schemaVersion != SaveEnvelopeVersion
                || envelope.generation <= 0
                || string.IsNullOrEmpty(envelope.payload)
                || !string.Equals(envelope.checksum,
                    StableHash.ComputeHex(envelope.payload),
                    StringComparison.Ordinal))
                return false;

            data = JsonUtility.FromJson<EchoRunSaveData>(envelope.payload);
            if (data == null) return false;
            generation = envelope.generation;
            return true;
        }
        catch (Exception)
        {
            data = null;
            generation = 0;
            return false;
        }
    }

    private static bool TryLoadLegacyArchive(out EchoRunSaveData data)
    {
        data = null;
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            data = JsonUtility.FromJson<EchoRunSaveData>(json);
            return data != null;
        }
        catch (Exception)
        {
            data = null;
            return false;
        }
    }

    private static bool MoveTelemetryOutOfArchive()
    {
        string inlineTelemetry = _data.lastRunTelemetryJson ?? "";
        if (string.IsNullOrEmpty(inlineTelemetry)) return false;
        if (!PlayerPrefs.HasKey(TelemetryKey))
            PlayerPrefs.SetString(TelemetryKey, inlineTelemetry);
        _data.lastRunTelemetryJson = "";
        return true;
    }

    private static void WriteArchive(bool flush)
    {
        Normalize();
        _data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        string payload = JsonUtility.ToJson(_data);
        long nextGeneration = _generation + 1;
        var envelope = new EchoRunSaveEnvelope
        {
            schemaVersion = SaveEnvelopeVersion,
            generation = nextGeneration,
            payload = payload,
            checksum = StableHash.ComputeHex(payload)
        };
        int targetSlot = _activeSlot == 0 ? 1 : 0;
        string targetKey = targetSlot == 0 ? SaveSlotAKey : SaveSlotBKey;

        try
        {
            PlayerPrefs.SetString(targetKey, JsonUtility.ToJson(envelope));
            if (flush) PlayerPrefs.Save();
            if (!TryReadSlot(targetKey, out _, out long writtenGeneration)
                || writtenGeneration != nextGeneration)
                return;

            _activeSlot = targetSlot;
            _generation = nextGeneration;
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSlot);
            PlayerPrefs.DeleteKey(SaveKey);
            if (flush) PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogError("EchoRun archive could not be committed: "
                           + exception.Message);
        }
    }

    private static bool TryCompletePendingTrainingReset()
    {
        if (_trainingResetInProgress)
            return false;
        if (PlayerPrefs.GetInt(TrainingResetPendingKey, 0) == 0)
            return true;
        if (!_initialized || _data == null)
            return false;

        _trainingResetInProgress = true;
        try
        {
            PrepareSingleContractCacheForTrainingReset();
            ClearLegacyTrainingFields(_data);
            PlayerPrefs.DeleteKey(ShadowProfileKey);
            PlayerPrefs.DeleteKey(TelemetryKey);

            // Write twice so both recovery slots contain the reset state. A
            // later checksum failure must never resurrect pre-reset training.
            if (!TryRewriteBothLegacySlotsWithResetState(out string legacyError))
            {
                Debug.LogError("EchoRun training reset could not clear the "
                               + "legacy archive: " + legacyError);
                return false;
            }

            var emptySingleContract = new EchoSingleContractSaveData
            {
                importedLegacyFingerprint = TrainingResetFingerprint
            };
            emptySingleContract.Normalize();
            if (!TryRewriteBothSingleContractSlotsWithResetState(
                    emptySingleContract, out string singleContractError))
            {
                Debug.LogError("EchoRun training reset could not clear the "
                               + "single-contract archive: "
                               + singleContractError);
                return false;
            }

            PlayerPrefs.DeleteKey(ShadowProfileKey);
            PlayerPrefs.DeleteKey(TelemetryKey);
            _trainingWritesEnabled = false;
            PlayerPrefs.DeleteKey(TrainingResetPendingKey);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("EchoRun training reset could not be completed: "
                           + exception.Message);
            return false;
        }
        finally
        {
            _trainingResetInProgress = false;
        }
    }

    private static void EnsurePendingTrainingResetCompleted()
    {
        if (TryCompletePendingTrainingReset()) return;
        throw new InvalidOperationException(
            "Training reset is pending and no training archive may be read "
            + "or written until recovery succeeds.");
    }

    private static void PrepareSingleContractCacheForTrainingReset()
    {
        if (_singleContractInitialized) return;

        LoadedExistingSingleContractArchive = false;
        MigratedLegacySingleContractIdentity = false;
        RecoveredSingleContractFromBackup = false;
        _singleContractActiveSlot = -1;
        _singleContractGeneration = 0;
        if (TryLoadBestSingleContractSlot(
                out EchoSingleContractSaveData data,
                out int slot, out long generation, out bool recovered))
        {
            _singleContractData = data;
            _singleContractActiveSlot = slot;
            _singleContractGeneration = generation;
            LoadedExistingSingleContractArchive = true;
            RecoveredSingleContractFromBackup = recovered;
        }
        else
        {
            _singleContractData = new EchoSingleContractSaveData
            {
                importedLegacyFingerprint = TrainingResetFingerprint
            };
        }
        _singleContractInitialized = true;
    }

    private static void ClearLegacyTrainingFields(EchoRunSaveData data)
    {
        data.shadowProfileJson = "";
        data.directorWeights = null;
        data.directorModelUpdateCount = 0;
        data.directorPolicyJson = "";
        data.lastRunTelemetryJson = "";
        data.skillProfileJson = "";
        data.playerStyleJson = "";
        data.lastEchoContractJson = "";
    }

    private static bool TryRewriteBothLegacySlotsWithResetState(
        out string error)
    {
        error = "";
        for (int writeIndex = 0; writeIndex < 2; writeIndex++)
        {
            long generationBefore = _generation;
            WriteArchive(true);
            if (_generation <= generationBefore)
            {
                error = "archive generation did not advance";
                return false;
            }
        }

        if (!TryReadSlot(SaveSlotAKey, out EchoRunSaveData slotA, out _)
            || !TryReadSlot(SaveSlotBKey, out EchoRunSaveData slotB, out _)
            || !IsLegacyTrainingEmpty(slotA)
            || !IsLegacyTrainingEmpty(slotB)
            || PlayerPrefs.HasKey(ShadowProfileKey)
            || PlayerPrefs.HasKey(TelemetryKey))
        {
            error = "verified slots still contain training data";
            return false;
        }
        return true;
    }

    private static bool TryRewriteBothSingleContractSlotsWithResetState(
        EchoSingleContractSaveData resetState, out string error)
    {
        if (!TrySeedBothSingleContractSlots(resetState, out error))
            return false;

        if (!TryReadSingleContractSlot(SingleContractSaveSlotAKey,
                out EchoSingleContractSaveData slotA, out _)
            || !TryReadSingleContractSlot(SingleContractSaveSlotBKey,
                out EchoSingleContractSaveData slotB, out _)
            || !IsSingleContractTrainingEmpty(slotA)
            || !IsSingleContractTrainingEmpty(slotB))
        {
            error = "verified slots still contain identity data";
            return false;
        }
        return true;
    }

    private static bool TrySeedBothSingleContractSlots(
        EchoSingleContractSaveData seed, out string error)
    {
        error = "";
        EchoSingleContractSaveData normalizedSeed = seed != null
            ? seed.Clone() : null;
        string expectedPayload = JsonUtility.ToJson(normalizedSeed);
        for (int writeIndex = 0; writeIndex < 2; writeIndex++)
        {
            if (!TryWriteSingleContractCandidate(normalizedSeed,
                    out string writeError))
            {
                error = writeError;
                return false;
            }
        }

        if (!TryReadSingleContractSlot(SingleContractSaveSlotAKey,
                out EchoSingleContractSaveData slotA, out _)
            || !TryReadSingleContractSlot(SingleContractSaveSlotBKey,
                out EchoSingleContractSaveData slotB, out _)
            || !string.Equals(expectedPayload, JsonUtility.ToJson(slotA),
                StringComparison.Ordinal)
            || !string.Equals(expectedPayload, JsonUtility.ToJson(slotB),
                StringComparison.Ordinal))
        {
            error = "both archive slots could not be verified";
            return false;
        }
        return true;
    }

    private static bool IsLegacyTrainingEmpty(EchoRunSaveData data)
    {
        return data != null
               && string.IsNullOrEmpty(data.shadowProfileJson)
               && (data.directorWeights == null
                   || data.directorWeights.Length == 0)
               && data.directorModelUpdateCount == 0
               && string.IsNullOrEmpty(data.directorPolicyJson)
               && string.IsNullOrEmpty(data.lastRunTelemetryJson)
               && string.IsNullOrEmpty(data.skillProfileJson)
               && string.IsNullOrEmpty(data.playerStyleJson)
               && string.IsNullOrEmpty(data.lastEchoContractJson);
    }

    private static bool IsSingleContractTrainingEmpty(
        EchoSingleContractSaveData data)
    {
        if (data == null || data.activeIdentity != null
            || data.lastResult != null
            || !string.IsNullOrEmpty(data.lastTransactionId)
            || data.lastCommittedRunSequence != 0)
            return false;
        EchoRetryState retry = data.retryState;
        return retry == null
               || string.IsNullOrEmpty(retry.identityId)
               && string.IsNullOrEmpty(retry.contractId)
               && retry.attemptCount == 0;
    }

    private static EchoSingleContractSaveData CreateInitialSingleContractData(
        bool allowLegacyMigration)
    {
        var data = new EchoSingleContractSaveData
        {
            importedLegacyFingerprint = allowLegacyMigration
                ? NoLegacyIdentityTombstone
                : CorruptSingleContractArchiveTombstone
        };
        if (!allowLegacyMigration) return data;

        string legacyProfileJson = _data != null
            ? _data.shadowProfileJson ?? "" : "";
        if (string.IsNullOrEmpty(legacyProfileJson)) return data;

        LegacyShadowProfileV5MigrationData legacy;
        try
        {
            legacy = JsonUtility.FromJson<LegacyShadowProfileV5MigrationData>(
                legacyProfileJson);
        }
        catch (Exception)
        {
            data.importedLegacyFingerprint = "v9:invalid-profile:"
                + StableHash.ComputeHex(legacyProfileJson).ToLowerInvariant();
            return data;
        }

        string frozenJson = legacy != null
            ? legacy.activeGenerationJson ?? "" : "";
        if (string.IsNullOrEmpty(frozenJson)) return data;

        EchoGenerationSnapshot snapshot =
            EchoGenerationSnapshot.FromJson(frozenJson);
        if (snapshot == null)
        {
            data.importedLegacyFingerprint = "v9:invalid-snapshot:"
                + StableHash.ComputeHex(frozenJson).ToLowerInvariant();
            return data;
        }

        string normalizedFrozenJson = snapshot.ToJson();
        data.importedLegacyFingerprint = "v9:"
            + StableHash.ComputeHex(normalizedFrozenJson).ToLowerInvariant();
        ActiveEchoIdentity identity = ActiveEchoIdentity.FromLegacySnapshot(
            snapshot, _data != null ? _data.runSequence : 0);
        if (identity == null) return data;

        identity.policyWeights = new AIShadowPolicy(
            identity.policyWeights).ExportWeights();
        var sequence = new AIShadowSequencePolicy(
            identity.sequenceTransitions, identity.sequencePairCount);
        AIShadowSequenceState sequenceState = sequence.ExportState();
        identity.sequenceTransitions = sequenceState.transitions;
        identity.sequencePairCount = sequenceState.pairCount;
        identity.Normalize();

        var probe = new EchoSingleContractSaveData
        {
            activeIdentity = identity,
            importedLegacyFingerprint = data.importedLegacyFingerprint
        };
        probe.Normalize();
        if (!probe.IsSemanticallyValid()) return data;

        data.activeIdentity = identity;
        MigratedLegacySingleContractIdentity = true;
        return data;
    }

    private static void ApplyFailedAttempt(
        EchoSingleContractSaveData candidate)
    {
        ActiveEchoIdentity identity = candidate.activeIdentity;
        EchoMemoryContract contract = identity != null
            ? identity.memoryContract : null;
        if (identity == null || contract == null
            || string.IsNullOrEmpty(contract.contractId))
        {
            candidate.retryState = new EchoRetryState();
            return;
        }

        EchoRetryState retry = candidate.retryState ?? new EchoRetryState();
        bool sameContract = string.Equals(retry.identityId,
                                identity.identityId,
                                StringComparison.Ordinal)
                            && string.Equals(retry.contractId,
                                contract.contractId,
                                StringComparison.Ordinal);
        candidate.retryState = new EchoRetryState
        {
            identityId = identity.identityId,
            contractId = contract.contractId,
            attemptCount = sameContract
                ? retry.attemptCount == int.MaxValue
                    ? int.MaxValue : retry.attemptCount + 1
                : 1
        };
    }

    private static bool ValidatePromotion(ActiveEchoIdentity current,
        ActiveEchoIdentity promoted, int runSequence, out string error)
    {
        error = "";
        if (promoted == null || promoted.memoryContract == null)
        {
            error = "Promoted identity must own a memory contract.";
            return false;
        }
        promoted.Normalize();
        if (promoted.sourceRunSequence != runSequence)
        {
            error = "Promoted identity source run does not match settlement.";
            return false;
        }

        int expectedGeneration = current != null
            ? current.generation + 1 : 1;
        string expectedParent = current != null ? current.identityId : "";
        if (promoted.generation != expectedGeneration
            || !string.Equals(promoted.parentIdentityId,
                expectedParent, StringComparison.Ordinal))
        {
            error = "Promoted identity generation or parent is invalid.";
            return false;
        }
        if (!string.Equals(promoted.identityId,
                ActiveEchoIdentity.CreateIdentityId(promoted),
                StringComparison.Ordinal))
        {
            error = "Promoted identity id does not match its contents.";
            return false;
        }

        var probe = new EchoSingleContractSaveData
        {
            activeIdentity = promoted,
            importedLegacyFingerprint = "validation"
        };
        probe.Normalize();
        if (!probe.IsSemanticallyValid())
        {
            error = "Promoted identity failed semantic validation.";
            return false;
        }
        return true;
    }

    private static bool TryLoadBestSingleContractSlot(
        out EchoSingleContractSaveData data, out int slot,
        out long generation, out bool recovered)
    {
        bool validA = TryReadSingleContractSlot(SingleContractSaveSlotAKey,
            out EchoSingleContractSaveData dataA, out long generationA);
        bool validB = TryReadSingleContractSlot(SingleContractSaveSlotBKey,
            out EchoSingleContractSaveData dataB, out long generationB);
        bool hasPublishedSlot = PlayerPrefs.HasKey(
            SingleContractActiveSaveSlotKey);
        int preferred = hasPublishedSlot
            ? PlayerPrefs.GetInt(SingleContractActiveSaveSlotKey, -1) : -1;
        recovered = false;

        if (!validA && !validB)
        {
            data = null;
            slot = -1;
            generation = 0;
            return false;
        }
        if (preferred == 0 && validA)
        {
            slot = 0;
        }
        else if (preferred == 1 && validB)
        {
            slot = 1;
        }
        else if (validA && validB)
        {
            slot = generationA >= generationB ? 0 : 1;
        }
        else
        {
            slot = validA ? 0 : 1;
        }

        data = slot == 0 ? dataA : dataB;
        generation = slot == 0 ? generationA : generationB;
        recovered = hasPublishedSlot
                    && (preferred != slot
                        || preferred == 0 && !validA
                        || preferred == 1 && !validB
                        || preferred != 0 && preferred != 1);
        return true;
    }

    private static bool TryReadSingleContractSlot(string key,
        out EchoSingleContractSaveData data, out long generation)
    {
        data = null;
        generation = 0;
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            EchoSingleContractSaveEnvelope envelope =
                JsonUtility.FromJson<EchoSingleContractSaveEnvelope>(json);
            if (envelope == null
                || envelope.schemaVersion
                != LegacySingleContractSaveEnvelopeVersion
                && envelope.schemaVersion
                != SingleContractSaveEnvelopeVersion
                || envelope.generation <= 0
                || string.IsNullOrEmpty(envelope.payload)
                || !string.Equals(envelope.checksum,
                    ComputeSingleContractEnvelopeChecksum(envelope),
                    StringComparison.Ordinal))
                return false;

            EchoSingleContractSaveData parsed =
                JsonUtility.FromJson<EchoSingleContractSaveData>(
                    envelope.payload);
            if (parsed == null
                || parsed.schemaVersion
                != EchoSingleContractSaveData.CurrentSchemaVersion
                || parsed.gameplayModeVersion
                != EchoSingleContractSaveData.CurrentGameplayModeVersion)
                return false;
            parsed.Normalize();
            if (!parsed.IsSemanticallyValid()) return false;
            data = parsed;
            generation = envelope.generation;
            return true;
        }
        catch (Exception)
        {
            data = null;
            generation = 0;
            return false;
        }
    }

    private static bool TryWriteSingleContractCandidate(
        EchoSingleContractSaveData candidate, out string error)
    {
        error = "";
        if (candidate == null)
        {
            error = "Candidate archive is missing.";
            return false;
        }

        EchoSingleContractSaveData normalized = candidate.Clone();
        if (!normalized.TryValidateSemantics(out string semanticError))
        {
            error = "Candidate archive failed semantic validation: "
                    + semanticError;
            return false;
        }

        if (_singleContractGeneration < 0
            || _singleContractGeneration == long.MaxValue)
        {
            error = "Archive generation cannot advance safely.";
            return false;
        }

        string payload = JsonUtility.ToJson(normalized);
        long nextGeneration = _singleContractGeneration + 1;
        var envelope = new EchoSingleContractSaveEnvelope
        {
            schemaVersion = SingleContractSaveEnvelopeVersion,
            generation = nextGeneration,
            payload = payload
        };
        envelope.checksum = ComputeSingleContractEnvelopeChecksum(envelope);
        int targetSlot = _singleContractActiveSlot == 0 ? 1 : 0;
        string targetKey = targetSlot == 0
            ? SingleContractSaveSlotAKey : SingleContractSaveSlotBKey;

        bool hadPublishedSlot = PlayerPrefs.HasKey(
            SingleContractActiveSaveSlotKey);
        int previousPublishedSlot = PlayerPrefs.GetInt(
            SingleContractActiveSaveSlotKey, -1);
        try
        {
            PlayerPrefs.SetString(targetKey, JsonUtility.ToJson(envelope));
            PlayerPrefs.Save();
            if (!TryReadSingleContractSlot(targetKey,
                    out EchoSingleContractSaveData written,
                    out long writtenGeneration)
                || writtenGeneration != nextGeneration
                || !string.Equals(payload, JsonUtility.ToJson(written),
                    StringComparison.Ordinal))
            {
                error = "Candidate archive could not be verified after write.";
                return false;
            }

            PlayerPrefs.SetInt(SingleContractActiveSaveSlotKey, targetSlot);
            PlayerPrefs.Save();
            _singleContractData = written;
            _singleContractActiveSlot = targetSlot;
            _singleContractGeneration = nextGeneration;
            return true;
        }
        catch (Exception exception)
        {
            if (hadPublishedSlot)
                PlayerPrefs.SetInt(SingleContractActiveSaveSlotKey,
                    previousPublishedSlot);
            else
                PlayerPrefs.DeleteKey(SingleContractActiveSaveSlotKey);
            error = exception.Message;
            return false;
        }
    }

    private static string ComputeSingleContractEnvelopeChecksum(
        EchoSingleContractSaveEnvelope envelope)
    {
        if (envelope == null) return "";
        if (envelope.schemaVersion
            == LegacySingleContractSaveEnvelopeVersion)
            return StableHash.ComputeHex(envelope.payload ?? "");
        if (envelope.schemaVersion != SingleContractSaveEnvelopeVersion)
            return "";
        return StableHash.ComputeHex(
            envelope.schemaVersion.ToString()
            + "\n" + envelope.generation.ToString()
            + "\n" + (envelope.payload ?? ""));
    }

    private static float[] Clone(float[] values)
    {
        return values == null ? null : (float[])values.Clone();
    }

    private static int[] CloneInventory(int[] values)
    {
        int[] result = new int[4];
        if (values != null)
            Array.Copy(values, result, Mathf.Min(values.Length, result.Length));
        for (int i = 0; i < result.Length; i++)
            result[i] = Mathf.Max(0, result[i]);
        return result;
    }
}

public sealed class EchoRunSaveLifecycle : MonoBehaviour
{
    void OnApplicationPause(bool paused)
    {
        if (paused) EchoRunSaveSystem.SaveLegacyState();
    }

    void OnApplicationQuit()
    {
        EchoRunSaveSystem.SaveLegacyState();
    }
}
