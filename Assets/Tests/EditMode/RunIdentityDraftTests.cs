using NUnit.Framework;
using UnityEngine;

public sealed class RunIdentityDraftTests
{
    private const int MinimumTotalSamples = 6;
    private const int MinimumActiveSamples = 4;
    private const int MinimumActionCategories = 2;
    private const int MinimumJumpSamples = 2;
    private const int MinimumSlideSamples = 2;

    [Test]
    public void FormalGateRecordingIsIdempotentAndRejectsInvalidLanes()
    {
        RunIdentityDraft draft = RunIdentityDraft.Create(null, 11);

        Assert.IsTrue(draft.RecordFormalGateChoice(1, 2, true));
        Assert.IsFalse(draft.RecordFormalGateChoice(1, 0, false));
        Assert.IsFalse(draft.RecordFormalGateChoice(2, -1, true));
        Assert.IsFalse(draft.RecordFormalGateChoice(2, 3, true));
        Assert.IsTrue(draft.RecordFormalGateChoice(2, 1, false));

        Assert.AreEqual(2, draft.gateChoices.FormalChoiceCount);
        Assert.AreEqual(1, draft.gateChoices.SuccessfulExecutionCount);
        Assert.AreEqual(0, draft.gateChoices.ChoiceCountForLane(0));
        Assert.AreEqual(1, draft.gateChoices.ChoiceCountForLane(1));
        Assert.AreEqual(1, draft.gateChoices.ChoiceCountForLane(2));
    }

    [Test]
    public void CalibrationProgressSnapshotReportsEveryPromotionRequirement()
    {
        RunIdentityDraft draft = RunIdentityDraft.Create(null, 12);
        draft.RecordSample(ShadowAction.Jump);
        draft.RecordSample(ShadowAction.Jump);
        draft.RecordSample(ShadowAction.Slide);
        draft.RecordSample(ShadowAction.Left);
        draft.RecordFormalGateChoice(1, 2, true);
        draft.RecordFormalGateChoice(2, 2, true);
        draft.RecordFormalGateChoice(3, 2, true);
        draft.RecordFormalGateChoice(4, 1, false);

        SingleContractCalibrationProgress progress =
            draft.BuildCalibrationProgress(
                MinimumTotalSamples, MinimumActiveSamples,
                MinimumActionCategories, MinimumJumpSamples,
                MinimumSlideSamples);

        Assert.AreEqual(4, progress.totalSamples);
        Assert.AreEqual(MinimumTotalSamples, progress.minimumTotalSamples);
        Assert.AreEqual(4, progress.activeSamples);
        Assert.AreEqual(MinimumActiveSamples, progress.minimumActiveSamples);
        Assert.AreEqual(3, progress.actionCategories);
        Assert.AreEqual(MinimumActionCategories,
            progress.minimumActionCategories);
        Assert.AreEqual(2, progress.jumpSamples);
        Assert.AreEqual(MinimumJumpSamples, progress.minimumJumpSamples);
        Assert.AreEqual(1, progress.slideSamples);
        Assert.AreEqual(MinimumSlideSamples, progress.minimumSlideSamples);
        Assert.AreEqual(4, progress.formalChoices);
        Assert.AreEqual(5, progress.minimumFormalChoices);
        Assert.AreEqual(3, progress.successfulChoices);
        Assert.AreEqual(3, progress.minimumSuccessfulChoices);
        Assert.AreEqual(3, progress.strongestRouteChoices);
        Assert.AreEqual(3, progress.minimumStrongestRouteChoices);
        Assert.IsTrue(progress.HasTargets);
        Assert.IsFalse(progress.IsComplete);
    }

    [Test]
    public void PromotionProgressSeparatesPlayerEvidenceFromFinishAndStructure()
    {
        RunIdentityDraft ready = CreateCalibrationDraft(true);
        RecordStrongGateEvidence(ready, 80);

        SingleContractCalibrationProgress beforeFinish =
            ready.BuildCalibrationProgress(
                MinimumTotalSamples, MinimumActiveSamples,
                MinimumActionCategories, MinimumJumpSamples,
                MinimumSlideSamples, false);
        SingleContractCalibrationProgress atFinish =
            ready.BuildCalibrationProgress(
                MinimumTotalSamples, MinimumActiveSamples,
                MinimumActionCategories, MinimumJumpSamples,
                MinimumSlideSamples, true);

        Assert.IsTrue(beforeFinish.evidenceReady);
        Assert.IsFalse(beforeFinish.promotionReady);
        Assert.IsTrue(atFinish.promotionReady);

        RunIdentityDraft invalidStructure = RunIdentityDraft.Create(null, 0);
        invalidStructure.physicalPace = 12f;
        RecordReadyMotionSamples(invalidStructure);
        RecordStrongGateEvidence(invalidStructure, 90);
        SingleContractCalibrationProgress invalid =
            invalidStructure.BuildCalibrationProgress(
                MinimumTotalSamples, MinimumActiveSamples,
                MinimumActionCategories, MinimumJumpSamples,
                MinimumSlideSamples, true);

        Assert.IsTrue(invalid.evidenceReady,
            "Player evidence can be complete even when internal structure is invalid.");
        Assert.IsFalse(invalid.promotionReady,
            "The UI must not claim promotion when the real predicate rejects it.");

        ready.Discard();
        Assert.IsTrue(atFinish.promotionReady,
            "The captured value must survive draft disposal for the result screen.");
        Assert.AreEqual(5, atFinish.formalChoices);
    }

