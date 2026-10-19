using System;
using UnityEngine;

public enum EchoContractType
{
    None,
    BreakLaneHabit,
    ChangeVerticalHabit,
    DisruptRhythm
}

public enum EchoChallengeStepStatus
{
    None,
    PendingSpawn,
    Active
}

public enum EchoChallengeObstacleRole
{
    None,
    Predicted,
    Required
}

public enum EchoEncounterOutcome
{
    None,
    Evidence,
    PredictionHit,
    PredictionBroken,
    SafeChoice,
    Collision,
    Cancelled
}

public enum EchoCounterStrategy
{
    None,
    OppositeAction,
    FarLane,
    SafeChoice,
    RewardChoice,
    AlternateChoice
}

[Serializable]
public struct EchoPredictionSnapshot
{
    public int hypothesisVersion;
    public int predictedLane;
    public ShadowAction predictedAction;
    public float predictedProbability;
    public EchoCounterStrategy counterStrategy;
}

[Serializable]
public struct EchoEncounterInputEvidence
{
    public bool recorded;
    public ShadowAction action;
    public int lane;
    public float routeDistance;
}

[Serializable]
public struct EchoDetectionEvidence
{
    public int leftLaneChoices;
    public int centerLaneChoices;
    public int rightLaneChoices;
    public int jumpChoices;
    public int slideChoices;
    public int repeatedVerticalChoices;
    public int changedVerticalChoices;
    public ShadowAction lastVerticalAction;

    public int LaneChoiceCount => leftLaneChoices + centerLaneChoices
                                  + rightLaneChoices;
    public int VerticalChoiceCount => jumpChoices + slideChoices;
    public int ValidChoiceCount => LaneChoiceCount + VerticalChoiceCount;
    public int VerticalTransitionCount => repeatedVerticalChoices
                                          + changedVerticalChoices;
    public float LaneConfidence01 => Mathf.Clamp01(LaneChoiceCount / 2f);
    public float VerticalConfidence01 => Mathf.Clamp01(
        VerticalChoiceCount / 2f);
    public float RhythmConfidence01 => Mathf.Clamp01(
        VerticalTransitionCount / 2f);
    public float LanePreference => LaneChoiceCount > 0
        ? (rightLaneChoices - leftLaneChoices) / (float)LaneChoiceCount
        : 0f;
    public float SlideFrequency => VerticalChoiceCount > 0
        ? slideChoices / (float)VerticalChoiceCount : 0.5f;
    public float RhythmStability => VerticalTransitionCount > 0
        ? repeatedVerticalChoices / (float)VerticalTransitionCount : 0.5f;

    public void RecordLane(int lane)
    {
        if (lane <= 0) leftLaneChoices++;
        else if (lane >= 2) rightLaneChoices++;
        else centerLaneChoices++;
    }

    public void RecordVertical(ShadowAction action)
    {
        if (action != ShadowAction.Jump && action != ShadowAction.Slide)
            return;
        if (action == ShadowAction.Jump) jumpChoices++;
        else slideChoices++;

        if (lastVerticalAction == ShadowAction.Jump
            || lastVerticalAction == ShadowAction.Slide)
        {
            if (lastVerticalAction == action) repeatedVerticalChoices++;
            else changedVerticalChoices++;
        }
        lastVerticalAction = action;
    }
}

[Serializable]
public struct EchoEncounterResult
{
    public int encounterId;
    public EchoDuelPhase phase;
    public EchoEncounterOutcome outcome;
    public int selectedLane;
    public ShadowAction selectedAction;
    public float predictedProbability;
    public float surprise;
    public float novelty;
    public float executionQuality;
    public float fracturePower;
    public float lockBefore;
    public float lockAfter;
    public float playerLeadDelta;
    public float shadowLeadDelta;
    public int hypothesisVersion;

    public bool IsResolved => encounterId > 0
                              && outcome != EchoEncounterOutcome.None;
}

[Serializable]
public struct EchoChallengeStep
{
    public int stepId;
    public EchoDuelPhase phase;
    public EchoContractType contractType;
    public EchoChallengeStepStatus status;
    public ShadowAction predictedAction;
    public ShadowAction requiredAction;
    public int predictedLane;
    public int challengeLane;
    public int safeLane;
    public int successes;
    public int requiredSuccesses;
    public float routeDistance;
    public EchoPredictionSnapshot prediction;

    public bool IsPending => stepId > 0
                             && status == EchoChallengeStepStatus.PendingSpawn;
    public bool IsActive => stepId > 0
                            && status == EchoChallengeStepStatus.Active;
    public int EncounterId => stepId;
}

/// <summary>
/// Owns the only live encounter and its input evidence. Inputs may be recorded
/// while the player approaches the gate, but only the evaluator can turn the
/// frozen snapshot into a result at an obstacle pass, collision, or gate.
/// </summary>
public sealed class EchoEncounterController
{
    public EchoChallengeStep Active { get; private set; }
    public EchoEncounterInputEvidence InputEvidence { get; private set; }
    public EchoEncounterResult LastResult { get; private set; }

    private int _nextEncounterId = 1;

    public EchoChallengeStep Begin(EchoChallengeStep template)
    {
        template.stepId = _nextEncounterId++;
        template.status = EchoChallengeStepStatus.PendingSpawn;
        Active = template;
        InputEvidence = default;
        return Active;
    }

    public bool Bind(int encounterId, int predictedLane, int challengeLane,
        int safeLane, float resolveDistance)
    {
        if (!Active.IsPending || Active.stepId != encounterId) return false;
        EchoChallengeStep bound = Active;
        bound.predictedLane = Mathf.Clamp(predictedLane, 0, 2);
        bound.challengeLane = Mathf.Clamp(challengeLane, 0, 2);
        bound.safeLane = Mathf.Clamp(safeLane, 0, 2);
        bound.routeDistance = Mathf.Max(0f, resolveDistance);
        bound.status = EchoChallengeStepStatus.Active;
        EchoPredictionSnapshot snapshot = bound.prediction;
        snapshot.predictedLane = bound.predictedLane;
        snapshot.predictedAction = bound.predictedAction;
        bound.prediction = snapshot;
        Active = bound;
        return true;
    }

    public bool RecordInput(int encounterId, ShadowAction action, int lane,
        float routeDistance)
    {
        if (!Active.IsActive || Active.stepId != encounterId
            || (action != ShadowAction.Jump && action != ShadowAction.Slide))
            return false;
        InputEvidence = new EchoEncounterInputEvidence
        {
            recorded = true,
            action = action,
            lane = Mathf.Clamp(lane, 0, 2),
            routeDistance = Mathf.Max(0f, routeDistance)
        };
        return true;
    }

    public bool TryGetActive(int encounterId, out EchoChallengeStep encounter,
        out EchoEncounterInputEvidence evidence)
    {
        encounter = Active;
        evidence = InputEvidence;
        return Active.IsActive && Active.stepId == encounterId;
    }

    public void Resolve(EchoEncounterResult result)
    {
        if (!result.IsResolved || !Active.IsActive
            || result.encounterId != Active.stepId)
            return;
        LastResult = result;
        Active = default;
        InputEvidence = default;
    }

    public void CancelActive(EchoDuelPhase phase)
    {
        if (Active.stepId > 0)
        {
            LastResult = new EchoEncounterResult
            {
                encounterId = Active.stepId,
                phase = phase,
                outcome = EchoEncounterOutcome.Cancelled,
                selectedLane = -1,
                selectedAction = ShadowAction.Keep,
                hypothesisVersion = Active.prediction.hypothesisVersion
            };
        }
        Active = default;
        InputEvidence = default;
    }
}

[Serializable]
public struct EchoChallengeObstacleBinding
{
    public int stepId;
    public EchoChallengeObstacleRole role;
    public ShadowAction action;
    public int lane;

    public bool IsBound => stepId > 0
                           && role != EchoChallengeObstacleRole.None;
}

public sealed class EchoChallengeObstacleTag : MonoBehaviour
{
    public EchoChallengeObstacleBinding Binding { get; private set; }

    public void Configure(EchoChallengeObstacleBinding binding)
    {
        Binding = binding;
    }

    public void Clear()
    {
        Binding = default;
    }
}

