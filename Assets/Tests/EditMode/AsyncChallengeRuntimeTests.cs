using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AsyncChallengeRuntimeTests
{
    private static readonly string[] StringKeys =
    {
        EchoRunSaveSystem.SaveKey, EchoRunSaveSystem.SaveSlotAKey,
        EchoRunSaveSystem.SaveSlotBKey, EchoRunSaveSystem.SingleContractSaveSlotAKey,
        EchoRunSaveSystem.SingleContractSaveSlotBKey, EchoRunSaveSystem.TelemetryKey,
        "AIShadowProfileV1"
    };
    private static readonly string[] IntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey, EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
        EchoRunSaveSystem.TrainingResetPendingKey, "HighScore", "TotalCoins",
        RunDifficultySettings.PreferenceKey
    };
    private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
    private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
    private readonly List<GameObject> _hosts = new List<GameObject>();
    private GameManager _manager;
    private AIShadowRunner _runner;
    private float _timeScale;

    [SetUp]
    public void SetUp()
    {
        Assert.IsNull(GameManager.Instance);
        Assert.IsNull(AIShadowRunner.Instance);
        Assert.IsNull(AITrackDirector.Instance);
        _timeScale = Time.timeScale;
        foreach (string key in StringKeys)
        {
            if (PlayerPrefs.HasKey(key)) _strings[key] = PlayerPrefs.GetString(key);
            PlayerPrefs.DeleteKey(key);
        }
        foreach (string key in IntKeys)
        {
            if (PlayerPrefs.HasKey(key)) _ints[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(key);
        }
        foreach (Type type in new[] { typeof(EchoRunSaveSystem), typeof(StyleTracker),
                     typeof(AIPlayerSkillEstimator), typeof(AIRunTelemetry),
                     typeof(AIRunRandom), typeof(GameManager) })
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (!field.IsLiteral && !field.IsInitOnly) _statics[field] = field.GetValue(null);
        }
        SetStatic(typeof(EchoRunSaveSystem), "_data", new EchoRunSaveData
        {
            highScore = 100, totalCoins = 20, directorModelUpdateCount = 7,
            shadowProfileJson = "{\"version\":5,\"sampleCount\":3}"
        });
        SetStatic(typeof(EchoRunSaveSystem), "_initialized", true);
        SetStatic(typeof(EchoRunSaveSystem), "_activeSlot", -1);
        SetStatic(typeof(EchoRunSaveSystem), "_generation", 0L);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractData", new EchoSingleContractSaveData());
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractInitialized", true);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractActiveSlot", -1);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractGeneration", 0L);
        SetStatic(typeof(EchoRunSaveSystem), "_trainingResetInProgress", false);
        SetStatic(typeof(EchoRunSaveSystem), "_trainingWritesEnabled", true);
        StyleTracker.ResetTrainingInMemory();
        AIPlayerSkillEstimator.ResetTrainingInMemory();
        AIRunTelemetry.ResetTrainingInMemory();
        PlayerPrefs.Save();
    }

    [TearDown]
    public void TearDown()
    {
        // Destruction itself is part of the isolation boundary, before restoring the fixture.
        for (int i = _hosts.Count - 1; i >= 0; i--)
            if (_hosts[i] != null) Object.DestroyImmediate(_hosts[i]);
        foreach (string key in StringKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in IntKeys) PlayerPrefs.DeleteKey(key);
        foreach (var entry in _strings) PlayerPrefs.SetString(entry.Key, entry.Value);
        foreach (var entry in _ints) PlayerPrefs.SetInt(entry.Key, entry.Value);
        foreach (var entry in _statics) entry.Key.SetValue(null, entry.Value);
        PlayerPrefs.Save();
        Time.timeScale = _timeScale;
        _hosts.Clear();
        _strings.Clear();
        _ints.Clear();
        _statics.Clear();
        _manager = null;
        _runner = null;
    }

    [Test]
    public void ZeroTimeGhostSmoothingKeepsVelocityFiniteWhenTheNextFrameResumes()
    {
        float engineVelocity = 0f;
        Mathf.SmoothDamp(1f, 1f, ref engineVelocity, .14f, 12f, 0f);
        Debug.Log("GHOST_SMOOTH_DAMP_ZERO_TIME engineVelocityIsNaN=" + float.IsNaN(engineVelocity));

        float velocity = 0f;
        float lane = AIShadowRunner.SmoothGhostVisualValue(1f, 1f,
            ref velocity, .14f, 12f, 0f);
        Assert.AreEqual(1f, lane);
        Assert.AreEqual(0f, velocity);
        lane = AIShadowRunner.SmoothGhostVisualValue(lane, 2f,
            ref velocity, .14f, 12f, 1f / 60f);
        Assert.IsFalse(float.IsNaN(lane) || float.IsInfinity(lane));
        Assert.IsFalse(float.IsNaN(velocity) || float.IsInfinity(velocity));
        Assert.Greater(lane, 1f);
        Assert.Less(lane, 2f);
    }

    [Test]
    public void GhostSmoothingPreservesTheOrdinaryPositiveTimeCalculation()
    {
        float expectedVelocity = .4f;
        float actualVelocity = expectedVelocity;
        float expected = Mathf.SmoothDamp(0f, 5f, ref expectedVelocity, .12f, 80f, .02f);
        float actual = AIShadowRunner.SmoothGhostVisualValue(0f, 5f,
            ref actualVelocity, .12f, 80f, .02f);
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(expectedVelocity, actualVelocity);
        Assert.AreEqual(actual, AIShadowRunner.SmoothGhostVisualValue(actual, 5f,
            ref actualVelocity, .12f, 80f, 0f));
        Assert.AreEqual(expectedVelocity, actualVelocity);
    }

    [Test]
    public void DefaultModeAndNormalCapabilitiesRemainUnchanged()
    {
        CreateManager();
        Assert.AreEqual(GameplayFlowMode.SingleContract, _manager.ConfiguredGameplayFlowMode);
        Assert.IsTrue(_manager.UsesSingleContractRules);
        Assert.AreEqual(0, (int)GameplayFlowMode.SixPhaseLegacy);
        Assert.AreEqual(1, (int)GameplayFlowMode.SingleContract);
        Assert.IsTrue(EchoRunRules.For(GameplayFlowMode.SingleContract).AllowGateRelearning);
        Assert.IsTrue(EchoRunRules.For(GameplayFlowMode.SingleContract).PersistRunProgress);
        Assert.IsTrue(EchoRunRules.For(GameplayFlowMode.SixPhaseLegacy).AllowDirectorTraining);
        Assert.IsFalse(EchoRunRules.For(GameplayFlowMode.AsyncChallenge).AllowIdentityCommit);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InjectedOpponentIsDeepFrozenAndOwnsCourseRegardlessOfLocalIdentity(bool hasLocal)
    {
        if (hasLocal) InstallLocalIdentity();
        CreateManager();
        ActiveEchoIdentity source = SingleContractValidationIdentity.Create();
        string expected = source.ToJson();
        Assert.IsTrue(_manager.TryConfigureAsyncChallenge(source, Parameters(), out string error), error);
        source.policyWeights[0] += 10f;
        source.memoryContract.preferredLane = 0;
        StartConfigured();
        Assert.IsTrue(_manager.IsAsyncChallengeRun);
        Assert.IsTrue(_manager.UsesSingleContractRules);
        Assert.IsTrue(_runner.HasActiveOpponent);
        Assert.AreEqual(GameplayFlowMode.AsyncChallenge, _runner.SingleContractRuntime.Mode);
        Assert.AreEqual(95f, _manager.CourseTargetDuration);
        Assert.AreEqual(expected, _runner.OpponentIdentityPreview.ToJson());
        var preview = _manager.ActiveOpponentIdentityPreview;
        preview.policyWeights[1] += 20f;
        Assert.AreEqual(expected, _manager.ActiveOpponentIdentityPreview.ToJson());
        Assert.IsNull(Get(_runner, "_runIdentityDraft"));
        Assert.AreEqual(GameplayFlowMode.AsyncChallenge, AIRunTelemetry.ActiveRun.gameplayFlowMode);
        Assert.AreEqual(1, AIRunTelemetry.ActiveRun.rulesVersion);
        Assert.AreEqual(_runner.Generation, AIRunTelemetry.ActiveRun.shadowGenerationAtStart);
    }

    [Test]
    public void InvalidConfigurationDoesNotReplaceLocalModeOrStartARun()
    {
        CreateManager();
        var identity = SingleContractValidationIdentity.Create();
        identity.version = 999;
        Assert.IsFalse(_manager.TryConfigureAsyncChallenge(identity, Parameters(), out _));
        Assert.AreEqual(GameplayFlowMode.SingleContract, _manager.ConfiguredGameplayFlowMode);
        Assert.IsFalse(_manager.TryConfigureGameplayFlow(GameplayFlowMode.AsyncChallenge));
        Assert.IsFalse(_manager.TryConfigureAsyncChallenge(SingleContractValidationIdentity.Create(),
            new AsyncChallengeRunParameters("id", 2, 123), out _));
        Assert.IsNull(_manager.ConfiguredAsyncChallengeParameters);
        Assert.AreEqual(GameState.Menu, _manager.State);
    }

    [Test]
    public void AsyncSpeedsAndStandardDifficultyDoNotChangeOrdinaryConfiguration()
    {
        CreateManager();
        _manager.startSpeed = 7f;
        _manager.maxSpeed = 31f;
        _manager.speedIncreaseRate = 0.4f;
        PlayerPrefs.SetInt(RunDifficultySettings.PreferenceKey, (int)RunDifficultyLevel.Relaxed);
        ConfigureAndStart();
        Assert.AreEqual(10f, _manager.startSpeed);
        Assert.AreEqual(24f, _manager.maxSpeed);
        Assert.AreEqual(0.12f, _manager.speedIncreaseRate);
        Assert.AreEqual(RunDifficultyLevel.Standard, _manager.ActiveRunDifficulty);
        Assert.AreEqual(RunDifficultyLevel.Relaxed, RunDifficultySettings.Current);
        Assert.AreEqual(EchoTimeRules.DistanceForAcceleratingRun(10f, 24f, 0.12f, 95f),
            _manager.CourseDistance, 0.001f);
        Invoke(_runner, "FinishRunWithReason", RunEndReason.Abandoned);
        Set(_manager, "<State>k__BackingField", GameState.Menu);
        Assert.IsTrue(_manager.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract));
        Invoke(_manager, "FreezeGameplayFlowConfiguration");
        Assert.AreEqual(7f, _manager.startSpeed);
        Assert.AreEqual(31f, _manager.maxSpeed);
        Assert.AreEqual(0.4f, _manager.speedIncreaseRate);
        Assert.AreEqual(RunDifficultyLevel.Relaxed, _manager.ActiveRunDifficulty);
        Assert.IsTrue(_manager.ActiveRunRules.AllowPlayerTraining);
    }

    [TestCase(GameplayFlowMode.AsyncChallenge, false)]
    [TestCase(GameplayFlowMode.SingleContract, true)]
    public void CounterPairChangesPredictionsOnlyInOrdinaryMode(GameplayFlowMode mode, bool relearns)
    {
        var flow = new SingleContractFlow(new SingleContractAcceleratingGateWindowFactory(10f, 24f, 0.12f), 2);
        flow.BeginRun(new EchoRunContext
        {
            mode = mode, runSeed = 123, runSequence = 2, hasOpponent = true,
            generation = 1, courseDistance = EchoTimeRules.DistanceForAcceleratingRun(10f, 24f, 0.12f, 95f)
        });
        for (int i = 0; i < 2; i++)
        {
            PredictionGateDefinition gate = flow.GetGate(i).Definition;
            int lane = -1;
            foreach (var candidate in gate.lanes)
                if (candidate.role == PredictionGateRole.Counter) lane = candidate.physicalLane;
            flow.Tick(new EchoRunFrame
            {
                elapsedTime = 12f + i * 16f, playerDistance = gate.commitDistance,
                currentSpeed = 20f, playerLane = lane
            });
            Assert.AreEqual(GateTransitionResult.Applied, flow.ResolveObstaclePassed(new GateObstacleEvent
            {
                gateId = gate.gateId, obstacleId = gate.gateId * 10, physicalLane = lane
            }));
        }
        Assert.AreEqual(2, flow.SettlementCount);
        Assert.Greater(flow.AccumulatedSignedLeadMeters, 0f);
        Assert.AreEqual(relearns, flow.RelearnTriggered);
        Assert.AreEqual(relearns ? 2 : 1, flow.GetGate(2).Definition.hypothesisVersion);
    }

    [TestCase(RunEndReason.FinishReached, 5f, true)]
    [TestCase(RunEndReason.FinishReached, -5f, false)]
    [TestCase(RunEndReason.Collision, 5f, false)]
    [TestCase(RunEndReason.Abandoned, 5f, false)]
    public void AsyncSettlementNeverPromotesOrWritesTrainingAndEmitsOnce(RunEndReason reason, float lead, bool won)
    {
        InstallLocalIdentity();
        CreateManager();
        string localBefore = ProtectedSave();
        string styleBefore = JsonUtility.ToJson(StyleTracker.GetSnapshot());
        string skillBefore = JsonUtility.ToJson(AIPlayerSkillEstimator.GetSnapshot());
        int completed = 0;
        int localSettled = 0;
        _manager.AsyncChallengeCompleted += _ => completed++;
        _manager.LocalSingleContractSettled += () => localSettled++;
        ConfigureAndStart();
        string opponentBefore = _runner.OpponentIdentityPreview.ToJson();
        Invoke(_runner, "Learn", ShadowAction.Jump, new[] { 1f, 0f, 0.4f, 1f, 0f, 0.3f, 0f, 0f }, true);
        StyleTracker.TickLane(0, 2f);
        StyleTracker.RecordAction(ShadowAction.Jump, 0.8f, 0.2f, false, true);
        AIPlayerSkillEstimator.RecordSegmentOutcome(true, 100f);
        Set(_manager, "<Distance>k__BackingField", 200f);
        Set(_manager, "<RunElapsed>k__BackingField", 20f);
        Set(_manager, "<Score>k__BackingField", 999);
        _manager.AddCoins(5);
        Set(_runner, "_ghostProgress", 200f - lead);
        Invoke(_manager, "SaveHighScore");
        Invoke(_runner, "FinishRunWithReason", reason);
        Invoke(_runner, "FinishRunWithReason", reason);
        Invoke(_manager, "FinishTelemetry", reason);
        Assert.AreEqual(1, completed);
        Assert.AreEqual(0, localSettled);
        Assert.AreEqual(won, _manager.LastAsyncChallengeResult.won);
        Assert.AreEqual(200f, _manager.LastAsyncChallengeResult.distanceMeters);
        Assert.AreEqual(lead, _manager.LastAsyncChallengeResult.playerLeadMeters, 0.001f);
        Assert.AreEqual(999, _manager.Score);
        Assert.AreEqual(5, _manager.Coins);
        Assert.AreEqual(100, _manager.HighScore);
        Assert.AreEqual(20, _manager.TotalCoins);
        Assert.IsFalse(_manager.IsNewHighScore);
        Assert.IsFalse(_runner.LastSingleContractIdentityPromoted);
        SingleContractResultFacts resultFacts = _runner.LastSingleContractResultFacts;
        Assert.IsTrue(resultFacts.available);
        Assert.IsTrue(resultFacts.isAsync);
        Assert.IsFalse(resultFacts.promotionBuilt);
        Assert.IsFalse(resultFacts.commitAttempted);
        Assert.IsFalse(resultFacts.identityPromoted);
        SingleContractResultViewData resultView = EchoRunPresentation.BuildSingleContractResultView(resultFacts);
        Assert.AreEqual(SingleContractResultKind.AsyncChallenge, resultView.kind);
        Assert.AreEqual(SingleContractResultNextAction.RetryAsync, resultView.nextAction);
        StringAssert.DoesNotContain("下一代", resultView.summary);
        Assert.AreEqual(opponentBefore, _runner.OpponentIdentityPreview.ToJson());
        Assert.AreEqual(styleBefore, JsonUtility.ToJson(StyleTracker.GetSnapshot()));
        Assert.AreEqual(skillBefore, JsonUtility.ToJson(AIPlayerSkillEstimator.GetSnapshot()));
        Assert.AreEqual(localBefore, ProtectedSave());
        Assert.IsFalse(AIRunTelemetry.IsCompletedTrainingRun(AIRunTelemetry.ActiveRun));
        Object.DestroyImmediate(_runner.gameObject);
        Assert.AreEqual(localBefore, ProtectedSave());
        Assert.AreEqual(1, completed);
    }

    [Test]
    public void DirectorStillGeneratesObstaclesWithoutUpdatingOrSavingItsModel()
    {
        CreateManager();
        ConfigureAndStart();
        AITrackDirector director = CreateInactive<AITrackDirector>("Async Director Test");
        Set(director, "_gameManager", _manager);
        director.observationSegments = 0;
        director.PrepareForRun();
        string modelBefore = director.GetPolicyStateSnapshot();
        string saveBefore = ProtectedSave();
        int updates = director.ModelUpdateCount;
        AITrackPlan plan = director.CreatePlan(0.8f, 0.6f, 0.7f, 0.1f, 1, false, 0f, 20f);
        Assert.AreNotEqual(AIDirectorIntent.Observe, plan.intent);
        Assert.Greater(plan.obstacleChance, 0f);
        director.ActivatePlanForDistance(1f);
        director.RecordCoin();
        director.RecordDodge();
        director.FinalizeActivePlanForRunEnd(20f);
        Invoke(director, "SaveDirectorModel");
        Assert.AreEqual(updates, director.ModelUpdateCount);
        Assert.AreEqual(modelBefore, director.GetPolicyStateSnapshot());
        Assert.AreEqual(saveBefore, ProtectedSave());
        Set(_runner, "_directiveSource", director);
        var directive = (ShadowAIDirective)Invoke(_runner, "GetShadowDirective");
        Assert.AreEqual(JsonUtility.ToJson(ShadowAIDirective.Neutral), JsonUtility.ToJson(directive));
    }

    [Test]
    public void RetryKeepsOpponentAndSeedButAllocatesANewInjectedId()
    {
        CreateManager();
        ConfigureAndStart();
        Invoke(_runner, "FinishRunWithReason", RunEndReason.Collision);
        AsyncChallengeResult firstResult = _manager.LastAsyncChallengeResult;
        string opponent = _manager.ActiveOpponentIdentityPreview.ToJson();
        _manager.ChallengeIdFactory = () => "retry-two";
        Invoke(_manager, "PrepareAsyncRetry");
        Assert.AreEqual("retry-two", _manager.ConfiguredAsyncChallengeParameters.challengeId);
        Assert.AreEqual(123, _manager.ConfiguredAsyncChallengeParameters.runSeed);
        Assert.AreEqual("challenge-one", firstResult.challengeId);
        Assert.AreEqual(opponent, ((ActiveEchoIdentity)Get(_manager, "_configuredAsyncOpponent")).ToJson());
        Invoke(_manager, "PreserveGameplayFlowAcrossSceneLoad");
        var carried = (AsyncChallengeRunParameters)typeof(GameManager).GetField(
            "_asyncParametersAfterSceneLoad", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Assert.AreEqual("retry-two", carried.challengeId);
    }

    [Test]
    public void UnfinishedDestructionReportsAbandonedOnceWithoutWritingIdentity()
    {
        InstallLocalIdentity();
        CreateManager();
        string before = ProtectedSave();
        ConfigureAndStart();
        int calls = 0;
        _manager.AsyncChallengeCompleted += _ => calls++;
        // EditMode does not drive the normal MonoBehaviour lifecycle for this fixture.
        // Exercise the boundary explicitly; PlayMode covers a real scene-owned Destroy.
        Invoke(_runner, "OnDestroy");
        Object.DestroyImmediate(_runner.gameObject);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(RunEndReason.Abandoned, _manager.LastAsyncChallengeResult.reason);
        Invoke(_manager, "OnDestroy");
        Assert.AreEqual(1, calls);
        Assert.AreEqual(before, ProtectedSave());
    }

    private void CreateManager()
    {
        _manager = CreateInactive<GameManager>("Async Manager Test");
        Set(_manager, "<HighScore>k__BackingField", 100);
        Set(_manager, "<TotalCoins>k__BackingField", 20);
    }

    private void ConfigureAndStart()
    {
        Assert.IsTrue(_manager.TryConfigureAsyncChallenge(SingleContractValidationIdentity.Create(),
            Parameters(), out string error), error);
        StartConfigured();
    }

    private void StartConfigured()
    {
        var host = new GameObject("Async Runner Test");
        _hosts.Add(host);
        _runner = host.AddComponent<AIShadowRunner>();
        Set(_runner, "_gameManager", _manager);
        _manager.OnStateChanged.AddListener(state => Invoke(_runner, "OnGameStateChanged", state));
        Assert.IsTrue(_manager.TryStartConfiguredRun(out string error), error);
    }

    private T CreateInactive<T>(string name) where T : Component
    {
        var host = new GameObject(name);
        host.SetActive(false);
        _hosts.Add(host);
        return host.AddComponent<T>();
    }

    private static AsyncChallengeRunParameters Parameters() => new AsyncChallengeRunParameters("challenge-one", 1, 123);

    private static void InstallLocalIdentity()
    {
        var local = SingleContractValidationIdentity.Create();
        local.identityId = "local-identity";
        local.memoryContract.identityId = local.identityId;
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractData", new EchoSingleContractSaveData { activeIdentity = local });
    }

    private static string ProtectedSave()
    {
        return JsonUtility.ToJson(EchoRunSaveSystem.GetSingleContractSaveData())
               + "|" + PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotAKey)
               + "|" + PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotBKey)
               + "|" + EchoRunSaveSystem.GetShadowProfileJson()
               + "|" + EchoRunSaveSystem.GetPlayerStyleJson()
               + "|" + EchoRunSaveSystem.GetSkillProfileJson()
               + "|" + EchoRunSaveSystem.GetDirectorPolicyJson()
               + "|" + EchoRunSaveSystem.DirectorModelUpdateCount;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, name);
        return method.Invoke(target, args);
    }
    private static object Get(object target, string name) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetStatic(Type type, string name, object value) => type.GetField(name,
        BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
}