    [Test]
    public void CalibrationReadinessRequiresEveryMotionAndGateThreshold()
    {
        RunIdentityDraft noMotion = CreateCalibrationDraft(false);
        RecordStrongGateEvidence(noMotion, 10);
        Assert.IsFalse(IsCalibrationReady(noMotion));

        RunIdentityDraft fourChoices = CreateCalibrationDraft(true);
        RecordStrongGateEvidence(fourChoices, 20, 4);
        Assert.IsFalse(IsCalibrationReady(fourChoices));

        RunIdentityDraft twoSuccessful = CreateCalibrationDraft(true);
        RecordGatePattern(twoSuccessful, 30,
            new[] { 2, 2, 2, 1, 0 },
            new[] { true, true, false, false, false });
        Assert.IsFalse(IsCalibrationReady(twoSuccessful));

        RunIdentityDraft weakPreference = CreateCalibrationDraft(true);
        RecordGatePattern(weakPreference, 40,
            new[] { 2, 2, 1, 1, 0 },
            new[] { true, true, true, false, false });
        Assert.IsFalse(IsCalibrationReady(weakPreference));

        RunIdentityDraft ready = CreateCalibrationDraft(true);
        RecordStrongGateEvidence(ready, 50);
        Assert.IsFalse(ready.IsCalibrationPromotionReady(
            MinimumTotalSamples + 1, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples));
        Assert.IsFalse(ready.IsCalibrationPromotionReady(
            MinimumTotalSamples, MinimumActiveSamples + 1,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples));
        Assert.IsFalse(ready.IsCalibrationPromotionReady(
            MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories + 1, MinimumJumpSamples,
            MinimumSlideSamples));
        Assert.IsFalse(ready.IsCalibrationPromotionReady(
            MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples + 1,
            MinimumSlideSamples));
        Assert.IsFalse(ready.IsCalibrationPromotionReady(
            MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples + 1));
        Assert.IsTrue(IsCalibrationReady(ready));
        Assert.IsFalse(ready.TryBuildCalibrationPromotion(
            false, 1f, MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples, out ActiveEchoIdentity unfinished));
        Assert.IsNull(unfinished);
        Assert.IsTrue(ready.TryBuildCalibrationPromotion(
            true, 1f, MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples, out ActiveEchoIdentity promoted));
        Assert.AreEqual(1, promoted.generation);
        Assert.IsEmpty(promoted.parentIdentityId);
        Assert.AreEqual(promoted.identityId,
            promoted.memoryContract.identityId);
    }

    [Test]
    public void LowConfidenceChoicePatternNeverCreatesPreciseRouteMemory()
    {
        RunIdentityDraft draft = CreateCalibrationDraft(true);
        RecordGatePattern(draft, 60,
            new[] { 2, 2, 2, 1, 1, 0 },
            new[] { true, true, true, true, true, true });

        Assert.IsFalse(draft.gateChoices.TryBuildMemoryContract(
            out EchoMemoryContract vague));
        Assert.IsNotNull(vague);
        Assert.AreEqual(3, vague.evidenceCount);
        Assert.AreEqual(0.5f, vague.confidence, 0.0001f);
        Assert.IsFalse(vague.HasPreciseRouteMemory);
        StringAssert.Contains("模糊", vague.BuildMemoryText());
        Assert.IsFalse(IsCalibrationReady(draft));
        Assert.IsFalse(draft.TryBuildCalibrationPromotion(
            true, 1f, MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples, out ActiveEchoIdentity promoted));
        Assert.IsNull(promoted);
    }