[Serializable]
public sealed class EchoContractData
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public EchoContractType type;
    public int generation;
    public int learnedLane = -1;
    public int targetLane = -1;
    public ShadowAction learnedAction = ShadowAction.Keep;
    public ShadowAction startingAction = ShadowAction.Keep;
    public ShadowAction targetAction = ShadowAction.Keep;
    public float targetProgress = 1f;
    public float progress;
    public float playerProgressBonus;
    public float shadowProgressBonus;
    public EchoDuelPhase duelPhase;
    public bool initialBreakCompleted;
    public bool counterattackActive;
    public int predictionLane = -1;
    public ShadowAction predictionAction = ShadowAction.Keep;
    public bool completed;
    public bool won;
    public bool duelFailed;
    public EchoDuelPhase failurePhase;
    public bool exploratory;
    public bool completionLocked;
    public float echoLock = 100f;
    public int hypothesisVersion = 1;
    public int counterRelockCount;
    public EchoCounterStrategy counterStrategy;
    public int detectionEvidenceCount;
    public bool detectionContractLocked;
    public bool preserveRuleForRetry;
    public int revealEncounterCount;
    public int resistanceEncounterCount;
    public int counterEncounterCount;
    public bool counterattackExhausted;
    public bool rewriteReady;
    public EchoEncounterResult lastEncounterResult;
    public string encounterDebug = "";
    public int feedbackSequence;
    public string title = "";
    public string learnedTrait = "";
    public string ruleDescription = "";
    public string objective = "";
    public string lastFeedback = "";

    public float Progress01 => Mathf.Clamp01(
        progress / Mathf.Max(0.01f, targetProgress));
    public float EchoLock01 => completionLocked
        ? 0f : Mathf.Clamp01(1f - Progress01);

    public EchoContractData Clone()
    {
        return JsonUtility.FromJson<EchoContractData>(JsonUtility.ToJson(this));
    }

    public void Normalize()
    {
        int sourceVersion = version;
        version = CurrentVersion;
        if (!Enum.IsDefined(typeof(EchoContractType), type))
            type = EchoContractType.None;
        generation = Mathf.Max(0, generation);
        learnedLane = Mathf.Clamp(learnedLane, -1, 2);
        targetLane = Mathf.Clamp(targetLane, -1, 2);
        if (!Enum.IsDefined(typeof(ShadowAction), learnedAction))
            learnedAction = ShadowAction.Keep;
        if (!Enum.IsDefined(typeof(ShadowAction), targetAction))
            targetAction = ShadowAction.Keep;
        if (!Enum.IsDefined(typeof(ShadowAction), startingAction))
            startingAction = ShadowAction.Keep;
        if ((type == EchoContractType.ChangeVerticalHabit
             || type == EchoContractType.DisruptRhythm)
            && learnedAction != ShadowAction.Jump
            && learnedAction != ShadowAction.Slide
            && (targetAction == ShadowAction.Jump
                || targetAction == ShadowAction.Slide))
            learnedAction = targetAction == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
        if (startingAction == ShadowAction.Keep
            && (type == EchoContractType.ChangeVerticalHabit
                || type == EchoContractType.DisruptRhythm))
            startingAction = targetAction != ShadowAction.Keep
                ? targetAction
                : generation % 2 == 0 ? ShadowAction.Jump : ShadowAction.Slide;
        if (type == EchoContractType.DisruptRhythm
            && targetAction == ShadowAction.Keep)
            targetAction = startingAction;
        if ((type == EchoContractType.ChangeVerticalHabit
             || type == EchoContractType.DisruptRhythm)
            && learnedAction != ShadowAction.Jump
            && learnedAction != ShadowAction.Slide)
            learnedAction = targetAction == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
        if (sourceVersion < 3 && targetProgress <= 10f)
        {
            float legacyTarget = Mathf.Max(0.01f, targetProgress);
            progress = Mathf.Clamp01(progress / legacyTarget) * 100f;
            targetProgress = 100f;
            if (completed && !won)
            {
                initialBreakCompleted = true;
                counterattackActive = true;
                progress = 55f;
                completed = false;
            }
        }
        targetProgress = Mathf.Max(1f, targetProgress);
        progress = Mathf.Clamp(progress, 0f, targetProgress);
        if (sourceVersion < 5)
        {
            completionLocked = completed
                               && (duelPhase == EchoDuelPhase.Rewrite
                                   || duelPhase == EchoDuelPhase.Finale
                                   || duelPhase == EchoDuelPhase.Finished
                                   || won);
            echoLock = completed
                ? 0f : 100f * (1f - Progress01);
            hypothesisVersion = Mathf.Max(1, hypothesisVersion);
        }
        completionLocked = completionLocked || won;
        if (completionLocked || won) completed = true;
        echoLock = completionLocked
            ? 0f : 100f * (1f - Progress01);
        hypothesisVersion = Mathf.Max(1, hypothesisVersion);
        counterRelockCount = Mathf.Clamp(counterRelockCount, 0, 1);
        if (!Enum.IsDefined(typeof(EchoCounterStrategy), counterStrategy))
            counterStrategy = EchoCounterStrategy.None;
        detectionEvidenceCount = Mathf.Max(0, detectionEvidenceCount);
        if (sourceVersion < 6)
        {
            detectionContractLocked = false;
            preserveRuleForRetry = false;
        }
        revealEncounterCount = Mathf.Max(0, revealEncounterCount);
        resistanceEncounterCount = Mathf.Max(0, resistanceEncounterCount);
        counterEncounterCount = Mathf.Max(0, counterEncounterCount);
        playerProgressBonus = Mathf.Max(0f, playerProgressBonus);
        shadowProgressBonus = Mathf.Max(0f, shadowProgressBonus);
        if (!Enum.IsDefined(typeof(EchoDuelPhase), duelPhase))
            duelPhase = EchoDuelPhase.None;
        if (!Enum.IsDefined(typeof(EchoDuelPhase), failurePhase))
            failurePhase = EchoDuelPhase.None;
        if (!duelFailed) failurePhase = EchoDuelPhase.None;
        if (duelPhase == EchoDuelPhase.None && type != EchoContractType.None)
            duelPhase = EchoDuelPhase.Detection;
        predictionLane = Mathf.Clamp(predictionLane, -1, 2);
        if (!Enum.IsDefined(typeof(ShadowAction), predictionAction))
            predictionAction = ShadowAction.Keep;
        if (predictionLane < 0 && type == EchoContractType.BreakLaneHabit)
            predictionLane = learnedLane;
        if (predictionAction == ShadowAction.Keep
            && type != EchoContractType.BreakLaneHabit)
            predictionAction = learnedAction;
        feedbackSequence = Mathf.Max(0, feedbackSequence);
        title = title ?? "";
        learnedTrait = learnedTrait ?? "";
        ruleDescription = ruleDescription ?? "";
        objective = objective ?? "";
        lastFeedback = lastFeedback ?? "";
        encounterDebug = encounterDebug ?? "";
    }

    public EchoContractData ResetForRun()
    {
        EchoContractData reset = Clone();
        reset.Normalize();
        reset.progress = 0f;
        reset.playerProgressBonus = 0f;
        reset.shadowProgressBonus = 0f;
        reset.duelPhase = EchoDuelPhase.Detection;
        reset.initialBreakCompleted = false;
        reset.counterattackActive = false;
        reset.predictionLane = reset.learnedLane;
        reset.predictionAction = reset.learnedAction;
        reset.completed = false;
        reset.won = false;
        reset.duelFailed = false;
        reset.failurePhase = EchoDuelPhase.None;
        reset.completionLocked = false;
        reset.echoLock = 100f;
        reset.hypothesisVersion = 1;
        reset.counterRelockCount = 0;
        reset.counterStrategy = EchoCounterStrategy.None;
        reset.detectionEvidenceCount = 0;
        reset.detectionContractLocked = false;
        reset.revealEncounterCount = 0;
        reset.resistanceEncounterCount = 0;
        reset.counterEncounterCount = 0;
        reset.counterattackExhausted = false;
        reset.rewriteReady = false;
        reset.lastEncounterResult = default;
        reset.encounterDebug = "";
        reset.feedbackSequence = 0;
        reset.lastFeedback = "";
        if (reset.startingAction != ShadowAction.Keep)
            reset.targetAction = reset.startingAction;
        return reset;
    }
}

