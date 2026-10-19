using NUnit.Framework;

public sealed class PredictionGateTemplateTests
{
    private const int OriginalHabitLane = 1;

    [Test]
    public void CreateBuildsAllTemplatesWithBoundedCoinsAndOneRiskObstacle()
    {
        PredictionGateDefinition[] definitions = CreateDefinitions(
            4187, OriginalHabitLane);
        PredictionGateTemplateKind[] expectedKinds =
        {
            PredictionGateTemplateKind.CounterJump,
            PredictionGateTemplateKind.CounterSlide,
            PredictionGateTemplateKind.CounterJump,
            PredictionGateTemplateKind.CounterSlide,
            PredictionGateTemplateKind.CounterJump,
            PredictionGateTemplateKind.FinalChoice
        };

        Assert.AreEqual(6, definitions.Length);
        for (int i = 0; i < definitions.Length; i++)
        {
            PredictionGateDefinition definition = definitions[i];
            Assert.IsTrue(definition.IsValid(), "Gate " + (i + 1));
            Assert.AreEqual(expectedKinds[i], definition.templateKind);
            Assert.AreEqual(i == definitions.Length - 1,
                definition.isFinal);

            PredictionGateLane predicted = FindLane(
                definition, StrategyKey.OriginalHabit);
            PredictionGateLane counter = FindLane(
                definition, StrategyKey.AvoidOriginal);
            PredictionGateLane neutral = FindLane(
                definition, StrategyKey.Neutral);

            AssertLane(predicted, PredictionGateRole.Predicted,
                RouteAttribute.Reward, 8, 11, false);
            AssertLane(counter, PredictionGateRole.Counter,
                RouteAttribute.Risk, 7, 10, true);
            AssertLane(neutral, PredictionGateRole.Neutral,
                RouteAttribute.Safe, 3, 4, false);

            int riskLaneCount = 0;
            int obstacleLaneCount = 0;
            int riskPhysicalLane = -1;
            int obstaclePhysicalLane = -1;
            for (int laneIndex = 0;
                 laneIndex < definition.lanes.Length;
                 laneIndex++)
            {
                PredictionGateLane lane = definition.lanes[laneIndex];
                if (lane.attribute == RouteAttribute.Risk)
                {
                    riskLaneCount++;
                    riskPhysicalLane = lane.physicalLane;
                }
                if (lane.obstacle.isRequired)
                {
                    obstacleLaneCount++;
                    obstaclePhysicalLane = lane.physicalLane;
                }
            }

            Assert.AreEqual(1, riskLaneCount);
            Assert.AreEqual(1, obstacleLaneCount);
            Assert.AreEqual(riskPhysicalLane, obstaclePhysicalLane);
            AssertObstacleMatchesTemplate(
                definition.templateKind, counter.obstacle);
        }
    }

    [Test]
    public void FinalChoiceCanRequireEitherJumpOrSlideAcrossSeeds()
    {
        bool sawJump = false;
        bool sawSlide = false;
        for (int seed = 0; seed < 64; seed++)
        {
            PredictionGateDefinition[] definitions = CreateDefinitions(
                seed, OriginalHabitLane);
            PredictionGateDefinition finalChoice =
                definitions[definitions.Length - 1];
            PredictionGateLane counter = FindLane(
                finalChoice, StrategyKey.AvoidOriginal);

            Assert.AreEqual(PredictionGateTemplateKind.FinalChoice,
                finalChoice.templateKind);
            sawJump |= counter.obstacle.obstacleType == ObstacleType.High;
            sawSlide |= counter.obstacle.obstacleType == ObstacleType.Low;
        }

        Assert.IsTrue(sawJump, "FinalChoice never selected Jump.");
        Assert.IsTrue(sawSlide, "FinalChoice never selected Slide.");
    }