    [Test]
    public void ChallengePromotionClonesFrozenBaseAndUsesOnlyDraftState()
    {
        ActiveEchoIdentity frozen = CreateFrozenIdentity();
        string frozenJson = frozen.ToJson();
        string frozenHash = frozen.ComputeHash();
        RunIdentityDraft draft = RunIdentityDraft.Create(frozen, 73);
        PopulateWinningChallengeDraft(draft);

        Assert.IsTrue(draft.TryBuildChallengePromotion(
            true, 0.9f, out ActiveEchoIdentity promoted));

        Assert.AreEqual(frozenJson, frozen.ToJson());
        Assert.AreEqual(frozenHash, frozen.ComputeHash());
        Assert.AreEqual(frozen.generation + 1, promoted.generation);
        Assert.AreEqual(frozen.identityId, promoted.parentIdentityId);
        Assert.AreEqual(73, promoted.sourceRunSequence);
        CollectionAssert.AreEqual(
            draft.policy.ExportWeights(), promoted.policyWeights);
        AIShadowSequenceState draftSequence = draft.sequence.ExportState();
        CollectionAssert.AreEqual(
            draftSequence.transitions, promoted.sequenceTransitions);
        Assert.AreEqual(draftSequence.pairCount,
            promoted.sequencePairCount);
        Assert.AreEqual(JsonUtility.ToJson(
                EchoIdentityStyleSnapshot.FromPlayerStyle(
                    draft.style.Snapshot())),
            JsonUtility.ToJson(promoted.style));
        Assert.AreEqual(draft.physicalPace, promoted.pace);
        Assert.AreEqual(promoted.identityId,
            promoted.memoryContract.identityId);
        Assert.AreNotSame(frozen.memoryContract,
            promoted.memoryContract);
        Assert.AreNotEqual(frozen.identityId,
            promoted.memoryContract.identityId);
        Assert.AreNotEqual(frozen.memoryContract.contractId,
            promoted.memoryContract.contractId);
        Assert.IsTrue(promoted.IsSemanticallyValid());

        Assert.IsTrue(draft.gateChoices.TryGetUniquePreferredLane(
            out int uniquePreferredLane));
        Assert.AreEqual(promoted.memoryContract.preferredLane,
            uniquePreferredLane);
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            frozen, promoted, successfulCounterCount: 3,
            totalGateCount: 5, relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);
        Assert.IsTrue(assessment.IsAvailable);
        Assert.AreEqual(EchoCognitionChangeKind.Reversed,
            assessment.ChangeKind);
        Assert.AreEqual(0, assessment.PreviousLane);
        Assert.AreEqual(2, assessment.NextLane);
    }

    [Test]
    public void ChallengeCognitionShiftRequiresUniqueNewLaneEvidence()
    {
        ActiveEchoIdentity frozen = CreateFrozenIdentity();
        RunIdentityDraft tiedDraft = RunIdentityDraft.Create(frozen, 74);
        RecordGatePattern(tiedDraft, 300,
            new[] { 1, 1, 2, 2, 0 },
            new[] { true, true, true, true, true });
        Assert.IsTrue(tiedDraft.TryBuildChallengePromotion(
            true, 1f, out ActiveEchoIdentity tiedPromotion));
        Assert.IsFalse(tiedDraft.gateChoices.TryGetUniquePreferredLane(
            out _));
        EchoCognitionAssessment tiedAssessment =
            EchoCognitionAssessment.Compare(
                frozen, tiedPromotion, successfulCounterCount: 2,
                totalGateCount: 5, relearnStartGateNumber: 0,
                nextLaneHasUniqueEvidence: false);
        Assert.AreEqual(EchoCognitionChangeKind.Shaken,
            tiedAssessment.ChangeKind);

        RunIdentityDraft uniqueDraft = RunIdentityDraft.Create(frozen, 75);
        RecordGatePattern(uniqueDraft, 400,
            new[] { 1, 1, 1, 2, 2, 0 },
            new[] { true, true, true, true, true, true });
        Assert.IsTrue(uniqueDraft.TryBuildChallengePromotion(
            true, 1f, out ActiveEchoIdentity uniquePromotion));
        Assert.IsTrue(uniqueDraft.gateChoices.TryGetUniquePreferredLane(
            out int uniqueLane));
        Assert.AreEqual(1, uniqueLane);
        EchoCognitionAssessment uniqueAssessment =
            EchoCognitionAssessment.Compare(
                frozen, uniquePromotion, successfulCounterCount: 3,
                totalGateCount: 6, relearnStartGateNumber: 3,
                nextLaneHasUniqueEvidence: true);
        Assert.AreEqual(EchoCognitionChangeKind.Shifted,
            uniqueAssessment.ChangeKind);
    }

    [Test]
    public void CompatibilityCalibrationKeepsFrozenMotionAndBuildsRouteMemory()
    {
        ActiveEchoIdentity compatibility = CreateFrozenIdentity();
        compatibility.memoryContract = null;
        compatibility.identityId = ActiveEchoIdentity.CreateIdentityId(
            compatibility);
        compatibility.Normalize();
        Assert.IsTrue(compatibility.RequiresRouteCalibration);

        RunIdentityDraft draft = RunIdentityDraft.Create(compatibility, 74);
        RecordReadyMotionSamples(draft);
        RecordStrongGateEvidence(draft, 700);

        Assert.IsFalse(draft.TryBuildChallengePromotion(
            true, 1f, out _));
        Assert.IsTrue(draft.TryBuildCompatibilityCalibrationPromotion(
            true, 1f, MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples, out ActiveEchoIdentity promoted));
        Assert.AreEqual(compatibility.generation + 1,
            promoted.generation);
        Assert.AreEqual(compatibility.identityId,
            promoted.parentIdentityId);
        Assert.AreEqual(compatibility.pace, promoted.pace, 0.0001f);
        Assert.IsFalse(promoted.RequiresRouteCalibration);
        Assert.AreEqual(promoted.identityId,
            promoted.memoryContract.identityId);
    }

    [Test]
    public void FailedDraftsCannotPersistOrChangeLaterWinningIdentity()
    {
        ActiveEchoIdentity frozen = CreateFrozenIdentity();
        string frozenJson = frozen.ToJson();
        string frozenHash = frozen.ComputeHash();
        const int runSequence = 91;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            RunIdentityDraft failed = RunIdentityDraft.Create(
                frozen, runSequence);
            PopulateFailedChallengeDraft(failed, attempt);
            Assert.IsFalse(failed.TryBuildChallengePromotion(
                false, 0.9f, out ActiveEchoIdentity failedIdentity));
            Assert.IsNull(failedIdentity);
            failed.Discard();
            Assert.IsTrue(failed.IsDiscarded);
            Assert.IsFalse(failed.RecordFormalGateChoice(
                999, 1, true));
            Assert.IsFalse(failed.TryBuildChallengePromotion(
                true, 0.9f, out ActiveEchoIdentity discardedIdentity));
            Assert.IsNull(discardedIdentity);
        }

        RunIdentityDraft afterFailures = RunIdentityDraft.Create(
            frozen, runSequence);
        PopulateWinningChallengeDraft(afterFailures);
        Assert.IsTrue(afterFailures.TryBuildChallengePromotion(
            true, 0.9f, out ActiveEchoIdentity afterFailuresIdentity));

        RunIdentityDraft direct = RunIdentityDraft.Create(
            frozen, runSequence);
        PopulateWinningChallengeDraft(direct);
        Assert.IsTrue(direct.TryBuildChallengePromotion(
            true, 0.9f, out ActiveEchoIdentity directIdentity));

        Assert.AreEqual(directIdentity.ToJson(),
            afterFailuresIdentity.ToJson());
        Assert.AreEqual(directIdentity.ComputeHash(),
            afterFailuresIdentity.ComputeHash());
        Assert.AreEqual(frozenJson, frozen.ToJson());
        Assert.AreEqual(frozenHash, frozen.ComputeHash());
    }

    [Test]
    public void DraftStyleCapturesEveryDecisionFeatureWithoutGlobalFallback()
    {
        RunIdentityDraft draft = RunIdentityDraft.Create(null, 92);

        draft.RecordStyleMistake();
        draft.RecordSample(ShadowAction.Jump, 2, 0.9f, 0.35f,
            true, true);
        draft.RecordSample(ShadowAction.Slide, 0, 0.65f, 0f,
            false, true);
        draft.RecordStyleObstacleOpportunity(ObstacleType.Low, true);
        draft.FinalizeStyle();

        PlayerStyleData style = draft.style.Snapshot();
        Assert.GreaterOrEqual(style.aggressivenessSamples, 2);
        Assert.AreEqual(1, style.jumpTimingSamples);
        Assert.AreEqual(2, style.verticalActionSamples);
        Assert.AreEqual(1, style.jumpActionSamples);
        Assert.AreEqual(1, style.slideActionSamples);
        Assert.AreEqual(1, style.slideOpportunitySamples);
        Assert.AreEqual(2, style.laneSamples);
        Assert.AreEqual(1, style.rhythmSamples);
        Assert.AreEqual(1, style.recoverySamples);
    }

    private static RunIdentityDraft CreateCalibrationDraft(bool addMotion)
    {
        RunIdentityDraft draft = RunIdentityDraft.Create(null, 17);
        draft.physicalPace = 12.5f;
        if (addMotion) RecordReadyMotionSamples(draft);
        return draft;
    }

    private static void RecordReadyMotionSamples(RunIdentityDraft draft)
    {
        draft.RecordSample(ShadowAction.Keep);
        draft.RecordSample(ShadowAction.Keep);
        draft.RecordSample(ShadowAction.Jump);
        draft.RecordSample(ShadowAction.Jump);
        draft.RecordSample(ShadowAction.Slide);
        draft.RecordSample(ShadowAction.Slide);
    }

    private static bool IsCalibrationReady(RunIdentityDraft draft)
    {
        return draft.IsCalibrationPromotionReady(
            MinimumTotalSamples, MinimumActiveSamples,
            MinimumActionCategories, MinimumJumpSamples,
            MinimumSlideSamples);
    }

    private static void RecordStrongGateEvidence(RunIdentityDraft draft,
        int firstGateId, int count = 5)
    {
        int[] lanes = { 2, 2, 2, 1, 0 };
        bool[] successes = { true, true, true, false, false };
        for (int index = 0; index < count; index++)
            Assert.IsTrue(draft.RecordFormalGateChoice(
                firstGateId + index, lanes[index], successes[index]));
    }

    private static void RecordGatePattern(RunIdentityDraft draft,
        int firstGateId, int[] lanes, bool[] successes)
    {
        Assert.AreEqual(lanes.Length, successes.Length);
        for (int index = 0; index < lanes.Length; index++)
            Assert.IsTrue(draft.RecordFormalGateChoice(
                firstGateId + index, lanes[index], successes[index]));
    }

    private static ActiveEchoIdentity CreateFrozenIdentity()
    {
        var sequence = new AIShadowSequencePolicy();
        sequence.Learn((int)ShadowAction.Left, (int)ShadowAction.Jump);
        AIShadowSequenceState sequenceState = sequence.ExportState();
        var style = new PlayerStyleData
        {
            aggressiveness = 0.72f,
            lanePreference = -0.4f,
            rhythmStability = 0.63f,
            laneSamples = 8
        };
        style.Normalize();
        var identity = new ActiveEchoIdentity
        {
            generation = 3,
            parentIdentityId = "echo-ancestor",
            sourceRunSequence = 22,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequenceState.transitions,
            sequencePairCount = sequenceState.pairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(style),
            pace = 13.25f,
            clarity = 0.84f,
            memoryContract = new EchoMemoryContract
            {
                contractId = "route-frozen-left",
                preferredLane = 0,
                confidence = 0.8f,
                evidenceCount = 4
            }
        };
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        identity.Normalize();
        Assert.IsTrue(identity.IsSemanticallyValid());
        return identity;
    }

    private static void PopulateWinningChallengeDraft(RunIdentityDraft draft)
    {
        float[] jumpFeatures = { 1f, 0f, 0.4f, 0.8f, 0f, 0.2f, 0f, 0f };
        float[] slideFeatures = { 1f, 0.5f, 0.3f, 0.7f, 0f, 0.4f, 0f, 0f };
        draft.policy.Learn((int)ShadowAction.Jump, jumpFeatures, 0.08f);
        draft.policy.Learn((int)ShadowAction.Slide, slideFeatures, 0.08f);
        draft.sequence.Learn(
            (int)ShadowAction.Jump, (int)ShadowAction.Slide);
        draft.RecordSample(ShadowAction.Jump);
        draft.RecordSample(ShadowAction.Slide);
        draft.style = new EchoIdentityStyleAccumulator(new PlayerStyleData
        {
            aggressiveness = 0.31f,
            lanePreference = 0.58f,
            rhythmStability = 0.77f,
            laneSamples = 5,
            rhythmSamples = 4
        });
        draft.physicalPace = 14.25f;
        RecordStrongGateEvidence(draft, 100);
    }

    private static void PopulateFailedChallengeDraft(RunIdentityDraft draft,
        int attempt)
    {
        float[] features =
        {
            1f, attempt - 1f, 0.2f + attempt * 0.1f, 0.7f,
            0f, 0.1f, 0f, 0f
        };
        ShadowAction action = attempt % 2 == 0
            ? ShadowAction.Left : ShadowAction.Right;
        draft.policy.Learn((int)action, features, 0.08f);
        draft.RecordSample(action);
        draft.physicalPace = 11f + attempt;
        RecordGatePattern(draft, 200 + attempt * 10,
            new[] { 0, 0, 0, 1, 2 },
            new[] { true, true, true, false, false });
    }
}
