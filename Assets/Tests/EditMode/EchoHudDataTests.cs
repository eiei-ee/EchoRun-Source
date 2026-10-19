using NUnit.Framework;
using UnityEngine;

public class EchoHudDataTests
{
    [Test]
    public void CalibrationKeepsItsOwnMeterAndHidesDuelOnlySignals()
    {
        EchoHudViewData view = EchoRunPresentation.BuildHud(false, null, 0f,
            2, 2, 1, 2, 0.45f, EchoDuelPhase.Calibration, 0.8f, "", 2,
            0f, 1.25f, 120f, 0);

        Assert.AreEqual(EchoHudMode.Calibration, view.mode);
        Assert.AreEqual(EchoHudMeterKind.Calibration, view.meterKind);
        Assert.AreEqual(0.45f, view.calibrationProgress01);
        Assert.AreEqual(0.45f, view.displayedMeter01);
        Assert.IsFalse(view.showPrediction);
        Assert.AreEqual(-1, view.phaseIndex);
    }

    [TestCase(EchoDuelPhase.Detection, EchoHudMode.Detection,
        EchoHudMeterKind.Phase)]
    [TestCase(EchoDuelPhase.Reveal, EchoHudMode.Reveal,
        EchoHudMeterKind.None)]
    [TestCase(EchoDuelPhase.Resistance, EchoHudMode.Resistance,
        EchoHudMeterKind.EchoLock)]
    [TestCase(EchoDuelPhase.Counterattack, EchoHudMode.Counterattack,
        EchoHudMeterKind.EchoLock)]
    [TestCase(EchoDuelPhase.Rewrite, EchoHudMode.Rewrite,
        EchoHudMeterKind.Phase)]
    public void ChallengePhaseChoosesOnePrimaryMeter(EchoDuelPhase phase,
        EchoHudMode expectedMode, EchoHudMeterKind expectedMeter)
    {
        EchoContractData contract = Contract(0.6f, false);
        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 2.4f,
            2, 2, 2, 2, 1f, phase, 0.35f, "预判右路", 2,
            0f, 1.25f, 80f, 4);

