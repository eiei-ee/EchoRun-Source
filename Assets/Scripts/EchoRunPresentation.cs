using UnityEngine;

public enum EchoLeadState
{
    Calibrating,
    Tied,
    Leading,
    Trailing
}

public enum EchoHudMode
{
    Calibration,
    Detection,
    Reveal,
    Resistance,
    Counterattack,
    Rewrite,
    FinaleClean,
    FinaleContract,
    FinaleFailed
}

public enum EchoHudMeterKind
{
    None,
    Calibration,
    Phase,
    EchoLock,
    Stability = EchoLock
}

public enum SingleContractVisualState
{
    Calibration,
    Challenge,
    RelearnPulse,
    Finale
}

public enum SingleContractLeadState
{
    Tied,
    PlayerLeading,
    EchoLeading
}

public enum SingleContractInstantFeedback
{
    None,
    PredictionHit,
    RewriteSucceeded,
    SafePass,
    CounterFailed,
    EchoRelearned,
    ExecutionIncomplete,
    ObservationInconclusive
}

public enum SingleContractResultKind
{
    Unknown,
    SaveFailed,
    UpdatedChallengeReady,
    UpdatedNeedsObservation,
    WonWithoutPromotion,
    ChallengeLost,
    ObservationIncomplete,
    ObservationGenerationFailed,
    AsyncChallenge,
    TransientValidation,
    ValidationIsolationFailed
}

public enum SingleContractResultNextAction
{
    ReturnToMenu,
    RestartLocal,
    RetryAsync
}

// A value snapshot survives draft disposal without retaining mutable identities.
// Generation, commit and race outcome are separate facts, not inferred from copy.
public struct SingleContractResultFacts
{
    public bool available;
    public bool isAsync;
    public bool isTransient;
    public bool validationIdentityUnchanged;
    public RunEndReason endReason;
    public bool wasChallenge;
    public bool won;
    public bool promotionBuilt;
    public bool commitAttempted;
    public bool commitSucceeded;
    public bool identityPromoted;
    public string previousIdentityId;
    public int generationBefore;
    public int previousPreferredLane;
    public bool previousIdentityCanChallenge;
    public string savedIdentityId;
    public int savedGeneration;
    public int savedPreferredLane;
    public bool savedIdentityCanChallenge;
    public SingleContractCalibrationProgress calibrationProgress;
    public EchoCognitionAssessment cognitionAssessment;
}

public struct SingleContractResultViewData
{
    public SingleContractResultKind kind;
    public string title;
    public string summary;
    public string primaryAction;
    public SingleContractResultNextAction nextAction;
}

[System.Serializable]
public struct SingleContractHudInput
{
    public SingleContractVisualState visualState;
    public bool openingMemory;
    public bool openingReplay;
    public ShadowAction openingReplayAction;
    public int openingReplayCount;
    public int generation;
    public string memory;
    public bool showPrediction;
    public int predictedLane;
    public int predictionGateNumber;
    public int predictionGateCount;
    public bool predictionGateActive;
    public float leadMeters;
    public int injuries;
    public int maximumCollisionStrikes;
    public float collisionRecoveryTimeRemaining;
    public float finishRemaining;
    public string powerUp;
    public SingleContractInstantFeedback instantFeedback;
    public float feedbackLeadDeltaMeters;
    public bool feedbackRelearned;
    public int feedbackSequence;
    public SingleContractCalibrationProgress calibrationProgress;
    public string result;
}

[System.Serializable]
public struct SingleContractHudData
{
    public SingleContractVisualState visualState;
    public bool openingMemory;
    public bool openingReplay;
    public string openingTitle;
    public int generation;
    public bool showMemory;
    public string memory;
    public string prediction;
    public int predictionGateNumber;
    public int predictionGateCount;
    public bool predictionGateActive;
    public float leadMeters;
    public SingleContractLeadState leadState;
    public string lead;
    public int injuries;
    public string injuriesText;
    public float finishRemaining;
    public string finishRemainingText;
    public bool showPowerUp;
    public string powerUp;
    public SingleContractInstantFeedback instantFeedbackKind;
    public float feedbackLeadDeltaMeters;
    public bool feedbackRelearned;
    public string instantFeedback;
    public int feedbackSequence;
    public bool showCalibrationProgress;
    public float calibrationProgress01;
    public string calibrationMeterText;
    public string calibrationActionProgress;
    public string calibrationRouteProgress;
    public string calibrationDetails;
    public string result;
}

public struct EchoHudViewData
{
    public EchoHudMode mode;
    public EchoHudMeterKind meterKind;

    public int phaseIndex;
    public float calibrationProgress01;
    public float phaseProgress01;
    public float contractStability01;
    public float echoLock01;
    public float displayedMeter01;

    public string announcement;
    public string directiveShort;
    public string predictionShort;

    public float leadMeters;
    public float leadPosition01;

    public int syncRemaining;
    public float recoveryProgress01;
    public float recoverySeconds;
    public float remainingDistance;

    public int contractMarkerCount;
    public bool showContractMarkers;

    public bool showPrediction;
    public bool phaseTransitionPending;
    public EchoDuelPhase pendingPhase;
    public bool showBuff;
    public string buffText;
}

public struct EchoMenuViewData
{
    public string generation;
    public string learned;
    public string rule;
    public string objective;
    public string primaryAction;
}

public struct EchoDuelViewData
{
    public string phase;
    public string contract;
    public string progress;
    public float progress01;
    public string lead;
    public EchoLeadState leadState;
    public string feedback;
    public string prediction;
    public int feedbackSequence;
}

public static class EchoRunPresentation
{
    public const float SingleContractFeedbackFadeInSeconds = 0.15f;
    public const float SingleContractFeedbackHoldSeconds = 1.7f;
    public const float SingleContractFeedbackFadeOutSeconds = 0.35f;
    public const float SingleContractFeedbackDurationSeconds =
        SingleContractFeedbackFadeInSeconds + SingleContractFeedbackHoldSeconds
        + SingleContractFeedbackFadeOutSeconds;
    public const string SingleContractRouteGuide =
        "方框：预测 / 三角：反制 / 双线：中立";
    public static EchoMenuViewData BuildMenu(int generation,
        PlayerStyleData style, int minimumJumpSamples, int minimumSlideSamples,
        EchoContractData contractPreview = null, float echoClarity = 1f)
    {
        if (generation <= 0)
        {
            return new EchoMenuViewData
            {
                generation = "首次回声校准",
                learned = "跑一局，让回声学习你的路线、动作与节奏",
                rule = "校准完成后会生成第 1 代回声",
                objective = "至少跳跃 " + minimumJumpSamples
                            + " 次并滑铲 " + minimumSlideSamples + " 次",
                primaryAction = "开始校准"
            };
        }

        EchoContractData contract = contractPreview != null
            ? contractPreview.ResetForRun()
            : EchoContractPolicy.Create(style, generation);
        return new EchoMenuViewData
        {
            generation = "第 " + generation + " 代回声"
                         + (echoClarity < 0.995f
                             ? " · 清晰度 "
                               + Mathf.RoundToInt(Mathf.Clamp01(echoClarity)
                                                  * 100f) + "%"
                             : ""),
            learned = TrimPrefix(contract.learnedTrait, "AI识别："),
            rule = contract.ruleDescription,
            objective = contract.objective,
            primaryAction = "挑战第 " + generation + " 代回声"
        };
    }

