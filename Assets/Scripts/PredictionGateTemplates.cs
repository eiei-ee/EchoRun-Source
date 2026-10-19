using System;

public static class PredictionGateTemplates
{
    public const int NormalGateCount = 5;
    public const int TotalGateCount = NormalGateCount + 1;
    public const int PredictionGateSeedSalt = unchecked((int)0x6E624EB7u);

    public static PredictionGateDefinition[] Create(
        int runId, int runSeed, int originalHabitLane,
        PredictionGateDistanceWindow[] distanceWindows,
        int hypothesisVersion = 1)
    {
        return CreateDefinitions(runId, runSeed, originalHabitLane,
            distanceWindows, hypothesisVersion, includeFinalGate: true);
    }

    public static PredictionGateDefinition[] CreateCalibration(
        int runId, int runSeed, int originalHabitLane,
        PredictionGateDistanceWindow[] distanceWindows,
        int hypothesisVersion = 1)
    {
        return CreateDefinitions(runId, runSeed, originalHabitLane,
            distanceWindows, hypothesisVersion, includeFinalGate: false);
    }

    private static PredictionGateDefinition[] CreateDefinitions(
        int runId, int runSeed, int originalHabitLane,
        PredictionGateDistanceWindow[] distanceWindows,
        int hypothesisVersion, bool includeFinalGate)
    {
        if (originalHabitLane < 0 || originalHabitLane > 2)
            throw new ArgumentOutOfRangeException(nameof(originalHabitLane));
        if (hypothesisVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(hypothesisVersion));
        int requiredGateCount = includeFinalGate
            ? TotalGateCount : NormalGateCount;
        if (distanceWindows == null
            || distanceWindows.Length != requiredGateCount)
        {
            throw new ArgumentException(
                includeFinalGate
                    ? "Exactly six challenge distance windows are required."
                    : "Exactly five calibration distance windows are required.",
                nameof(distanceWindows));
        }

        for (int i = 0; i < distanceWindows.Length; i++)
        {
            if (!distanceWindows[i].IsValid)
            {
                throw new ArgumentException(
                    "Every prediction gate distance window must be ordered.",
                    nameof(distanceWindows));
            }
        }

        var random = new System.Random(
            unchecked(runSeed ^ PredictionGateSeedSalt));
        var definitions = new PredictionGateDefinition[requiredGateCount];
        for (int index = 0; index < definitions.Length; index++)
        {
            PredictionGateDistanceWindow window = distanceWindows[index];
            bool isFinal = includeFinalGate && index == NormalGateCount;
            // Calibration has no player-facing prediction. Rotate the internal
            // reward lane so the first profile cannot be biased toward a fixed
            // physical lane by the authored gate economy.
            int templateHabitLane = includeFinalGate
                ? originalHabitLane : (originalHabitLane + index) % 3;
            PredictionGateTemplateKind templateKind = isFinal
                ? PredictionGateTemplateKind.FinalChoice
                : index % 2 == 0
                    ? PredictionGateTemplateKind.CounterJump
                    : PredictionGateTemplateKind.CounterSlide;
            definitions[index] = new PredictionGateDefinition
            {
                runId = runId,
                gateId = index + 1,
                sequence = index + 1,
                hypothesisVersion = hypothesisVersion,
                predictedStrategy = StrategyKey.OriginalHabit,
                isFinal = isFinal,
                templateKind = templateKind,
                presentationDistance = window.presentationDistance,
                commitDistance = window.commitDistance,
                resolveDistance = window.resolveDistance,
                exitDistance = window.exitDistance,
                lanes = CreateLanes(random, templateHabitLane, templateKind)
            };
        }

        return definitions;
    }

    private static PredictionGateLane[] CreateLanes(
        System.Random random, int originalHabitLane,
        PredictionGateTemplateKind templateKind)
    {
        int firstAlternativeLane = (originalHabitLane + 1) % 3;
        int secondAlternativeLane = (originalHabitLane + 2) % 3;
        int avoidLane = random.Next(0, 2) == 0
            ? firstAlternativeLane : secondAlternativeLane;
        int neutralLane = avoidLane == firstAlternativeLane
            ? secondAlternativeLane : firstAlternativeLane;

        var lanes = new PredictionGateLane[3];
        bool finalUsesJump = random.Next(0, 2) == 0;
        ObstacleType counterObstacle = templateKind
            == PredictionGateTemplateKind.CounterSlide
            ? ObstacleType.Low
            : templateKind == PredictionGateTemplateKind.CounterJump
                ? ObstacleType.High
                : finalUsesJump ? ObstacleType.High : ObstacleType.Low;
        lanes[originalHabitLane] = new PredictionGateLane
        {
            physicalLane = originalHabitLane,
            role = PredictionGateRole.Predicted,
            strategyKey = StrategyKey.OriginalHabit,
            attribute = RouteAttribute.Reward,
            obstacle = PredictionGateObstacle.None,
            coinCount = random.Next(8, 12)
        };
        lanes[avoidLane] = new PredictionGateLane
        {
            physicalLane = avoidLane,
            role = PredictionGateRole.Counter,
            strategyKey = StrategyKey.AvoidOriginal,
            attribute = RouteAttribute.Risk,
            obstacle = new PredictionGateObstacle
            {
                isRequired = true,
                obstacleType = counterObstacle,
                prefabIndex = counterObstacle == ObstacleType.High ? 1 : 0
            },
            coinCount = counterObstacle == ObstacleType.High
                ? TrackSpawnRules.JumpRewardCoinCount
                : random.Next(7, 11)
        };
        lanes[neutralLane] = new PredictionGateLane
        {
            physicalLane = neutralLane,
            role = PredictionGateRole.Neutral,
            strategyKey = StrategyKey.Neutral,
            attribute = RouteAttribute.Safe,
            obstacle = PredictionGateObstacle.None,
            coinCount = random.Next(3, 5)
        };
        return lanes;
    }
}
