using System;
using System.Collections.Generic;

public readonly struct EchoRelearnResult
{
    public readonly bool accepted;
    public readonly bool triggered;
    public readonly int hypothesisVersion;
    public readonly int remappedGateCount;

    public EchoRelearnResult(bool accepted, bool triggered,
        int hypothesisVersion, int remappedGateCount)
    {
        this.accepted = accepted;
        this.triggered = triggered;
        this.hypothesisVersion = hypothesisVersion;
        this.remappedGateCount = remappedGateCount;
    }
}

public sealed class SingleContractGatePlan
{
    private readonly PredictionGateController[] _gates;
    private readonly Dictionary<int, PredictionGateController> _gatesById;
    private readonly HashSet<int> _recordedSettlements = new HashSet<int>();
    private readonly bool _allowRelearning;

    public int GateCount => _gates.Length;
    public int HypothesisVersion { get; private set; }
    public StrategyKey PredictedStrategy { get; private set; }
    public bool RelearnTriggered { get; private set; }
    public int CounterSuccessStreak { get; private set; }

    public SingleContractGatePlan(PredictionGateDefinition[] definitions,
        bool allowRelearning = true)
    {
        ValidateDefinitions(definitions);
        _allowRelearning = allowRelearning;

        HypothesisVersion = definitions[0].hypothesisVersion;
        PredictedStrategy = definitions[0].predictedStrategy;
        _gates = new PredictionGateController[definitions.Length];
        _gatesById = new Dictionary<int, PredictionGateController>(
            definitions.Length);
        for (int i = 0; i < definitions.Length; i++)
        {
            var gate = new PredictionGateController(definitions[i]);
            _gates[i] = gate;
            _gatesById.Add(definitions[i].gateId, gate);
        }
    }

    public PredictionGateController GetGate(int index)
    {
        if (index < 0 || index >= _gates.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _gates[index];
    }

    public bool TryGetGate(int gateId, out PredictionGateController gate)
    {
        return _gatesById.TryGetValue(gateId, out gate);
    }

    public EchoRelearnResult RecordSettlement(
        PredictionGateSettlement settlement)
    {
        if (!_gatesById.TryGetValue(settlement.gateId,
                out PredictionGateController gate)
            || _recordedSettlements.Contains(settlement.gateId)
            || !gate.TryGetSettlement(out PredictionGateSettlement actual)
            || !SettlementMatches(actual, settlement))
        {
            return CurrentResult(false, false, 0);
        }

        _recordedSettlements.Add(settlement.gateId);
        CounterSuccessStreak = settlement.IsCounterSuccess
            ? CounterSuccessStreak + 1 : 0;

        if (!_allowRelearning || RelearnTriggered || CounterSuccessStreak < 2
            || CountScheduledGates() < 2)
            return CurrentResult(true, false, 0);

        RelearnTriggered = true;
        HypothesisVersion++;
        PredictedStrategy = StrategyKey.AvoidOriginal;

        int remappedGateCount = 0;
        for (int i = 0; i < _gates.Length; i++)
        {
            PredictionGateController candidate = _gates[i];
            if (candidate.State != PredictionGateLifecycle.Scheduled)
                continue;
            if (candidate.RemapScheduledPrediction(
                    PredictedStrategy, HypothesisVersion)
                == GateTransitionResult.Applied)
                remappedGateCount++;
        }

        return CurrentResult(true, true, remappedGateCount);
    }

    private EchoRelearnResult CurrentResult(bool accepted, bool triggered,
        int remappedGateCount)
    {
        return new EchoRelearnResult(accepted, triggered,
            HypothesisVersion, remappedGateCount);
    }

    private int CountScheduledGates()
    {
        int count = 0;
        for (int i = 0; i < _gates.Length; i++)
        {
            if (_gates[i].State == PredictionGateLifecycle.Scheduled)
                count++;
        }
        return count;
    }

    private static bool SettlementMatches(PredictionGateSettlement expected,
        PredictionGateSettlement supplied)
    {
        return expected.gateId == supplied.gateId
               && expected.chosenRole == supplied.chosenRole
               && expected.execution == supplied.execution
               && expected.playerLeadSeconds == supplied.playerLeadSeconds
               && expected.echoLeadSeconds == supplied.echoLeadSeconds
               && expected.signedLeadSeconds == supplied.signedLeadSeconds
               && expected.playerLeadMeters == supplied.playerLeadMeters
               && expected.echoLeadMeters == supplied.echoLeadMeters
               && expected.signedLeadMeters == supplied.signedLeadMeters;
    }

    private static void ValidateDefinitions(
        PredictionGateDefinition[] definitions)
    {
        if (definitions == null
            || definitions.Length != PredictionGateTemplates.NormalGateCount
            && definitions.Length != PredictionGateTemplates.TotalGateCount)
        {
            throw new ArgumentException(
                "A single-contract plan requires five calibration gates or five normal gates and one final gate.",
                nameof(definitions));
        }

        var gateIds = new HashSet<int>();
        int hypothesisVersion = definitions[0] != null
            ? definitions[0].hypothesisVersion : -1;
        StrategyKey predictedStrategy = definitions[0] != null
            ? definitions[0].predictedStrategy : StrategyKey.Neutral;
        for (int i = 0; i < definitions.Length; i++)
        {
            PredictionGateDefinition definition = definitions[i];
            bool shouldBeFinal = definitions.Length
                                 == PredictionGateTemplates.TotalGateCount
                                 && i == definitions.Length - 1;
            if (definition == null || !definition.IsValid()
                || definition.sequence != i + 1
                || definition.isFinal != shouldBeFinal
                || definition.hypothesisVersion != hypothesisVersion
                || definition.predictedStrategy != predictedStrategy
                || !gateIds.Add(definition.gateId))
            {
                throw new ArgumentException(
                    "Prediction gate definitions do not form a valid ordered plan.",
                    nameof(definitions));
            }
        }
    }
}