public static class EchoContractPolicy
{
    public const float FrozenDetectionWeight = 0.7f;
    public const float CurrentDetectionWeight = 0.3f;

    public static EchoContractData CreateForRun(PlayerStyleData source,
        int generation, string retryJson)
    {
        if (!string.IsNullOrEmpty(retryJson))
        {
            try
            {
                EchoContractData retry = JsonUtility.FromJson<EchoContractData>(
                    retryJson);
                if (retry != null)
                {
                    retry.Normalize();
                    if (retry.type != EchoContractType.None && !retry.won
                        && retry.generation == Mathf.Max(1, generation))
                    {
                        EchoContractData reset = retry.ResetForRun();
                        reset.preserveRuleForRetry = true;
                        return reset;
                    }
                }
            }
            catch (Exception)
            {
                // A damaged retry snapshot must not block a playable run.
            }
        }
        return Create(source, generation);
    }

    public static EchoContractData CreateFromDetection(PlayerStyleData frozen,
        int generation, EchoDetectionEvidence evidence)
    {
        return Create(BlendDetectionStyle(frozen, evidence), generation);
    }

    public static PlayerStyleData BlendDetectionStyle(PlayerStyleData frozen,
        EchoDetectionEvidence evidence)
    {
        PlayerStyleData blended = frozen != null
            ? frozen.Clone() : new PlayerStyleData();
        blended.Normalize();
        if (evidence.ValidChoiceCount < 2) return blended;

        float laneInfluence = CurrentDetectionWeight
                              * evidence.LaneConfidence01;
        if (evidence.LaneChoiceCount > 0)
        {
            blended.lanePreference = Mathf.Lerp(blended.lanePreference,
                evidence.LanePreference, laneInfluence);
            blended.laneSamples = Mathf.Max(blended.laneSamples,
                Mathf.Min(12, evidence.LaneChoiceCount * 6));
        }

        float verticalInfluence = CurrentDetectionWeight
                                  * evidence.VerticalConfidence01;
        if (evidence.VerticalChoiceCount > 0)
        {
            blended.slideFrequency = Mathf.Lerp(blended.slideFrequency,
                evidence.SlideFrequency, verticalInfluence);
            int verticalSamples = Mathf.Min(5,
                evidence.VerticalChoiceCount * 3);
            blended.verticalActionSamples = Mathf.Max(
                blended.verticalActionSamples, verticalSamples);
            int inferredSlides = Mathf.RoundToInt(
                blended.slideFrequency * blended.verticalActionSamples);
            blended.slideActionSamples = Mathf.Clamp(inferredSlides, 0,
                blended.verticalActionSamples);
            blended.jumpActionSamples = blended.verticalActionSamples
                                         - blended.slideActionSamples;
        }

        float rhythmInfluence = CurrentDetectionWeight
                                * evidence.RhythmConfidence01;
        if (evidence.VerticalTransitionCount > 0)
        {
            blended.rhythmStability = Mathf.Lerp(blended.rhythmStability,
                evidence.RhythmStability, rhythmInfluence);
            blended.rhythmSamples = Mathf.Max(blended.rhythmSamples,
                Mathf.Min(6, evidence.VerticalTransitionCount * 3));
        }

        blended.Normalize();
        return blended;
    }

    public static EchoContractData Create(PlayerStyleData source, int generation)
    {
        PlayerStyleData style = source != null
            ? source.Clone()
            : new PlayerStyleData();
        style.Normalize();

        float laneEvidence = Mathf.Clamp01(style.laneSamples / 12f);
        float verticalEvidence = Mathf.Clamp01(style.verticalActionSamples / 5f);
        float rhythmEvidence = Mathf.Clamp01(style.rhythmSamples / 6f);
        float laneScore = Mathf.Abs(style.lanePreference) * laneEvidence;
        float verticalScore = Mathf.Abs(style.slideFrequency - 0.5f)
                              * 2f * verticalEvidence;
        float rhythmScore = Mathf.Clamp01((style.rhythmStability - 0.45f) / 0.55f)
                            * rhythmEvidence;

        // Rotate close calls so generations create different rules while still
        // prioritizing the strongest learned habit.
        float rotation = Mathf.Abs(generation) % 3 * 0.015f;
        laneScore += generation % 3 == 0 ? rotation : 0f;
        verticalScore += generation % 3 == 1 ? rotation : 0f;
        rhythmScore += generation % 3 == 2 ? rotation : 0f;

        if (laneScore >= verticalScore && laneScore >= rhythmScore
            && laneScore >= 0.12f)
            return CreateLaneContract(style, generation);
        if (verticalScore >= rhythmScore && verticalScore >= 0.12f)
            return CreateVerticalContract(style, generation);
        if (rhythmScore >= 0.12f)
            return CreateRhythmContract(style, generation);

        // Sparse early data still yields a playable contract. The choice is
        // deterministic and comes from the frozen style snapshot.
        EchoContractData exploration;
        if (generation % 3 == 1)
            exploration = CreateVerticalContract(style, generation);
        else if (generation % 3 == 2)
            exploration = CreateRhythmContract(style, generation);
        else
            exploration = CreateLaneContract(style, generation);
        exploration.exploratory = true;
        exploration.learnedTrait = "AI探测：有效习惯样本不足";
        exploration.ruleDescription = "本代用于验证你的反制选择；完成目标后才会形成明确画像。";
        return exploration;
    }

    private static EchoContractData CreateLaneContract(
        PlayerStyleData style, int generation)
    {
        int learnedLane = style.lanePreference < 0f ? 0 : 2;
        int targetLane = learnedLane == 0 ? 2 : 0;
        string learnedName = LaneName(learnedLane);
        string targetName = LaneName(targetLane);
        return new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            generation = Mathf.Max(1, generation),
            learnedLane = learnedLane,
            targetLane = targetLane,
            targetProgress = 100f,
            title = "回声契约：逆向改道",
            learnedTrait = "AI识别：你经常依赖" + learnedName,
            ruleDescription = "本代把高价值安全路线迁移到" + targetName
                              + "；停留在旧路线会让回声加速。",
            objective = "持续改变路线使契约稳定度达到 100%，应对反扑后再进入决胜。"
        };
    }

    private static EchoContractData CreateVerticalContract(
        PlayerStyleData style, int generation)
    {
        bool prefersSlide = style.slideFrequency >= 0.5f;
        ShadowAction learned = prefersSlide ? ShadowAction.Slide : ShadowAction.Jump;
        ShadowAction target = prefersSlide ? ShadowAction.Jump : ShadowAction.Slide;
        int challengeLane = Mathf.Abs(generation) % 3;
        string learnedName = ActionName(learned);
        string targetName = ActionName(target);
        return new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = Mathf.Max(1, generation),
            learnedAction = learned,
            targetLane = challengeLane,
            startingAction = target,
            targetAction = target,
            targetProgress = 100f,
            title = "回声契约：动作反转",
            learnedTrait = "AI识别：你上一代更常执行" + learnedName,
            ruleDescription = "本代提高需要" + targetName
                              + "破解的组合；重复旧动作会给回声推进优势。",
            objective = "用有效的" + targetName
                        + "机会破坏旧预测，并在反扑组合中稳定新策略。"
        };
    }

    private static EchoContractData CreateRhythmContract(
        PlayerStyleData style, int generation)
    {
        ShadowAction learned = style.slideFrequency >= 0.5f
            ? ShadowAction.Slide : ShadowAction.Jump;
        ShadowAction first = learned == ShadowAction.Jump
            ? ShadowAction.Slide : ShadowAction.Jump;
        int challengeLane = Mathf.Abs(generation) % 3;
        string rhythm = style.rhythmStability >= 0.65f
            ? "你的跳跃/滑铲节奏高度固定"
            : "你的动作序列已被AI归纳";
        return new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            generation = Mathf.Max(1, generation),
            targetLane = challengeLane,
            learnedAction = learned,
            predictionAction = learned,
            startingAction = first,
            targetAction = first,
            targetProgress = 100f,
            title = "回声契约：打乱节拍",
            learnedTrait = "AI识别：" + rhythm,
            ruleDescription = "本代交替生成高低障碍；连续复用同一种动作会让回声加速。",
            objective = "适应变化节拍，将契约稳定度推至 100%并通过反扑。"
        };
    }

    public static string LaneName(int lane)
    {
        if (lane <= 0) return "左侧路线";
        if (lane >= 2) return "右侧路线";
        return "中间路线";
    }

    public static string ActionName(ShadowAction action)
    {
        if (action == ShadowAction.Jump) return "跳跃";
        if (action == ShadowAction.Slide) return "滑铲";
        if (action == ShadowAction.Left) return "左移";
        if (action == ShadowAction.Right) return "右移";
        return "保持路线";
    }

    public static string BuildStyleSummary(PlayerStyleData source)
    {
        PlayerStyleData style = source != null
            ? source.Clone()
            : new PlayerStyleData();
        style.Normalize();
        string lane = Mathf.Abs(style.lanePreference) < 0.2f
            ? "路线较均衡"
            : style.lanePreference < 0f ? "偏爱左路" : "偏爱右路";
        string vertical = style.verticalActionSamples < 3
            ? "跳滑样本不足"
            : Mathf.Abs(style.slideFrequency - 0.5f) < 0.12f
                ? "跳滑均衡"
                : style.slideFrequency > 0.5f ? "常用滑铲" : "常用跳跃";
        string rhythm = style.rhythmStability >= 0.65f
            ? "节奏固定"
            : style.rhythmStability <= 0.35f ? "节奏多变" : "节奏中性";
        return lane + " · " + vertical + " · " + rhythm;
    }
}

