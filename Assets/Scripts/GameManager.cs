using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Paused, GameOver }
public enum RunEndReason { None, FinishReached, Collision, Abandoned }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private static bool _startAfterSceneLoad;
    private static int? _nextRunSeed;
    private static GameplayFlowMode? _gameplayFlowAfterSceneLoad;
    private static SingleContractValidationConfig
        _validationAfterSceneLoad;
    private static ActiveEchoIdentity _asyncOpponentAfterSceneLoad;
    private static AsyncChallengeRunParameters _asyncParametersAfterSceneLoad;

    [Header("Speed")]
    public float startSpeed = 10f;
    public float maxSpeed = 40f;
    public float speedIncreaseRate = 0.5f;

    [Header("Score")]
    public int coinScore = 10;

    [Header("Gameplay Flow")]
    [SerializeField] private GameplayFlowMode gameplayFlowMode =
        GameplayFlowMode.SingleContract;
    [SerializeField] private SingleContractValidationConfig
        singleContractValidationConfig = new SingleContractValidationConfig();

    public float CurrentSpeed { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int Coins { get; private set; }
    public float Distance { get; private set; }
    public int HighScore { get; private set; }
    public int TotalCoins { get; private set; }
    public bool IsNewHighScore { get; private set; }
    public bool IsDeathSequence { get; private set; }
    public int RunSeed { get; private set; }
    public float CourseDistance { get; private set; }
    public float CourseTargetDuration { get; private set; }
    public int FinishScheduleCount { get; private set; }
    public float RunElapsed { get; private set; }
    public float RemainingDistance => Mathf.Max(0f, CourseDistance - Distance);
    public RunEndReason LastEndReason { get; private set; }
    public int CollisionStrikes { get; private set; }
    public int MaximumCollisionStrikes => 2;
    public int SyncRemaining => Mathf.Max(0,
        MaximumCollisionStrikes - CollisionStrikes);
    public float CollisionRecoveryDuration => 1.25f;
    public float CollisionRecoveryTimeRemaining { get; private set; }
    public int ContractMarkerCount { get; private set; }
    public GameplayFlowMode ConfiguredGameplayFlowMode => gameplayFlowMode;
    public GameplayFlowMode ActiveGameplayFlowMode { get; private set; } =
        GameplayFlowMode.SixPhaseLegacy;
    public SingleContractValidationConfig
        ConfiguredSingleContractValidationConfig =>
            SingleContractValidationConfig.CopyOf(
                singleContractValidationConfig);
    public SingleContractValidationConfig
        ActiveSingleContractValidationConfig =>
            SingleContractValidationConfig.CopyOf(
                _activeSingleContractValidationConfig);
    public bool IsSingleContractRun =>
        ActiveGameplayFlowMode == GameplayFlowMode.SingleContract;
    public bool IsAsyncChallengeRun =>
        ActiveGameplayFlowMode == GameplayFlowMode.AsyncChallenge;
    public EchoRunRules ActiveRunRules => EchoRunRules.For(ActiveGameplayFlowMode);
    public bool UsesSingleContractRules => State == GameState.Menu
        ? EchoRunRules.For(gameplayFlowMode).UsesSingleContractRules
        : ActiveRunRules.UsesSingleContractRules;
    public AsyncChallengeRunParameters ConfiguredAsyncChallengeParameters =>
        _configuredAsyncParameters;
    public ActiveEchoIdentity ActiveOpponentIdentityPreview =>
        _activeRunOpponent != null ? _activeRunOpponent.Clone() : null;
    public AsyncChallengeResult LastAsyncChallengeResult { get; private set; }
    public System.Func<string> ChallengeIdFactory { get; set; } =
        () => System.Guid.NewGuid().ToString("N");
    public event System.Action<AsyncChallengeRunParameters> AsyncChallengeStarted;
    public event System.Action<AsyncChallengeResult> AsyncChallengeCompleted;
    public event System.Action LocalSingleContractSettled;
    public RunDifficultyLevel ActiveRunDifficulty { get; private set; } =
        RunDifficultySettings.DefaultLevel;

    [Header("Buff (runtime)")]
    public float BuffTimeRemaining;
    public string BuffName;

    public UnityEvent<GameState> OnStateChanged = new UnityEvent<GameState>();
    public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCoinsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnContractMarkerChanged = new UnityEvent<int>();
    public UnityEvent<int> OnBankedCoinsChanged = new UnityEvent<int>();
    public UnityEvent<float> OnDistanceChanged = new UnityEvent<float>();

    private float _distanceTraveled;
    private float _prePauseTimeScale = 1f;
    private PlayerController _telemetryPlayer;
    private int _lastBaseScore;
    private float _powerUpBonusScore;
    private float _collisionRecoverySpeedDebt;
    private bool _telemetryFinished;
    private ActiveEchoIdentity _configuredAsyncOpponent;
    private AsyncChallengeRunParameters _configuredAsyncParameters;
    private ActiveEchoIdentity _activeRunOpponent;
    private AsyncChallengeRunParameters _activeAsyncParameters;
    private bool _hasAsyncSpeedOverride;
    private float _ordinaryStartSpeed;
    private float _ordinaryMaxSpeed;
    private float _ordinaryAcceleration;
    private SingleContractValidationConfig
        _activeSingleContractValidationConfig =
            new SingleContractValidationConfig();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<GameManager>() != null) return;
        new GameObject("GameManager_Runtime").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (_gameplayFlowAfterSceneLoad.HasValue)
        {
            gameplayFlowMode = _gameplayFlowAfterSceneLoad.Value;
            singleContractValidationConfig =
                SingleContractValidationConfig.CopyOf(
                    _validationAfterSceneLoad);
            _configuredAsyncOpponent = _asyncOpponentAfterSceneLoad;
            _configuredAsyncParameters = _asyncParametersAfterSceneLoad;
            _gameplayFlowAfterSceneLoad = null;
            _validationAfterSceneLoad = null;
            _asyncOpponentAfterSceneLoad = null;
            _asyncParametersAfterSceneLoad = null;
        }
        ApplySingleContractValidationLaunchOptions(
            System.Environment.GetCommandLineArgs());
        EnsureSceneRuntimeServices();
        EchoRunSaveSystem.EnsureInitialized();
        GameplayBalance balance = GameBalanceConfig.Current.gameplay;
        startSpeed = balance.startSpeed;
        maxSpeed = balance.maxSpeed;
        speedIncreaseRate = balance.speedIncreaseRate;
        coinScore = balance.coinScore;
        int savedFps = PlayerPrefs.GetInt("TargetFrameRate", 60);
        int runtimeFps = NormalizeFrameRate(
            savedFps > 0 ? savedFps : 60, IsFrameRateConstrainedPlatform());
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = runtimeFps;
        if (runtimeFps != savedFps)
            EchoRunSaveSystem.SaveFrameRate(runtimeFps);
    }

    private void EnsureSceneRuntimeServices()
    {
        // RuntimeInitializeOnLoadMethod runs for the player's initial scene,
        // not for every SceneManager reload. Restart destroys the scene-owned
        // styler, so recreate it here before TrackManager.Start builds pools.
        EnsureSceneService<WorldStyler>("WorldStyler_Runtime");
        EnsureSceneService<TrackManager>("TrackManager_Runtime");
        EnsureSceneService<AITrainingDashboardUI>("AI Training Dashboard UI");
    }

    private void EnsureSceneService<T>(string objectName) where T : Component
    {
        if (FindObjectOfType<T>() != null) return;
        GameObject host = new GameObject(objectName);
        host.transform.SetParent(transform, false);
        host.AddComponent<T>();
    }

    public void SetFrameRate(int fps)
    {
        if (fps <= 0) return;
        int runtimeFps = NormalizeFrameRate(
            fps, IsFrameRateConstrainedPlatform());
        Application.targetFrameRate = runtimeFps;
        EchoRunSaveSystem.SaveFrameRate(runtimeFps);
    }

    public int GetFrameRate()
    {
        return Application.targetFrameRate;
    }