        Assert.AreEqual(expectedMode, view.mode);
        Assert.AreEqual(expectedMeter, view.meterKind);
        Assert.AreEqual(0.4f, view.echoLock01, 0.0001f);
        Assert.AreEqual(view.echoLock01, view.contractStability01, 0.0001f);
        Assert.AreEqual(0.35f, view.phaseProgress01, 0.0001f);
        float expectedDisplayed = expectedMeter == EchoHudMeterKind.Phase
            ? 0.35f
            : expectedMeter == EchoHudMeterKind.EchoLock ? 0.4f : 0f;
        Assert.AreEqual(expectedDisplayed, view.displayedMeter01, 0.0001f);
    }

    [Test]
    public void FinaleNeverOffersAHiddenLastContractChance()
    {
        EchoHudViewData incomplete = BuildFinale(Contract(0.65f, false));
        EchoHudViewData complete = BuildFinale(Contract(1f, true));

        Assert.AreEqual(EchoHudMode.FinaleFailed, incomplete.mode);
        Assert.AreEqual(EchoHudMeterKind.None, incomplete.meterKind);
        Assert.AreEqual("契约锁定 · 完成追逐", incomplete.directiveShort);
        Assert.AreEqual(EchoHudMode.FinaleClean, complete.mode);
        Assert.AreEqual(EchoHudMeterKind.None, complete.meterKind);
        Assert.AreEqual(0f, complete.displayedMeter01);
    }

    [Test]
    public void LeadMarkerUsesSoftSaturationWhileDistanceStaysReal()
    {
        EchoHudViewData normal = EchoRunPresentation.BuildHud(true,
            Contract(0.5f, false), 80f, 2, 2, 2, 2, 1f,
            EchoDuelPhase.Resistance, 0f, "", 2, 0f, 1.25f, 50f, 0);
        EchoHudViewData finale = EchoRunPresentation.BuildHud(true,
            Contract(0.5f, false), 2.4f, 2, 2, 2, 2, 1f,
            EchoDuelPhase.Finale, 0f, "", 2, 0f, 1.25f, 20f, 0);

        Assert.AreEqual(80f, normal.leadMeters);
        Assert.That(normal.leadPosition01, Is.GreaterThan(0.99f));
        Assert.That(normal.leadPosition01, Is.LessThanOrEqualTo(1f));
        Assert.That(finale.leadPosition01, Is.GreaterThan(0.5f));
        Assert.That(finale.leadPosition01, Is.LessThan(0.6f));
    }

    [Test]
    public void RouteContractAloneShowsIndependentMarkerCount()
    {
        EchoContractData route = Contract(0.5f, false);
        route.type = EchoContractType.BreakLaneHabit;
        EchoContractData action = Contract(0.5f, false);
        action.type = EchoContractType.ChangeVerticalHabit;

        EchoHudViewData routeView = BuildResistance(route, 7);
        EchoHudViewData actionView = BuildResistance(action, 7);

        Assert.IsTrue(routeView.showContractMarkers);
        Assert.AreEqual(7, routeView.contractMarkerCount);
        Assert.IsFalse(actionView.showContractMarkers);
    }

    [Test]
    public void LaneCounterattackSeparatesPredictionFromPlayerDirective()
    {
        EchoContractData contract = Contract(0.55f, false);
        contract.targetLane = 2;
        contract.predictionLane = 2;
        contract.initialBreakCompleted = true;
        contract.counterattackActive = true;
        var step = new EchoChallengeStep
        {
            stepId = 7,
            phase = EchoDuelPhase.Counterattack,
            contractType = EchoContractType.BreakLaneHabit,
            status = EchoChallengeStepStatus.Active,
            predictedLane = 2,
            challengeLane = 0,
            safeLane = 1
        };

        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 0f,
            2, 2, 2, 2, 1f, EchoDuelPhase.Counterattack,
            challengeStep: step);

        Assert.AreEqual("等待交锋", view.directiveShort);
        Assert.AreEqual("预判右侧路线", view.predictionShort);
        Assert.AreNotEqual(view.directiveShort, view.predictionShort);
    }

    [Test]
    public void RhythmCounterattackAsksForAChoiceWithoutGivingTheAnswer()
    {
        EchoContractData contract = Contract(0.55f, false);
        contract.type = EchoContractType.DisruptRhythm;
        contract.predictionAction = ShadowAction.Slide;
        contract.targetAction = ShadowAction.Jump;
        contract.initialBreakCompleted = true;
        contract.counterattackActive = true;
        var step = new EchoChallengeStep
        {
            stepId = 8,
            phase = EchoDuelPhase.Counterattack,
            contractType = EchoContractType.DisruptRhythm,
            status = EchoChallengeStepStatus.Active,
            predictedAction = ShadowAction.Slide,
            requiredAction = ShadowAction.Jump,
            predictedLane = 0,
            challengeLane = 2,
            safeLane = 1
        };

        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 0f,
            2, 2, 2, 2, 1f, EchoDuelPhase.Counterattack,
            challengeStep: step);

        Assert.AreEqual("预判滑铲", view.predictionShort);
        Assert.AreEqual("等待交锋", view.directiveShort);
        StringAssert.DoesNotContain("右侧", view.directiveShort);
        StringAssert.DoesNotContain("高障碍", view.directiveShort);
        StringAssert.DoesNotContain("跳跃", view.directiveShort);
    }

    [Test]
    public void CounterattackKeepsAResolvedBreakVisibleAfterTheToast()
    {
        EchoContractData contract = Contract(0.51f, false);
        contract.type = EchoContractType.DisruptRhythm;
        contract.duelPhase = EchoDuelPhase.Counterattack;
        contract.initialBreakCompleted = true;
        contract.hypothesisVersion = 2;
        contract.lastEncounterResult = new EchoEncounterResult
        {
            encounterId = 14,
            phase = EchoDuelPhase.Counterattack,
            outcome = EchoEncounterOutcome.PredictionBroken,
            hypothesisVersion = 2
        };

        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 0f,
            2, 2, duelPhase: EchoDuelPhase.Counterattack);

        Assert.AreEqual("本次成功 · 裂解 51%", view.directiveShort);
    }

    [Test]
    public void CounterattackRelockNamesTheSuccessAndUsesNewestPrediction()
    {
        EchoContractData contract = Contract(0.64f, false);
        contract.type = EchoContractType.DisruptRhythm;
        contract.duelPhase = EchoDuelPhase.Counterattack;
        contract.initialBreakCompleted = true;
        contract.hypothesisVersion = 3;
        contract.predictionAction = ShadowAction.Jump;
        contract.lastEncounterResult = new EchoEncounterResult
        {
            encounterId = 15,
            phase = EchoDuelPhase.Counterattack,
            outcome = EchoEncounterOutcome.PredictionBroken,
            hypothesisVersion = 2
        };
        var staleStep = new EchoChallengeStep
        {
            stepId = 15,
            phase = EchoDuelPhase.Counterattack,
            predictedAction = ShadowAction.Slide,
            prediction = new EchoPredictionSnapshot
            {
                hypothesisVersion = 2,
                predictedAction = ShadowAction.Slide
            }
        };

        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 0f,
            2, 2, duelPhase: EchoDuelPhase.Counterattack,
            challengeStep: staleStep);

        Assert.AreEqual("本次成功 · 回声改判", view.directiveShort);
        Assert.AreEqual("预判跳跃", view.predictionShort);
    }

    [Test]
    public void CounterattackDuelProgressUsesAVisibleFracturePercentage()
    {
        EchoContractData contract = Contract(0.64f, false);
        contract.duelPhase = EchoDuelPhase.Counterattack;
        contract.initialBreakCompleted = true;

        EchoDuelViewData view = EchoRunPresentation.BuildDuel(true, contract,
            0f, 2, 2, duelPhase: EchoDuelPhase.Counterattack);

        Assert.AreEqual("裂解 64%", view.progress);
    }

    [Test]
    public void RevealSeparatesAiPredictionFromPlayerDirective()
    {
        EchoContractData contract = Contract(0f, false);

        EchoHudViewData view = EchoRunPresentation.BuildHud(true, contract, 0f,
            2, 2, 2, 2, 1f, EchoDuelPhase.Reveal);

        Assert.AreEqual("下注", view.directiveShort);
        Assert.AreEqual("预判右侧路线", view.predictionShort);
        Assert.AreNotEqual(view.directiveShort, view.predictionShort);
    }

    [Test]
    public void FailedFinaleLocksContractInsteadOfOfferingSilentRecovery()
    {
        EchoContractData contract = Contract(0.65f, false);
        contract.duelFailed = true;
        contract.failurePhase = EchoDuelPhase.Resistance;

        EchoHudViewData view = BuildFinale(contract);

        Assert.AreEqual(EchoHudMode.FinaleFailed, view.mode);
        Assert.AreEqual(EchoHudMeterKind.None, view.meterKind);
        Assert.AreEqual("契约锁定 · 完成追逐", view.directiveShort);
        Assert.AreEqual(0f, view.displayedMeter01);
    }

    [Test]
    public void PendingTransitionShowsTheUpcomingPhaseGate()
    {
        EchoHudViewData view = EchoRunPresentation.BuildHud(true,
            Contract(0f, false), 0f, 2, 2, 2, 2, 1f,
            EchoDuelPhase.Detection, phaseTransitionPending: true,
            pendingPhase: EchoDuelPhase.Reveal);

        Assert.IsTrue(view.phaseTransitionPending);
        Assert.AreEqual(EchoDuelPhase.Reveal, view.pendingPhase);
        Assert.AreEqual("前方同步：暴露", view.directiveShort);
    }

    [Test]
    public void RewriteHudShowsTheLiveEffectiveStyleProfile()
    {
        EchoHudViewData view = EchoRunPresentation.BuildHud(
            true, Contract(1f, true), 0f, 2, 2,
            duelPhase: EchoDuelPhase.Rewrite,
            phaseProgress01: 0.5f,
            rewriteStyleSummary: "路线多变 · 跳滑均衡 · 节奏多变");

        Assert.AreEqual(EchoHudMode.Rewrite, view.mode);
        Assert.AreEqual("新影成形", view.directiveShort);
        Assert.AreEqual(0.5f, view.displayedMeter01);
    }

    [Test]
    public void FinaleHudNamesTheCurrentAuthoredSection()
    {
        EchoHudViewData view = EchoRunPresentation.BuildHud(
            true, Contract(1f, true), 0f, 2, 2,
            duelPhase: EchoDuelPhase.Finale,
            finaleSegmentSummary: "第二段 · 反制策略锁定");

        Assert.AreEqual(EchoHudMode.FinaleClean, view.mode);
        Assert.AreEqual("冲刺", view.directiveShort);
    }

    private static EchoHudViewData BuildFinale(EchoContractData contract)
    {
        return EchoRunPresentation.BuildHud(true, contract, 0f, 2, 2, 2, 2,
            1f, EchoDuelPhase.Finale, 0f, "", 2, 0f, 1.25f, 20f, 0);
    }

    private static EchoHudViewData BuildResistance(EchoContractData contract,
        int markerCount)
    {
        return EchoRunPresentation.BuildHud(true, contract, 0f, 2, 2, 2, 2,
            1f, EchoDuelPhase.Resistance, 0f, "", 2, 0f, 1.25f, 20f,
            markerCount);
    }

    private static EchoContractData Contract(float progress01, bool completed)
    {
        return new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f,
            progress = progress01 * 100f,
            completed = completed,
            targetLane = 0,
            learnedLane = 2,
            predictionLane = 2
        };
    }
}