public sealed class EchoContractEvaluator
{
    public EchoContractData Contract { get; }
    public bool ScoringSuspended { get; private set; }
    public EchoChallengeStep ActiveChallengeStep => _encounters.Active;
    public EchoEncounterResult LastEncounterResult => _encounters.LastResult;
    public EchoDetectionEvidence DetectionEvidence => _detectionEvidence;

    private float _learnedLaneFeedbackTimer;
    private float _lastLaneMarkerDistance = float.NegativeInfinity;
    private float _lastFinaleMarkerDistance = float.NegativeInfinity;
    private readonly EchoEncounterController _encounters =
        new EchoEncounterController();
    private EchoDetectionEvidence _detectionEvidence;
    private int _counterattackSuccesses;
    private int _counterattackRequiredSuccesses;
    private int _counterRepeatedChoiceCount;
    private int _lastCounterLane = -1;
    private ShadowAction _lastCounterAction = ShadowAction.Keep;
    private const float LaneMarkerSpacingSeconds = 4.5f;
    private const float CorrectLeadSeconds = 0.35f;
    private const float MistakeLeadSeconds = 0.2f;

    public EchoContractEvaluator(EchoContractData contract)
    {
        Contract = contract != null ? contract.Clone() : new EchoContractData();
        Contract.Normalize();
    }

    public bool LockDetectionContract(PlayerStyleData frozenStyle,
        int generation)
    {
        if (Contract.detectionContractLocked) return false;
        Contract.detectionContractLocked = true;
        if (Contract.preserveRuleForRetry) return true;

        EchoContractData selected = EchoContractPolicy.CreateFromDetection(
            frozenStyle, generation, _detectionEvidence);
        selected.Normalize();
        Contract.type = selected.type;
        Contract.generation = selected.generation;
        Contract.learnedLane = selected.learnedLane;
        Contract.targetLane = selected.targetLane;
        Contract.learnedAction = selected.learnedAction;
        Contract.startingAction = selected.startingAction;
        Contract.targetAction = selected.targetAction;
        Contract.predictionLane = selected.predictionLane;
        Contract.predictionAction = selected.predictionAction;
        Contract.targetProgress = selected.targetProgress;
        Contract.exploratory = selected.exploratory;
        Contract.title = selected.title;
        Contract.learnedTrait = selected.learnedTrait;
        Contract.ruleDescription = selected.ruleDescription;
        Contract.objective = selected.objective;
        Contract.Normalize();
        return true;
    }

    public void SetPhase(EchoDuelPhase phase)
    {
        if (Contract.type == EchoContractType.None || phase == EchoDuelPhase.None)
            return;
        if (Contract.duelPhase == phase) return;

        EchoDuelPhase previous = Contract.duelPhase;
        _encounters.CancelActive(previous);
        Contract.duelPhase = phase;
        switch (phase)
        {
            case EchoDuelPhase.Detection:
                SetFeedback("回声正在复现你的旧习惯");
                break;
            case EchoDuelPhase.Reveal:
                SetFeedback(BuildPredictionText(false));
                break;
            case EchoDuelPhase.Resistance:
                Contract.predictionLane = Contract.learnedLane;
                Contract.predictionAction = Contract.learnedAction;
                SetFeedback("开始反抗：让回声的预测失效");
                break;
            case EchoDuelPhase.Counterattack:
                BeginCounterattack();
                break;
            case EchoDuelPhase.Rewrite:
                Contract.completed = true;
                Contract.completionLocked = true;
                Contract.echoLock = 0f;
                SetFeedback("契约已重写：本阶段行为将更强地塑造下一代回声");
                break;
            case EchoDuelPhase.Finale:
                if (!Contract.completed && !Contract.duelFailed)
                {
                    Contract.duelFailed = true;
                    Contract.failurePhase = previous == EchoDuelPhase.Resistance
                        ? EchoDuelPhase.Resistance
                        : EchoDuelPhase.Counterattack;
                }
                SetFeedback(Contract.duelFailed
                    ? "契约锁定：反抗未完成，进入回声决胜追逐"
                    : Contract.completed
                        ? "进入决胜：守住领先并完成终局组合"
                        : "最终契约机会：在冲线前稳定新策略");
                break;
        }
    }

    public void SetScoringSuspended(bool suspended)
    {
        ScoringSuspended = suspended;
    }

    public void LockForFinale(EchoDuelPhase failurePhase)
    {
        if (Contract.type == EchoContractType.None) return;
        Contract.duelFailed = true;
        Contract.failurePhase = failurePhase;
        Contract.completed = false;
        Contract.completionLocked = false;
        Contract.counterattackActive = false;
        _encounters.CancelActive(failurePhase);
        ScoringSuspended = false;
        SetFeedback(failurePhase == EchoDuelPhase.Counterattack
            ? "反扑失败：契约锁定，进入决胜追逐"
            : "反抗失败：契约锁定，进入决胜追逐");
    }

    public string BuildPredictionText(bool counterattack = true)
    {
        string prefix = counterattack && Contract.initialBreakCompleted
            ? "新预判：" : "回声预判：";
        if (Contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = Contract.predictionLane >= 0
                ? Contract.predictionLane : Contract.learnedLane;
            return prefix + "你会继续依赖" + EchoContractPolicy.LaneName(lane);
        }

        ShadowAction action = Contract.predictionAction != ShadowAction.Keep
            ? Contract.predictionAction : Contract.learnedAction;
        return prefix + "你会继续使用" + EchoContractPolicy.ActionName(action);
    }

    public void TickLane(int lane, float deltaTime, float currentSpeed = 10f)
    {
        if (Contract.type != EchoContractType.BreakLaneHabit
            || !CanChangeStability() || deltaTime <= 0f)
            return;
        if (Contract.initialBreakCompleted)
        {
            _learnedLaneFeedbackTimer = 0f;
            return;
        }

        int predictedLane = Contract.initialBreakCompleted
            ? Contract.predictionLane : Contract.learnedLane;
        if (lane == predictedLane)
        {
            ReduceStability(5f * deltaTime);
            Contract.shadowProgressBonus += EchoTimeRules.SecondsToDistance(
                0.1f * deltaTime, currentSpeed);
            _learnedLaneFeedbackTimer += deltaTime;
            if (_learnedLaneFeedbackTimer >= 1.5f)
            {
                _learnedLaneFeedbackTimer = 0f;
                SetFeedback(Contract.initialBreakCompleted
                    ? "回声施压：新路线正在变成另一个固定习惯"
                    : "回声施压：旧路线正在修复契约");
            }
        }
        else _learnedLaneFeedbackTimer = 0f;
    }