#if UNITY_WEBGL && !UNITY_EDITOR && !MINIGAME_SUBPLATFORM_WEIXIN
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int EchoRun_SetWebFrameRate(int fps);

    private void LateUpdate()
    {
        // The engine can reset scheduling during startup or focus changes.
        // The bridge only changes it when it differs from the selected target.
        EchoRun_SetWebFrameRate(Application.targetFrameRate);
    }
#endif

    public bool SupportsHighFrameRate => !IsFrameRateConstrainedPlatform();

    public static bool ShouldConstrainHighFrameRate(bool isAndroid,
        bool isWebGl, bool usesTouchLayout)
    {
        // Touch capability does not identify a low-refresh display. Let all
        // platforms request 120; the display/browser still determines delivery.
        return false;
    }

    public static int NormalizeFrameRate(int requested, bool constrainedPlatform)
    {
        if (requested <= 30) return 30;
        if (constrainedPlatform || requested < 90) return 60;
        return 120;
    }

    private static bool IsFrameRateConstrainedPlatform()
    {
#if UNITY_ANDROID
        return ShouldConstrainHighFrameRate(true, false, true);
#elif UNITY_WEBGL
        bool usesTouchLayout = Application.isMobilePlatform || Input.touchSupported;
        return ShouldConstrainHighFrameRate(false, true, usesTouchLayout);
#else
        return false;
#endif
    }

    void Start()
    {
        HighScore = PlayerPrefs.GetInt("HighScore", 0);
        TotalCoins = EchoRunSaveSystem.TotalCoins;

        bool autoStartValidation = singleContractValidationConfig.enabled
                                   && singleContractValidationConfig.autoStart;
        if (_startAfterSceneLoad || autoStartValidation)
        {
            _startAfterSceneLoad = false;
            StartGame();
        }
    }

    void Update()
    {
        if (State != GameState.Playing || IsDeathSequence) return;

        RunElapsed += Time.deltaTime;
        float recoveryDistanceAdjustment = AdvanceRunSpeed(Time.deltaTime);
        _distanceTraveled += Mathf.Max(0f,
            CurrentSpeed * Time.deltaTime - recoveryDistanceAdjustment);
        if (CourseDistance > 0f)
            _distanceTraveled = Mathf.Min(_distanceTraveled, CourseDistance);

        int newDist = Mathf.FloorToInt(_distanceTraveled);
        if (newDist != Mathf.FloorToInt(Distance))
        {
            Distance = _distanceTraveled;
            OnDistanceChanged.Invoke(Distance);
        }

        int baseScore = Mathf.FloorToInt(_distanceTraveled) + Coins * coinScore;
        int baseGain = Mathf.Max(0, baseScore - _lastBaseScore);
        const float multiplier = 1f;
        _powerUpBonusScore += baseGain * Mathf.Max(0f, multiplier - 1f);
        _lastBaseScore = baseScore;
        int newScore = baseScore + Mathf.FloorToInt(_powerUpBonusScore);
        if (newScore != Score)
        {
            Score = newScore;
            OnScoreChanged.Invoke(Score);
        }

        if (_telemetryPlayer == null)
            _telemetryPlayer = FindObjectOfType<PlayerController>();
        AIRunTelemetry.Tick(this, _telemetryPlayer);

        if (CourseDistance > 0f && _distanceTraveled >= CourseDistance)
        {
            CompleteCourse();
            return;
        }

        // Buff countdown
        if (BuffTimeRemaining > 0f)
        {
            BuffTimeRemaining -= Time.deltaTime;
            if (BuffTimeRemaining <= 0f)
            {
                BuffTimeRemaining = 0f;
                BuffName = null;
            }
        }
    }

    public void StartGame()
    {
        if (!TryStartConfiguredRun(out string error)
            && error != "RUN_ALREADY_STARTED")
            Debug.LogWarning("Run could not start: " + error);
    }

    public bool TryStartConfiguredRun(out string error)
    {
        error = "";
        if (State != GameState.Menu)
        {
            error = "RUN_ALREADY_STARTED";
            return false;
        }
        if (gameplayFlowMode == GameplayFlowMode.AsyncChallenge
            && (_configuredAsyncOpponent == null
                || _configuredAsyncParameters == null))
        {
            error = "ASYNC_CHALLENGE_NOT_CONFIGURED";
            return false;
        }
        if (gameplayFlowMode == GameplayFlowMode.AsyncChallenge
            && !_configuredAsyncParameters.TryValidate(out error)) return false;

        FreezeGameplayFlowConfiguration();
        LastAsyncChallengeResult = null;

        int runSequence = EchoRunSaveSystem.ReserveRunSequence();
        bool fixedSingleContractRun = IsSingleContractRun
                                      && _activeSingleContractValidationConfig.enabled;
        if (fixedSingleContractRun)
        {
            Debug.Log("Single-contract validation run started: seed="
                      + _activeSingleContractValidationConfig.fixedSeed);
        }
        RunSeed = IsAsyncChallengeRun ? _activeAsyncParameters.runSeed
            : fixedSingleContractRun
            ? _activeSingleContractValidationConfig.fixedSeed
            : _nextRunSeed ?? CreateRunSeed(runSequence);
        _nextRunSeed = null;
        bool fixedValidationIdentity = fixedSingleContractRun
                                       && SingleContractValidationIdentity
                                           .IsEnabled(
                                               _activeSingleContractValidationConfig);
        int runGeneration = IsAsyncChallengeRun ? _activeRunOpponent.generation
            : fixedValidationIdentity
            ? SingleContractValidationIdentity.Generation
            : AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.Generation : 0;
        AIRunRandom.BeginRun(RunSeed);
        AIPlayerSkillEstimator.BeginRun(ActiveRunRules.AllowPlayerTraining);
        StyleTracker.BeginRun(ActiveRunRules.AllowPlayerTraining);
        AITrackDirector.Instance?.PrepareForRun();
        AIRunTelemetry.BeginRun(RunSeed, runSequence, HighScore,
            runGeneration,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.ModelUpdateCount
                : EchoRunSaveSystem.DirectorModelUpdateCount,
            IsAsyncChallengeRun ? (float[])_activeRunOpponent.policyWeights.Clone()
            : AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetModelWeightsSnapshot()
                : null,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetModelWeightsSnapshot()
                : EchoRunSaveSystem.GetDirectorWeights(),
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetPolicyStateSnapshot()
                : EchoRunSaveSystem.GetDirectorPolicyJson(),
            IsAsyncChallengeRun ? JsonUtility.ToJson(new AIShadowSequenceState
            {
                transitions = (float[])_activeRunOpponent.sequenceTransitions.Clone(),
                pairCount = _activeRunOpponent.sequencePairCount
            }) : AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetSequenceStateSnapshot()
                : "", ActiveGameplayFlowMode,
            IsAsyncChallengeRun ? _activeAsyncParameters.rulesVersion : 0,
            IsAsyncChallengeRun ? _activeRunOpponent.identityId : "");

        Time.timeScale = 1f;
        // Retired supplies must neither activate nor consume archived inventory.
        PowerUpController.Instance?.BeginRun(false);
        CurrentSpeed = Mathf.Min(maxSpeed, startSpeed);
        Score = 0;
        Coins = 0;
        ContractMarkerCount = 0;
        Distance = 0;
        _distanceTraveled = 0;
        _lastBaseScore = 0;
        _powerUpBonusScore = 0f;
        BuffTimeRemaining = 0;
        BuffName = null;
        IsDeathSequence = false;
        LastEndReason = RunEndReason.None;
        RunElapsed = 0f;
        CollisionStrikes = 0;
        CollisionRecoveryTimeRemaining = 0f;
        _collisionRecoverySpeedDebt = 0f;
        _telemetryFinished = false;
        GameplayBalance balance = GameBalanceConfig.Current.gameplay;
        ActiveEchoIdentity singleContractIdentity = IsAsyncChallengeRun
            ? _activeRunOpponent : IsSingleContractRun
                ? EchoRunSaveSystem.GetActiveEchoIdentity() : null;
        bool hasSingleContractOpponent = fixedValidationIdentity
                                         || singleContractIdentity != null
                                         && !singleContractIdentity
                                             .RequiresRouteCalibration;
        CourseTargetDuration = IsAsyncChallengeRun
            ? AsyncChallengeRules.CourseDurationSeconds : IsSingleContractRun
            ? SingleContractCourseDuration(hasSingleContractOpponent)
            : SelectCourseDuration(runGeneration,
                balance.calibrationDuration, balance.challengeDuration);
        CourseDistance = EchoTimeRules.DistanceForAcceleratingRun(
            CurrentSpeed, maxSpeed, speedIncreaseRate, CourseTargetDuration);
        FinishScheduleCount = 1;
        _telemetryPlayer = null;
        State = GameState.Playing;
        if (IsAsyncChallengeRun)
            InvokeOptional(AsyncChallengeStarted, _activeAsyncParameters);
        OnStateChanged.Invoke(State);
        OnScoreChanged.Invoke(0);
        OnCoinsChanged.Invoke(0);
        OnContractMarkerChanged.Invoke(0);
        OnDistanceChanged.Invoke(0);
        InputManager.Instance?.ClearInput();
        AudioManager.Instance?.StartFootsteps();
        return true;
    }

    public bool TryConfigureGameplayFlow(GameplayFlowMode mode,
        SingleContractValidationConfig validation = null)
    {
        if (State != GameState.Menu) return false;
        if (mode == GameplayFlowMode.AsyncChallenge) return false;
        ClearAsyncChallengeConfiguration();
        gameplayFlowMode = NormalizeGameplayFlowMode(mode);
        singleContractValidationConfig =
            SingleContractValidationConfig.CopyOf(validation);
        return true;
    }

    public bool TryConfigureAsyncChallenge(ActiveEchoIdentity opponent,
        AsyncChallengeRunParameters parameters, out string error)
    {
        error = "";
        if (State != GameState.Menu)
        {
            error = "RUN_ALREADY_STARTED";
            return false;
        }
        if (parameters == null)
        {
            error = "INVALID_CHALLENGE_PARAMETERS";
            return false;
        }
        if (!parameters.TryValidate(out error)) return false;
        // Serialize without Normalize: an unknown input version must stay unknown.
        if (!ActiveEchoIdentity.TryFromExternalJson(
                opponent != null ? JsonUtility.ToJson(opponent) : "",
                out ActiveEchoIdentity validated, out error)) return false;
        _configuredAsyncOpponent = validated;
        _configuredAsyncParameters = parameters;
        gameplayFlowMode = GameplayFlowMode.AsyncChallenge;
        singleContractValidationConfig = new SingleContractValidationConfig();
        return true;
    }

    private void ClearAsyncChallengeConfiguration()
    {
        _configuredAsyncOpponent = null;
        _configuredAsyncParameters = null;
        RestoreOrdinarySpeeds();
    }

    private void RestoreOrdinarySpeeds()
    {
        if (!_hasAsyncSpeedOverride) return;
        startSpeed = _ordinaryStartSpeed;
        maxSpeed = _ordinaryMaxSpeed;
        speedIncreaseRate = _ordinaryAcceleration;
        _hasAsyncSpeedOverride = false;
    }

    private void ApplySingleContractValidationLaunchOptions(
        string[] arguments)
    {
        if (SingleContractValidationLaunchOptions.TryParse(arguments,
                out SingleContractValidationConfig validation,
                out string error))
        {
            gameplayFlowMode = GameplayFlowMode.SingleContract;
            singleContractValidationConfig = validation;
            Debug.Log("Single-contract validation launch enabled: seed="
                      + validation.fixedSeed
                      + ", directorFrozen=true, powerUps=false, difficulty=Standard"
                      + ", autoStart=" + validation.autoStart
                      + ", fixedIdentity=" + validation.useFixedIdentity);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError(error);
        }
    }

    private void FreezeGameplayFlowConfiguration()
    {
        ActiveGameplayFlowMode = NormalizeGameplayFlowMode(gameplayFlowMode);
        _activeAsyncParameters = IsAsyncChallengeRun ? _configuredAsyncParameters : null;
        _activeRunOpponent = IsAsyncChallengeRun && _configuredAsyncOpponent != null
            ? _configuredAsyncOpponent.Clone() : null;
        if (IsAsyncChallengeRun)
        {
            if (!_hasAsyncSpeedOverride)
            {
                _ordinaryStartSpeed = startSpeed;
                _ordinaryMaxSpeed = maxSpeed;
                _ordinaryAcceleration = speedIncreaseRate;
                _hasAsyncSpeedOverride = true;
            }
            startSpeed = AsyncChallengeRules.StartSpeed;
            maxSpeed = AsyncChallengeRules.MaximumSpeed;
            speedIncreaseRate = AsyncChallengeRules.Acceleration;
        }
        else RestoreOrdinarySpeeds();
        _activeSingleContractValidationConfig =
            SingleContractValidationConfig.CopyOf(
                singleContractValidationConfig);
        bool forceStandard = ActiveGameplayFlowMode
                             == GameplayFlowMode.SingleContract
                             && _activeSingleContractValidationConfig.enabled
                             && _activeSingleContractValidationConfig
                                 .forceStandardDifficulty;
        ActiveRunDifficulty = IsAsyncChallengeRun || forceStandard
            ? RunDifficultyLevel.Standard
            : RunDifficultySettings.Current;
    }

    private static GameplayFlowMode NormalizeGameplayFlowMode(
        GameplayFlowMode mode)
    {
        if (mode == GameplayFlowMode.AsyncChallenge) return mode;
        return mode == GameplayFlowMode.SingleContract
            ? GameplayFlowMode.SingleContract
            : GameplayFlowMode.SixPhaseLegacy;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        _prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        State = GameState.Paused;
        OnStateChanged.Invoke(State);
        EchoRunSaveSystem.SaveLegacyState();
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = _prePauseTimeScale;
        State = GameState.Playing;
        OnStateChanged.Invoke(State);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        if (State == GameState.Playing || State == GameState.Paused)
        {
            LastEndReason = RunEndReason.Abandoned;
            AIShadowRunner.Instance?.FinalizeRunIfNeeded();
        }
        FinishTelemetry(LastEndReason == RunEndReason.None
            ? RunEndReason.Abandoned
            : LastEndReason);
        EchoRunSaveSystem.SaveLegacyState();
        if (gameplayFlowMode == GameplayFlowMode.AsyncChallenge)
        {
            ClearAsyncChallengeConfiguration();
            gameplayFlowMode = GameplayFlowMode.SingleContract;
        }
        PreserveGameplayFlowAcrossSceneLoad();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GameOver()
    {
        if (State != GameState.Playing || IsDeathSequence) return;
        LastEndReason = RunEndReason.Collision;
        IsDeathSequence = true;
        var player = GameObject.Find("player");
        if (player != null) ParticleManager.Instance?.EmitDeath(player.transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDeath();
        if (AudioManager.Instance != null) AudioManager.Instance.StopFootsteps();
        InputManager.Instance?.ClearInput();
        StartCoroutine(DeathSequenceCoroutine());
    }

    public bool TryRecoverFromCollision()
    {
        if (State != GameState.Playing || IsDeathSequence) return false;
        if (CollisionRecoveryTimeRemaining > 0f) return true;
        CollisionStrikes++;
        if (CollisionStrikes >= MaximumCollisionStrikes) return false;

        float speedBeforeImpact = CurrentSpeed;
        CurrentSpeed = Mathf.Max(startSpeed * 0.75f,
            speedBeforeImpact * 0.55f);
        _collisionRecoverySpeedDebt = Mathf.Max(0f,
            speedBeforeImpact - CurrentSpeed);
        CollisionRecoveryTimeRemaining = CollisionRecoveryDuration;
        InputManager.Instance?.ClearInput();
        AIRunTelemetry.RecordEvent("player_collision_recovery",
            CollisionStrikes, -1, CurrentSpeed, CollisionRecoveryTimeRemaining);
        return true;
    }

    private float AdvanceRunSpeed(float deltaTime)
    {
        float elapsed = Mathf.Max(0f, deltaTime);
        float recoveryTime = CollisionRecoveryTimeRemaining;
        float recoveryElapsed = Mathf.Min(elapsed, recoveryTime);
        float distanceAdjustment = 0f;
        if (recoveryTime > 0f && _collisionRecoverySpeedDebt > 0f)
        {
            float debtBefore = _collisionRecoverySpeedDebt;
            float baselineSpeed = Mathf.Min(maxSpeed,
                CurrentSpeed + debtBefore
                + speedIncreaseRate * recoveryElapsed);
            float debtAfter = debtBefore
                              * Mathf.Clamp01((recoveryTime - recoveryElapsed)
                                              / recoveryTime);
            CurrentSpeed = Mathf.Max(0f, baselineSpeed - debtAfter);
            _collisionRecoverySpeedDebt = debtAfter;
            distanceAdjustment = 0.5f * (debtBefore - debtAfter)
                                 * recoveryElapsed;
        }
        else
        {
            CurrentSpeed = Mathf.Min(CurrentSpeed
                                     + speedIncreaseRate * recoveryElapsed,
                maxSpeed);
        }

        CollisionRecoveryTimeRemaining = Mathf.Max(0f,
            recoveryTime - elapsed);
        CurrentSpeed = Mathf.Min(CurrentSpeed + speedIncreaseRate
                                 * (elapsed - recoveryElapsed), maxSpeed);
        if (CollisionRecoveryTimeRemaining <= 0.0001f)
        {
            CollisionRecoveryTimeRemaining = 0f;
            _collisionRecoverySpeedDebt = 0f;
        }
        return distanceAdjustment;
    }

    System.Collections.IEnumerator DeathSequenceCoroutine()
    {
        bool reducedMotion = EchoRunAccessibility.ReducedMotion;
        Time.timeScale = reducedMotion ? 1f : 0.3f;
        yield return new WaitForSecondsRealtime(reducedMotion ? 0.35f : 1.2f);
        Time.timeScale = 1f;
        State = GameState.GameOver;
        SaveHighScore();
        OnStateChanged.Invoke(State);
        FinishTelemetry(LastEndReason);
        IsDeathSequence = false;
    }

    private void CompleteCourse()
    {
        if (State != GameState.Playing || IsDeathSequence) return;
        LastEndReason = RunEndReason.FinishReached;
        IsDeathSequence = true;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        Distance = CourseDistance;
        _distanceTraveled = CourseDistance;
        OnDistanceChanged.Invoke(Distance);
        State = GameState.GameOver;
        SaveHighScore();
        OnStateChanged.Invoke(State);
        FinishTelemetry(LastEndReason);
        IsDeathSequence = false;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        if (IsAsyncChallengeRun)
        {
            AIShadowRunner.Instance?.FinalizeRunIfNeeded();
            PrepareAsyncRetry();
        }
        FinishTelemetry(LastEndReason == RunEndReason.None
            ? RunEndReason.Abandoned
            : LastEndReason);
        EchoRunSaveSystem.SaveLegacyState();
        PreserveGameplayFlowAcrossSceneLoad();
        _startAfterSceneLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void PrepareAsyncRetry()
    {
        string id = ChallengeIdFactory != null ? ChallengeIdFactory() : "";
        var next = _activeAsyncParameters.WithChallengeId(id);
        if (!next.TryValidate(out _) || id == _activeAsyncParameters.challengeId)
            next = _activeAsyncParameters.WithChallengeId(System.Guid.NewGuid().ToString("N"));
        _configuredAsyncParameters = next;
        _configuredAsyncOpponent = _activeRunOpponent.Clone();
    }

    private void PreserveGameplayFlowAcrossSceneLoad()
    {
        _gameplayFlowAfterSceneLoad = gameplayFlowMode;
        _validationAfterSceneLoad =
            SingleContractValidationConfig.CopyOf(
                singleContractValidationConfig);
        _asyncOpponentAfterSceneLoad = gameplayFlowMode == GameplayFlowMode.AsyncChallenge
            ? _configuredAsyncOpponent?.Clone() : null;
        _asyncParametersAfterSceneLoad = gameplayFlowMode == GameplayFlowMode.AsyncChallenge
            ? _configuredAsyncParameters : null;
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        OnCoinsChanged.Invoke(Coins);
    }

    public void AddContractMarker()
    {
        ContractMarkerCount++;
        OnContractMarkerChanged.Invoke(ContractMarkerCount);
    }

    public bool TryPurchasePowerUp(PowerUpId id)
    {
        if (!PowerUpController.IsAvailable) return false;
        PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
        if (definition == null
            || !EchoRunSaveSystem.TryPurchasePowerUp(id, definition.cost))
        {
            AudioManager.Instance?.PlayUIError();
            return false;
        }
        TotalCoins = EchoRunSaveSystem.TotalCoins;
        OnBankedCoinsChanged.Invoke(TotalCoins);
        AudioManager.Instance?.PlayUIConfirm();
        return true;
    }

    public bool SelectPowerUp(PowerUpId id)
    {
        if (!PowerUpController.IsAvailable) return false;
        bool selected = EchoRunSaveSystem.SelectPowerUp(id);
        if (selected) AudioManager.Instance?.PlayUIConfirm();
        else AudioManager.Instance?.PlayUIError();
        return selected;
    }

    void SaveHighScore()
    {
        if (!ActiveRunRules.PersistRunProgress)
        {
            IsNewHighScore = false;
            return;
        }
        IsNewHighScore = Score > HighScore;
        if (IsNewHighScore) HighScore = Score;
        TotalCoins += Coins;
        EchoRunSaveSystem.SaveProgress(HighScore, TotalCoins);
        OnBankedCoinsChanged.Invoke(TotalCoins);
    }

    public static void SetNextRunSeed(int seed)
    {
        _nextRunSeed = seed;
    }

    public static float SelectCourseDistance(int generation,
        float calibrationDistance, float challengeDistance)
    {
        float calibration = Mathf.Max(1f, calibrationDistance);
        return generation <= 0
            ? calibration
            : Mathf.Max(calibration, challengeDistance);
    }

    public float ScheduleCourseFinishAfter(float seconds)
    {
        if (State != GameState.Playing) return CourseDistance;
        if (UsesSingleContractRules) return CourseDistance;
        float window = Mathf.Max(0f, seconds);
        CourseDistance = CalculateScheduledCourseDistance(
            Distance, CurrentSpeed, maxSpeed, speedIncreaseRate, window);
        CourseTargetDuration = RunElapsed + window;
        FinishScheduleCount++;
        return CourseDistance;
    }

    public static float CalculateScheduledCourseDistance(float currentDistance,
        float currentSpeed, float maximumSpeed, float acceleration,
        float seconds)
    {
        return Mathf.Max(0f, currentDistance)
               + EchoTimeRules.DistanceForAcceleratingRun(currentSpeed,
                   maximumSpeed, acceleration, seconds);
    }

    public static float SelectCourseDuration(int generation,
        float calibrationDuration, float challengeDuration)
    {
        float calibration = Mathf.Max(1f, calibrationDuration);
        return generation <= 0
            ? calibration
            : Mathf.Max(calibration, challengeDuration);
    }

    public static float SingleContractCourseDuration(bool hasActiveOpponent)
    {
        return hasActiveOpponent ? 95f : 55f;
    }

    private static int CreateRunSeed(int sequence)
    {
        unchecked
        {
            long ticks = System.DateTime.UtcNow.Ticks;
            return (int)(ticks ^ (ticks >> 32) ^ (sequence * 486187739));
        }
    }

    private void FinishTelemetry(RunEndReason endReason)
    {
        if (_telemetryFinished) return;
        _telemetryFinished = true;
        string reason = ToTelemetryReason(endReason);
        AIPlayerSkillEstimator.EndRun(Distance,
            AIRunTelemetry.IsCompletedTrainingReason(reason));
        StyleTracker.EndRun();
        AIRunTelemetry.FinishRun(this, reason,
            IsAsyncChallengeRun ? _activeRunOpponent.generation
            : AIShadowRunner.Instance != null ? AIShadowRunner.Instance.Generation : 0,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.ModelUpdateCount
                : EchoRunSaveSystem.DirectorModelUpdateCount,
            IsAsyncChallengeRun ? (float[])_activeRunOpponent.policyWeights.Clone()
            : AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetModelWeightsSnapshot()
                : null,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetModelWeightsSnapshot()
                : EchoRunSaveSystem.GetDirectorWeights(),
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetPolicyStateSnapshot()
                : EchoRunSaveSystem.GetDirectorPolicyJson(),
            IsAsyncChallengeRun ? JsonUtility.ToJson(new AIShadowSequenceState
            {
                transitions = (float[])_activeRunOpponent.sequenceTransitions.Clone(),
                pairCount = _activeRunOpponent.sequencePairCount
            }) : AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetSequenceStateSnapshot()
                : "");
    }

    internal void CompleteAsyncChallenge(RunEndReason reason, float playerLead)
    {
        if (!IsAsyncChallengeRun || LastAsyncChallengeResult != null
            || _activeAsyncParameters == null || _activeRunOpponent == null) return;
        LastAsyncChallengeResult = new AsyncChallengeResult(_activeAsyncParameters,
            _activeRunOpponent.identityId, Distance, playerLead, reason,
            reason == RunEndReason.FinishReached && playerLead >= 0f);
        InvokeOptional(AsyncChallengeCompleted, LastAsyncChallengeResult);
    }

    internal void NotifyLocalSingleContractSettled()
    {
        if (!IsSingleContractRun || LocalSingleContractSettled == null) return;
        foreach (System.Action handler in LocalSingleContractSettled.GetInvocationList())
        {
            try { handler(); }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Optional run listener failed: " + exception.GetType().Name);
            }
        }
    }

    private static void InvokeOptional<T>(System.Action<T> handlers, T value)
    {
        if (handlers == null) return;
        foreach (System.Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(value); }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Optional run listener failed: " + exception.GetType().Name);
            }
        }
    }

    public static string ToTelemetryReason(RunEndReason endReason)
    {
        switch (endReason)
        {
            case RunEndReason.FinishReached:
                return "finish_reached";
            case RunEndReason.Collision:
                return "collision";
            default:
                return "abandoned";
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && State == GameState.Playing)
            Pause();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && State == GameState.Playing)
            Pause();
    }

    void OnDestroy()
    {
        if (IsAsyncChallengeRun && (State == GameState.Playing || State == GameState.Paused))
        {
            LastEndReason = RunEndReason.Abandoned;
            AIShadowRunner.Instance?.FinalizeRunIfNeeded();
            FinishTelemetry(LastEndReason);
        }
        if (Instance == this) Instance = null;
    }
}
