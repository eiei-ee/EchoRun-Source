using NUnit.Framework;

public sealed class SingleContractTelemetryTests
{
    [SetUp]
    public void SetUp()
    {
        AIRunTelemetry.ResetTrainingInMemory();
    }

    [TearDown]
    public void TearDown()
    {
        AIRunTelemetry.ResetTrainingInMemory();
    }

    [Test]
    public void GateEventRoundTripPreservesEveryContractField()
    {
        BeginTelemetry();
        var source = new AISingleContractEventSample
        {
            type = AISingleContractEventType.GateResolved,
            runSequence = 999,
            seed = 999,
            generation = 4,
            gateId = 82,
            sequence = 6,
            hypothesisVersion = 2,
            predictedLane = 2,
            committedLane = 0,
            chosenRole = PredictionGateRole.Counter,
            strategyKey = StrategyKey.AvoidOriginal,
            execution = GateExecutionOutcome.Success,
            reactionTime = 0.42f,
            speedAtResolution = 23.5f,
            secondsDelta = -0.65f,
            metersDelta = 5.2f,
            leadBefore = -1.5f,
            leadAfter = 3.7f,
            relearned = true
        };

        AIRunTelemetry.RecordSingleContractEvent(source);
        source.gateId = 999;
        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson());

        Assert.AreEqual(AIRunTelemetry.SchemaVersion, restored.schemaVersion);
        Assert.AreEqual(19, restored.runSequence);
        Assert.AreEqual(1, restored.singleContractEvents.Count);
        AISingleContractEventSample sample = restored.singleContractEvents[0];
        Assert.AreEqual(AISingleContractEventType.GateResolved, sample.type);
        Assert.AreEqual(19, sample.runSequence,
            "Recorder must bind the event to the active run.");
        Assert.AreEqual(731, sample.seed,
            "Recorder must bind the event to the active seed.");
        Assert.AreEqual(4, sample.generation);
        Assert.AreEqual(82, sample.gateId,
            "Recorder must snapshot rather than retain the caller's object.");
        Assert.AreEqual(6, sample.sequence);
        Assert.AreEqual(2, sample.hypothesisVersion);
        Assert.AreEqual(2, sample.predictedLane);
        Assert.AreEqual(0, sample.committedLane);
        Assert.AreEqual(PredictionGateRole.Counter, sample.chosenRole);
        Assert.AreEqual(StrategyKey.AvoidOriginal, sample.strategyKey);
        Assert.AreEqual(GateExecutionOutcome.Success, sample.execution);
        Assert.AreEqual(0.42f, sample.reactionTime, 0.0001f);
        Assert.AreEqual(23.5f, sample.speedAtResolution, 0.0001f);
        Assert.AreEqual(-0.65f, sample.secondsDelta, 0.0001f);
        Assert.AreEqual(5.2f, sample.metersDelta, 0.0001f);
        Assert.AreEqual(-1.5f, sample.leadBefore, 0.0001f);
        Assert.AreEqual(3.7f, sample.leadAfter, 0.0001f);
        Assert.IsTrue(sample.relearned);
    }

    [Test]
    public void UnchosenRoutesAreDeepCopiedAndSurviveSerialization()
    {
        BeginTelemetry();
        var source = new AISingleContractEventSample
        {
            type = AISingleContractEventType.GatePresented,
            availableLanes = new[]
            {
                new PredictionGateLane { physicalLane = 2,
                    role = PredictionGateRole.Counter, coinCount = 7,
                    obstacle = new PredictionGateObstacle {
                        isRequired = true, obstacleType = ObstacleType.High,
                        prefabIndex = 1 } }
            },
            playerLane = 0, playerLateralOffset = -2.5f,
            collisionStrikes = 1, recoveryRemaining = 0.75f,
            currentSpeed = 20f, leadBefore = -4f, leadAfter = -4f,
            sourceIdentityId = "echo-from-earlier-win", sourceRunSequence = 6,
            presentationDistance = 100f, commitDistance = 120f,
            resolveDistance = 140f, exitDistance = 160f,
            relearnStartGateNumber = 3
        };
        AIRunTelemetry.RecordSingleContractEvent(source);
        source.availableLanes[0] = default;
        AISingleContractEventSample restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson()).singleContractEvents[0];
        Assert.AreEqual(2, restored.availableLanes[0].physicalLane);
        Assert.IsTrue(restored.availableLanes[0].obstacle.isRequired);
        Assert.AreEqual(7, restored.availableLanes[0].coinCount);
        Assert.AreEqual(0, restored.playerLane);
        Assert.AreEqual(-2.5f, restored.playerLateralOffset);
        Assert.AreEqual(1, restored.collisionStrikes);
        Assert.AreEqual(0.75f, restored.recoveryRemaining);
        Assert.AreEqual(20f, restored.currentSpeed);
        Assert.AreEqual(-4f, restored.leadBefore);
        Assert.AreEqual(6, restored.sourceRunSequence);
        Assert.AreEqual("echo-from-earlier-win", restored.sourceIdentityId);
        Assert.AreEqual(100f, restored.presentationDistance);
        Assert.AreEqual(120f, restored.commitDistance);
        Assert.AreEqual(140f, restored.resolveDistance);
        Assert.AreEqual(160f, restored.exitDistance);
        Assert.AreEqual(3, restored.relearnStartGateNumber);
    }

    [Test]
    public void IdentityEventRoundTripPreservesTransactionEvidence()
    {
        BeginTelemetry();
        AIRunTelemetry.RecordSingleContractEvent(
            new AISingleContractEventSample
            {
                type = AISingleContractEventType.IdentityPromoted,
                generation = 5,
                oldIdentityId = "echo-old",
                newIdentityId = "echo-new",
                transactionId = "tx-run-19",
                commitResult = "committed",
                identityHashBefore = "HASH-BEFORE",
                identityHashAfter = "HASH-AFTER"
            });

        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson());
        AISingleContractEventSample sample = restored.singleContractEvents[0];

        Assert.AreEqual(AISingleContractEventType.IdentityPromoted, sample.type);
        Assert.AreEqual("echo-old", sample.oldIdentityId);
        Assert.AreEqual("echo-new", sample.newIdentityId);
        Assert.AreEqual("tx-run-19", sample.transactionId);
        Assert.AreEqual("committed", sample.commitResult);
        Assert.AreEqual("HASH-BEFORE", sample.identityHashBefore);
        Assert.AreEqual("HASH-AFTER", sample.identityHashAfter);
    }

    [Test]
    public void LegacyTelemetryJsonInitializesSingleContractEventList()
    {
        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            "{\"schemaVersion\":8,\"runId\":\"legacy\",\"seed\":1}");

        Assert.IsNotNull(restored);
        Assert.IsNotNull(restored.singleContractEvents);
        Assert.IsEmpty(restored.singleContractEvents);
    }

    [Test]
    public void SingleContractRecorderHonorsItsBoundedCapacity()
    {
        BeginTelemetry();
        for (int i = 0; i < 4100; i++)
        {
            AIRunTelemetry.RecordSingleContractEvent(
                new AISingleContractEventSample
                {
                    type = AISingleContractEventType.GateScheduled,
                    gateId = i + 1
                });
        }

        Assert.AreEqual(4096,
            AIRunTelemetry.ActiveRun.singleContractEvents.Count);
    }

    private static void BeginTelemetry()
    {
        AIRunTelemetry.BeginRun(731, 19, 0, 3, 0,
            new float[0], new float[0], "", "");
    }
}
