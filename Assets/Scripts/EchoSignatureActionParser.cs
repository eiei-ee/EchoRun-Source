using System;
using System.Collections.Generic;

public sealed class EchoSignatureActionResult
{
    public static readonly EchoSignatureActionResult Unavailable =
        new EchoSignatureActionResult(false, ShadowAction.Keep, 0, -1, 0);

    public bool available { get; }
    public ShadowAction action { get; }
    public int count { get; }
    public int laneBeforeAction { get; }
    public int sourceRunSequence { get; }

    internal EchoSignatureActionResult(bool available, ShadowAction action,
        int count, int laneBeforeAction, int sourceRunSequence)
    {
        this.available = available;
        this.action = action;
        this.count = count;
        this.laneBeforeAction = laneBeforeAction;
        this.sourceRunSequence = sourceRunSequence;
    }
}

public static class EchoSignatureActionParser
{
    public const int MinimumSignatureActionCount = 2;

    private const string PlayerActionEventType = "player_action";

    // The array order is the stable tie-break order.
    private static readonly ShadowAction[] ActionPriority =
    {
        ShadowAction.Jump,
        ShadowAction.Slide,
        ShadowAction.Left,
        ShadowAction.Right
    };

    public static EchoSignatureActionResult FromJson(string telemetryJson,
        ActiveEchoIdentity activeIdentity)
    {
        return FromTelemetry(AIRunTelemetry.FromJson(telemetryJson),
            activeIdentity);
    }

    public static EchoSignatureActionResult FromTelemetry(
        AIRunTelemetryData telemetry, ActiveEchoIdentity activeIdentity)
    {
        if (telemetry == null || activeIdentity == null
            || !AIRunTelemetry.IsCompletedTrainingRun(telemetry)
            || telemetry.runSequence != activeIdentity.sourceRunSequence)
            return EchoSignatureActionResult.Unavailable;

        List<AIRunEventSample> events = telemetry.events;
        if (events == null || events.Count == 0)
            return EchoSignatureActionResult.Unavailable;

        var counts = new int[AIShadowPolicy.ActionCount];
        var representativeLanes = new int[AIShadowPolicy.ActionCount];
        var hasRepresentativeLane = new bool[AIShadowPolicy.ActionCount];

        for (int i = 0; i < events.Count; i++)
        {
            AIRunEventSample sample = events[i];
            if (sample == null
                || !string.Equals(sample.type, PlayerActionEventType,
                    StringComparison.Ordinal)
                || !IsReplayableAction(sample.action)
                || sample.lane < 0 || sample.lane > 2)
                continue;

            int actionIndex = sample.action;
            counts[actionIndex]++;
            if (hasRepresentativeLane[actionIndex]) continue;

            // Keep the earliest concrete sample so the replay lane always
            // comes from the same recorded action that won the count.
            representativeLanes[actionIndex] = sample.lane;
            hasRepresentativeLane[actionIndex] = true;
        }

        ShadowAction selectedAction = ShadowAction.Keep;
        int selectedCount = 0;
        for (int i = 0; i < ActionPriority.Length; i++)
        {
            ShadowAction candidate = ActionPriority[i];
            int candidateCount = counts[(int)candidate];
            if (candidateCount <= selectedCount) continue;
            selectedAction = candidate;
            selectedCount = candidateCount;
        }

        if (selectedCount < MinimumSignatureActionCount)
            return EchoSignatureActionResult.Unavailable;

        int selectedIndex = (int)selectedAction;
        return new EchoSignatureActionResult(true, selectedAction,
            selectedCount, representativeLanes[selectedIndex],
            telemetry.runSequence);
    }

    private static bool IsReplayableAction(int action)
    {
        return action == (int)ShadowAction.Left
               || action == (int)ShadowAction.Right
               || action == (int)ShadowAction.Jump
               || action == (int)ShadowAction.Slide;
    }
}