    public void RecordLaneMarker(int lane, float routeDistance,
        float currentSpeed = 10f, int challengeStepId = 0)
    {
        bool detectionEvidence = Contract.duelPhase
                                 == EchoDuelPhase.Detection;
        if ((!detectionEvidence
             && Contract.type != EchoContractType.BreakLaneHabit)
            || Contract.completed || Contract.duelFailed || ScoringSuspended)
            return;
        bool revealChoice = Contract.duelPhase == EchoDuelPhase.Reveal;
        if (!detectionEvidence && !revealChoice && !CanChangeStability())
            return;

        int predictedLane = Contract.predictionLane >= 0
            ? Contract.predictionLane : Contract.learnedLane;
        predictedLane = Mathf.Clamp(predictedLane, 0, 2);
        bool counterattack = Contract.initialBreakCompleted
                             && Contract.duelPhase
                             == EchoDuelPhase.Counterattack;
        if (counterattack)
        {
            RecordCounterattackLaneChoice(challengeStepId, lane,
                routeDistance, currentSpeed);
            return;
        }
        float spacing = EchoTimeRules.MinimumSpacingDistance(
            LaneMarkerSpacingSeconds, currentSpeed);
        if (!float.IsNegativeInfinity(_lastLaneMarkerDistance)
            && routeDistance - _lastLaneMarkerDistance < spacing)
            return;

        _lastLaneMarkerDistance = routeDistance;
        if (detectionEvidence)
        {
            _detectionEvidence.RecordLane(lane);
            EchoChallengeStep encounter = BeginImmediateEncounter(
                EchoDuelPhase.Detection, predictedLane, ShadowAction.Keep,
                lane, routeDistance);
            Contract.detectionEvidenceCount++;
            SetFeedback("侦测样本 "
                        + Mathf.Min(2, Contract.detectionEvidenceCount)
                        + "/2");
            FinishImmediateEncounter(encounter, EchoEncounterOutcome.Evidence,
                lane, ShadowAction.Keep, Contract.echoLock,
                Contract.playerProgressBonus, Contract.shadowProgressBonus);
            return;
        }

        if (revealChoice)
        {
            EchoChallengeStep encounter = BeginImmediateEncounter(
                EchoDuelPhase.Reveal, predictedLane, ShadowAction.Keep,
                lane, routeDistance);
            float lockBefore = Contract.echoLock;
            float playerBefore = Contract.playerProgressBonus;
            float shadowBefore = Contract.shadowProgressBonus;
            Contract.revealEncounterCount++;
            if (lane == predictedLane)
            {
                AddShadowLead(0.35f, currentSpeed);
                SetFeedback("公开预判命中：回声获得推进");
            }
            else
            {
                AddPlayerLead(0.2f, currentSpeed);
                SetFeedback("公开预判失效：你获得反超窗口");
            }
            FinishImmediateEncounter(encounter,
                lane == predictedLane
                    ? EchoEncounterOutcome.PredictionHit
                    : EchoEncounterOutcome.PredictionBroken,
                lane, ShadowAction.Keep, lockBefore, playerBefore,
                shadowBefore);
            return;
        }

        EchoChallengeStep resistanceEncounter = BeginImmediateEncounter(
            EchoDuelPhase.Resistance, predictedLane, ShadowAction.Keep,
            lane, routeDistance);
        float resistanceLockBefore = Contract.echoLock;
        float resistancePlayerBefore = Contract.playerProgressBonus;
        float resistanceShadowBefore = Contract.shadowProgressBonus;
        bool validLane = lane != predictedLane;
        Contract.resistanceEncounterCount++;
        if (!validLane)
        {
            ReduceStability(14f);
            AddShadowLead(MistakeLeadSeconds, currentSpeed);
            SetFeedback(counterattack
                ? "回声施压：你重复了刚被识别的反制路线"
                : "回声施压：你重复了被公开的预测路线");
            FinishImmediateEncounter(resistanceEncounter,
                EchoEncounterOutcome.PredictionHit, lane, ShadowAction.Keep,
                resistanceLockBefore, resistancePlayerBefore,
                resistanceShadowBefore);
            return;
        }

        AddStability(34f, CorrectLeadSeconds, currentSpeed);
        SetFeedback("预测失效：你主动选择了非习惯路线");
        CompleteIfReady();
        FinishImmediateEncounter(resistanceEncounter,
            EchoEncounterOutcome.PredictionBroken, lane, ShadowAction.Keep,
            resistanceLockBefore, resistancePlayerBefore,
            resistanceShadowBefore);
    }

    public bool BindChallengeStep(int stepId, int predictedLane,
        int challengeLane, int safeLane, float routeDistance)
    {
        return _encounters.Bind(stepId, predictedLane, challengeLane,
            safeLane, routeDistance);
    }

    public bool RecordEncounterInput(int encounterId, ShadowAction action,
        int lane, float routeDistance)
    {
        return _encounters.RecordInput(encounterId, action, lane,
            routeDistance);
    }

    public bool ResolveChallengeAtGate(int encounterId, int playerLane,
        float currentSpeed = 10f)
    {
        if (!_encounters.TryGetActive(encounterId,
                out EchoChallengeStep encounter,
                out EchoEncounterInputEvidence evidence))
            return false;

        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (Contract.type == EchoContractType.BreakLaneHabit)
            return ResolveCounterattackChoice(encounter, lane,
                ShadowAction.Keep, currentSpeed, 1f);

        if (lane == encounter.safeLane)
            return ResolveCounterattackChoice(encounter, lane,
                ShadowAction.Keep, currentSpeed, 1f);

        if (evidence.recorded)
            return ResolveCounterattackChoice(encounter, lane,
                evidence.action, currentSpeed, 1f);

        return CancelEncounter(encounter, "交锋取消 · 未形成有效选择");
    }

    public void RecordChallengeMissed(int stepId)
    {
        if (!_encounters.TryGetActive(stepId, out EchoChallengeStep encounter,
                out _))
            return;

        CancelEncounter(encounter, "交锋取消 · 锁定不变");
        BeginNextChallengeStep();
    }

    private void RecordCounterattackLaneChoice(int challengeStepId, int lane,
        float routeDistance, float currentSpeed)
    {
        if (!_encounters.TryGetActive(challengeStepId,
                out EchoChallengeStep encounter, out _))
            return;
        _lastLaneMarkerDistance = routeDistance;
        ResolveCounterattackChoice(encounter, Mathf.Clamp(lane, 0, 2),
            ShadowAction.Keep, currentSpeed, 1f);
    }

    public void RecordFinaleRouteChoice(int lane, int predictedLane,
        int safeLane, int riskLane, float routeDistance,
        float currentSpeed = 10f)
    {
        if (Contract.type == EchoContractType.None
            || Contract.duelPhase != EchoDuelPhase.Finale
            || !Contract.completed || Contract.duelFailed
            || ScoringSuspended)
            return;

        int selected = Mathf.Clamp(lane, 0, 2);
        int predicted = Mathf.Clamp(predictedLane, 0, 2);
        int safe = Mathf.Clamp(safeLane, 0, 2);
        int risk = Mathf.Clamp(riskLane, 0, 2);
        if (selected != predicted && selected != safe && selected != risk)
            return;

        float spacing = EchoTimeRules.MinimumSpacingDistance(
            LaneMarkerSpacingSeconds, currentSpeed);
        if (!float.IsNegativeInfinity(_lastFinaleMarkerDistance)
            && routeDistance - _lastFinaleMarkerDistance < spacing)
            return;
        _lastFinaleMarkerDistance = routeDistance;

        if (selected == predicted)
        {
            AddShadowLead(0.3f, currentSpeed);
            SetFeedback("旧预测命中：高价值路线让回声获得推进");
        }
        else if (selected == risk)
        {
            AddPlayerLead(0.4f, currentSpeed);
            SetFeedback("激进决胜：你用高风险路线抢回距离");
        }
        else
        {
            SetFeedback("安全决胜：你放弃追赶收益并守住当前距离");
        }
    }

    public void RecordDodge(ObstacleType obstacleType, int playerLane = -1,
        float currentSpeed = 10f)
    {
        RecordDodge(obstacleType, playerLane, currentSpeed, default);
    }

