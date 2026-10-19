using NUnit.Framework;
using UnityEngine;

public sealed class SingleContractResultPresentationTests
{
    [Test]
    public void SavedSettlementWithoutPromotionKeepsTheCurrentIdentity()
    {
        SingleContractResultFacts facts = ChallengeFacts();
        facts.won = false;
        facts.promotionBuilt = false;
        facts.identityPromoted = false;
        facts.savedGeneration = 3;
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        Assert.AreEqual(SingleContractResultKind.ChallengeLost, view.kind);
        Assert.AreEqual(SingleContractResultNextAction.RestartLocal, view.nextAction);
        StringAssert.Contains("第3代", view.primaryAction);
        StringAssert.DoesNotContain("新回声", view.primaryAction);
        StringAssert.DoesNotContain("已生效", view.summary);
    }

    [Test]
    public void UnbuiltWinningCandidateIsNotMisreportedAsStorageFailure()
    {
        SingleContractResultFacts facts = ChallengeFacts();
        facts.promotionBuilt = false;
        facts.commitSucceeded = false;
        facts.identityPromoted = false;
        facts.savedGeneration = 3;
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        Assert.AreEqual(SingleContractResultKind.WonWithoutPromotion, view.kind);
        Assert.AreEqual(SingleContractResultNextAction.RestartLocal, view.nextAction);
        StringAssert.Contains("跑赢", view.title);
        StringAssert.DoesNotContain("保存失败", view.summary);
        StringAssert.DoesNotContain("已生效", view.summary);
    }

    [Test]
    public void FailedCommitCannotExposeTheCandidateAsSavedEvenAfterAWin()
    {
        SingleContractResultFacts facts = ChallengeFacts();
        facts.commitSucceeded = false;
        facts.identityPromoted = false;
        facts.savedGeneration = 3;
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        Assert.AreEqual(SingleContractResultKind.SaveFailed, view.kind);
        Assert.AreEqual(SingleContractResultNextAction.ReturnToMenu, view.nextAction);
        StringAssert.Contains("跑赢", view.title);
        StringAssert.Contains("新代未生效", view.summary);
        StringAssert.DoesNotContain("第4代", view.summary);
    }

    [TestCase(true, SingleContractResultKind.UpdatedChallengeReady, "挑战新回声")]
    [TestCase(false, SingleContractResultKind.UpdatedNeedsObservation, "继续观察")]
    public void CommittedIdentityReadinessSelectsTheNextModeAndCopy(
        bool canChallenge, SingleContractResultKind kind, string action)
    {
        SingleContractResultFacts facts = ChallengeFacts();
        facts.savedIdentityCanChallenge = canChallenge;
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        Assert.AreEqual(kind, view.kind);
        Assert.AreEqual(action, view.primaryAction);
        Assert.AreEqual(SingleContractResultNextAction.RestartLocal, view.nextAction);
        StringAssert.Contains("第4代回声已生效", view.summary);
        if (!canChallenge) StringAssert.Contains("仍需观察", view.summary);
    }

    [Test]
    public void AsyncAndValidationDoNotTreatTheirMissingLocalCommitAsFailure()
    {
        SingleContractResultFacts facts = ChallengeFacts();
        facts.promotionBuilt = false;
        facts.commitAttempted = false;
        facts.commitSucceeded = false;
        facts.identityPromoted = false;
        facts.isAsync = true;
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        Assert.AreEqual(SingleContractResultKind.AsyncChallenge, view.kind);
        Assert.AreEqual(SingleContractResultNextAction.RetryAsync, view.nextAction);
        StringAssert.DoesNotContain("下一代", view.summary);
        StringAssert.DoesNotContain("保存失败", view.summary);
        facts.isAsync = false;
        facts.isTransient = true;
        facts.validationIdentityUnchanged = true;
        Assert.AreEqual(SingleContractResultKind.TransientValidation,
            EchoRunPresentation.BuildSingleContractResultView(facts).kind);
        facts.validationIdentityUnchanged = false;
        Assert.AreEqual(SingleContractResultKind.ValidationIsolationFailed,
            EchoRunPresentation.BuildSingleContractResultView(facts).kind);
    }

