using NUnit.Framework;

public sealed class EchoContractTests
{
    [Test]
    public void StrongLaneBiasCreatesOppositeLaneContract()
    {
        var style = new PlayerStyleData
        {
            lanePreference = 0.9f,
            laneSamples = 30
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 3);

        Assert.AreEqual(EchoContractType.BreakLaneHabit, contract.type);
        Assert.AreEqual(2, contract.learnedLane);
        Assert.AreEqual(0, contract.targetLane);
        StringAssert.Contains("右侧", contract.learnedTrait);
        StringAssert.Contains("路线", contract.objective);
    }

    [Test]
    public void StrongVerticalBiasCreatesOppositeActionContract()
    {
        var style = new PlayerStyleData
        {
            slideFrequency = 0.95f,
            verticalActionSamples = 12
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 4);

        Assert.AreEqual(EchoContractType.ChangeVerticalHabit, contract.type);
        Assert.AreEqual(ShadowAction.Slide, contract.learnedAction);
        Assert.AreEqual(ShadowAction.Jump, contract.targetAction);
    }

    [Test]
    public void StableRhythmCreatesAlternationContract()
    {
        var style = new PlayerStyleData
        {
            rhythmStability = 0.98f,
            rhythmSamples = 12
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 5);

        Assert.AreEqual(EchoContractType.DisruptRhythm, contract.type);
        StringAssert.Contains("节拍", contract.objective);
        Assert.AreNotEqual(ShadowAction.Keep, contract.targetAction);
        Assert.AreNotEqual(contract.learnedAction, contract.targetAction);
    }

    [Test]
    public void SparseStyleUsesHonestExplorationCopy()
    {
        EchoContractData contract = EchoContractPolicy.Create(
            new PlayerStyleData(), 1);

        Assert.IsTrue(contract.exploratory);
        StringAssert.StartsWith("AI探测：", contract.learnedTrait);
        StringAssert.DoesNotContain("AI识别：", contract.learnedTrait);
    }