    public void RecordDodge(ObstacleType obstacleType, int playerLane,
        float currentSpeed, EchoChallengeObstacleBinding binding)
    {
        ShadowAction action = obstacleType == ObstacleType.High
            ? ShadowAction.Jump
            : obstacleType == ObstacleType.Low
                ? ShadowAction.Slide
                : ShadowAction.Keep;
        if (action == ShadowAction.Keep) return;

        if (Contract.duelPhase == EchoDuelPhase.Detection
            && !Contract.completed && !Contract.duelFailed
            && !ScoringSuspended)
        {
            _detectionEvidence.RecordVertical(action);
            ShadowAction prediction = ResolvePredictedVerticalAction();
            EchoChallengeStep encounter = BeginImmediateEncounter(
                EchoDuelPhase.Detection, playerLane, prediction, playerLane,
                Contract.detectionEvidenceCount + 1f);
            Contract.detectionEvidenceCount++;
            SetFeedback("侦测样本 "
                        + Mathf.Min(2, Contract.detectionEvidenceCount)
                        + "/2");
            FinishImmediateEncounter(encounter, EchoEncounterOutcome.Evidence,
                playerLane, action, Contract.echoLock,
                Contract.playerProgressBonus, Contract.shadowProgressBonus);
            return;
        }

        if (Contract.duelPhase == EchoDuelPhase.Reveal
            && !Contract.completed && !Contract.duelFailed
            && !ScoringSuspended
            && (Contract.type == EchoContractType.ChangeVerticalHabit
                || Contract.type == EchoContractType.DisruptRhythm))
        {
            ShadowAction predicted = ResolvePredictedVerticalAction();
            EchoChallengeStep encounter = BeginImmediateEncounter(
                EchoDuelPhase.Reveal, playerLane, predicted, playerLane,
                Contract.revealEncounterCount + 1f);
            float lockBefore = Contract.echoLock;
            float playerBefore = Contract.playerProgressBonus;
            float shadowBefore = Contract.shadowProgressBonus;
            Contract.revealEncounterCount++;
            if (action == predicted)
            {
                AddShadowLead(0.35f, currentSpeed);
                SetFeedback("公开预判命中：回声复制了你的旧动作");
            }
            else
            {
                AddPlayerLead(0.2f, currentSpeed);
                SetFeedback("公开预判失效：你的动作选择骗过了回声");
            }
            FinishImmediateEncounter(encounter,
                action == predicted
                    ? EchoEncounterOutcome.PredictionHit
                    : EchoEncounterOutcome.PredictionBroken,
                playerLane, action, lockBefore, playerBefore, shadowBefore);
            return;
        }

        if (!CanChangeStability()) return;

        if (Contract.initialBreakCompleted
            && Contract.duelPhase == EchoDuelPhase.Counterattack)
        {
            RecordCounterattackDodge(action, playerLane, currentSpeed, binding);
            return;
        }

        if (Contract.type == EchoContractType.ChangeVerticalHabit)
        {
            if (!Contract.initialBreakCompleted && Contract.targetLane >= 0
                && playerLane != Contract.targetLane)
                return;
            Contract.resistanceEncounterCount++;
            EchoChallengeStep encounter = BeginImmediateEncounter(
                EchoDuelPhase.Resistance, playerLane,
                ResolvePredictedVerticalAction(), playerLane,
                Contract.resistanceEncounterCount + 1f);
            float lockBefore = Contract.echoLock;
            float playerBefore = Contract.playerProgressBonus;
            float shadowBefore = Contract.shadowProgressBonus;
            if (action == Contract.targetAction)
            {
                AddStability(Contract.initialBreakCompleted ? 24f : 34f,
                    CorrectLeadSeconds, currentSpeed);
                SetFeedback("预测失效：有效动作骗过了回声");
                if (Contract.initialBreakCompleted)
                {
                    Contract.predictionAction = action;
                    Contract.targetAction = OppositeVertical(action);
                }
                CompleteIfReady();
                FinishImmediateEncounter(encounter,
                    EchoEncounterOutcome.PredictionBroken, playerLane, action,
                    lockBefore, playerBefore, shadowBefore);
            }
            else if (action == Contract.predictionAction
                     || action == Contract.learnedAction)
            {
                ReduceStability(14f);
                AddShadowLead(MistakeLeadSeconds, currentSpeed);
                SetFeedback("回声施压：你重复了被公开的预测");
                FinishImmediateEncounter(encounter,
                    EchoEncounterOutcome.PredictionHit, playerLane, action,
                    lockBefore, playerBefore, shadowBefore);
            }
            else
            {
                CancelEncounter(encounter, "交锋取消 · 无有效选择");
            }
            return;
        }

        if (Contract.type != EchoContractType.DisruptRhythm) return;
        if (!Contract.initialBreakCompleted && Contract.targetLane >= 0
            && playerLane != Contract.targetLane)
            return;
        Contract.resistanceEncounterCount++;
        EchoChallengeStep rhythmEncounter = BeginImmediateEncounter(
            EchoDuelPhase.Resistance, playerLane,
            ResolvePredictedVerticalAction(), playerLane,
            Contract.resistanceEncounterCount + 1f);
        float rhythmLockBefore = Contract.echoLock;
        float rhythmPlayerBefore = Contract.playerProgressBonus;
        float rhythmShadowBefore = Contract.shadowProgressBonus;
        if (action == Contract.targetAction)
        {
            AddStability(Contract.initialBreakCompleted ? 22f : 27f,
                CorrectLeadSeconds, currentSpeed);
            SetFeedback("预测失效：动作节拍已改变");
            Contract.targetAction = action == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
            CompleteIfReady();
            FinishImmediateEncounter(rhythmEncounter,
                EchoEncounterOutcome.PredictionBroken, playerLane, action,
                rhythmLockBefore, rhythmPlayerBefore, rhythmShadowBefore);
        }
        else
        {
            ReduceStability(14f);
            AddShadowLead(MistakeLeadSeconds, currentSpeed);
            SetFeedback("回声施压：固定节拍正在修复契约");
            FinishImmediateEncounter(rhythmEncounter,
                EchoEncounterOutcome.PredictionHit, playerLane, action,
                rhythmLockBefore, rhythmPlayerBefore, rhythmShadowBefore);
        }
    }

    public bool RecordCounterattackActionResponse(ShadowAction action,
        float currentSpeed = 10f)
    {
        // Kept as a compatibility boundary. Raw input is evidence only and
        // must never directly settle an encounter.
        return false;
    }

    private void RecordCounterattackDodge(ShadowAction action, int playerLane,
        float currentSpeed, EchoChallengeObstacleBinding binding)
    {
        if ((Contract.type != EchoContractType.ChangeVerticalHabit
             && Contract.type != EchoContractType.DisruptRhythm)
            || !binding.IsBound
            || !_encounters.TryGetActive(binding.stepId,
                out EchoChallengeStep encounter, out _))
            return;

        bool counterChoice = binding.role
                             == EchoChallengeObstacleRole.Required
                             && binding.lane == encounter.challengeLane
                             && binding.action == encounter.requiredAction
                             && action == binding.action;
        bool predictedChoice = binding.role
                               == EchoChallengeObstacleRole.Predicted
                               && binding.lane == encounter.predictedLane
                               && binding.action == encounter.predictedAction
                               && action == binding.action;
        if (counterChoice || predictedChoice)
            ResolveCounterattackChoice(encounter, playerLane, action,
                currentSpeed, 1f);
    }