    public static EchoMenuViewData BuildSingleContractMenu(
        ActiveEchoIdentity identity, int minimumJumpSamples = 2,
        int minimumSlideSamples = 2, int minimumTotalSamples = 24,
        int minimumActiveSamples = 6,
        int minimumActionCategories = 2)
    {
        if (identity == null)
        {
            return new EchoMenuViewData
            {
                generation = "你的跑法，会成为下一局的对手",
                learned = "最近选路：还需要观察",
                rule = "尝试选路、跳跃和滑铲，让回声认识你的跑法",
                objective = "跑到终点；观察充分后形成下一局的回声",
                primaryAction = "开始第一局"
            };
        }

        int generation = Mathf.Max(1, identity.generation);
        string generationText = "第" + generation + "代回声";
        EchoMemoryContract memory = identity.memoryContract != null
            ? identity.memoryContract.Clone()
            : null;
        if (memory == null)
        {
            return new EchoMenuViewData
            {
                generation = generationText + "还在",
                learned = "最近选路：还需要观察",
                rule = "这局重新观察动作与选路；旧回声仍保留",
                objective = "本局观察充分并到达终点后，才能更新回声",
                primaryAction = "继续观察"
            };
        }

        memory.Normalize();
        if (identity.RequiresRouteCalibration)
        {
            return new EchoMenuViewData
            {
                generation = generationText + "还在",
                learned = "最近选路：还需要观察",
                rule = "这局重新观察动作与选路；旧回声仍保留",
                objective = "本局观察充分并到达终点后，才能更新回声",
                primaryAction = "继续观察"
            };
        }

        return new EchoMenuViewData
        {
            generation = generationText,
            learned = "当前回声记录：" + SingleContractRouteObservation(
                memory.preferredLane) + " → 初始预测依据",
            rule = "预测路线通过会让回声抢先；连续两次反制通过，且至少剩两次选路时改猜",
            objective = "领先回声到终点",
            primaryAction = "挑战第" + generation + "代回声"
        };
    }

    public static string BuildSingleContractCognitionSummary(
        EchoCognitionAssessment assessment)
    {
        if (!assessment.IsAvailable) return "";

        string previousLane = SingleContractCognitionLaneName(
            assessment.PreviousLane);
        string nextLane = SingleContractCognitionLaneName(
            assessment.NextLane);
        string runEvidence = "本局反制通过 "
                             + assessment.SuccessfulCounterCount + "/"
                             + assessment.TotalGateCount + " 次 · "
                             + (assessment.RelearnStartGateNumber > 0
                                 ? "从第" + assessment.RelearnStartGateNumber
                                   + "次选路起，后续预测已调整"
                                 : "后续预测未调整");

        string nextCognition;
        switch (assessment.ChangeKind)
        {
            case EchoCognitionChangeKind.Consolidated:
                nextCognition = "下一局记录：更确定偏向" + nextLane;
                break;
            case EchoCognitionChangeKind.Shaken:
                nextCognition = assessment.NextMemoryPrecise
                    ? "下一局记录：仍可能偏向" + nextLane
                    : "下一局记录：路线倾向还需要观察";
                break;
            case EchoCognitionChangeKind.Shifted:
                nextCognition = "下一局记录：开始偏向" + nextLane;
                break;
            case EchoCognitionChangeKind.Reversed:
                nextCognition = "下一局记录：已更新为偏向" + nextLane;
                break;
            default:
                nextCognition = "下一局记录：仍偏向" + nextLane;
                break;
        }

        return "此前记录：选路偏向" + previousLane + "\n"
               + runEvidence + "\n" + nextCognition;
    }

    public static SingleContractResultViewData BuildSingleContractResultView(
        SingleContractResultFacts facts)
    {
        var view = new SingleContractResultViewData
        {
            kind = SingleContractResultKind.Unknown,
            title = "本局结果",
            summary = "",
            primaryAction = "返回菜单",
            nextAction = SingleContractResultNextAction.ReturnToMenu
        };
        if (!facts.available) return view;

        int generation = Mathf.Max(1, facts.generationBefore);
        view.title = facts.wasChallenge
            ? facts.won ? "你跑赢了第" + generation + "代回声"
                : "第" + generation + "代回声胜出"
            : "AI 看到了你的跑法";
        // Frozen friend challenges and isolated validation never imply a local
        // save failure just because their commit flags are false.
        if (facts.isAsync)
        {
            view.kind = SingleContractResultKind.AsyncChallenge;
            view.title = facts.endReason == RunEndReason.Abandoned
                ? "好友挑战已结束" : facts.won ? "好友挑战成功" : "好友影子胜出";
            view.summary = "本局不影响你的回声成长和本地纪录";
            view.primaryAction = "再挑战";
            view.nextAction = SingleContractResultNextAction.RetryAsync;
            return view;
        }
        if (facts.isTransient)
        {
            view.kind = facts.validationIdentityUnchanged
                ? SingleContractResultKind.TransientValidation
                : SingleContractResultKind.ValidationIsolationFailed;
            view.summary = facts.validationIdentityUnchanged
                ? "固定验收 · 身份档未修改"
                : "固定验收隔离失败\n真实身份档发生意外变化";
            return view;
        }

        view.nextAction = SingleContractResultNextAction.RestartLocal;
        view.primaryAction = facts.savedIdentityCanChallenge
            ? "再次挑战第" + Mathf.Max(1, facts.savedGeneration) + "代回声"
            : facts.savedGeneration > 0 ? "继续观察" : "重新观察";
        // A missing winning candidate is rejected by the existing transaction
        // validator. That is a generation outcome, not evidence of an I/O fault.
        if (facts.wasChallenge && facts.won && !facts.promotionBuilt)
        {
            view.kind = SingleContractResultKind.WonWithoutPromotion;
            view.summary = "下一代尚未形成\n当前回声保持不变";
            return view;
        }
        if (!facts.commitAttempted || !facts.commitSucceeded)
        {
            view.kind = SingleContractResultKind.SaveFailed;
            if (!facts.wasChallenge) view.title = "回声保存失败";
            view.summary = "回声保存失败\n新代未生效，当前回声保持不变";
            view.primaryAction = "返回菜单";
            view.nextAction = SingleContractResultNextAction.ReturnToMenu;
            return view;
        }
        if (facts.identityPromoted)
        {
            view.kind = facts.savedIdentityCanChallenge
                ? SingleContractResultKind.UpdatedChallengeReady
                : SingleContractResultKind.UpdatedNeedsObservation;
            string updated = "第" + Mathf.Max(1, facts.savedGeneration)
                             + "代回声已生效";
            if (!facts.wasChallenge) view.title = updated;
            view.summary = updated + "\n" + (facts.savedIdentityCanChallenge
                ? BuildSavedMemorySummary(facts) : "路线倾向仍需观察");
            view.primaryAction = facts.savedIdentityCanChallenge
                ? "挑战新回声" : "继续观察";
            return view;
        }
        if (facts.wasChallenge)
        {
            view.kind = facts.won ? SingleContractResultKind.WonWithoutPromotion
                : SingleContractResultKind.ChallengeLost;
            view.summary = facts.won
                ? "下一代尚未形成\n当前回声保持不变"
                : "本局" + (facts.endReason == RunEndReason.FinishReached
                    ? "未能领先到终点" : "未到终点") + "\n当前回声保持不变";
            return view;
        }
        bool generationFailed = facts.calibrationProgress.finishReached
                                && facts.calibrationProgress.PlayerEvidenceReady;
        view.kind = generationFailed
            ? SingleContractResultKind.ObservationGenerationFailed
            : SingleContractResultKind.ObservationIncomplete;
        if (generationFailed) view.title = "回声形成遇到问题";
        view.summary = generationFailed
            ? "观察已充分，新回声未形成\n当前回声保持不变"
            : "本局未形成新回声\n"
              + BuildSingleContractObservationCue(facts.calibrationProgress);
        return view;
    }

