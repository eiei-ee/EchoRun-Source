using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PredictionGateSettlementEventTests
{
    private GameObject _runnerObject;

    [TearDown]
    public void TearDown()
    {
        if (_runnerObject != null)
            Object.DestroyImmediate(_runnerObject);
    }

    [Test]
    public void PrewarmedSignsFollowRelearnWithoutChangingPhysicalContent()
    {
        SingleContractFlow flow = CreateFlowWithCounterSuccess();
        var host = new GameObject("Prewarmed Gate Visual Test");
        host.SetActive(false);
        TrackManager track = host.AddComponent<TrackManager>();
        var segments = new System.Collections.Generic.List<GameObject>();
        try
        {
            for (int index = 0; index < 3; index++)
            {
                var segment = new GameObject("GateRoad" + index);
                segment.transform.SetParent(host.transform);
                PredictionGateDefinition gate = flow.GetGate(index).Definition;
                var data = segment.AddComponent<TrackSegmentData>();
                data.routeDistance = gate.resolveDistance - 5f;
                new GameObject("ExistingObstacleAndCoins").transform.SetParent(segment.transform);
                typeof(TrackManager).GetMethod("SpawnPredictionGateVisual",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(track,
                    new object[] { segment, gate, data.routeDistance });
                segments.Add(segment);
            }
            SetPrivateField(track, "_activeSegments", segments);
            Transform settledVisual = segments[0].transform.Find(TrackManager.PredictionGateVisualRootName);
            Transform pendingVisual = segments[2].transform.Find(TrackManager.PredictionGateVisualRootName);
            Transform content = segments[2].transform.Find("ExistingObstacleAndCoins");
            ResolveCounter(flow, 1);
            Assert.IsTrue(flow.RelearnTriggered);
            track.RefreshSingleContractGateVisuals(flow);
            Assert.AreSame(settledVisual, segments[0].transform.Find(TrackManager.PredictionGateVisualRootName));
            Transform refreshed = segments[2].transform.Find(TrackManager.PredictionGateVisualRootName);
            Assert.AreNotSame(pendingVisual, refreshed);
            foreach (PredictionGateLane lane in flow.GetGate(2).Definition.lanes)
                Assert.IsNotNull(refreshed.Find("Lane_" + lane.physicalLane + "_" + lane.role));
            Assert.AreSame(content, segments[2].transform.Find("ExistingObstacleAndCoins"));
            track.RefreshSingleContractGateVisuals(flow);
            Assert.AreSame(refreshed, segments[2].transform.Find(TrackManager.PredictionGateVisualRootName),
                "Repeated synchronization must not respawn signs.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ConsumedCounterSettlementIsPublishedExactlyOnce()
    {
        SingleContractFlow flow = CreateFlowWithCounterSuccess();
        Assert.AreEqual(1, flow.SettlementCount);
        Assert.IsTrue(flow.GetSettlement(0).IsCounterSuccess);

        _runnerObject = new GameObject("AIShadowRunner Settlement Test");
        _runnerObject.SetActive(false);
        AIShadowRunner runner = _runnerObject.AddComponent<AIShadowRunner>();
        SetPrivateField(runner, "_singleContractFlow", flow);
        int eventCount = 0;
        PredictionGateSettlement observed = default;
        runner.PredictionGateSettlementConsumed += settlement =>
        {
            eventCount++;
            observed = settlement;
        };

        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        SetPrivateField(runner, "_nextSingleContractSettlementIndex", 0);
        InvokePrivate(runner, "ConsumeSingleContractSettlements");

        Assert.AreEqual(1, eventCount,
            "The runner and flow consumption guards must prevent duplicate publication.");
        Assert.AreEqual(flow.GetSettlement(0).gateId, observed.gateId);
        Assert.AreEqual(PredictionGateRole.Counter, observed.chosenRole);
        Assert.AreEqual(GateExecutionOutcome.Success, observed.execution);
        Assert.IsTrue(observed.IsCounterSuccess,
            "The event must preserve the authoritative settlement semantics.");
    }

    [TestCase(PredictionGateRole.Counter, GateExecutionReason.Collision,
        SingleContractInstantFeedback.CounterFailed)]
    [TestCase(PredictionGateRole.Predicted, GateExecutionReason.Collision,
        SingleContractInstantFeedback.ExecutionIncomplete)]
    [TestCase(PredictionGateRole.Counter, GateExecutionReason.Unresolved,
        SingleContractInstantFeedback.ObservationInconclusive)]
    public void FailedExecutionDoesNotBecomePredictionSuccess(
        PredictionGateRole role, GateExecutionReason reason,
        SingleContractInstantFeedback expected)
    {
        PredictionGateSettlement settlement = PredictionGateEvaluator.Evaluate(
            1, role, GateExecutionOutcome.Hit, 20f, 1f, reason);
        Assert.AreEqual(expected,
            AIShadowRunner.FeedbackForSingleContractSettlement(true, settlement));
        Assert.Less(settlement.signedLeadMeters, 0f,
            "A more precise observation must not silently change racing penalties.");
    }

    [Test]
    public void RelearnKeepsGateResultAndPublishesOneFeedbackSequencePerGate()
    {
        SingleContractFlow flow = CreateFlowWithCounterSuccess();
        ResolveCounter(flow, 1);
        _runnerObject = new GameObject("AIShadowRunner Combined Feedback Test");
        _runnerObject.SetActive(false);
        AIShadowRunner runner = _runnerObject.AddComponent<AIShadowRunner>();
        SetPrivateField(runner, "_activeGameplayFlowMode", GameplayFlowMode.SingleContract);
        SetPrivateField(runner, "_singleContractFlow", flow);
        SetPrivateField(runner, "_runAdaptationState", new RunAdaptationState());
        typeof(AIShadowRunner).GetProperty("HasActiveOpponent")
            .GetSetMethod(true).Invoke(runner, new object[] { true });

        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        Assert.AreEqual(SingleContractInstantFeedback.RewriteSucceeded,
            runner.SingleContractFeedback);
        Assert.IsTrue(runner.SingleContractFeedbackRelearned);
        Assert.AreEqual(2, runner.SingleContractFeedbackSequence,
            "The second gate's result and relearn must be one notification.");
        string review = (string)typeof(AIShadowRunner).GetMethod(
            "AppendSingleContractGateReview",
            BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(runner, new object[] { "回声胜出\n下一局仍使用本代记录" });
        StringAssert.Contains("改猜原因：连续两次反制通过", review);
        StringAssert.Contains("从第3次选路起调整预测", review);
        StringAssert.Contains("下一局仍使用本代记录", review,
            "Observed adaptation must not claim identity promotion after defeat.");
        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        Assert.AreEqual(2, runner.SingleContractFeedbackSequence);
    }

    private static SingleContractFlow CreateFlowWithCounterSuccess()
    {
        var flow = new SingleContractFlow(
            new SingleContractFixedGateWindowFactory(CreateWindows()),
            2, 1f);
        flow.BeginRun(new EchoRunContext
        {
            mode = GameplayFlowMode.SingleContract,
            hasOpponent = true,
            courseDistance = 950f,
            runSequence = 7,
            runSeed = 424242,
            generation = 3
        });

        ResolveCounter(flow, 0);
        return flow;
    }

    private static void ResolveCounter(SingleContractFlow flow, int index)
    {
        PredictionGateDefinition definition = flow.GetGate(index).Definition;
        int counterLane = FindLaneForRole(
            definition, PredictionGateRole.Counter);
        flow.Tick(new EchoRunFrame
        {
            elapsedTime = 12f + index * 14f,
            playerDistance = definition.commitDistance,
            currentSpeed = 20f,
            playerLane = counterLane
        });
        Assert.AreEqual(PredictionGateLifecycle.ChoiceCommitted,
            flow.GetGate(index).State);
        GateTransitionResult result = flow.ResolveObstaclePassed(
            new GateObstacleEvent
            {
                gateId = definition.gateId,
                obstacleId = definition.gateId * 10,
                physicalLane = counterLane
            });
        Assert.AreEqual(GateTransitionResult.Applied, result);
    }

    private static PredictionGateDistanceWindow[] CreateWindows()
    {
        var windows = new PredictionGateDistanceWindow[6];
        for (int i = 0; i < windows.Length; i++)
        {
            float start = 100f * (i + 1);
            windows[i] = new PredictionGateDistanceWindow
            {
                presentationDistance = start,
                commitDistance = start + 10f,
                resolveDistance = start + 20f,
                exitDistance = start + 30f
            };
        }
        return windows;
    }

    private static int FindLaneForRole(PredictionGateDefinition definition,
        PredictionGateRole role)
    {
        for (int i = 0; i < definition.lanes.Length; i++)
            if (definition.lanes[i].role == role)
                return definition.lanes[i].physicalLane;
        Assert.Fail("Requested role was not present in the gate.");
        return -1;
    }

    private static void SetPrivateField(object target, string name,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing private field: " + name);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing private method: " + name);
        method.Invoke(target, null);
    }
}