    private bool ResolveCounterattackChoice(EchoChallengeStep encounter,
        int playerLane, ShadowAction action, float currentSpeed,
        float executionQuality)
    {
        if (!_encounters.TryGetActive(encounter.stepId, out _, out _))
            return false;

        bool laneContract = Contract.type == EchoContractType.BreakLaneHabit;
        int selectedLane = Mathf.Clamp(playerLane, 0, 2);
        bool predictionHit = laneContract
            ? selectedLane == encounter.predictedLane
            : action == encounter.predictedAction;
        bool safeChoice = !predictionHit
                          && selectedLane == encounter.safeLane
                          && (laneContract || action == ShadowAction.Keep);
        bool counterChoice = !predictionHit && (laneContract
            || action == encounter.requiredAction || safeChoice);
        if (!predictionHit && !counterChoice) return false;

        float lockBefore = Contract.echoLock;
        float probability = predictionHit ? 0.68f : safeChoice ? 0.10f : 0.22f;
        float surprise = predictionHit ? 0f : 1f - probability;
        float novelty = predictionHit ? 0f
            : ResolveCounterNovelty(selectedLane, action, laneContract);
        float quality = Mathf.Clamp01(executionQuality);
        float fracture = predictionHit ? 0f
            : Mathf.Clamp(65f * surprise * novelty * quality, 12f, 58f);
        float playerBefore = Contract.playerProgressBonus;
        float shadowBefore = Contract.shadowProgressBonus;

        Contract.counterEncounterCount++;
        if (predictionHit)
        {
            AddShadowLead(MistakeLeadSeconds, currentSpeed);
            SetFeedback("预判命中 · 回声追近");
        }
        else
        {
            AddStability(fracture, safeChoice ? 0f : CorrectLeadSeconds,
                currentSpeed);
            _counterattackSuccesses++;
            SetFeedback((safeChoice ? "偏离成功" : "裂解成功")
                        + " · 锁定 -" + fracture.ToString("0") + "%");
        }

        EchoEncounterResult result = new EchoEncounterResult
        {
            encounterId = encounter.stepId,
            phase = encounter.phase,
            outcome = predictionHit
                ? EchoEncounterOutcome.PredictionHit
                : safeChoice ? EchoEncounterOutcome.SafeChoice
                    : EchoEncounterOutcome.PredictionBroken,
            selectedLane = selectedLane,
            selectedAction = action,
            predictedProbability = probability,
            surprise = surprise,
            novelty = novelty,
            executionQuality = quality,
            fracturePower = fracture,
            lockBefore = lockBefore,
            lockAfter = Contract.echoLock,
            playerLeadDelta = Contract.playerProgressBonus - playerBefore,
            shadowLeadDelta = Contract.shadowProgressBonus - shadowBefore,
            hypothesisVersion = encounter.prediction.hypothesisVersion
        };
        FinishEncounter(result);
        CompleteIfReady();
        if (!Contract.completed)
        {
            TryRelockCounterHypothesis(result);
            BeginNextChallengeStep();
        }
        return true;
    }

    public void RecordHit(float currentSpeed = 10f,
        EchoChallengeObstacleBinding binding = default)
    {
        if (Contract.type == EchoContractType.None || ScoringSuspended
            || Contract.duelFailed)
            return;

        if (binding.IsBound
            && _encounters.TryGetActive(binding.stepId,
                out EchoChallengeStep encounter, out _))
        {
            float lockBefore = Contract.echoLock;
            float shadowBefore = Contract.shadowProgressBonus;
            AddShadowLead(0.4f, currentSpeed);
            EchoEncounterResult result = new EchoEncounterResult
            {
                encounterId = encounter.stepId,
                phase = encounter.phase,
                outcome = EchoEncounterOutcome.Collision,
                selectedLane = binding.lane,
                selectedAction = binding.action,
                executionQuality = 0f,
                lockBefore = lockBefore,
                lockAfter = Contract.echoLock,
                shadowLeadDelta = Contract.shadowProgressBonus - shadowBefore,
                hypothesisVersion = encounter.prediction.hypothesisVersion
            };
            FinishEncounter(result);
            Contract.counterEncounterCount++;
            SetFeedback("命中");
            BeginNextChallengeStep();
            return;
        }

        if (Contract.completionLocked
            || Contract.duelPhase == EchoDuelPhase.Rewrite
            || Contract.duelPhase == EchoDuelPhase.Finale)
        {
            AddShadowLead(0.4f, currentSpeed);
            SetFeedback("碰撞 · 回声追近");
            return;
        }
        ReduceStability(18f);
        AddShadowLead(0.4f, currentSpeed);
        SetFeedback("命中");
    }

    public string BuildHudText()
    {
        if (Contract.type == EchoContractType.None) return "";
        if (Contract.duelPhase == EchoDuelPhase.Detection)
        {
            return Contract.detectionContractLocked
                ? "回声侦测 · 画像已锁定"
                : "回声侦测 · 有效样本 "
                  + Mathf.Min(2, Contract.detectionEvidenceCount) + "/2";
        }
        string state = Contract.completed
            ? "锁定碎裂"
            : Contract.initialBreakCompleted
                ? "回声追学"
                : Contract.EchoLock01 >= 0.98f
                    ? "完整锁定" : "锁定开裂";
        string feedback = string.IsNullOrEmpty(Contract.lastFeedback)
            ? ""
            : " · " + Contract.lastFeedback;
        return Contract.title + " · " + state + feedback;
    }

    private void CompleteIfReady()
    {
        if (Contract.progress < Contract.targetProgress) return;
        if (!Contract.initialBreakCompleted)
        {
            Contract.initialBreakCompleted = true;
            Contract.counterattackActive = true;
            Contract.progress = Contract.targetProgress;
            Contract.echoLock = 0f;
            Contract.predictionLane = Contract.targetLane;
            Contract.predictionAction = Contract.targetAction;
            SetFeedback("裂解");
            return;
        }

        Contract.completed = true;
        Contract.completionLocked = true;
        Contract.echoLock = 0f;
        Contract.counterattackActive = false;
        _encounters.CancelActive(EchoDuelPhase.Counterattack);
        SetFeedback("锁定碎裂");
    }

    private void BeginCounterattack()
    {
        Contract.counterattackActive = true;
        Contract.progress = 0f;
        Contract.echoLock = 100f;
        Contract.hypothesisVersion++;
        Contract.counterRelockCount = 0;
        Contract.counterEncounterCount = 0;
        Contract.counterattackExhausted = false;
        if (Contract.type == EchoContractType.BreakLaneHabit)
        {
            Contract.predictionLane = Contract.targetLane;
            Contract.counterStrategy = EchoCounterStrategy.FarLane;
        }
        else
        {
            Contract.predictionAction = Contract.targetAction;
            Contract.targetAction = OppositeVertical(Contract.targetAction);
            Contract.counterStrategy = Contract.type
                == EchoContractType.DisruptRhythm
                ? EchoCounterStrategy.AlternateChoice
                : EchoCounterStrategy.OppositeAction;
        }
        _counterattackSuccesses = 0;
        _counterattackRequiredSuccesses = 4;
        _counterRepeatedChoiceCount = 0;
        _lastCounterLane = -1;
        _lastCounterAction = ShadowAction.Keep;
        BeginNextChallengeStep();
        SetFeedback("回声锁定新预判");
    }

    private void BeginNextChallengeStep()
    {
        if (Contract.completed || Contract.duelFailed
            || Contract.duelPhase != EchoDuelPhase.Counterattack)
        {
            _encounters.CancelActive(Contract.duelPhase);
            return;
        }

        if (Contract.counterEncounterCount >= 4)
        {
            Contract.counterattackExhausted = true;
            _encounters.CancelActive(Contract.duelPhase);
            SetFeedback("锁定未破");
            return;
        }

        EchoChallengeStep template = new EchoChallengeStep
        {
            phase = EchoDuelPhase.Counterattack,
            contractType = Contract.type,
            predictedAction = Contract.predictionAction,
            requiredAction = Contract.targetAction,
            predictedLane = Contract.predictionLane >= 0
                ? Mathf.Clamp(Contract.predictionLane, 0, 2) : -1,
            challengeLane = -1,
            safeLane = -1,
            successes = _counterattackSuccesses,
            requiredSuccesses = _counterattackRequiredSuccesses,
            routeDistance = 0f,
            prediction = new EchoPredictionSnapshot
            {
                hypothesisVersion = Contract.hypothesisVersion,
                predictedLane = Contract.predictionLane,
                predictedAction = Contract.predictionAction,
                predictedProbability = 0.68f,
                counterStrategy = Contract.counterStrategy
            }
        };
        _encounters.Begin(template);
    }

    private string BuildChallengeSuccessFeedback(string action)
    {
        return action + " · 反制 "
               + Mathf.Min(_counterattackSuccesses,
                   _counterattackRequiredSuccesses)
               + "/" + _counterattackRequiredSuccesses;
    }

    private float ResolveCounterNovelty(int lane, ShadowAction action,
        bool laneContract)
    {
        bool repeated;
        if (laneContract)
        {
            repeated = _lastCounterLane == lane;
            _lastCounterLane = lane;
        }
        else
        {
            repeated = _lastCounterAction == action;
            _lastCounterAction = action;
        }

        _counterRepeatedChoiceCount = repeated
            ? _counterRepeatedChoiceCount + 1 : 1;
        return _counterRepeatedChoiceCount <= 1
            ? 1f
            : Mathf.Max(0.35f,
                1f - 0.35f * (_counterRepeatedChoiceCount - 1));
    }