    private static string BuildSavedMemorySummary(SingleContractResultFacts facts)
    {
        EchoCognitionAssessment assessment = facts.cognitionAssessment;
        if (!assessment.IsAvailable)
            return "它记住了：" + SingleContractRouteObservation(facts.savedPreferredLane);
        string lane = SingleContractRouteObservation(assessment.NextLane);
        switch (assessment.ChangeKind)
        {
            case EchoCognitionChangeKind.Consolidated:
                return "回声记录：更确定" + lane;
            case EchoCognitionChangeKind.Shaken:
                return "回声记录：仍可能" + lane;
            case EchoCognitionChangeKind.Shifted:
                return "回声记录：开始" + lane;
            case EchoCognitionChangeKind.Reversed:
                return "回声记录：" + SingleContractRouteObservation(
                    assessment.PreviousLane) + " → " + lane;
            default:
                return "回声记录：仍" + lane;
        }
    }

    public static string BuildSingleContractObservationCue(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets) return "正在观察本局跑法";
        if (progress.PlayerEvidenceReady)
            return progress.finishReached ? "本局观察已充分"
                : "观察已充分，请跑到终点";
        if (progress.jumpSamples < progress.minimumJumpSamples)
            return progress.jumpSamples == 0 ? "本局尚未记录到跳跃"
                : "本局跳跃还缺" + (progress.minimumJumpSamples - progress.jumpSamples) + "次";
        if (progress.slideSamples < progress.minimumSlideSamples)
            return progress.slideSamples == 0 ? "本局尚未记录到滑铲"
                : "本局滑铲还缺" + (progress.minimumSlideSamples - progress.slideSamples) + "次";
        if (progress.formalChoices < progress.minimumFormalChoices)
            return "本局选路 " + Count(progress.formalChoices) + "/"
                   + Count(progress.minimumFormalChoices);
        if (progress.successfulChoices < progress.minimumSuccessfulChoices)
            return "本局成功通过 " + Count(progress.successfulChoices) + "/"
                   + Count(progress.minimumSuccessfulChoices);
        if (!progress.routeMemoryReady)
            return "本局选路倾向尚未稳定";
        if (progress.activeSamples < progress.minimumActiveSamples
            || progress.actionCategories < progress.minimumActionCategories)
            return "本局还需要更多动作观察";
        if (progress.totalSamples < progress.minimumTotalSamples)
            return "继续跑，留下本局跑法";
        return "本局证据仍需确认";
    }

    public static string BuildSingleContractMissingEvidence(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets) return "本局暂无完整观察记录";
        var missing = new System.Collections.Generic.List<string>();
        AddMissingEvidence(missing, "跳跃", progress.jumpSamples, progress.minimumJumpSamples);
        AddMissingEvidence(missing, "滑铲", progress.slideSamples, progress.minimumSlideSamples);
        AddMissingEvidence(missing, "动作种类", progress.actionCategories, progress.minimumActionCategories);
        AddMissingEvidence(missing, "主动动作", progress.activeSamples, progress.minimumActiveSamples);
        AddMissingEvidence(missing, "观察记录", progress.totalSamples, progress.minimumTotalSamples);
        AddMissingEvidence(missing, "选路", progress.formalChoices, progress.minimumFormalChoices);
        AddMissingEvidence(missing, "成功通过", progress.successfulChoices, progress.minimumSuccessfulChoices);
        if (!progress.routeMemoryReady) missing.Add("选路倾向尚未稳定");
        if (!progress.finishReached) missing.Add("尚未到达终点");
        return missing.Count > 0 ? "本局还缺：" + string.Join("；", missing)
            : BuildSingleContractObservationCue(progress);
    }

    private static void AddMissingEvidence(System.Collections.Generic.List<string> missing,
        string label, int current, int required)
    {
        if (current < required) missing.Add(label + " " + Count(current) + "/" + Count(required));
    }

    public static SingleContractHudData BuildSingleContractHud(
        SingleContractHudInput input)
    {
        bool showCalibrationProgress = input.visualState
                                       == SingleContractVisualState.Calibration
                                       && input.calibrationProgress.HasTargets;
        float calibrationProgress01 = showCalibrationProgress
            ? input.calibrationProgress.Progress01 : 0f;
        bool openingMemory = input.openingMemory
                             && input.generation > 0
                             && input.visualState
                             != SingleContractVisualState.Calibration;
        bool openingReplay = openingMemory && input.openingReplay
                             && input.openingReplayCount
                             >= EchoSignatureActionParser
                                 .MinimumSignatureActionCount
                             && IsOpeningReplayAction(
                                 input.openingReplayAction);
        int generation = Mathf.Max(0, input.generation);
        string memory = openingMemory
            ? openingReplay
                ? BuildOpeningReplayDetail(input.openingReplayAction,
                    input.openingReplayCount)
                : "领先它到终点"
            : showCalibrationProgress
                ? BuildSingleContractObservationCue(input.calibrationProgress)
                : NormalizeSingleContractMemory(input.memory);
        bool showPrediction = !openingMemory && input.showPrediction
                              && input.predictionGateActive
                              && input.visualState
                              != SingleContractVisualState.Calibration
                              && input.predictedLane >= 0
                               && input.predictedLane <= 2;
        int predictionGateCount = Mathf.Max(0, input.predictionGateCount);
        int predictionGateNumber = predictionGateCount > 0
            ? Mathf.Clamp(input.predictionGateNumber, 1,
                predictionGateCount)
            : Mathf.Max(0, input.predictionGateNumber);
        float leadMeters = input.leadMeters;
        SingleContractLeadState leadState;
        string lead;
        if (leadMeters > 0.05f)
        {
            leadState = SingleContractLeadState.PlayerLeading;
            lead = "你领先：" + leadMeters.ToString("0.0") + "米";
        }
        else if (leadMeters < -0.05f)
        {
            leadState = SingleContractLeadState.EchoLeading;
            lead = "回声领先：" + Mathf.Abs(leadMeters).ToString("0.0")
                   + "米";
        }
        else
        {
            leadState = SingleContractLeadState.Tied;
            lead = "并驾齐驱：0.0米";
        }

        int injuries = Mathf.Max(0, input.injuries);
        int maximumCollisionStrikes = input.maximumCollisionStrikes > 0
            ? input.maximumCollisionStrikes : 2;
        string injuriesText = "伤势 " + injuries + "/" + maximumCollisionStrikes;
        if (injuries >= maximumCollisionStrikes)
            injuriesText += " · 已出局";
        else if (input.collisionRecoveryTimeRemaining > 0f)
            injuriesText += " · 恢复中";
        else if (injuries == maximumCollisionStrikes - 1)
            // A shield or the recovery window can absorb a collision without
            // another injury. Describe the next injury, not the next contact.
            injuriesText += " · 再受伤即出局";
        float finishRemaining = Mathf.Max(0f, input.finishRemaining);
        string powerUp = (input.powerUp ?? "").Trim();
        string result = (input.result ?? "").Trim();
        return new SingleContractHudData
        {
            visualState = input.visualState,
            openingMemory = openingMemory,
            openingReplay = openingReplay,
            openingTitle = openingMemory
                ? "第" + Mathf.Max(1, generation) + "代回声现身"
                : "",
            generation = generation,
            showMemory = openingMemory
                         || input.visualState
                         == SingleContractVisualState.Calibration,
            memory = memory,
            prediction = showPrediction
                ? "本次预测：" + CompactSingleContractLaneName(input.predictedLane)
                : "",
            predictionGateNumber = predictionGateNumber,
            predictionGateCount = predictionGateCount,
            predictionGateActive = input.predictionGateActive,
            leadMeters = leadMeters,
            leadState = leadState,
            lead = lead,
            injuries = injuries,
            injuriesText = injuriesText,
            finishRemaining = finishRemaining,
            finishRemainingText = finishRemaining > 0.01f
                ? "终点距离：" + Mathf.CeilToInt(finishRemaining) + "米"
                : "终点已到达",
            showPowerUp = !openingMemory && !string.IsNullOrEmpty(powerUp),
            powerUp = string.IsNullOrEmpty(powerUp)
                ? "当前补给：无"
                : NormalizeSingleContractLabel(powerUp, "当前补给：", "无"),
            instantFeedbackKind = openingMemory
                ? SingleContractInstantFeedback.None
                : input.instantFeedback,
            feedbackLeadDeltaMeters = input.feedbackLeadDeltaMeters,
            feedbackRelearned = !openingMemory && input.feedbackRelearned,
            instantFeedback = SingleContractFeedbackFor(
                openingMemory
                    ? SingleContractInstantFeedback.None
                    : input.instantFeedback,
                input.feedbackLeadDeltaMeters,
                !openingMemory && input.feedbackRelearned),
            feedbackSequence = Mathf.Max(0, input.feedbackSequence),
            showCalibrationProgress = showCalibrationProgress,
            calibrationProgress01 = calibrationProgress01,
            calibrationMeterText = showCalibrationProgress
                ? BuildSingleContractObservationCue(input.calibrationProgress)
                : "",
            calibrationActionProgress = showCalibrationProgress
                ? BuildCalibrationActionLine(
                    input.calibrationProgress, injuries, compact: true)
                : "",
            calibrationRouteProgress = showCalibrationProgress
                ? BuildCalibrationRouteLine(
                    input.calibrationProgress, compact: true)
                : "",
            calibrationDetails = showCalibrationProgress
                ? BuildSingleContractCalibrationEvidence(input.calibrationProgress)
                  + "\n" + BuildSingleContractMissingEvidence(input.calibrationProgress)
                : "",
            result = result
        };
    }

    public static string BuildSingleContractCalibrationResult(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets)
        {
            return "AI 看到了你的跑法\n"
                   + "本局未形成新的回声；再跑一局，继续观察";
        }

        bool generationProblem = progress.finishReached
                                 && progress.evidenceReady
                                 && !progress.promotionReady;
        string title = generationProblem
            ? "回声形成遇到问题" : "AI 看到了你的跑法";
        return title + "\n"
               + BuildSingleContractCalibrationEvidence(progress)
               + " · " + (progress.finishReached
                   ? "已经到终点" : "还没到终点")
               + "\n" + BuildSingleContractMissingEvidence(progress)
               + (generationProblem
                   ? "\n观察已充分，但新回声没有形成；再跑一局，继续观察"
                   : "\n本局未形成新的回声；再跑一局，继续观察");
    }

    public static string BuildSingleContractCalibrationEvidence(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets) return "";
        return BuildCalibrationLearningLine(progress, compact: false)
               + " · " + BuildCalibrationActionLine(
                   progress, 0, compact: false)
               + "\n" + BuildCalibrationRouteLine(
                   progress, compact: true);
    }

    public static string BuildPlayerRouteEvidence(SingleContractCalibrationProgress progress)
    {
        if (progress.formalChoices <= 0) return "本局还没有可用的选路记录";
        if (!progress.preferredLaneUnique || progress.preferredLane < 0)
            return "本局记录了 " + progress.formalChoices + " 次选路，尚无唯一偏向";
        string lane = progress.preferredLane == 0 ? "左路"
            : progress.preferredLane == 2 ? "右路" : "中路";
        return "本局 " + progress.formalChoices + " 次选路中，有 "
               + progress.strongestRouteChoices + " 次选择了" + lane;
    }

    // The original result remains the evidence record. These two projections
    // let the result screen keep the record change brief without losing detail.
    public static string BuildSingleContractResultSummary(string fullResult)
    {
        string[] lines = SingleContractResultLines(fullResult);
        if (FindSingleContractResultLine(lines, "固定验收隔离失败") != null)
            return "固定验收隔离失败\n真实身份档发生意外变化";
        if (FindSingleContractResultLine(lines, "回声保存失败") != null
            || FindSingleContractResultLine(lines, "身份结算保存失败") != null)
            return "回声保存失败\n当前回声未改变";
        if (FindSingleContractResultLine(lines, "固定验收模式 · 身份档未修改") != null
            || FindSingleContractResultLine(lines, "真实身份档未修改") != null)
            return "固定验收 · 身份档未修改";
        if (FindSingleContractResultLine(lines, "回声形成遇到问题") != null
            || FindSingleContractResultLine(lines, "观察已充分，但新回声没有形成") != null)
            return "回声形成遇到问题\n观察已充分，新回声未形成";
        if (FindSingleContractResultLine(lines, "本局未形成新的回声") != null)
            return "新回声尚未形成\n再跑一局，继续观察";
        if (FindSingleContractResultLine(lines, "这局还不足以形成下一代") != null)
            return "下一代尚未形成\n当前回声保持不变";
        if (FindSingleContractResultLine(lines, "下一局仍使用本代记录") != null)
            return "下一局仍使用本代记录";

        foreach (string line in lines)
        {
            if (!line.StartsWith("第", System.StringComparison.Ordinal)
                || !line.EndsWith("代回声已经形成", System.StringComparison.Ordinal)) continue;
            string evidence = FindSingleContractResultLine(lines, "本局 ");
            if (evidence != null)
                return line + "\n" + evidence + "\n下一局，回声会依据这份记录进行预测";
        }

        string next = FindSingleContractResultLine(lines, "下一局记录：");
        if (next != null)
        {
            next = next.Substring("下一局记录：".Length);
            string previous = FindSingleContractResultLine(lines, "此前记录：选路偏向");
            previous = previous == null ? "" : CompactSingleContractResultLane(
                previous.Substring("此前记录：选路偏向".Length));
            if (next.StartsWith("已更新为偏向", System.StringComparison.Ordinal))
            {
                string lane = CompactSingleContractResultLane(
                    next.Substring("已更新为偏向".Length));
                return "回声记录：" + (string.IsNullOrEmpty(previous)
                    ? lane : previous + " → " + lane);
            }
            if (next.StartsWith("开始偏向", System.StringComparison.Ordinal))
                return "回声记录：开始" + CompactSingleContractResultLane(
                    next.Substring("开始偏向".Length));
            if (next.StartsWith("仍可能偏向", System.StringComparison.Ordinal))
                return "回声记录：仍可能" + CompactSingleContractResultLane(
                    next.Substring("仍可能偏向".Length));
            if (next.StartsWith("更确定偏向", System.StringComparison.Ordinal))
                return "回声记录：更确定" + CompactSingleContractResultLane(
                    next.Substring("更确定偏向".Length));
            if (next.StartsWith("仍偏向", System.StringComparison.Ordinal))
                return "回声记录：仍" + CompactSingleContractResultLane(
                    next.Substring("仍偏向".Length));
            if (next == "路线倾向还需要观察")
                return "回声记录：还需要观察";
            // Preserve any future qualifier rather than turning an unfamiliar
            // record description into a definite lane claim.
            return "回声记录：" + next;
        }

        string memory = FindSingleContractResultLine(lines, "它记住了：");
        if (memory != null)
        {
            memory = memory.Substring("它记住了：".Length);
            if (memory == "回声记忆模糊") return "回声记录：还需要观察";
            const string lanePrefix = "选路记录中，你更常选择";
            if (memory.StartsWith(lanePrefix, System.StringComparison.Ordinal))
                return "回声记录：" + CompactSingleContractResultLane(
                    memory.Substring(lanePrefix.Length));
            const string legacyPrefix = "压力出现时，你偏向";
            if (memory.StartsWith(legacyPrefix, System.StringComparison.Ordinal))
                return "回声记录：" + CompactSingleContractResultLane(
                    memory.Substring(legacyPrefix.Length));
        }
        foreach (string line in lines)
            if (line.StartsWith("第", System.StringComparison.Ordinal)
                && line.EndsWith("代回声已经形成", System.StringComparison.Ordinal))
                return line;
        return "";
    }

    public static string BuildSingleContractResultDetails(string fullResult,
        string visibleTitle)
    {
        string[] lines = SingleContractResultLines(fullResult);
        int start = lines.Length > 0 && string.Equals(lines[0],
            (visibleTitle ?? "").Trim(), System.StringComparison.Ordinal) ? 1 : 0;
        for (int index = start; index < lines.Length; index++)
        {
            const string choicePrefix = "关键选择：";
            if (lines[index].StartsWith(choicePrefix, System.StringComparison.Ordinal))
                lines[index] = "最近一次选路：" + lines[index].Substring(choicePrefix.Length);
        }
        return string.Join("\n", lines, start, lines.Length - start);
    }

    private static string[] SingleContractResultLines(string result)
    {
        return (result ?? "").Trim().Replace("\r\n", "\n").Split(
            new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    private static string FindSingleContractResultLine(string[] lines, string prefix)
    {
        foreach (string line in lines)
            if (line.StartsWith(prefix, System.StringComparison.Ordinal)) return line;
        return null;
    }

    private static string CompactSingleContractResultLane(string lane)
    {
        switch (lane)
        {
            case "左侧": return "偏左";
            case "右侧": return "偏右";
            case "中间": return "中路";
            default: return lane;
        }
    }

    public static string BuildSingleContractAdaptationReview(
        GateAttempt trigger, int startGateNumber)
    {
        if (trigger == null || trigger.gateId <= 0 || startGateNumber <= 0
            || trigger.chosenRole != PredictionGateRole.Counter
            || trigger.execution != GateExecutionOutcome.Success)
            return "";
        return "改猜原因：连续两次反制通过\n"
               + "触发选路（第" + trigger.gateId + "次）："
               + CompactSingleContractLaneName(trigger.committedLane)
               + "反制通过；从第" + startGateNumber + "次选路起调整预测";
    }

    public static string BuildSingleContractGateReview(GateAttempt attempt)
    {
        if (attempt == null || attempt.gateId <= 0
            || attempt.committedLane < 0 || attempt.committedLane > 2)
            return "";

        string role = attempt.chosenRole == PredictionGateRole.Counter
            ? "反制"
            : attempt.chosenRole == PredictionGateRole.Predicted
                ? "预测" : "安全";
        string choice = "关键选择：提交"
                        + CompactSingleContractLaneName(attempt.committedLane)
                        + "（" + role + "）";
        if (attempt.hasLateralEvidence && attempt.laneChangeInProgress)
            choice += "，当时仍在换道";

        string execution;
        switch (attempt.executionReason)
        {
            case GateExecutionReason.Completed:
                execution = "成功通过";
                break;
            case GateExecutionReason.Collision:
                execution = "发生碰撞，通过未完成";
                break;
            case GateExecutionReason.RouteAbandoned:
                execution = "离开原提交路线，通过未完成";
                break;
            case GateExecutionReason.Cancelled:
                execution = "观察中断，通过结果未确认";
                break;
            case GateExecutionReason.Unresolved:
                execution = "未取得通过证据，无法确认完成";
                break;
            default:
                execution = attempt.execution == GateExecutionOutcome.Success
                    ? "成功通过"
                    : attempt.execution == GateExecutionOutcome.Cancelled
                        ? "观察中断，通过结果未确认"
                        : "未取得通过证据，无法确认完成";
                break;
        }
        return choice + "\n动作结果：" + execution;
    }

    public static EchoHudViewData BuildHud(bool hasOpponent,
        EchoContractData contract, float playerLead,
        int minimumJumpSamples, int minimumSlideSamples,
        int jumpSamples = 0, int slideSamples = 0,
        float calibrationProgress01 = 0f,
        EchoDuelPhase duelPhase = EchoDuelPhase.None,
        float phaseProgress01 = 0f, string publicPrediction = "",
        int syncRemaining = 2, float recoverySeconds = 0f,
        float recoveryDuration = 1.25f, float remainingDistance = 0f,
        int contractMarkerCount = 0, bool showBuff = false,
        string buffText = "", bool phaseTransitionPending = false,
        EchoDuelPhase pendingPhase = EchoDuelPhase.None,
        string rewriteStyleSummary = "",
        string finaleSegmentSummary = "",
        EchoChallengeStep challengeStep = default)
    {
        bool calibrating = !hasOpponent || contract == null
                           || contract.type == EchoContractType.None;
        float calibration = Mathf.Clamp01(calibrationProgress01);
        float phaseProgress = Mathf.Clamp01(phaseProgress01);
        float echoLock = contract != null ? contract.EchoLock01 : 0f;
        if (calibrating)
        {
            return new EchoHudViewData
            {
                mode = EchoHudMode.Calibration,
                meterKind = EchoHudMeterKind.Calibration,
                phaseIndex = -1,
                calibrationProgress01 = calibration,
                phaseProgress01 = phaseProgress,
                contractStability01 = echoLock,
                echoLock01 = echoLock,
                displayedMeter01 = calibration,
                announcement = "回声校准",
                directiveShort = "跳跃 "
                    + Mathf.Min(jumpSamples, minimumJumpSamples) + "/"
                    + minimumJumpSamples + " · 滑铲 "
                    + Mathf.Min(slideSamples, minimumSlideSamples) + "/"
                    + minimumSlideSamples,
                predictionShort = "",
                leadMeters = 0f,
                leadPosition01 = 0.5f,
                syncRemaining = Mathf.Max(0, syncRemaining),
                recoveryProgress01 = RecoveryProgress(recoverySeconds,
                    recoveryDuration),
                recoverySeconds = Mathf.Max(0f, recoverySeconds),
                remainingDistance = Mathf.Max(0f, remainingDistance),
                showPrediction = false,
                showBuff = showBuff,
                buffText = buffText ?? ""
            };
        }

        EchoDuelPhase phase = duelPhase != EchoDuelPhase.None
            ? duelPhase
            : contract.duelPhase != EchoDuelPhase.None
                ? contract.duelPhase
                : EchoDuelPhase.Resistance;
        bool finaleFailed = phase == EchoDuelPhase.Finale
                            && (contract.duelFailed || !contract.completed);
        bool finaleNeedsContract = false;
        EchoHudMode mode = ResolveHudMode(phase, finaleNeedsContract,
            finaleFailed);
        EchoHudMeterKind meterKind = ResolveMeterKind(mode);
        float displayed = meterKind == EchoHudMeterKind.Phase
            ? phaseProgress
            : meterKind == EchoHudMeterKind.EchoLock ? echoLock : 0f;
        bool showPrediction = phase == EchoDuelPhase.Reveal
                              || phase == EchoDuelPhase.Resistance
                              || phase == EchoDuelPhase.Counterattack;
        float leadRange = phase == EchoDuelPhase.Finale ? 20f : 12f;
        float leadPosition = 0.5f + 0.5f * (float)System.Math.Tanh(
            playerLead / leadRange);

        return new EchoHudViewData
        {
            mode = mode,
            meterKind = meterKind,
            phaseIndex = PhaseIndex(phase),
            calibrationProgress01 = calibration,
            phaseProgress01 = phaseProgress,
            contractStability01 = echoLock,
            echoLock01 = echoLock,
            displayedMeter01 = displayed,
            announcement = AnnouncementFor(mode),
            directiveShort = phaseTransitionPending
                && pendingPhase != EchoDuelPhase.None
                ? "前方同步：" + EchoDuelFlow.PhaseName(pendingPhase)
                : DirectiveFor(mode, contract, rewriteStyleSummary,
                    finaleSegmentSummary, challengeStep),
            predictionShort = showPrediction
                ? BuildShortPrediction(contract, challengeStep) : "",
            leadMeters = playerLead,
            leadPosition01 = Mathf.Clamp01(leadPosition),
            syncRemaining = Mathf.Max(0, syncRemaining),
            recoveryProgress01 = RecoveryProgress(recoverySeconds,
                recoveryDuration),
            recoverySeconds = Mathf.Max(0f, recoverySeconds),
            remainingDistance = Mathf.Max(0f, remainingDistance),
            contractMarkerCount = Mathf.Max(0, contractMarkerCount),
            showContractMarkers = contract.type
                                  == EchoContractType.BreakLaneHabit,
            showPrediction = showPrediction,
            phaseTransitionPending = phaseTransitionPending,
            pendingPhase = pendingPhase,
            showBuff = showBuff,
            buffText = buffText ?? ""
        };
    }

    public static string BuildShortDirective(EchoContractData contract)
    {
        if (contract == null) return "";
        switch (contract.type)
        {
            case EchoContractType.BreakLaneHabit:
                return "改走" + EchoContractPolicy.LaneName(contract.targetLane);
            case EchoContractType.ChangeVerticalHabit:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + EchoContractPolicy.ActionName(contract.targetAction);
            case EchoContractType.DisruptRhythm:
                return "下一次" + EchoContractPolicy.ActionName(
                    contract.targetAction);
            default:
                return "";
        }
    }

    public static string BuildShortPrediction(EchoContractData contract)
    {
        return BuildShortPrediction(contract, default);
    }

    public static string BuildShortPrediction(EchoContractData contract,
        EchoChallengeStep challengeStep)
    {
        if (contract == null) return "";
        bool currentHypothesis = challengeStep.stepId > 0
                                 && challengeStep.prediction.hypothesisVersion
                                 == contract.hypothesisVersion;
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = currentHypothesis
                       && challengeStep.predictedLane >= 0
                ? challengeStep.predictedLane
                : contract.predictionLane >= 0
                ? contract.predictionLane : contract.learnedLane;
            return "预判" + EchoContractPolicy.LaneName(lane);
        }
        ShadowAction action = currentHypothesis
                              && challengeStep.predictedAction
                              != ShadowAction.Keep
            ? challengeStep.predictedAction
            : contract.predictionAction != ShadowAction.Keep
            ? contract.predictionAction : contract.learnedAction;
        return "预判" + EchoContractPolicy.ActionName(action);
    }

    public static EchoDuelViewData BuildDuel(bool hasOpponent,
        EchoContractData contract, float playerLead,
        int minimumJumpSamples, int minimumSlideSamples,
        int jumpSamples = 0, int slideSamples = 0,
        float calibrationProgress01 = 0f,
        EchoDuelPhase duelPhase = EchoDuelPhase.None,
        float phaseProgress01 = 0f, string publicPrediction = "")
    {
        if (!hasOpponent || contract == null
            || contract.type == EchoContractType.None)
        {
            return new EchoDuelViewData
            {
                phase = "校准",
                contract = "正在校准你的回声",
                progress = "跳跃 " + Mathf.Min(jumpSamples, minimumJumpSamples)
                           + "/" + minimumJumpSamples + " · 滑铲 "
                           + Mathf.Min(slideSamples, minimumSlideSamples)
                           + "/" + minimumSlideSamples,
                progress01 = Mathf.Clamp01(calibrationProgress01),
                lead = "记录路线、动作与节奏",
                leadState = EchoLeadState.Calibrating,
                feedback = ""
            };
        }

        EchoDuelPhase phase = duelPhase != EchoDuelPhase.None
            ? duelPhase
            : contract.duelPhase != EchoDuelPhase.None
                ? contract.duelPhase : EchoDuelPhase.Resistance;

        EchoHudViewData hud = BuildHud(true, contract, playerLead,
            minimumJumpSamples, minimumSlideSamples, jumpSamples, slideSamples,
            calibrationProgress01, phase, phaseProgress01, publicPrediction);

        EchoLeadState state;
        string lead;
        if (playerLead > 0.05f)
        {
            state = EchoLeadState.Leading;
            lead = "领先 +" + playerLead.ToString("0.0") + "m";
        }
        else if (playerLead < -0.05f)
        {
            state = EchoLeadState.Trailing;
            lead = "落后 -" + Mathf.Abs(playerLead).ToString("0.0") + "m";
        }
        else
        {
            state = EchoLeadState.Tied;
            lead = "并驾齐驱";
        }

        return new EchoDuelViewData
        {
            phase = EchoDuelFlow.PhaseName(phase),
            contract = ContractStatusFor(contract, phase),
            progress = BuildProgressText(contract, phase, phaseProgress01),
            progress01 = hud.displayedMeter01,
            lead = lead,
            leadState = state,
            feedback = BuildFeedback(contract.lastFeedback),
            prediction = ShouldShowPrediction(phase)
                ? (string.IsNullOrEmpty(publicPrediction)
                    ? BuildPublicPrediction(contract) : publicPrediction)
                : "",
            feedbackSequence = contract.feedbackSequence
        };
    }

    private static EchoHudMode ResolveHudMode(EchoDuelPhase phase,
        bool finaleNeedsContract, bool finaleFailed)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return EchoHudMode.Detection;
            case EchoDuelPhase.Reveal: return EchoHudMode.Reveal;
            case EchoDuelPhase.Counterattack: return EchoHudMode.Counterattack;
            case EchoDuelPhase.Rewrite: return EchoHudMode.Rewrite;
            case EchoDuelPhase.Finale:
                if (finaleFailed) return EchoHudMode.FinaleFailed;
                return finaleNeedsContract
                    ? EchoHudMode.FinaleContract
                    : EchoHudMode.FinaleClean;
            default: return EchoHudMode.Resistance;
        }
    }

    private static EchoHudMeterKind ResolveMeterKind(EchoHudMode mode)
    {
        switch (mode)
        {
            case EchoHudMode.Calibration:
                return EchoHudMeterKind.Calibration;
            case EchoHudMode.Detection:
            case EchoHudMode.Rewrite:
                return EchoHudMeterKind.Phase;
            case EchoHudMode.Resistance:
            case EchoHudMode.Counterattack:
                return EchoHudMeterKind.EchoLock;
            default:
                return EchoHudMeterKind.None;
        }
    }

    private static int PhaseIndex(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return 0;
            case EchoDuelPhase.Reveal: return 1;
            case EchoDuelPhase.Resistance: return 2;
            case EchoDuelPhase.Counterattack: return 3;
            case EchoDuelPhase.Rewrite: return 4;
            case EchoDuelPhase.Finale: return 5;
            default: return -1;
        }
    }

    private static string AnnouncementFor(EchoHudMode mode)
    {
        switch (mode)
        {
            case EchoHudMode.Detection: return "回声侦测";
            case EchoHudMode.Reveal: return "回声暴露";
            case EchoHudMode.Resistance: return "回声反抗";
            case EchoHudMode.Counterattack: return "回声反扑";
            case EchoHudMode.Rewrite: return "回声重写";
            case EchoHudMode.FinaleClean:
            case EchoHudMode.FinaleContract: return "回声决胜";
            case EchoHudMode.FinaleFailed: return "契约锁定 · 回声决胜";
            default: return "回声校准";
        }
    }

    private static string DirectiveFor(EchoHudMode mode,
        EchoContractData contract, string rewriteStyleSummary,
        string finaleSegmentSummary, EchoChallengeStep challengeStep)
    {
        switch (mode)
        {
            case EchoHudMode.Detection: return "复现";
            case EchoHudMode.Reveal: return "下注";
            case EchoHudMode.Resistance: return "裂解";
            case EchoHudMode.Counterattack:
                return BuildCounterattackDirective(contract, challengeStep);
            case EchoHudMode.Rewrite:
                return "新影成形";
            case EchoHudMode.FinaleClean:
                return "冲刺";
            case EchoHudMode.FinaleContract:
                return string.IsNullOrEmpty(finaleSegmentSummary)
                    ? "最后机会"
                    : finaleSegmentSummary + " · 最后机会";
            case EchoHudMode.FinaleFailed: return "契约锁定 · 完成追逐";
            default: return "";
        }
    }

    public static string BuildCounterattackDirective(
        EchoContractData contract, EchoChallengeStep challengeStep)
    {
        if (contract == null) return "等待交锋";
        if (contract.completed) return "锁定碎裂";

        EchoEncounterResult result = contract.lastEncounterResult;
        if (!result.IsResolved
            || result.phase != EchoDuelPhase.Counterattack)
            return "等待交锋";

        int fracturePercent = Mathf.RoundToInt(contract.Progress01 * 100f);
        bool relockedAfterResult = contract.hypothesisVersion
                                   > result.hypothesisVersion;
        switch (result.outcome)
        {
            case EchoEncounterOutcome.PredictionBroken:
            case EchoEncounterOutcome.SafeChoice:
                return relockedAfterResult
                    ? "本次成功 · 回声改判"
                    : "本次成功 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.PredictionHit:
                return "预判命中 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.Collision:
                return "碰撞失手 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.Cancelled:
                return "未交锋 · 裂解 " + fracturePercent + "%";
            default:
                return "等待交锋";
        }
    }

    private static float RecoveryProgress(float remaining, float duration)
    {
        if (remaining <= 0f || duration <= 0f) return 0f;
        return 1f - Mathf.Clamp01(remaining / duration);
    }

    private static string BuildProgressText(EchoContractData contract,
        EchoDuelPhase phase, float phaseProgress01)
    {
        if (contract.duelFailed) return "锁定未破";
        if (phase == EchoDuelPhase.Detection)
            return "复现 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (phase == EchoDuelPhase.Reveal)
            return "习惯暴露";
        if (phase == EchoDuelPhase.Rewrite)
            return "重写 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (contract.completed) return "锁定碎裂";
        if (phase == EchoDuelPhase.Counterattack)
            return "裂解 " + Mathf.RoundToInt(contract.Progress01 * 100f)
                   + "%";
        float echoLock = contract.EchoLock01;
        if (echoLock >= 0.98f) return "完整锁定";
        if (echoLock >= 0.55f) return "锁定开裂";
        if (echoLock > 0.01f) return "深度裂解";
        return "锁定碎裂";
    }

    private static bool ShouldShowPrediction(EchoDuelPhase phase)
    {
        return phase == EchoDuelPhase.Reveal
               || phase == EchoDuelPhase.Resistance
               || phase == EchoDuelPhase.Counterattack;
    }

    private static string ContractStatusFor(EchoContractData contract,
        EchoDuelPhase phase)
    {
        if (contract.duelFailed) return "锁定未破 · 完成追逐";
        if (phase == EchoDuelPhase.Detection) return "AI正在复现旧习惯";
        if (phase == EchoDuelPhase.Rewrite) return "AI正在记录新策略";
        if (phase == EchoDuelPhase.Finale) return "只争距离";
        if (phase == EchoDuelPhase.Counterattack)
            return "AI已学习你的反制 · 等待你的选择";
        return "AI预测已公开 · 打破旧习惯";
    }

    private static string BuildPublicPrediction(EchoContractData contract)
    {
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = contract.predictionLane >= 0
                ? contract.predictionLane : contract.learnedLane;
            return "预判：依赖" + EchoContractPolicy.LaneName(lane);
        }
        ShadowAction action = contract.predictionAction != ShadowAction.Keep
            ? contract.predictionAction : contract.learnedAction;
        return "预判：继续" + EchoContractPolicy.ActionName(action);
    }

    public static string BuildContractAction(EchoContractData contract)
    {
        if (contract == null) return "回声契约";
        switch (contract.type)
        {
            case EchoContractType.BreakLaneHabit:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + " · 收集引导金币";
            case EchoContractType.ChangeVerticalHabit:
                return EchoContractPolicy.LaneName(contract.targetLane) + " · "
                       + EchoContractPolicy.ActionName(contract.targetAction)
                       + "躲避";
            case EchoContractType.DisruptRhythm:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + " · 下一次："
                       + EchoContractPolicy.ActionName(contract.targetAction);
            default:
                return TrimPrefix(contract.title, "回声契约：");
        }
    }

    private static string BuildFeedback(string feedback)
    {
        if (string.IsNullOrEmpty(feedback)) return "";
        if (feedback.StartsWith("反制生效："))
            return "反制成功 · " + TrimPrefix(feedback, "反制生效：");
        if (feedback.StartsWith("AI施压："))
            return "回声施压 · " + TrimPrefix(feedback, "AI施压：");
        if (feedback.StartsWith("回声施压："))
            return "回声施压 · " + TrimPrefix(feedback, "回声施压：");
        if (feedback.StartsWith("预测失效："))
            return "预测失效 · " + TrimPrefix(feedback, "预测失效：");
        return feedback;
    }

    private static string BuildCalibrationLearningLine(
        SingleContractCalibrationProgress progress, bool compact)
    {
        if (compact)
        {
            return "AI 学习 " + Count(progress.totalSamples) + "/"
                   + Count(progress.minimumTotalSamples) + " · 主动 "
                   + Count(progress.activeSamples) + "/"
                   + Count(progress.minimumActiveSamples) + " · 种类 "
                   + Count(progress.actionCategories) + "/"
                   + Count(progress.minimumActionCategories);
        }
        return "观察 " + Count(progress.totalSamples) + "/"
               + Count(progress.minimumTotalSamples) + " · 主动 "
               + Count(progress.activeSamples) + "/"
               + Count(progress.minimumActiveSamples) + " · 动作种类 "
               + Count(progress.actionCategories) + "/"
               + Count(progress.minimumActionCategories);
    }

    private static string BuildCalibrationActionLine(
        SingleContractCalibrationProgress progress, int injuries,
        bool compact)
    {
        if (compact)
        {
            return "跳 " + Count(progress.jumpSamples) + "/"
                   + Count(progress.minimumJumpSamples) + " · 滑 "
                   + Count(progress.slideSamples) + "/"
                   + Count(progress.minimumSlideSamples) + " · 受伤 "
                   + Count(injuries);
        }
        return "跳跃 " + Count(progress.jumpSamples) + "/"
               + Count(progress.minimumJumpSamples) + " · 滑铲 "
               + Count(progress.slideSamples) + "/"
               + Count(progress.minimumSlideSamples);
    }

    private static string BuildCalibrationRouteLine(
        SingleContractCalibrationProgress progress, bool compact)
    {
        if (compact)
        {
            string route = progress.preferredLaneUnique
                           && progress.preferredLane >= 0
                ? CompactSingleContractLaneName(progress.preferredLane)
                : "同路";
            string routeEvidence;
            if (progress.strongestRouteChoices
                < progress.minimumStrongestRouteChoices)
            {
                routeEvidence = route
                                + Count(progress.strongestRouteChoices)
                                + "/"
                                + Count(progress
                                    .minimumStrongestRouteChoices);
            }
            else if (progress.preferredLaneConfidence
                     < EchoMemoryContract.PreciseDescriptionConfidence)
            {
                routeEvidence = "同路"
                                + Mathf.RoundToInt(Mathf.Clamp01(
                                    progress.preferredLaneConfidence) * 100f)
                                + "%/"
                                + Mathf.RoundToInt(EchoMemoryContract
                                    .PreciseDescriptionConfidence * 100f)
                                + "%";
            }
            else
            {
                routeEvidence = route
                                + Count(progress.strongestRouteChoices)
                                + "次";
            }
            return "选路 " + Count(progress.formalChoices) + "/"
                   + Count(progress.minimumFormalChoices) + " · 通过"
                   + " " + Count(progress.successfulChoices) + "/"
                   + Count(progress.minimumSuccessfulChoices) + " · "
                   + routeEvidence;
        }

        string tendency = progress.preferredLaneUnique
                          && progress.preferredLane >= 0
            ? SingleContractCognitionLaneName(progress.preferredLane)
              + "倾向"
            : progress.strongestRouteChoices > 0
                ? "倾向形成中" : "路线倾向";
        return "路线选择 " + Count(progress.formalChoices) + "/"
               + Count(progress.minimumFormalChoices) + " · "
               + "成功通过 " + Count(progress.successfulChoices) + "/"
               + Count(progress.minimumSuccessfulChoices) + " · "
               + tendency + " " + Count(progress.strongestRouteChoices)
               + "/" + Count(progress.minimumStrongestRouteChoices);
    }

    private static string CompactSingleContractLaneName(int lane)
    {
        switch (Mathf.Clamp(lane, 0, 2))
        {
            case 0: return "左路";
            case 2: return "右路";
            default: return "中路";
        }
    }

    private static int Count(int value)
    {
        return Mathf.Max(0, value);
    }

    private static string SingleContractFeedbackFor(
        SingleContractInstantFeedback feedback, float leadDeltaMeters,
        bool relearned)
    {
        string message;
        switch (feedback)
        {
            case SingleContractInstantFeedback.PredictionHit:
                message = "它猜中了" + SingleContractLeadDelta(
                    leadDeltaMeters);
                break;
            case SingleContractInstantFeedback.RewriteSucceeded:
                message = "反制成功" + SingleContractLeadDelta(leadDeltaMeters);
                break;
            case SingleContractInstantFeedback.SafePass:
                message = "中立通过" + SingleContractLeadDelta(leadDeltaMeters);
                break;
            case SingleContractInstantFeedback.CounterFailed:
                message = "尝试反制 · 通过未完成";
                break;
            case SingleContractInstantFeedback.EchoRelearned:
                return "连续反制通过 · 后续预测已调整";
            case SingleContractInstantFeedback.ExecutionIncomplete:
                message = "通过未完成";
                break;
            case SingleContractInstantFeedback.ObservationInconclusive:
                message = "通过结果未确认";
                break;
            default:
                return "";
        }
        return relearned ? message + "\n后续预测已调整" : message;
    }

    private static string SingleContractLeadDelta(float meters)
    {
        if (Mathf.Abs(meters) < 0.05f) return " · 距离不变";
        return (meters > 0f ? " · 你 +" : " · 回声 +")
               + Mathf.Abs(meters).ToString("0.0") + "米";
    }

    private static string SingleContractRouteObservation(int lane)
    {
        switch (lane)
        {
            case 0: return "偏左";
            case 2: return "偏右";
            default: return "中路";
        }
    }

    private static string SingleContractCognitionLaneName(int lane)
    {
        switch (lane)
        {
            case 0: return "左侧";
            case 2: return "右侧";
            default: return "中间";
        }
    }

    private static string NormalizeSingleContractLabel(
        string value, string label, string fallback)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.StartsWith(label)) return normalized;
        return label + (string.IsNullOrEmpty(normalized)
            ? fallback
            : normalized);
    }

    private static string NormalizeSingleContractMemory(string value)
    {
        string normalized = (value ?? "").Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Contains("尚未形成稳定模式"))
            return "AI 正在观察你的跑法";
        if (normalized.StartsWith("回声记忆模糊"))
            return "AI 正在观察你的跑法";
        if (normalized.StartsWith("旧回声已保留"))
            return "旧回声还在 · AI 正在观察新跑法";
        normalized = TrimPrefix(normalized.Replace('\n', ' '), "回声记忆：");
        normalized = TrimPrefix(normalized, "它记住了：");
        return "它记住了：" + (string.IsNullOrEmpty(normalized)
            ? "AI 正在观察你的跑法" : normalized);
    }

    private static bool IsOpeningReplayAction(ShadowAction action)
    {
        return action == ShadowAction.Jump || action == ShadowAction.Slide
               || action == ShadowAction.Left || action == ShadowAction.Right;
    }

    private static string BuildOpeningReplayDetail(ShadowAction action,
        int count)
    {
        string actionText;
        switch (action)
        {
            case ShadowAction.Jump:
                actionText = "跳跃";
                break;
            case ShadowAction.Slide:
                actionText = "滑铲";
                break;
            case ShadowAction.Left:
                actionText = "左移";
                break;
            case ShadowAction.Right:
                actionText = "右移";
                break;
            default:
                actionText = "动作";
                break;
        }

        return "正在重演：回声记录的" + actionText + "×" + Mathf.Max(1, count);
    }

    private static string TrimPrefix(string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
    }
}