    [Test]
    public void LaneContractRequiresSpacedRouteMarkersAndPunishesLearnedRoute()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            targetProgress = 100f,
            title = "lane"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        evaluator.TickLane(2, 2.1f);
        evaluator.RecordLaneMarker(0, 50f, 10f);
        evaluator.RecordLaneMarker(0, 51f, 10f);
        evaluator.RecordLaneMarker(0, 100f, 10f);
        evaluator.RecordLaneMarker(0, 150f, 10f);

        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        Assert.IsFalse(evaluator.Contract.completed);

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        RecordRequiredCounterattackLane(evaluator, 1, 2, 200f);
        RecordRequiredCounterattackLane(evaluator, 2, 0, 250f);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.playerProgressBonus, 0f);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void VerticalContractOnlyCountsRequiredSuccessfulDodges()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f,
            title = "vertical"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        evaluator.RecordDodge(ObstacleType.High, 1);
        Assert.AreEqual(0f, evaluator.Contract.progress);
        evaluator.RecordDodge(ObstacleType.Low, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);

        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        Assert.IsFalse(evaluator.Contract.completed);

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        while (!evaluator.Contract.completed)
            RecordRequiredCounterattackDodge(evaluator, 0);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void RhythmContractRejectsRepeatedDodgeAndCountsAlternation()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            targetProgress = 100f,
            title = "rhythm"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        ObstacleType first = RequiredObstacle(evaluator.Contract.targetAction);
        evaluator.RecordDodge(first);
        int firstFeedback = evaluator.Contract.feedbackSequence;
        evaluator.RecordDodge(first);
        while (!evaluator.Contract.initialBreakCompleted)
            evaluator.RecordDodge(RequiredObstacle(
                evaluator.Contract.targetAction));

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        while (!evaluator.Contract.completed)
            RecordRequiredCounterattackDodge(evaluator, 1);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
        Assert.Greater(evaluator.Contract.feedbackSequence, firstFeedback);
    }

    [Test]
    public void ContractChangesTrackPlanWithoutBlockingSafeLane()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.2f,
            coinChance = 0.3f,
            maxBlockedLanes = 1,
            shouldTurn = true
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetAction = ShadowAction.Jump,
            targetProgress = 3f
        };

        AITrackPlan changed = AITrackDirector.ApplyEchoContract(
            plan, contract, 2);
        int[] blocked = TrackManager.SelectContractBlockedLanes(
            changed.safeLane, changed.maxBlockedLanes, new[] { 0, 0, 0 },
            changed.echoContractType, changed.echoChallengeLane);

        Assert.AreEqual(EchoContractType.ChangeVerticalHabit,
            changed.echoContractType);
        Assert.AreNotEqual(changed.safeLane, changed.echoChallengeLane);
        Assert.IsFalse(changed.shouldTurn,
            "A contract test must remain a readable straight choice segment.");
        Assert.Contains(changed.echoChallengeLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == changed.safeLane));
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            changed.echoContractType, changed.echoTargetAction,
            10, changed.difficulty, 0.9f));
        Assert.IsTrue(TrackManager.RequiresGuaranteedContractRow(
            changed.echoContractType));
    }

    [Test]
    public void SixDuelPhasesProduceDistinctTrackEncounters()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.4f,
            coinChance = 0.5f,
            maxBlockedLanes = 1,
            shouldTurn = true
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            generation = 2,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 2,
            targetProgress = 100f
        };

        Assert.AreEqual(EchoEncounterKind.DetectionEvidence,
            AITrackDirector.ApplyEchoContract(plan, contract, 1,
                EchoDuelPhase.Detection).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.RevealChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 2,
                EchoDuelPhase.Reveal).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.ResistanceTest,
            AITrackDirector.ApplyEchoContract(plan, contract, 3,
                EchoDuelPhase.Resistance).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.CounterTest,
            AITrackDirector.ApplyEchoContract(plan, contract, 4,
                EchoDuelPhase.Counterattack).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.RewriteChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 5,
                EchoDuelPhase.Rewrite).echoEncounterKind);

        AITrackPlan finale = AITrackDirector.ApplyEchoContract(
            plan, contract, 6, EchoDuelPhase.Finale);
        Assert.AreEqual(EchoEncounterKind.FinaleOldHabit,
            finale.echoEncounterKind);
        Assert.IsFalse(finale.shouldTurn);
    }

    [Test]
    public void RevealChoiceExposesPredictedSafeAndRiskRewardRoutes()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 2,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 1 },
            contract, 2, EchoDuelPhase.Reveal);

        EchoEncounterLaneChoice[] choices =
            TrackManager.BuildEchoEncounterLaneChoices(plan);

        Assert.AreEqual(3, choices.Length);
        Assert.AreEqual(plan.echoPredictedLane, choices[0].lane);
        Assert.AreEqual(plan.echoSafeChoiceLane, choices[1].lane);
        Assert.AreEqual(plan.echoRiskChoiceLane, choices[2].lane);
        Assert.AreNotEqual(choices[0].lane, choices[1].lane);
        Assert.AreNotEqual(choices[0].lane, choices[2].lane);
        Assert.AreNotEqual(choices[1].lane, choices[2].lane);
        Assert.Greater(choices[0].maxCoinCount,
            choices[1].maxCoinCount);
        Assert.Greater(choices[2].maxCoinCount,
            choices[1].maxCoinCount);
        Assert.IsTrue(choices[0].echoContractMarker);
        Assert.IsTrue(choices[1].echoContractMarker);
        Assert.IsTrue(choices[2].echoContractMarker);
    }

    [Test]
    public void LaneResistanceAcceptsEitherCounterRouteButOnlyOnePerTest()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 1,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Resistance);

        evaluator.RecordLaneMarker(2, 50f, 10f);
        float afterFirstChoice = evaluator.Contract.progress;
        evaluator.RecordLaneMarker(0, 55f, 10f);

        Assert.AreEqual(34f, afterFirstChoice);
        Assert.AreEqual(afterFirstChoice, evaluator.Contract.progress,
            "A player cannot harvest both counter routes from one test segment.");
    }

    [Test]
    public void CounterattackRelocksOnceAfterARepeatedCounterRoute()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 0,
            initialBreakCompleted = true,
            counterattackActive = true,
            progress = 55f,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Counterattack);

        int initialHypothesis = evaluator.Contract.hypothesisVersion;
        RecordRequiredCounterattackLane(evaluator, 1, 2, 50f);
        Assert.AreEqual(0, evaluator.Contract.predictionLane,
            "One choice must not mutate the frozen hypothesis.");

        RecordRequiredCounterattackLane(evaluator, 1, 2, 100f);
        Assert.AreEqual(1, evaluator.Contract.predictionLane);
        Assert.AreEqual(1, evaluator.Contract.counterRelockCount);
        Assert.AreEqual(initialHypothesis + 1,
            evaluator.Contract.hypothesisVersion);
        StringAssert.StartsWith("反制生效 · 回声改判",
            evaluator.Contract.lastFeedback);
        StringAssert.Contains("中间路线",
            evaluator.BuildPredictionText(true));

        float progressBeforeHit = evaluator.Contract.progress;
        float shadowBeforeHit = evaluator.Contract.shadowProgressBonus;
        EchoChallengeStep repeat = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(repeat.stepId, repeat.predictedLane,
            2, 0, 150f);
        evaluator.RecordLaneMarker(1, 150f, 10f, repeat.stepId);
        Assert.AreEqual(progressBeforeHit, evaluator.Contract.progress,
            "A prediction hit changes the race lead, not settled lock damage.");
        Assert.Greater(evaluator.Contract.shadowProgressBonus,
            shadowBeforeHit);
        Assert.AreEqual(1, evaluator.Contract.counterRelockCount,
            "Counterattack may relock at most once.");
    }

    [Test]
    public void CounterattackOnlyScoresObstacleBoundToCurrentStep()
    {
        var evaluator = CounterattackEvaluator(
            EchoContractType.ChangeVerticalHabit);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        float before = evaluator.Contract.progress;

        evaluator.RecordDodge(RequiredObstacle(step.requiredAction), 1, 10f);
        Assert.AreEqual(before, evaluator.Contract.progress,
            "An ordinary obstacle must not score the current counterattack step.");

        EchoChallengeObstacleBinding binding = BindRequiredObstacle(
            evaluator, 1, step);
        evaluator.RecordDodge(RequiredObstacle(step.requiredAction), 1, 10f,
            binding);

        Assert.Greater(evaluator.Contract.progress, before);
        Assert.AreEqual(step.predictedAction,
            evaluator.Contract.predictionAction,
            "One counter must not rewrite the frozen prediction.");
        Assert.AreEqual(step.requiredAction,
            evaluator.ActiveChallengeStep.requiredAction);
        Assert.AreNotEqual(step.stepId,
            evaluator.ActiveChallengeStep.stepId);
    }

    [Test]
    public void StaleChallengeObstacleCannotScoreTheNextStep()
    {
        var evaluator = CounterattackEvaluator(EchoContractType.DisruptRhythm);
        EchoChallengeStep first = evaluator.ActiveChallengeStep;
        EchoChallengeObstacleBinding stale = BindRequiredObstacle(
            evaluator, 1, first);
        evaluator.RecordDodge(RequiredObstacle(first.requiredAction), 1, 10f,
            stale);
        float afterFirst = evaluator.Contract.progress;

        evaluator.RecordDodge(RequiredObstacle(first.requiredAction), 1, 10f,
            stale);

        Assert.AreEqual(afterFirst, evaluator.Contract.progress);
        Assert.AreNotEqual(first.stepId,
            evaluator.ActiveChallengeStep.stepId);
    }

    [Test]
    public void EncounterGateSettlesOnceAndRejectsTheStaleId()
    {
        var evaluator = CounterattackEvaluator(
            EchoContractType.ChangeVerticalHabit);
        EchoChallengeStep first = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(first.stepId, 0, 2, 1, 50f);
        Assert.IsTrue(evaluator.RecordEncounterInput(first.stepId,
            first.requiredAction, 2, 48f));

        Assert.IsTrue(evaluator.ResolveChallengeAtGate(first.stepId, 2, 10f));
        float afterSettlement = evaluator.Contract.progress;
        EchoEncounterResult result = evaluator.LastEncounterResult;
        Assert.IsFalse(evaluator.ResolveChallengeAtGate(first.stepId, 2, 10f));
        Assert.IsFalse(evaluator.RecordEncounterInput(first.stepId,
            first.requiredAction, 2, 52f));
        Assert.AreEqual(afterSettlement, evaluator.Contract.progress);
        Assert.AreEqual(first.stepId, result.encounterId);
        Assert.AreNotEqual(first.stepId,
            evaluator.ActiveChallengeStep.stepId);
    }

    [Test]
    public void DetectionAndRevealChoicesProduceTraceableEncounterResults()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            predictionLane = 2,
            targetLane = 0,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Detection);
        evaluator.RecordLaneMarker(2, 50f, 10f);
        int detectionId = evaluator.LastEncounterResult.encounterId;
        Assert.Greater(detectionId, 0);
        Assert.AreEqual(EchoEncounterOutcome.Evidence,
            evaluator.LastEncounterResult.outcome);
        Assert.AreEqual(1, evaluator.Contract.detectionEvidenceCount);

        evaluator.SetPhase(EchoDuelPhase.Reveal);
        evaluator.RecordLaneMarker(0, 100f, 10f);
        Assert.Greater(evaluator.LastEncounterResult.encounterId,
            detectionId);
        Assert.AreEqual(EchoEncounterOutcome.PredictionBroken,
            evaluator.LastEncounterResult.outcome);
        Assert.AreEqual(1, evaluator.Contract.revealEncounterCount);
    }

    [Test]
    public void CompletedContractCannotBeReopenedByCollision()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            initialBreakCompleted = true,
            completed = true,
            completionLocked = true,
            progress = 100f,
            targetProgress = 100f,
            duelPhase = EchoDuelPhase.Rewrite
        });
        float progressBefore = evaluator.Contract.progress;
        float shadowBefore = evaluator.Contract.shadowProgressBonus;

        evaluator.RecordHit(10f);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.IsTrue(evaluator.Contract.completionLocked);
        Assert.AreEqual(progressBefore, evaluator.Contract.progress);
        Assert.AreEqual(0f, evaluator.Contract.EchoLock01);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, shadowBefore,
            "A late collision may change the race but not reopen the contract.");
    }

    [Test]
    public void MissedChallengeAdvancesWithoutChangingStability()
    {
        var evaluator = CounterattackEvaluator(EchoContractType.DisruptRhythm);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(step.stepId, 0, 1, 2, 50f);
        float before = evaluator.Contract.progress;

        evaluator.RecordChallengeMissed(step.stepId);

        Assert.AreEqual(before, evaluator.Contract.progress);
        Assert.AreNotEqual(step.stepId,
            evaluator.ActiveChallengeStep.stepId);
        Assert.AreEqual("交锋取消 · 锁定不变",
            evaluator.Contract.lastFeedback);
        StringAssert.DoesNotContain("本题", evaluator.Contract.lastFeedback);
    }

    [Test]
    public void CounterattackInputIsEvidenceUntilTheGateSettlesIt()
    {
        var evaluator = CounterattackEvaluator(
            EchoContractType.ChangeVerticalHabit);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(step.stepId, 0, 2, 1, 50f);
        float before = evaluator.Contract.progress;

        Assert.IsFalse(evaluator.RecordCounterattackActionResponse(
            step.requiredAction, 10f));
        Assert.AreEqual(before, evaluator.Contract.progress);
        Assert.IsTrue(evaluator.RecordEncounterInput(step.stepId,
            step.requiredAction, 2, 48f));
        Assert.AreEqual(before, evaluator.Contract.progress,
            "Raw input evidence must never score directly.");
        Assert.IsTrue(evaluator.ResolveChallengeAtGate(step.stepId, 2, 10f));
        Assert.Greater(evaluator.Contract.progress, before);
        Assert.AreEqual(EchoEncounterOutcome.PredictionBroken,
            evaluator.LastEncounterResult.outcome);
        Assert.AreNotEqual(step.stepId,
            evaluator.ActiveChallengeStep.stepId);
    }

    [Test]
    public void CounterattackPredictedActionResponseCountsAsPredictionHit()
    {
        var evaluator = CounterattackEvaluator(EchoContractType.DisruptRhythm);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(step.stepId, 0, 2, 1, 50f);
        float before = evaluator.Contract.progress;
        float shadowBefore = evaluator.Contract.shadowProgressBonus;

        Assert.IsTrue(evaluator.RecordEncounterInput(step.stepId,
            step.predictedAction, 2, 48f));
        Assert.IsTrue(evaluator.ResolveChallengeAtGate(step.stepId, 2, 10f));

        Assert.AreEqual(before, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, shadowBefore);
        Assert.AreEqual(EchoEncounterOutcome.PredictionHit,
            evaluator.LastEncounterResult.outcome);
    }

    [Test]
    public void BoundCounterObstacleSettlesFromItsIdentityDuringLaneMotion()
    {
        var evaluator = CounterattackEvaluator(
            EchoContractType.ChangeVerticalHabit);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        EchoChallengeObstacleBinding binding = BindRequiredObstacle(
            evaluator, 2, step);
        float before = evaluator.Contract.progress;

        evaluator.RecordDodge(RequiredObstacle(step.requiredAction), 0, 10f,
            binding);

        Assert.Greater(evaluator.Contract.progress, before,
            "The bound obstacle is authoritative even while CurrentLane is " +
            "already changing again.");
    }

    [Test]
    public void PredictedObstacleSettlesAsEchoPredictionHit()
    {
        var evaluator = CounterattackEvaluator(EchoContractType.DisruptRhythm);
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(step.stepId, 0, 2, 1, 50f);
        var binding = new EchoChallengeObstacleBinding
        {
            stepId = step.stepId,
            role = EchoChallengeObstacleRole.Predicted,
            action = step.predictedAction,
            lane = 0
        };
        float before = evaluator.Contract.progress;
        float shadowBefore = evaluator.Contract.shadowProgressBonus;

        evaluator.RecordDodge(RequiredObstacle(step.predictedAction), 2, 10f,
            binding);

        Assert.AreEqual(before, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, shadowBefore);
        Assert.AreEqual(EchoEncounterOutcome.PredictionHit,
            evaluator.LastEncounterResult.outcome);
        Assert.AreNotEqual(step.stepId,
            evaluator.ActiveChallengeStep.stepId);
    }

    [Test]
    public void DirectorUsesChallengeStepAsCounterattackActionSource()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            targetAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Jump,
            targetProgress = 100f,
            duelPhase = EchoDuelPhase.Counterattack
        };
        var step = new EchoChallengeStep
        {
            stepId = 17,
            phase = EchoDuelPhase.Counterattack,
            contractType = EchoContractType.DisruptRhythm,
            status = EchoChallengeStepStatus.PendingSpawn,
            predictedAction = ShadowAction.Slide,
            requiredAction = ShadowAction.Jump,
            predictedLane = -1
        };

        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { maxBlockedLanes = 2 }, contract, 4,
            EchoDuelPhase.Counterattack, step);

        Assert.AreEqual(17, plan.echoChallengeStepId);
        Assert.AreEqual(ShadowAction.Slide, plan.echoPredictedAction);
        Assert.AreEqual(ShadowAction.Jump, plan.echoTargetAction);

        step.status = EchoChallengeStepStatus.Active;
        AITrackPlan deferred = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { maxBlockedLanes = 2 }, contract, 5,
            EchoDuelPhase.Counterattack, step);
        Assert.AreEqual(-17, deferred.echoChallengeStepId);
        Assert.IsTrue(TrackManager.ShouldDeferChallengeContent(deferred));
    }

    [Test]
    public void RevealActionChoiceImmediatelyChangesLeadWithoutBreakingContract()
    {
        var predicted = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetProgress = 100f
        });
        predicted.SetPhase(EchoDuelPhase.Reveal);
        predicted.RecordDodge(ObstacleType.Low, 1, 10f);

        var counter = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetProgress = 100f
        });
        counter.SetPhase(EchoDuelPhase.Reveal);
        counter.RecordDodge(ObstacleType.High, 1, 10f);

        Assert.Greater(predicted.Contract.shadowProgressBonus, 0f);
        Assert.AreEqual(0f, predicted.Contract.progress);
        Assert.Greater(counter.Contract.playerProgressBonus, 0f);
        Assert.AreEqual(0f, counter.Contract.progress);
    }

    [Test]
    public void RevealActionEncounterOffersPredictedAndCounterActionsTogether()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 1 },
            contract, 2, EchoDuelPhase.Reveal);

        int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
            plan, new[] { 0, 0, 0 });

        Assert.AreEqual(2, blocked.Length);
        Assert.Contains(plan.echoPredictedLane, blocked);
        Assert.Contains(plan.echoRiskChoiceLane, blocked);
        Assert.AreEqual(0, TrackManager.SelectEchoEncounterObstaclePrefabIndex(
            plan, plan.echoPredictedLane, 0, 0.5f, 0.5f));
        Assert.AreEqual(1, TrackManager.SelectEchoEncounterObstaclePrefabIndex(
            plan, plan.echoRiskChoiceLane, 0, 0.5f, 0.5f));
    }

    [Test]
    public void ActionEncounterKeepsSafeLaneOpenAndPlacesTestOnRiskLane()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 3,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 2 },
            contract, 3, EchoDuelPhase.Resistance);

        int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
            plan, new[] { 0, 0, 0 });

        Assert.AreEqual(contract.targetLane, plan.echoRiskChoiceLane);
        Assert.AreEqual(contract.targetAction, plan.echoTargetAction);
        Assert.AreEqual(contract.predictionAction,
            plan.echoPredictedAction);
        Assert.Contains(plan.echoRiskChoiceLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == plan.echoSafeChoiceLane));
    }

    [Test]
    public void CounterattackRotatesLanesPatternsAndSpacingBands()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 2,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        var basePlan = new AITrackPlan
        {
            safeLane = 1,
            maxBlockedLanes = 2
        };
        var patterns = new System.Collections.Generic.HashSet<EchoObstaclePattern>();
        var spacingBands = new System.Collections.Generic.HashSet<int>();
        bool[] predictedLanes = new bool[3];
        bool[] riskLanes = new bool[3];
        string previousSignature = null;

        for (int step = 0; step < 12; step++)
        {
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                basePlan, contract, step, EchoDuelPhase.Counterattack);
            plan = TrackManager.PrepareCounterObstacleRowPlan(plan, step);
            string signature = plan.echoObstaclePattern + ":"
                               + plan.echoPredictedLane + ":"
                               + plan.echoRiskChoiceLane + ":"
                               + plan.echoObstacleSpacingBand;

            Assert.AreNotEqual(previousSignature, signature,
                "Adjacent counter rows must not repeat the same layout.");
            Assert.AreNotEqual(plan.echoPredictedLane,
                plan.echoRiskChoiceLane);
            Assert.AreNotEqual(plan.echoSafeChoiceLane,
                plan.echoPredictedLane);
            Assert.AreNotEqual(plan.echoSafeChoiceLane,
                plan.echoRiskChoiceLane);
            patterns.Add(plan.echoObstaclePattern);
            spacingBands.Add(plan.echoObstacleSpacingBand);
            predictedLanes[plan.echoPredictedLane] = true;
            riskLanes[plan.echoRiskChoiceLane] = true;
            previousSignature = signature;
        }

        Assert.AreEqual(4, patterns.Count);
        Assert.AreEqual(3, spacingBands.Count);
        CollectionAssert.DoesNotContain(predictedLanes, false);
        CollectionAssert.DoesNotContain(riskLanes, false);
    }

    [Test]
    public void CounterattackDoesNotLeaveAnyLaneWithoutThreeRowsOfPressure()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 2,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        var basePlan = new AITrackPlan
        {
            safeLane = 1,
            maxBlockedLanes = 2
        };
        int[] drought = { 0, 0, 0 };
        var acceptedPatterns =
            new System.Collections.Generic.HashSet<EchoObstaclePattern>();

        for (int encounter = 0; encounter < 12; encounter++)
        {
            int step = encounter * 2;
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                basePlan, contract, step, EchoDuelPhase.Counterattack);
            plan = TrackManager.BalanceCounterEncounterLanes(plan, drought);
            plan = TrackManager.PrepareCounterObstacleRowPlan(
                plan, encounter);
            acceptedPatterns.Add(plan.echoObstaclePattern);
            int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
                plan, drought);

            Assert.IsFalse(System.Array.Exists(blocked,
                lane => lane == plan.echoSafeChoiceLane));
            for (int lane = 0; lane < drought.Length; lane++)
                drought[lane]++;
            for (int laneIndex = 0; laneIndex < blocked.Length; laneIndex++)
                drought[blocked[laneIndex]] = 0;
            for (int lane = 0; lane < drought.Length; lane++)
                Assert.LessOrEqual(drought[lane], 2,
                    "A lane stayed visually empty for three encounter rows.");
        }

        Assert.AreEqual(4, acceptedPatterns.Count,
            "Accepted rows must cycle every authored counter pattern.");
    }

    [Test]
    public void CounterattackLayoutBandsDoNotStretchRecoverySafeSpacing()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetAction = ShadowAction.Jump,
            predictionAction = ShadowAction.Slide,
            targetProgress = 100f
        };
        float recoveryFloor = TrackSpawnRules.MinimumObstacleRowSpacing(
            24f, 0.9f, 20f);

        for (int step = 0; step < 6; step++)
        {
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                new AITrackPlan { maxBlockedLanes = 2 }, contract, step,
                EchoDuelPhase.Counterattack);
            plan = TrackManager.PrepareCounterObstacleRowPlan(plan, step);
            float multiplier = TrackManager.EchoObstacleSpacingMultiplier(plan);
            Assert.AreEqual(1f, multiplier);
            Assert.AreEqual(recoveryFloor,
                recoveryFloor * multiplier, 0.001f);
        }
    }

    [Test]
    public void CounterattackStaggeredPatternsReverseObstacleOrder()
    {
        var plan = new AITrackPlan
        {
            echoEncounterKind = EchoEncounterKind.CounterTest,
            echoPredictedLane = 0,
            echoRiskChoiceLane = 2,
            echoObstaclePattern = EchoObstaclePattern.PredictedThenRisk
        };

        Assert.Less(TrackManager.EchoObstacleLaneOffset(plan, 0), 0f);
        Assert.Greater(TrackManager.EchoObstacleLaneOffset(plan, 2), 0f);

        plan.echoObstaclePattern = EchoObstaclePattern.RiskThenPredicted;
        Assert.Greater(TrackManager.EchoObstacleLaneOffset(plan, 0), 0f);
        Assert.Less(TrackManager.EchoObstacleLaneOffset(plan, 2), 0f);
    }

    [Test]
    public void FinaleCyclesOldHabitCounterHabitAndFreeChoiceStructures()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        };
        var plan = new AITrackPlan { safeLane = 1, maxBlockedLanes = 2 };

        Assert.AreEqual(EchoEncounterKind.FinaleOldHabit,
            AITrackDirector.ApplyEchoContract(plan, contract, 6,
                EchoDuelPhase.Finale).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.FinaleCounterHabit,
            AITrackDirector.ApplyEchoContract(plan, contract, 7,
                EchoDuelPhase.Finale).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.FinaleFreeChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 8,
                EchoDuelPhase.Finale).echoEncounterKind);
    }

    [Test]
    public void FinaleRouteWindowIsSplitIntoThreeStableSections()
    {
        Assert.AreEqual(0, AITrackDirector.FinaleSectionForRoute(
            100f, 100f, 300f));
        Assert.AreEqual(0, AITrackDirector.FinaleSectionForRoute(
            199f, 100f, 300f));
        Assert.AreEqual(1, AITrackDirector.FinaleSectionForRoute(
            201f, 100f, 300f));
        Assert.AreEqual(2, AITrackDirector.FinaleSectionForRoute(
            301f, 100f, 300f));
        Assert.AreEqual(2, AITrackDirector.FinaleSectionForRoute(
            450f, 100f, 300f));
    }

    [Test]
    public void FinaleStructuresUseDifferentPressureAndRewardTradeoffs()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        };
        var baseline = new AITrackPlan
        {
            difficulty = 0.8f,
            obstacleChance = 0.7f,
            coinChance = 0.7f,
            safeLane = 1,
            maxBlockedLanes = 2
        };

        AITrackPlan oldHabit = AITrackDirector.ApplyEchoContract(
            baseline, contract, 0, EchoDuelPhase.Finale);
        AITrackPlan counterHabit = AITrackDirector.ApplyEchoContract(
            baseline, contract, 1, EchoDuelPhase.Finale);
        AITrackPlan freeChoice = AITrackDirector.ApplyEchoContract(
            baseline, contract, 2, EchoDuelPhase.Finale);

        Assert.AreEqual(1, oldHabit.maxBlockedLanes);
        Assert.AreEqual(2, counterHabit.maxBlockedLanes);
        Assert.AreEqual(1, freeChoice.maxBlockedLanes);
        Assert.AreEqual(1, TrackManager.SelectEchoEncounterBlockedLanes(
            oldHabit, new[] { 0, 0, 0 }).Length);
        Assert.AreEqual(2, TrackManager.SelectEchoEncounterBlockedLanes(
            counterHabit, new[] { 0, 0, 0 }).Length);
        Assert.AreEqual(1, TrackManager.SelectEchoEncounterBlockedLanes(
            freeChoice, new[] { 0, 0, 0 }).Length);

        EchoEncounterLaneChoice[] oldRewards =
            TrackManager.BuildEchoEncounterLaneChoices(oldHabit);
        EchoEncounterLaneChoice[] counterRewards =
            TrackManager.BuildEchoEncounterLaneChoices(counterHabit);
        EchoEncounterLaneChoice[] freeRewards =
            TrackManager.BuildEchoEncounterLaneChoices(freeChoice);
        Assert.Greater(oldRewards[0].minCoinCount,
            oldRewards[1].minCoinCount,
            "The familiar route must be the visible old-habit temptation.");
        Assert.Greater(counterRewards[2].minCoinCount,
            counterRewards[0].minCoinCount,
            "The aggressive counter route must pay more than the predicted route.");
        Assert.Greater(freeRewards[2].minCoinCount,
            freeRewards[0].minCoinCount,
            "The free-choice risk route must carry the largest catch-up reward.");
    }

    [Test]
    public void OldHabitActionFinaleActuallyReplaysTheLearnedAction()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Jump,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { difficulty = 0.8f, maxBlockedLanes = 2 },
            contract, 0, EchoDuelPhase.Finale);

        int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
            plan, new[] { 0, 0, 0 });
        Assert.AreEqual(1, blocked.Length);
        Assert.AreEqual(plan.echoPredictedLane, blocked[0]);
        Assert.AreEqual(0, TrackManager.SelectEchoEncounterObstaclePrefabIndex(
            plan, blocked[0], 0, 0.8f, 0.5f),
            "The first finale section must replay the learned slide habit.");
    }

    [Test]
    public void RewriteProfileRewardsClearEvidenceNotOnlyVariety()
    {
        var style = new PlayerStyleData();
        var spam = new EchoRewriteTracker();
        spam.RecordRouteChoice(2, 0f);
        spam.RecordRouteChoice(2, 20f);
        spam.RecordRouteChoice(2, 40f);
        spam.RecordRouteChoice(2, 60f);
        spam.RecordVerticalAction(ShadowAction.Jump, false, 1f);
        spam.RecordVerticalAction(ShadowAction.Slide, false, 3f);
        EchoRewriteSnapshot spamSnapshot = spam.BuildSnapshot(style);
        Assert.Greater(spamSnapshot.writeStrength, 1f,
            "Repeated, valid route choices are a clear stable style.");
        Assert.AreEqual(0, spamSnapshot.effectiveVerticalActions);
        Assert.Greater(spamSnapshot.profileChange01, 0f);

        var varied = new EchoRewriteTracker();
        varied.RecordRouteChoice(0, 0f);
        varied.RecordRouteChoice(1, 20f);
        varied.RecordRouteChoice(2, 40f);
        varied.RecordRouteChoice(0, 60f);
        varied.RecordVerticalAction(ShadowAction.Jump, true, 1f);
        varied.RecordVerticalAction(ShadowAction.Slide, true, 2f);
        varied.RecordVerticalAction(ShadowAction.Jump, true, 5f);
        varied.RecordVerticalAction(ShadowAction.Slide, true, 6f);
        for (int i = 0; i < 4; i++)
            varied.RecordSuccessfulExecution();

        EchoRewriteSnapshot strong = varied.BuildSnapshot(style);
        Assert.Greater(strong.routeVariation01, 0.9f);
        Assert.Greater(strong.actionMix01, 0.9f);
        Assert.Greater(strong.rhythmNovelty01, 0.6f);
        Assert.Greater(strong.writeStrength, spamSnapshot.writeStrength);
        Assert.LessOrEqual(strong.writeStrength, 2f);

        varied.RecordMistake();
        EchoRewriteSnapshot afterHit = varied.BuildSnapshot(style);
        Assert.Less(afterHit.execution01, strong.execution01);
        Assert.Less(afterHit.writeStrength, strong.writeStrength);
    }

    [Test]
    public void RewriteMultiplierOnlyAppliesToEffectiveSamples()
    {
        Assert.AreEqual(1f, AIShadowRunner.ResolveRewriteLearningWeight(
            true, false, 1.9f));
        Assert.AreEqual(1f, AIShadowRunner.ResolveRewriteLearningWeight(
            false, true, 1.9f));
        Assert.AreEqual(1.9f, AIShadowRunner.ResolveRewriteLearningWeight(
            true, true, 1.9f), 0.001f);
        Assert.AreEqual(2f, AIShadowRunner.ResolveRewriteLearningWeight(
            true, true, 3f), 0.001f);
    }

    [Test]
    public void RewriteSnapshotFreezesTheNextGenerationStyle()
    {
        var style = new PlayerStyleData
        {
            lanePreference = 0.7f,
            laneSamples = 12
        };
        var tracker = new EchoRewriteTracker();
        tracker.RecordRouteChoice(0, 10f);
        EchoRewriteSnapshot snapshot = tracker.BuildSnapshot(style);
        EchoRewriteSnapshot clone = snapshot.Clone();
        float frozenLanePreference = snapshot.GetStyle().lanePreference;

        style.lanePreference = -0.9f;
        snapshot.styleJson = "{}";

        Assert.AreEqual(frozenLanePreference,
            clone.GetStyle().lanePreference, 0.001f);
        Assert.AreEqual(clone.BuildProfileSummary(),
            clone.Clone().BuildProfileSummary());
    }

    [Test]
    public void LaneRewriteAndFinaleChoicesAlwaysCarryEffectiveMarkers()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        };
        var baseline = new AITrackPlan { maxBlockedLanes = 2 };

        foreach (EchoDuelPhase phase in new[]
                 {
                     EchoDuelPhase.Rewrite,
                     EchoDuelPhase.Finale
                 })
        {
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                baseline, contract, phase == EchoDuelPhase.Rewrite ? 0 : 2,
                phase);
            EchoEncounterLaneChoice[] choices =
                TrackManager.BuildEchoEncounterLaneChoices(plan);
            Assert.AreEqual(3, choices.Length);
            Assert.IsTrue(choices[0].echoContractMarker);
            Assert.IsTrue(choices[1].echoContractMarker);
            Assert.IsTrue(choices[2].echoContractMarker);
        }
    }

    [Test]
    public void CompletedLaneFinaleMarkersStillChangeTheRaceLead()
    {
        EchoContractData CreateCompletedContract()
        {
            return new EchoContractData
            {
                type = EchoContractType.BreakLaneHabit,
                targetProgress = 100f,
                progress = 100f,
                completed = true,
                duelPhase = EchoDuelPhase.Finale
            };
        }

        var predicted = new EchoContractEvaluator(CreateCompletedContract());
        predicted.RecordFinaleRouteChoice(2, 2, 1, 0, 100f, 10f);
        Assert.Greater(predicted.Contract.shadowProgressBonus, 0f);
        Assert.AreEqual(0f, predicted.Contract.playerProgressBonus);

        var aggressive = new EchoContractEvaluator(CreateCompletedContract());
        aggressive.RecordFinaleRouteChoice(0, 2, 1, 0, 100f, 10f);
        Assert.Greater(aggressive.Contract.playerProgressBonus, 0f);
        Assert.AreEqual(0f, aggressive.Contract.shadowProgressBonus);
    }

    [Test]
    public void PhaseGateLeadUsesPreparedContentNotTheLongRoadShell()
    {
        Assert.AreEqual(3f, AIShadowRunner.CalculatePhaseGateLeadSeconds(
            0f, 60f, 20f), 0.001f);
        Assert.AreEqual(0f, AIShadowRunner.CalculatePhaseGateLeadSeconds(
            80f, 60f, 20f), 0.001f);
    }

    [Test]
    public void LaneContractKeepsItsPromisedTargetLaneSafe()
    {
        Assert.AreEqual(2, TrackManager.ChooseContractSafeLane(
            EchoContractType.BreakLaneHabit, 2, 1, new[] { 0, 20, 20 }));
    }

    [Test]
    public void RhythmContractRowsAlternateFromJumpToSlide()
    {
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            EchoContractType.DisruptRhythm, ShadowAction.Jump,
            0, 1f, 0f));
        Assert.AreEqual(0, TrackManager.SelectContractObstaclePrefabIndex(
            EchoContractType.DisruptRhythm, ShadowAction.Jump,
            1, 1f, 0f));
    }

    [Test]
    public void RetryContractResetsProgressButPreservesRule()
    {
        var failed = new EchoContractData
        {
            generation = 3,
            type = EchoContractType.DisruptRhythm,
            startingAction = ShadowAction.Jump,
            targetAction = ShadowAction.Slide,
            targetProgress = 4f,
            progress = 2f,
            shadowProgressBonus = 5f,
            lastFeedback = "failed"
        };

        EchoContractData retry = EchoContractPolicy.CreateForRun(
            new PlayerStyleData(), 3, UnityEngine.JsonUtility.ToJson(failed));

        Assert.AreEqual(EchoContractType.DisruptRhythm, retry.type);
        Assert.AreEqual(ShadowAction.Jump, retry.targetAction);
        Assert.AreEqual(0f, retry.progress);
        Assert.AreEqual(0f, retry.shadowProgressBonus);
        Assert.IsEmpty(retry.lastFeedback);
    }

    [Test]
    public void EmptyVerticalActionsCannotRewriteContractStyle()
    {
        Assert.IsFalse(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Jump, false));
        Assert.IsTrue(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Jump, true));
        Assert.IsFalse(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Left, true));
    }

    [Test]
    public void ChallengeGenerationOnlyAdvancesAfterVictory()
    {
        Assert.IsFalse(AIShadowRunner.ShouldAdvanceGeneration(
            true, false, false, false));
        Assert.IsFalse(AIShadowRunner.ShouldAdvanceGeneration(
            true, true, false, false));
        Assert.IsTrue(AIShadowRunner.ShouldAdvanceGeneration(
            true, true, true, false));
        Assert.IsTrue(AIShadowRunner.ShouldAdvanceGeneration(
            false, true, false, true));
    }

    [Test]
    public void ActiveGenerationSnapshotCloneIsDeepAndStable()
    {
        var source = new EchoGenerationSnapshot
        {
            generation = 3,
            policyWeights = new[] { 1f, 2f },
            sequenceTransitions = new[] { 3f, 4f },
            sequencePairCount = 7,
            styleJson = UnityEngine.JsonUtility.ToJson(new PlayerStyleData
            {
                lanePreference = 0.75f,
                laneSamples = 12
            }),
            pace = 13.5f,
            clarity = 0.8f
        };

        EchoGenerationSnapshot clone = source.Clone();
        string frozenJson = clone.ToJson();
        source.policyWeights[0] = 99f;
        source.sequenceTransitions[0] = 99f;
        source.styleJson = "{}";
        source.pace = 99f;

        Assert.AreEqual(1f, clone.policyWeights[0]);
        Assert.AreEqual(3f, clone.sequenceTransitions[0]);
        Assert.AreEqual(13.5f, clone.pace);
        Assert.AreEqual(0.75f, clone.GetStyle().lanePreference, 0.001f);
        Assert.AreEqual(frozenJson, clone.ToJson());
    }

    [Test]
    public void PendingPaceRejectsAbandonedShortAndTurboRuns()
    {
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.Abandoned, 500f, 30f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.Collision, 59f, 30f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 7.9f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 30f, true));
        Assert.IsTrue(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 30f, false));
    }

    [Test]
    public void DuelCadenceUsesTheFirstBatchPhaseWindows()
    {
        Assert.AreEqual(12f, EchoDuelFlow.DefaultDetectionDuration);
        Assert.AreEqual(4f, EchoDuelFlow.DefaultRevealDuration);
        Assert.AreEqual(24f, EchoDuelFlow.DefaultRewriteDuration);
        Assert.AreEqual(25f, EchoDuelFlow.DefaultFinaleDuration);
    }

    [Test]
    public void DuelWaitsForRouteBoundaryBeforeCommittingPhase()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(2f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Detection, flow.Phase);
        Assert.IsTrue(flow.TransitionPending);
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.PendingPhase);

        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsFalse(flow.TransitionPending);

        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);
    }

    [Test]
    public void RewriteRequestsFinaleAfterItsOwnWindow()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f,
            completed = true
        };

        Assert.IsTrue(flow.TransitionTo(EchoDuelPhase.Rewrite));
        Assert.IsFalse(flow.Tick(2.9f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);
        Assert.IsTrue(flow.IsRewriteLearningWindow);

        Assert.IsTrue(flow.Tick(0.1f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Finale, flow.PendingPhase);
        Assert.IsFalse(flow.PendingTransitionFailed);
    }

    [Test]
    public void UnbrokenResistanceRequestsExplicitFailureFinale()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.TransitionTo(EchoDuelPhase.Resistance));
        Assert.IsTrue(flow.Tick(0.1f, 5f, contract));
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Finale, flow.PendingPhase);
        Assert.IsTrue(flow.PendingTransitionFailed);
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.PendingFailurePhase);
    }

    [Test]
    public void ExhaustedCounterattackRequestsExplicitFailureFinale()
    {
        var flow = new EchoDuelFlow(true, 12f, 4f, 24f, 25f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            initialBreakCompleted = true,
            counterattackExhausted = true,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.TransitionTo(EchoDuelPhase.Counterattack));
        Assert.IsTrue(flow.Tick(0.1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Finale, flow.PendingPhase);
        Assert.IsTrue(flow.PendingTransitionFailed);
        Assert.AreEqual(EchoDuelPhase.Counterattack,
            flow.PendingFailurePhase);
    }

    [Test]
    public void EvidenceCanShortenFixedPhasesButNotTheirMinimumBeat()
    {
        var flow = new EchoDuelFlow(true, 12f, 4f, 24f, 25f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            detectionEvidenceCount = 2,
            targetProgress = 100f
        };

        Assert.IsFalse(flow.Tick(7.9f, 100f, contract));
        Assert.IsTrue(flow.Tick(0.1f, 100f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());

        contract.revealEncounterCount = 1;
        Assert.IsFalse(flow.Tick(2.9f, 100f, contract));
        Assert.IsTrue(flow.Tick(0.1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.PendingPhase);
    }

    [Test]
    public void ContractScoringFreezesAtGateAndLocksAfterFailedFinale()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetLane = 2,
            learnedLane = 0,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Resistance);

        evaluator.SetScoringSuspended(true);
        evaluator.RecordLaneMarker(2, 50f, 10f);
        Assert.AreEqual(0f, evaluator.Contract.progress);

        evaluator.SetScoringSuspended(false);
        evaluator.RecordLaneMarker(2, 50f, 10f);
        Assert.Greater(evaluator.Contract.progress, 0f);

        evaluator.LockForFinale(EchoDuelPhase.Resistance);
        float lockedProgress = evaluator.Contract.progress;
        evaluator.RecordLaneMarker(2, 100f, 10f);
        Assert.IsTrue(evaluator.Contract.duelFailed);
        Assert.AreEqual(EchoDuelPhase.Resistance,
            evaluator.Contract.failurePhase);
        Assert.AreEqual(lockedProgress, evaluator.Contract.progress);

        EchoContractData retry = evaluator.Contract.ResetForRun();
        Assert.IsFalse(retry.duelFailed);
        Assert.AreEqual(EchoDuelPhase.None, retry.failurePhase);
    }

    [Test]
    public void NextRouteBoundaryIsAlwaysTheNextSegmentEnd()
    {
        Assert.AreEqual(20f, TrackManager.NextRouteBoundary(0f, 20f));
        Assert.AreEqual(20f, TrackManager.NextRouteBoundary(19.9f, 20f));
        Assert.AreEqual(40f, TrackManager.NextRouteBoundary(20f, 20f));
    }

    [Test]
    public void PhaseGateStartsAfterAllAlreadyPreparedTrack()
    {
        Assert.AreEqual(140f, TrackManager.PreparedPhaseBoundary(
            35f, 131f, 20f));
        Assert.AreEqual(60f, TrackManager.PreparedPhaseBoundary(
            41f, 0f, 20f));
    }

    [Test]
    public void EveryPhaseKeepsAtLeastOneHundredTwentyMetersVisible()
    {
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Reveal));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Resistance));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Counterattack));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Rewrite));
        Assert.IsTrue(TrackSpawnRules.NeedsSegment(
            119.9f, 0f, 20f, 12));
        Assert.IsFalse(TrackSpawnRules.NeedsSegment(
            120f, 0f, 20f, 12));
        Assert.AreEqual(120f, TrackManager.ContentLookaheadDistance(20f));
        Assert.IsTrue(TrackManager.ShouldPrepareSegmentContent(
            119.9f, 0f, 20f));
        Assert.IsFalse(TrackManager.ShouldPrepareSegmentContent(
            120f, 0f, 20f));
        Assert.AreEqual(160f,
            TrackManager.ContentLookaheadDistance(20f, 16));
    }

    [Test]
    public void FixedPhaseCanPrepareItsGateBeforeTheVisibleWindowEnds()
    {
        var flow = new EchoDuelFlow(true, 12f, 4f, 24f, 25f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(4f, 100f, contract, 8f));
        Assert.AreEqual(EchoDuelPhase.Detection, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.PendingPhase);
    }

    [Test]
    public void OnlyScoredContractPhasesFreezeWhileWaitingForTheirGate()
    {
        Assert.IsFalse(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Detection));
        Assert.IsFalse(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Reveal));
        Assert.IsTrue(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Resistance));
        Assert.IsTrue(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Counterattack));
    }

    [Test]
    public void ScheduledPhaseOverridePreparesPlansBeyondTheRouteGate()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.4f,
            coinChance = 0.5f,
            maxBlockedLanes = 1
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            duelPhase = EchoDuelPhase.Detection,
            targetLane = 2,
            targetProgress = 100f
        };

        AITrackPlan beforeGate = AITrackDirector.ApplyEchoContract(
            plan, contract, 1);
        AITrackPlan beyondGate = AITrackDirector.ApplyEchoContract(
            plan, contract, 1, EchoDuelPhase.Resistance);

        Assert.AreEqual(EchoContractType.None,
            beforeGate.echoContractType);
        Assert.AreEqual(EchoContractType.BreakLaneHabit,
            beyondGate.echoContractType);
        Assert.AreEqual(2, beyondGate.echoRiskChoiceLane);
        Assert.AreNotEqual(beyondGate.echoPredictedLane,
            beyondGate.echoSafeChoiceLane);
    }

    [Test]
    public void SuccessfulDuelCanRequestAndCommitAllSixPhases()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(2f, 100f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);

        contract.initialBreakCompleted = true;
        Assert.IsTrue(flow.Tick(0.1f, 90f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Counterattack, flow.Phase);
        contract.completed = true;
        Assert.IsTrue(flow.Tick(0.1f, 80f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);

        Assert.IsTrue(flow.Tick(3f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase,
            "A phase request must wait for the route boundary.");
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Finale, flow.Phase);
    }

    [Test]
    public void OrdinaryCoinsCannotAdvanceLaneContract()
    {
        Assert.IsFalse(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.BreakLaneHabit, false));
        Assert.IsTrue(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.BreakLaneHabit, true));
        Assert.IsFalse(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.ChangeVerticalHabit, true));
    }

    [Test]
    public void DuelLeadUsesOnlyRunnerRoutePositions()
    {
        Assert.AreEqual(4f,
            AIShadowRunner.CalculatePhysicalLead(104f, 100f));
    }

    [Test]
    public void DistanceLeadCannotBypassEchoContract()
    {
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, true, false, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            -1f, true, true, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, false, true, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, true, true, RunEndReason.Collision));
        Assert.IsTrue(AIShadowRunner.IsContractVictory(
            0f, true, true, RunEndReason.FinishReached));
    }

    [Test]
    public void CourseDistanceUsesCalibrationThenChallengeLength()
    {
        Assert.AreEqual(450f,
            GameManager.SelectCourseDistance(0, 450f, 700f));
        Assert.AreEqual(700f,
            GameManager.SelectCourseDistance(1, 450f, 700f));
    }

    [Test]
    public void CourseDurationUsesCalibrationThenChallengeTiming()
    {
        Assert.AreEqual(75f,
            GameManager.SelectCourseDuration(0, 75f, 190f));
        Assert.AreEqual(190f,
            GameManager.SelectCourseDuration(1, 75f, 190f));
    }

    [Test]
    public void PartialEchoRequiresTimeActiveInputAndMinimumEvidence()
    {
        int[] actionCounts = { 0, 0, 0, 1, 0 };

        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            6, 1, actionCounts, 7.9f, 24));
        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            5, 1, actionCounts, 8f, 24));
        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            6, 0, actionCounts, 8f, 24));
        Assert.IsTrue(AIShadowRunner.HasPartialEchoSamples(
            6, 1, actionCounts, 8f, 24));
    }

    [Test]
    public void RunEndReasonsUseStableTelemetryNames()
    {
        Assert.AreEqual("finish_reached",
            GameManager.ToTelemetryReason(RunEndReason.FinishReached));
        Assert.AreEqual("collision",
            GameManager.ToTelemetryReason(RunEndReason.Collision));
        Assert.AreEqual("abandoned",
            GameManager.ToTelemetryReason(RunEndReason.Abandoned));
    }

    [Test]
    public void StyleSummaryExposesThreeHumanReadableSignals()
    {
        string summary = EchoContractPolicy.BuildStyleSummary(
            new PlayerStyleData
            {
                lanePreference = 0.8f,
                slideFrequency = 0.9f,
                verticalActionSamples = 12,
                rhythmStability = 0.9f
            });

        StringAssert.Contains("偏爱右路", summary);
        StringAssert.Contains("常用滑铲", summary);
        StringAssert.Contains("节奏固定", summary);
    }

    [Test]
    public void DetectionBlendsFrozenAndCurrentVerticalHabitSeventyThirty()
    {
        var frozen = new PlayerStyleData
        {
            slideFrequency = 0.58f,
            verticalActionSamples = 10,
            jumpActionSamples = 4,
            slideActionSamples = 6
        };
        var evidence = new EchoDetectionEvidence();
        evidence.RecordVertical(ShadowAction.Jump);
        evidence.RecordVertical(ShadowAction.Jump);

        PlayerStyleData blended = EchoContractPolicy.BlendDetectionStyle(
            frozen, evidence);
        Assert.AreEqual(0.406f, blended.slideFrequency, 0.001f);

        EchoContractData contract = EchoContractPolicy.CreateFromDetection(
            frozen, 1, evidence);
        Assert.AreEqual(EchoContractType.ChangeVerticalHabit, contract.type);
        Assert.AreEqual(ShadowAction.Jump, contract.learnedAction);
    }

    [Test]
    public void DetectionNeedsTwoValidChoicesBeforeCurrentRunCanInfluenceRule()
    {
        var frozen = new PlayerStyleData
        {
            slideFrequency = 0.58f,
            verticalActionSamples = 10,
            jumpActionSamples = 4,
            slideActionSamples = 6
        };
        var evidence = new EchoDetectionEvidence();
        evidence.RecordVertical(ShadowAction.Jump);

        PlayerStyleData blended = EchoContractPolicy.BlendDetectionStyle(
            frozen, evidence);
        Assert.AreEqual(0.58f, blended.slideFrequency, 0.001f);
    }

    [Test]
    public void DetectionContractLocksOnceAndCannotMoveDuringReveal()
    {
        var frozen = new PlayerStyleData
        {
            slideFrequency = 0.58f,
            verticalActionSamples = 10,
            jumpActionSamples = 4,
            slideActionSamples = 6
        };
        var evaluator = new EchoContractEvaluator(
            EchoContractPolicy.Create(frozen, 1));
        evaluator.SetPhase(EchoDuelPhase.Detection);
        evaluator.RecordDodge(ObstacleType.High, 1, 10f);
        evaluator.RecordDodge(ObstacleType.High, 1, 10f);

        Assert.IsTrue(evaluator.LockDetectionContract(frozen, 1));
        Assert.AreEqual(ShadowAction.Jump,
            evaluator.Contract.learnedAction);

        evaluator.RecordDodge(ObstacleType.Low, 1, 10f);
        evaluator.RecordDodge(ObstacleType.Low, 1, 10f);
        Assert.IsFalse(evaluator.LockDetectionContract(frozen, 1));
        Assert.AreEqual(ShadowAction.Jump,
            evaluator.Contract.learnedAction);
    }

    [Test]
    public void DetectionHudHidesProvisionalContractUntilItIsLocked()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            title = "不应提前公开",
            duelPhase = EchoDuelPhase.Detection
        });

        StringAssert.Contains("有效样本 0/2", evaluator.BuildHudText());
        StringAssert.DoesNotContain("不应提前公开", evaluator.BuildHudText());

        evaluator.LockDetectionContract(new PlayerStyleData(), 1);
        Assert.AreEqual("回声侦测 · 画像已锁定", evaluator.BuildHudText());
    }

    [Test]
    public void RetryKeepsItsPublishedRuleDuringDetection()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 2,
            learnedAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            preserveRuleForRetry = true,
            duelPhase = EchoDuelPhase.Detection
        };
        var evaluator = new EchoContractEvaluator(contract);
        evaluator.RecordDodge(ObstacleType.High, 1, 10f);
        evaluator.RecordDodge(ObstacleType.High, 1, 10f);

        Assert.IsTrue(evaluator.LockDetectionContract(
            new PlayerStyleData(), 2));
        Assert.AreEqual(ShadowAction.Slide,
            evaluator.Contract.learnedAction);
    }

    [Test]
    public void DetectionRowsOfferEqualMarkedChoicesAcrossAllThreeLanes()
    {
        var plan = new AITrackPlan
        {
            echoEncounterKind = EchoEncounterKind.DetectionEvidence,
            echoEncounterContractType = EchoContractType.ChangeVerticalHabit,
            echoPredictedLane = 0,
            echoSafeChoiceLane = 1,
            echoRiskChoiceLane = 2,
            echoEncounterStep = 2
        };

        EchoEncounterLaneChoice[] choices =
            TrackManager.BuildEchoEncounterLaneChoices(plan);
        Assert.AreEqual(3, choices.Length);
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 },
            new[] { choices[0].lane, choices[1].lane, choices[2].lane });
        Assert.IsTrue(choices[0].echoContractMarker);
        Assert.IsTrue(choices[1].echoContractMarker);
        Assert.IsTrue(choices[2].echoContractMarker);
        Assert.IsTrue(TrackManager.RequiresGuaranteedEchoEncounterRow(plan));
    }

    [Test]
    public void DetectionMarkersCountForEveryProvisionalContractType()
    {
        Assert.IsTrue(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.ChangeVerticalHabit,
            EchoDuelPhase.Detection, true));
        Assert.IsFalse(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.ChangeVerticalHabit,
            EchoDuelPhase.Reveal, true));
    }

    private static ObstacleType RequiredObstacle(ShadowAction action)
    {
        return action == ShadowAction.Jump
            ? ObstacleType.High : ObstacleType.Low;
    }

    private static EchoContractEvaluator CounterattackEvaluator(
        EchoContractType type)
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = type,
            learnedAction = ShadowAction.Slide,
            targetAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Jump,
            targetLane = 1,
            initialBreakCompleted = true,
            counterattackActive = true,
            progress = 55f,
            targetProgress = 100f,
            duelPhase = EchoDuelPhase.Resistance
        });
        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        return evaluator;
    }

    private static EchoChallengeObstacleBinding BindRequiredObstacle(
        EchoContractEvaluator evaluator, int lane, EchoChallengeStep step)
    {
        evaluator.BindChallengeStep(step.stepId, 0, lane,
            lane == 2 ? 1 : 2, 50f);
        return new EchoChallengeObstacleBinding
        {
            stepId = step.stepId,
            role = EchoChallengeObstacleRole.Required,
            action = step.requiredAction,
            lane = lane
        };
    }

    private static void RecordRequiredCounterattackDodge(
        EchoContractEvaluator evaluator, int lane)
    {
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        EchoChallengeObstacleBinding binding = BindRequiredObstacle(
            evaluator, lane, step);
        evaluator.RecordDodge(RequiredObstacle(step.requiredAction), lane, 10f,
            binding);
    }

    private static void RecordRequiredCounterattackLane(
        EchoContractEvaluator evaluator, int lane, int safeLane,
        float routeDistance)
    {
        EchoChallengeStep step = evaluator.ActiveChallengeStep;
        evaluator.BindChallengeStep(step.stepId, step.predictedLane,
            lane, safeLane, routeDistance);
        evaluator.RecordLaneMarker(lane, routeDistance, 10f, step.stepId);
    }

    private static ShadowAction Opposite(ShadowAction action)
    {
        return action == ShadowAction.Jump
            ? ShadowAction.Slide : ShadowAction.Jump;
    }
}