    private void TryRelockCounterHypothesis(EchoEncounterResult result)
    {
        if (Contract.counterRelockCount > 0
            || Contract.counterEncounterCount < 2
            || _counterRepeatedChoiceCount < 2
            || result.outcome == EchoEncounterOutcome.PredictionHit
            || result.outcome == EchoEncounterOutcome.Collision)
            return;

        Contract.counterRelockCount = 1;
        Contract.hypothesisVersion++;
        if (Contract.type == EchoContractType.BreakLaneHabit)
        {
            Contract.predictionLane = Mathf.Clamp(result.selectedLane, 0, 2);
            Contract.counterStrategy = EchoCounterStrategy.FarLane;
        }
        else if (result.selectedAction == ShadowAction.Jump
                 || result.selectedAction == ShadowAction.Slide)
        {
            Contract.predictionAction = result.selectedAction;
            Contract.targetAction = OppositeVertical(result.selectedAction);
            Contract.counterStrategy = EchoCounterStrategy.OppositeAction;
        }
        else
        {
            Contract.predictionLane = Mathf.Clamp(result.selectedLane, 0, 2);
            Contract.counterStrategy = EchoCounterStrategy.SafeChoice;
        }

        Contract.progress = Mathf.Max(0f, Contract.progress - 20f);
        SyncLockFromProgress();
        SetFeedback("反制生效 · 回声改判"
                    + (Contract.type == EchoContractType.BreakLaneHabit
                        ? EchoContractPolicy.LaneName(Contract.predictionLane)
                        : EchoContractPolicy.ActionName(
                            Contract.predictionAction)));
    }

    private EchoChallengeStep BeginImmediateEncounter(EchoDuelPhase phase,
        int predictedLane, ShadowAction predictedAction, int selectedLane,
        float routeDistance)
    {
        if (_encounters.Active.stepId > 0)
            _encounters.CancelActive(phase);

        int lane = Mathf.Clamp(selectedLane, 0, 2);
        int frozenPredictionLane = predictedLane >= 0
            ? Mathf.Clamp(predictedLane, 0, 2) : lane;
        EchoChallengeStep started = _encounters.Begin(new EchoChallengeStep
        {
            phase = phase,
            contractType = Contract.type,
            predictedAction = predictedAction,
            requiredAction = Contract.targetAction,
            predictedLane = frozenPredictionLane,
            challengeLane = lane,
            safeLane = lane,
            routeDistance = Mathf.Max(0f, routeDistance),
            prediction = new EchoPredictionSnapshot
            {
                hypothesisVersion = Contract.hypothesisVersion,
                predictedLane = frozenPredictionLane,
                predictedAction = predictedAction,
                predictedProbability = 0.68f,
                counterStrategy = Contract.counterStrategy
            }
        });
        _encounters.Bind(started.stepId, frozenPredictionLane, lane, lane,
            routeDistance);
        return _encounters.Active;
    }

    private void FinishImmediateEncounter(EchoChallengeStep encounter,
        EchoEncounterOutcome outcome, int selectedLane,
        ShadowAction selectedAction, float lockBefore, float playerBefore,
        float shadowBefore)
    {
        float probability = outcome == EchoEncounterOutcome.PredictionHit
            ? 0.68f : outcome == EchoEncounterOutcome.PredictionBroken
                ? 0.22f : 0.5f;
        EchoEncounterResult result = new EchoEncounterResult
        {
            encounterId = encounter.stepId,
            phase = encounter.phase,
            outcome = outcome,
            selectedLane = Mathf.Clamp(selectedLane, 0, 2),
            selectedAction = selectedAction,
            predictedProbability = probability,
            surprise = outcome == EchoEncounterOutcome.PredictionBroken
                ? 1f - probability : 0f,
            novelty = outcome == EchoEncounterOutcome.PredictionBroken
                ? 1f : 0f,
            executionQuality = outcome == EchoEncounterOutcome.Evidence
                ? 0.5f : 1f,
            fracturePower = Mathf.Max(0f, lockBefore - Contract.echoLock),
            lockBefore = lockBefore,
            lockAfter = Contract.echoLock,
            playerLeadDelta = Contract.playerProgressBonus - playerBefore,
            shadowLeadDelta = Contract.shadowProgressBonus - shadowBefore,
            hypothesisVersion = encounter.prediction.hypothesisVersion
        };
        FinishEncounter(result);
    }

    private void FinishEncounter(EchoEncounterResult result)
    {
        Contract.lastEncounterResult = result;
        Contract.encounterDebug = "EncounterId=" + result.encounterId
            + " Prediction=" + ActivePredictionLabel()
            + " Choice=" + EchoContractPolicy.ActionName(result.selectedAction)
            + " Lane=" + result.selectedLane
            + " Probability=" + result.predictedProbability.ToString("0.00")
            + " Surprise=" + result.surprise.ToString("0.00")
            + " Novelty=" + result.novelty.ToString("0.00")
            + " Fracture=" + result.fracturePower.ToString("0.0")
            + " Lock=" + result.lockBefore.ToString("0.0")
            + "->" + result.lockAfter.ToString("0.0")
            + " Hypothesis=" + result.hypothesisVersion;
        _encounters.Resolve(result);
    }

    private bool CancelEncounter(EchoChallengeStep encounter, string feedback)
    {
        EchoEncounterResult result = new EchoEncounterResult
        {
            encounterId = encounter.stepId,
            phase = encounter.phase,
            outcome = EchoEncounterOutcome.Cancelled,
            selectedLane = -1,
            selectedAction = ShadowAction.Keep,
            lockBefore = Contract.echoLock,
            lockAfter = Contract.echoLock,
            hypothesisVersion = encounter.prediction.hypothesisVersion
        };
        FinishEncounter(result);
        SetFeedback(feedback);
        return true;
    }

    private string ActivePredictionLabel()
    {
        EchoChallengeStep encounter = _encounters.Active;
        if (Contract.type == EchoContractType.BreakLaneHabit)
            return EchoContractPolicy.LaneName(encounter.predictedLane);
        return EchoContractPolicy.ActionName(encounter.predictedAction);
    }

    private bool CanChangeStability()
    {
        if (Contract.completed || Contract.completionLocked
            || Contract.duelFailed || ScoringSuspended)
            return false;
        return Contract.duelPhase == EchoDuelPhase.Resistance
               || Contract.duelPhase == EchoDuelPhase.Counterattack;
    }

    private void AddStability(float amount, float leadSeconds,
        float currentSpeed)
    {
        Contract.progress = Mathf.Min(Contract.targetProgress,
            Contract.progress + Mathf.Max(0f, amount));
        SyncLockFromProgress();
        Contract.playerProgressBonus += EchoTimeRules.SecondsToDistance(
            leadSeconds, currentSpeed);
    }

    private void ReduceStability(float amount)
    {
        if (Contract.completionLocked) return;
        Contract.progress = Mathf.Max(0f,
            Contract.progress - Mathf.Max(0f, amount));
        Contract.completed = false;
        SyncLockFromProgress();
    }

    private void SyncLockFromProgress()
    {
        Contract.echoLock = Contract.completionLocked
            ? 0f : 100f * (1f - Contract.Progress01);
    }

    private void AddShadowLead(float seconds, float currentSpeed)
    {
        Contract.shadowProgressBonus += EchoTimeRules.SecondsToDistance(
            seconds, currentSpeed);
    }

    private void AddPlayerLead(float seconds, float currentSpeed)
    {
        Contract.playerProgressBonus += EchoTimeRules.SecondsToDistance(
            seconds, currentSpeed);
    }

    private static ShadowAction OppositeVertical(ShadowAction action)
    {
        return action == ShadowAction.Jump
            ? ShadowAction.Slide : ShadowAction.Jump;
    }

    private ShadowAction ResolvePredictedVerticalAction()
    {
        if (Contract.predictionAction == ShadowAction.Jump
            || Contract.predictionAction == ShadowAction.Slide)
            return Contract.predictionAction;
        if (Contract.learnedAction == ShadowAction.Jump
            || Contract.learnedAction == ShadowAction.Slide)
            return Contract.learnedAction;
        return OppositeVertical(Contract.targetAction);
    }

    private void SetFeedback(string feedback)
    {
        Contract.lastFeedback = feedback;
        Contract.feedbackSequence++;
    }
}
