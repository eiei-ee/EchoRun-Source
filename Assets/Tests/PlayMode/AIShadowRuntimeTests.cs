using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class AIShadowRuntimeTests
{
    [UnityTest]
    public IEnumerator CalibrationReloadAndContractVictoryFormCompleteLoop()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        for (int frame = 0; frame < 240
             && (GameManager.Instance == null || AIShadowRunner.Instance == null);
             frame++)
            yield return null;

        GameManager gameManager = GameManager.Instance;
        AIShadowRunner runner = AIShadowRunner.Instance;
        Assert.IsNotNull(gameManager);
        Assert.IsNotNull(runner);
        Assert.IsTrue(gameManager.TryConfigureGameplayFlow(
            GameplayFlowMode.SixPhaseLegacy));

        runner.ResetTraining();
        StyleTracker.ResetTraining();
        AIPlayerSkillEstimator.ResetTraining();
        gameManager.StartGame();
        for (int frame = 0; frame < 10; frame++) yield return null;
        int calibrationActions = Mathf.Max(
            runner.minimumTrainingSamples, runner.minimumActiveTrainingSamples);
        for (int i = 0; i < calibrationActions; i++)
            runner.RecordPlayerAction(i % 2 == 0
                ? ShadowAction.Jump
                : ShadowAction.Slide, 1);
        Invoke(gameManager, "CompleteCourse");
        string calibrationResult = runner.LastResult;

        Assert.AreEqual(1, runner.Generation);
        StringAssert.Contains("校准完成", calibrationResult);

        gameManager.Restart();
        AIShadowRunner previousRunner = runner;
        for (int frame = 0; frame < 300
             && (AIShadowRunner.Instance == null
                 || AIShadowRunner.Instance == previousRunner
                 || GameManager.Instance == null
                 || GameManager.Instance.State != GameState.Playing
                 || !AIShadowRunner.Instance.HasActiveOpponent
                 || AIShadowRunner.Instance.ActiveContract == null);
             frame++)
            yield return null;

        runner = AIShadowRunner.Instance;
        Assert.IsNotNull(runner);
        Assert.AreNotSame(previousRunner, runner);
        Assert.IsTrue(runner.HasActiveOpponent);
        Assert.IsNotNull(runner.ActiveContract);
        StringAssert.Contains("回声侦测", runner.CurrentStatus);

        EchoContractEvaluator evaluator =
            (EchoContractEvaluator)GetField(runner, "_contractEvaluator");
        CompleteContract(evaluator);
        Assert.IsTrue(runner.ActiveContract.completed);

        SetField(runner, "<PlayerLead>k__BackingField", 2f);
        Invoke(GameManager.Instance, "CompleteCourse");

        Assert.IsTrue(runner.LastRunWasChallenge);
        Assert.IsTrue(runner.LastRunWon);
        StringAssert.Contains("契约破解", runner.LastResult);
        StringAssert.Contains("本代学习", runner.LastResult);
        StringAssert.Contains("下一代变化", runner.LastResult);

        runner.ResetTraining();
        StyleTracker.ResetTraining();
        AIPlayerSkillEstimator.ResetTraining();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FailedRetryKeepsPolicySequenceStyleAndPaceFrozen()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        for (int frame = 0; frame < 240
             && (GameManager.Instance == null || AIShadowRunner.Instance == null);
             frame++)
            yield return null;

        GameManager gameManager = GameManager.Instance;
        AIShadowRunner runner = AIShadowRunner.Instance;
        Assert.IsNotNull(gameManager);
        Assert.IsNotNull(runner);
        Assert.IsTrue(gameManager.TryConfigureGameplayFlow(
            GameplayFlowMode.SixPhaseLegacy));

        runner.ResetTraining();
        StyleTracker.ResetTraining();
        AIPlayerSkillEstimator.ResetTraining();
        gameManager.StartGame();
        for (int frame = 0; frame < 10; frame++) yield return null;
        int calibrationActions = Mathf.Max(
            runner.minimumTrainingSamples, runner.minimumActiveTrainingSamples);
        for (int i = 0; i < calibrationActions; i++)
            runner.RecordPlayerAction(i % 2 == 0
                ? ShadowAction.Jump : ShadowAction.Slide, i % 3);
        Invoke(gameManager, "CompleteCourse");

        Assert.AreEqual(1, runner.Generation);
        string promotedGeneration = runner.GetActiveGenerationSnapshotJson();
        Assert.IsNotEmpty(promotedGeneration);

        gameManager.Restart();
        AIShadowRunner calibrationRunner = runner;
        yield return WaitForChallenge(calibrationRunner);
        runner = AIShadowRunner.Instance;
        gameManager = GameManager.Instance;
        string beforeFailure = runner.GetActiveGenerationSnapshotJson();
        Assert.AreEqual(promotedGeneration, beforeFailure);

        for (int i = 0; i < calibrationActions * 2; i++)
            runner.RecordPlayerAction(i % 3 == 0
                ? ShadowAction.Left : ShadowAction.Right, i % 3);
        gameManager.GameOver();
        for (int frame = 0; frame < 360
             && GameManager.Instance.State != GameState.GameOver; frame++)
            yield return null;

        Assert.AreEqual(1, runner.Generation);
        Assert.AreEqual(beforeFailure,
            runner.GetActiveGenerationSnapshotJson(),
            "A failed run must not mutate the active generation snapshot.");

        gameManager.Restart();
        AIShadowRunner failedRunner = runner;
        yield return WaitForChallenge(failedRunner);
        runner = AIShadowRunner.Instance;
        Assert.AreEqual(beforeFailure,
            runner.GetActiveGenerationSnapshotJson(),
            "Retry must load identical policy, sequence, style and pace.");

        runner.ResetTraining();
        StyleTracker.ResetTraining();
        AIPlayerSkillEstimator.ResetTraining();
        yield return null;
    }

    [UnityTest]
    public IEnumerator TrainingDashboardBuildsOptionalLiveDebugPanel()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        AITrainingDashboardUI dashboard = null;
        for (int frame = 0; frame < 180 && dashboard == null; frame++)
        {
            dashboard = Object.FindObjectOfType<AITrainingDashboardUI>();
            yield return null;
        }

        Assert.IsNotNull(dashboard);
        for (int frame = 0; frame < 180
             && GetField(dashboard, "_liveDebugPanel") == null; frame++)
            yield return null;
        GameObject liveDebug = (GameObject)GetField(
            dashboard, "_liveDebugPanel");
        Assert.IsNotNull(liveDebug);
        Assert.IsNotNull(liveDebug.transform.Find("Content"));
        Assert.IsFalse(liveDebug.activeSelf,
            "Live diagnostics must be opt-in during normal play.");
    }

    [UnityTest]
    public IEnumerator GhostVisualStaysAboveTrackAndUsesDedicatedShader()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        for (int frame = 0; frame < 180 &&
             (AIShadowRunner.Instance == null ||
              Object.FindObjectOfType<PlayerController>() == null); frame++)
            yield return null;

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        AIShadowRunner runner = AIShadowRunner.Instance;
        Assert.IsNotNull(player);
        Assert.IsNotNull(runner);
        for (int frame = 0; frame < 30; frame++) yield return null;

        SetField(runner, "_player", player);
        Invoke(runner, "CreateGhost");
        SetField(runner, "_ghostGroundY", player.transform.position.y);
        SetField(runner, "_displayedGap", 10f);
        SetField(runner, "_ghostProgress", 10f);
        SetField(runner, "_playerProgress", 0f);
        Invoke(runner, "UpdateGhostPose");
        yield return null;

        GameObject ghost = (GameObject)GetField(runner, "_ghost");
        Material material = (Material)GetField(runner, "_ghostMaterial");
        Assert.IsNotNull(material);
        Assert.AreEqual("EchoRun/GhostRunner", material.shader.name);

        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Assert.IsTrue(Physics.Raycast(ghost.transform.position + Vector3.up * 5f,
            Vector3.down, out RaycastHit hit, 10f, player.groundLayer,
            QueryTriggerInteraction.Ignore));
        Assert.GreaterOrEqual(bounds.min.y, hit.point.y - 0.05f,
            "The AI shadow visual is submerged below the track.");

        SetField(runner, "_ghost", null);
        Object.Destroy(ghost);
        yield return null;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static object GetField(object target, string name)
    {
        return target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void CompleteContract(EchoContractEvaluator evaluator)
    {
        evaluator.SetPhase(EchoDuelPhase.Resistance);
        CompleteContractStage(evaluator, false);
        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        CompleteContractStage(evaluator, true);
    }

    private static void CompleteContractStage(EchoContractEvaluator evaluator,
        bool counterattack)
    {
        EchoContractData contract = evaluator.Contract;
        int guard = 0;
        while (!(counterattack ? contract.completed
                   : contract.initialBreakCompleted) && guard++ < 20)
        {
            if (contract.type == EchoContractType.BreakLaneHabit)
            {
                int lane = counterattack
                    ? (contract.predictionLane + guard) % 3
                    : contract.targetLane;
                if (lane == contract.predictionLane && counterattack)
                    lane = (lane + 1) % 3;
                if (counterattack)
                {
                    EchoChallengeStep step = evaluator.ActiveChallengeStep;
                    int safeLane = 0;
                    while (safeLane == lane
                           || safeLane == step.predictedLane)
                        safeLane++;
                    evaluator.BindChallengeStep(step.stepId,
                        step.predictedLane, lane, safeLane, guard * 100f);
                    evaluator.RecordLaneMarker(lane, guard * 100f, 10f,
                        step.stepId);
                }
                else
                {
                    evaluator.RecordLaneMarker(lane, guard * 100f, 10f);
                }
            }
            else
            {
                ObstacleType required = contract.targetAction == ShadowAction.Jump
                    ? ObstacleType.High : ObstacleType.Low;
                if (counterattack)
                {
                    EchoChallengeStep step = evaluator.ActiveChallengeStep;
                    int lane = 1;
                    evaluator.BindChallengeStep(step.stepId, 0, lane, 2,
                        guard * 100f);
                    evaluator.RecordDodge(required, lane, 10f,
                        new EchoChallengeObstacleBinding
                        {
                            stepId = step.stepId,
                            role = EchoChallengeObstacleRole.Required,
                            action = step.requiredAction,
                            lane = lane
                        });
                }
                else
                {
                    evaluator.RecordDodge(required, contract.targetLane);
                }
            }
        }
        Assert.Less(guard, 20, "Contract stage did not converge.");
    }

    private static IEnumerator WaitForChallenge(AIShadowRunner previousRunner)
    {
        for (int frame = 0; frame < 360
             && (AIShadowRunner.Instance == null
                 || AIShadowRunner.Instance == previousRunner
                 || GameManager.Instance == null
                 || GameManager.Instance.State != GameState.Playing
                 || !AIShadowRunner.Instance.HasActiveOpponent
                 || AIShadowRunner.Instance.ActiveContract == null);
             frame++)
            yield return null;

        Assert.IsNotNull(AIShadowRunner.Instance);
        Assert.AreNotSame(previousRunner, AIShadowRunner.Instance);
        Assert.IsTrue(AIShadowRunner.Instance.HasActiveOpponent);
        Assert.IsNotNull(AIShadowRunner.Instance.ActiveContract);
    }
}