    [Test]
    public void SameSeedReproducesAllFieldsForAllSixGates()
    {
        PredictionGateDefinition[] first = PredictionGateTemplates.Create(
            73, 99173, 2, CreateWindows(), 4);
        PredictionGateDefinition[] second = PredictionGateTemplates.Create(
            73, 99173, 2, CreateWindows(), 4);

        Assert.AreEqual(6, first.Length);
        Assert.AreEqual(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
            AssertDefinitionsEqual(first[i], second[i]);
    }

    [Test]
    public void RoleMappingIsNotPermanentlyBoundToPhysicalLanes()
    {
        var predictedLanes = new bool[3];
        for (int habitLane = 0; habitLane < 3; habitLane++)
        {
            PredictionGateDefinition[] definitions = CreateDefinitions(
                127, habitLane);
            PredictionGateLane predicted = FindLane(
                definitions[0], StrategyKey.OriginalHabit);
            predictedLanes[predicted.physicalLane] = true;
            Assert.AreEqual(habitLane, predicted.physicalLane);
        }

        for (int lane = 0; lane < predictedLanes.Length; lane++)
            Assert.IsTrue(predictedLanes[lane]);

        var counterLanes = new bool[3];
        var neutralLanes = new bool[3];
        for (int seed = 0; seed < 64; seed++)
        {
            PredictionGateDefinition[] definitions = CreateDefinitions(
                seed, OriginalHabitLane);
            for (int i = 0; i < definitions.Length; i++)
            {
                PredictionGateLane counter = FindLane(
                    definitions[i], StrategyKey.AvoidOriginal);
                PredictionGateLane neutral = FindLane(
                    definitions[i], StrategyKey.Neutral);
                counterLanes[counter.physicalLane] = true;
                neutralLanes[neutral.physicalLane] = true;
            }
        }

        Assert.IsTrue(counterLanes[0]);
        Assert.IsTrue(counterLanes[2]);
        Assert.IsTrue(neutralLanes[0]);
        Assert.IsTrue(neutralLanes[2]);
    }

    [Test]
    public void CalibrationRotatesRewardLaneAcrossAllPhysicalLanes()
    {
        PredictionGateDistanceWindow[] windows = CreateWindows(5);
        PredictionGateDefinition[] definitions =
            PredictionGateTemplates.CreateCalibration(
                21, 771, OriginalHabitLane, windows);
        var rewardLanes = new bool[3];
        for (int index = 0; index < definitions.Length; index++)
        {
            PredictionGateLane reward = FindLane(
                definitions[index], StrategyKey.OriginalHabit);
            rewardLanes[reward.physicalLane] = true;
        }
        for (int lane = 0; lane < rewardLanes.Length; lane++)
            Assert.IsTrue(rewardLanes[lane]);
    }

    [Test]
    public void RemapChangesRolesWithoutChangingFrozenObstacleOrCoinContent()
    {
        PredictionGateDefinition original = CreateDefinitions(
            2671, OriginalHabitLane)[0];
        PredictionGateDefinition frozen = original.Clone();

        PredictionGateDefinition remapped = original.RemapPrediction(
            StrategyKey.AvoidOriginal, 2);

        AssertDefinitionsEqual(frozen, original);
        Assert.AreEqual(StrategyKey.AvoidOriginal,
            remapped.predictedStrategy);
        Assert.AreEqual(2, remapped.hypothesisVersion);
        Assert.AreEqual(original.runId, remapped.runId);
        Assert.AreEqual(original.gateId, remapped.gateId);
        Assert.AreEqual(original.sequence, remapped.sequence);
        Assert.AreEqual(original.isFinal, remapped.isFinal);
        Assert.AreEqual(original.templateKind, remapped.templateKind);
        Assert.AreEqual(original.presentationDistance,
            remapped.presentationDistance);
        Assert.AreEqual(original.commitDistance,
            remapped.commitDistance);
        Assert.AreEqual(original.resolveDistance,
            remapped.resolveDistance);
        Assert.AreEqual(original.exitDistance, remapped.exitDistance);

        int changedRoleCount = 0;
        for (int i = 0; i < original.lanes.Length; i++)
        {
            PredictionGateLane before = original.lanes[i];
            PredictionGateLane after = FindPhysicalLane(
                remapped, before.physicalLane);
            Assert.AreEqual(before.physicalLane, after.physicalLane);
            Assert.AreEqual(before.strategyKey, after.strategyKey);
            Assert.AreEqual(before.attribute, after.attribute);
            Assert.AreEqual(before.coinCount, after.coinCount);
            AssertObstacleEqual(before.obstacle, after.obstacle);

            PredictionGateRole expectedRole = before.strategyKey
                == StrategyKey.AvoidOriginal
                ? PredictionGateRole.Predicted
                : before.strategyKey == StrategyKey.OriginalHabit
                    ? PredictionGateRole.Counter
                    : PredictionGateRole.Neutral;
            Assert.AreEqual(expectedRole, after.role);
            if (before.role != after.role) changedRoleCount++;
        }

        Assert.AreEqual(2, changedRoleCount);
    }

    private static PredictionGateDefinition[] CreateDefinitions(
        int runSeed, int originalHabitLane)
    {
        return PredictionGateTemplates.Create(
            19, runSeed, originalHabitLane, CreateWindows());
    }

    private static PredictionGateDistanceWindow[] CreateWindows()
    {
        return CreateWindows(PredictionGateTemplates.TotalGateCount);
    }

    private static PredictionGateDistanceWindow[] CreateWindows(int count)
    {
        var windows = new PredictionGateDistanceWindow[
            count];
        for (int i = 0; i < windows.Length; i++)
        {
            float start = 80f * (i + 1);
            windows[i] = new PredictionGateDistanceWindow
            {
                presentationDistance = start,
                commitDistance = start + 9f,
                resolveDistance = start + 18f,
                exitDistance = start + 27f
            };
        }
        return windows;
    }

    private static PredictionGateLane FindLane(
        PredictionGateDefinition definition, StrategyKey strategy)
    {
        for (int i = 0; i < definition.lanes.Length; i++)
        {
            if (definition.lanes[i].strategyKey == strategy)
                return definition.lanes[i];
        }

        Assert.Fail("Strategy was not present: " + strategy);
        return default;
    }

    private static PredictionGateLane FindPhysicalLane(
        PredictionGateDefinition definition, int physicalLane)
    {
        for (int i = 0; i < definition.lanes.Length; i++)
        {
            if (definition.lanes[i].physicalLane == physicalLane)
                return definition.lanes[i];
        }

        Assert.Fail("Physical lane was not present: " + physicalLane);
        return default;
    }

    private static void AssertLane(PredictionGateLane lane,
        PredictionGateRole expectedRole,
        RouteAttribute expectedAttribute,
        int minimumCoins, int maximumCoins,
        bool obstacleRequired)
    {
        Assert.AreEqual(expectedRole, lane.role);
        Assert.AreEqual(expectedAttribute, lane.attribute);
        Assert.GreaterOrEqual(lane.coinCount, minimumCoins);
        Assert.LessOrEqual(lane.coinCount, maximumCoins);
        Assert.AreEqual(obstacleRequired, lane.obstacle.isRequired);
        if (!obstacleRequired)
        {
            Assert.AreEqual(ObstacleType.Barrier,
                lane.obstacle.obstacleType);
            Assert.AreEqual(-1, lane.obstacle.prefabIndex);
        }
    }

    private static void AssertObstacleMatchesTemplate(
        PredictionGateTemplateKind templateKind,
        PredictionGateObstacle obstacle)
    {
        Assert.IsTrue(obstacle.isRequired);
        if (templateKind == PredictionGateTemplateKind.CounterJump)
        {
            Assert.AreEqual(ObstacleType.High, obstacle.obstacleType);
            Assert.AreEqual(1, obstacle.prefabIndex);
            return;
        }
        if (templateKind == PredictionGateTemplateKind.CounterSlide)
        {
            Assert.AreEqual(ObstacleType.Low, obstacle.obstacleType);
            Assert.AreEqual(0, obstacle.prefabIndex);
            return;
        }

        Assert.AreEqual(PredictionGateTemplateKind.FinalChoice,
            templateKind);
        Assert.IsTrue(obstacle.obstacleType == ObstacleType.High
                      || obstacle.obstacleType == ObstacleType.Low);
        Assert.AreEqual(obstacle.obstacleType == ObstacleType.High ? 1 : 0,
            obstacle.prefabIndex);
    }

    private static void AssertDefinitionsEqual(
        PredictionGateDefinition expected,
        PredictionGateDefinition actual)
    {
        Assert.AreEqual(expected.runId, actual.runId);
        Assert.AreEqual(expected.gateId, actual.gateId);
        Assert.AreEqual(expected.sequence, actual.sequence);
        Assert.AreEqual(expected.hypothesisVersion,
            actual.hypothesisVersion);
        Assert.AreEqual(expected.predictedStrategy,
            actual.predictedStrategy);
        Assert.AreEqual(expected.isFinal, actual.isFinal);
        Assert.AreEqual(expected.templateKind, actual.templateKind);
        Assert.AreEqual(expected.presentationDistance,
            actual.presentationDistance);
        Assert.AreEqual(expected.commitDistance, actual.commitDistance);
        Assert.AreEqual(expected.resolveDistance, actual.resolveDistance);
        Assert.AreEqual(expected.exitDistance, actual.exitDistance);
        Assert.AreEqual(expected.lanes.Length, actual.lanes.Length);
        for (int i = 0; i < expected.lanes.Length; i++)
        {
            PredictionGateLane expectedLane = expected.lanes[i];
            PredictionGateLane actualLane = actual.lanes[i];
            Assert.AreEqual(expectedLane.physicalLane,
                actualLane.physicalLane);
            Assert.AreEqual(expectedLane.role, actualLane.role);
            Assert.AreEqual(expectedLane.strategyKey,
                actualLane.strategyKey);
            Assert.AreEqual(expectedLane.attribute,
                actualLane.attribute);
            Assert.AreEqual(expectedLane.coinCount, actualLane.coinCount);
            AssertObstacleEqual(expectedLane.obstacle, actualLane.obstacle);
        }
    }

    private static void AssertObstacleEqual(
        PredictionGateObstacle expected,
        PredictionGateObstacle actual)
    {
        Assert.AreEqual(expected.isRequired, actual.isRequired);
        Assert.AreEqual(expected.obstacleType, actual.obstacleType);
        Assert.AreEqual(expected.prefabIndex, actual.prefabIndex);
    }
}