    private static SingleContractResultFacts ChallengeFacts()
    {
        return new SingleContractResultFacts
        {
            available = true,
            endReason = RunEndReason.FinishReached,
            wasChallenge = true,
            won = true,
            promotionBuilt = true,
            commitAttempted = true,
            commitSucceeded = true,
            identityPromoted = true,
            generationBefore = 3,
            savedGeneration = 4,
            savedPreferredLane = 2,
            savedIdentityCanChallenge = true
        };
    }

    [TestCase("已更新为偏向左侧", "回声记录：偏右 → 偏左")]
    [TestCase("已更新为偏向中间", "回声记录：偏右 → 中路")]
    [TestCase("开始偏向左侧", "回声记录：开始偏左")]
    [TestCase("更确定偏向右侧", "回声记录：更确定偏右")]
    [TestCase("仍偏向右侧", "回声记录：仍偏右")]
    [TestCase("仍可能偏向左侧", "回声记录：仍可能偏左")]
    [TestCase("路线倾向还需要观察", "回声记录：还需要观察")]
    public void SummaryKeepsTheRecordChangeAndItsCertainty(string nextRecord,
        string expected)
    {
        string full = "你跑赢了第3代回声\n此前记录：选路偏向右侧\n"
                      + "本局反制通过 4/6 次 · 从第3次选路起，后续预测已调整\n"
                      + "下一局记录：" + nextRecord + "\n"
                      + "关键选择：提交左路（反制）\n动作结果：成功通过";

        Assert.AreEqual(expected,
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [TestCase("压力出现时，你偏向左侧", "回声记录：偏左")]
    [TestCase("压力出现时，你偏向中间", "回声记录：中路")]
    [TestCase("回声记忆模糊\n你的选择尚未形成稳定模式", "回声记录：还需要观察")]
    public void NewEchoSummaryDoesNotInventAPreciseRoute(string memory,
        string expected)
    {
        Assert.AreEqual(expected,
            EchoRunPresentation.BuildSingleContractResultSummary(
                "第1代回声已经形成\n它记住了：" + memory
                + "\n下一局，它会带着这些习惯追上你"));
    }

    [TestCase(false, false, RunEndReason.Collision)]
    [TestCase(false, false, RunEndReason.FinishReached)]
    [TestCase(true, false, RunEndReason.FinishReached)]
    [TestCase(true, true, RunEndReason.FinishReached)]
    public void SaveFailureRemainsInSummaryEvenWhenThePlayerWon(
        bool challenged, bool won, RunEndReason endReason)
    {
        string full = AIShadowRunner.BuildSingleContractSaveFailureResult(
            endReason, challenged, won, 3);
        Assert.AreEqual("回声保存失败\n当前回声未改变",
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [TestCase(false, false, RunEndReason.Collision)]
    [TestCase(false, false, RunEndReason.FinishReached)]
    [TestCase(true, false, RunEndReason.FinishReached)]
    [TestCase(true, true, RunEndReason.FinishReached)]
    public void FixedValidationSummaryMakesNonPersistenceVisible(
        bool challenged, bool won, RunEndReason endReason)
    {
        string full = AIShadowRunner.BuildSingleContractValidationResult(
            endReason, challenged, won, 3);
        Assert.AreEqual("固定验收 · 身份档未修改",
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [Test]
    public void ValidationIsolationFailureCannotBeSummarizedAsSafeNonPersistence()
    {
        const string failure = "固定验收隔离失败\n真实身份档发生意外变化";
        Assert.AreEqual(failure,
            EchoRunPresentation.BuildSingleContractResultSummary(failure));
    }

    [Test]
    public void IncompleteCalibrationKeepsTheMissingEchoVisible()
    {
        string full = EchoRunPresentation.BuildSingleContractCalibrationResult(default);
        Assert.AreEqual("新回声尚未形成\n再跑一局，继续观察",
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [TestCase("回声形成遇到问题\n观察已充分，但新回声没有形成；再跑一局，继续观察",
        "回声形成遇到问题\n观察已充分，新回声未形成")]
    [TestCase("你跑赢了第3代回声\n这局还不足以形成下一代，当前回声保持不变",
        "下一代尚未形成\n当前回声保持不变")]
    [TestCase("第3代回声胜出\n本局未到终点\n下一局仍使用本代记录",
        "下一局仍使用本代记录")]
    public void SummaryDoesNotTurnAnUnchangedRecordIntoAPromotion(
        string full, string expected)
    {
        Assert.AreEqual(expected,
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [Test]
    public void SaveFailureTakesPrecedenceOverAnyEarlierIntendedRecordChange()
    {
        const string full = "你跑赢了第3代回声\n此前记录：选路偏向右侧\n"
                            + "下一局记录：已更新为偏向左侧\n回声保存失败\n当前回声保持不变";
        Assert.AreEqual("回声保存失败\n当前回声未改变",
            EchoRunPresentation.BuildSingleContractResultSummary(full));
    }

    [TestCase(GateExecutionReason.Unresolved)]
    [TestCase(GateExecutionReason.Cancelled)]
    [TestCase(GateExecutionReason.RouteAbandoned)]
    [TestCase(GateExecutionReason.Collision)]
    public void DetailsKeepTheExactExecutionEvidenceAndRelabelOnlyTheLatestChoice(
        GateExecutionReason reason)
    {
        const string title = "第3代回声胜出";
        string review = EchoRunPresentation.BuildSingleContractGateReview(
            new GateAttempt
            {
                gateId = 6,
                committedLane = 0,
                chosenRole = PredictionGateRole.Counter,
                executionReason = reason,
                hasLateralEvidence = true,
                laneChangeInProgress = true
            });
        string full = title + "\n下一局仍使用本代记录\n" + review;
        string details = EchoRunPresentation.BuildSingleContractResultDetails(full, title);
        Assert.AreEqual("下一局仍使用本代记录\n"
                        + review.Replace("关键选择：", "最近一次选路："), details);
        StringAssert.Contains(review.Substring(review.IndexOf("动作结果：")), details,
            "Missing evidence, abandoned routes and collisions cannot be merged or rewritten.");
    }

    [Test]
    public void DetailsRemoveOnlyTheMatchingFirstTitleAndNormalizeWindowsNewlines()
    {
        const string full = "回声保存失败\r\n本局学习没有写入\r\n回声保存失败";
        Assert.AreEqual("本局学习没有写入\n回声保存失败",
            EchoRunPresentation.BuildSingleContractResultDetails(full, "回声保存失败"));
        Assert.AreEqual(full.Replace("\r\n", "\n"),
            EchoRunPresentation.BuildSingleContractResultDetails(full, "本局结果"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \r\n ")]
    public void EmptyResultsProduceNoInventedRecord(string full)
    {
        Assert.IsEmpty(EchoRunPresentation.BuildSingleContractResultSummary(full));
        Assert.IsEmpty(EchoRunPresentation.BuildSingleContractResultDetails(full, "本局结果"));
    }

    [Test]
    public void SummaryGlyphsExistInTheCurrentBundledFont()
    {
        Font font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        Assert.IsNotNull(font);
        const string text = "回声记录：偏右 → 偏左中路仍可能更确定开始还需要观察"
                            + "最近一次选路保存失败当前未改变本局结果固定验收身份档修改隔离发生意外变化"
                            + "形成遇到问题已充分新回声尚下一代再跑一局继续观察保持不变";
        foreach (char character in text)
            Assert.IsTrue(font.HasCharacter(character), "Missing result glyph: " + character);
    }
}
