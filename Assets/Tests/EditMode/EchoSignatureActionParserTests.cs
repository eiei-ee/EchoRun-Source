using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EchoSignatureActionParserTests
{
    [Test]
    public void CompletedSourceRunReturnsMostFrequentPlayerAction()
    {
        AIRunTelemetryData telemetry = Telemetry(37,
            Event("player_action", ShadowAction.Left, 0),
            Event("player_action", ShadowAction.Jump, 2),
            Event("player_action", ShadowAction.Left, 1));

        EchoSignatureActionResult result = EchoSignatureActionParser.FromJson(
            JsonUtility.ToJson(telemetry), Identity(37));

        Assert.IsTrue(result.available);
        Assert.AreEqual(ShadowAction.Left, result.action);
        Assert.AreEqual(2, result.count);
        Assert.AreEqual(0, result.laneBeforeAction,
            "The earliest event for the winning action is the replay sample.");
        Assert.AreEqual(37, result.sourceRunSequence);
    }

    [Test]
    public void EqualCountsUseJumpSlideLeftRightPriority()
    {
        EchoSignatureActionResult allActions = Parse(8,
            Event("player_action", ShadowAction.Right, 2),
            Event("player_action", ShadowAction.Right, 1),
            Event("player_action", ShadowAction.Left, 0),
            Event("player_action", ShadowAction.Left, 1),
            Event("player_action", ShadowAction.Slide, 1),
            Event("player_action", ShadowAction.Slide, 2),
            Event("player_action", ShadowAction.Jump, 2),
            Event("player_action", ShadowAction.Jump, 0));
        EchoSignatureActionResult withoutJump = Parse(8,
            Event("player_action", ShadowAction.Right, 2),
            Event("player_action", ShadowAction.Right, 1),
            Event("player_action", ShadowAction.Left, 0),
            Event("player_action", ShadowAction.Left, 1),
            Event("player_action", ShadowAction.Slide, 1),
            Event("player_action", ShadowAction.Slide, 2));
        EchoSignatureActionResult lanesOnly = Parse(8,
            Event("player_action", ShadowAction.Right, 2),
            Event("player_action", ShadowAction.Right, 1),
            Event("player_action", ShadowAction.Left, 0),
            Event("player_action", ShadowAction.Left, 1));

        Assert.AreEqual(ShadowAction.Jump, allActions.action);
        Assert.AreEqual(2, allActions.count);
        Assert.AreEqual(2, allActions.laneBeforeAction);
        Assert.AreEqual(ShadowAction.Slide, withoutJump.action);
        Assert.AreEqual(ShadowAction.Left, lanesOnly.action);
    }

    [Test]
    public void SingleOccurrenceIsNotAReplayableSignature()
    {
        EchoSignatureActionResult result = Parse(15,
            Event("player_action", ShadowAction.Jump, 1));

        AssertUnavailable(result);
    }

    [Test]
    public void ParserIgnoresKeepInvalidActionsAndOtherEventTypes()
    {
        AIRunTelemetryData telemetry = Telemetry(12,
            Event("player_action", ShadowAction.Keep, 0),
            new AIRunEventSample
            {
                type = "player_action",
                action = -1,
                lane = 1
            },
            new AIRunEventSample
            {
                type = "player_action",
                action = 99,
                lane = 2
            },
            Event("player_action", ShadowAction.Jump, 9),
            Event("player_action", ShadowAction.Jump, 9),
            Event("shadow_action", ShadowAction.Jump, 2),
            Event("player_action", ShadowAction.Slide, 1),
            Event("player_action", ShadowAction.Slide, 2));

        EchoSignatureActionResult result =
            EchoSignatureActionParser.FromTelemetry(telemetry, Identity(12));

        Assert.IsTrue(result.available);
        Assert.AreEqual(ShadowAction.Slide, result.action);
        Assert.AreEqual(2, result.count);
        Assert.AreEqual(1, result.laneBeforeAction);
    }

    [TestCase("menu", 21, 21)]
    [TestCase("restart", 21, 21)]
    [TestCase("finish_reached", 20, 21)]
    public void ParserRejectsNonFinishOrWrongIdentityLineage(string finishReason,
        int telemetrySequence, int identitySequence)
    {
        AIRunTelemetryData telemetry = Telemetry(telemetrySequence,
            Event("player_action", ShadowAction.Jump, 1),
            Event("player_action", ShadowAction.Jump, 2));
        telemetry.finishReason = finishReason;

        EchoSignatureActionResult result =
            EchoSignatureActionParser.FromTelemetry(telemetry,
                Identity(identitySequence));

        AssertUnavailable(result);
    }

    [Test]
    public void MatchingRunWithoutReplayablePlayerActionIsUnavailable()
    {
        AIRunTelemetryData telemetry = Telemetry(4,
            Event("player_action", ShadowAction.Keep, 1),
            Event("run_start", ShadowAction.Jump, 2));

        AssertUnavailable(EchoSignatureActionParser.FromTelemetry(
            telemetry, Identity(4)));
    }

    [Test]
    public void FinishReasonWithoutCompletedFlagIsUnavailable()
    {
        AIRunTelemetryData telemetry = Telemetry(6,
            Event("player_action", ShadowAction.Jump, 1),
            Event("player_action", ShadowAction.Jump, 2));
        telemetry.completed = false;

        AssertUnavailable(EchoSignatureActionParser.FromTelemetry(
            telemetry, Identity(6)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{broken-json")]
    public void EmptyOrDamagedJsonFallsBackSafely(string json)
    {
        EchoSignatureActionResult result = null;

        Assert.DoesNotThrow(() =>
            result = EchoSignatureActionParser.FromJson(json, Identity(3)));
        AssertUnavailable(result);
    }

    [Test]
    public void MissingIdentityFallsBackSafely()
    {
        AIRunTelemetryData telemetry = Telemetry(5,
            Event("player_action", ShadowAction.Right, 1),
            Event("player_action", ShadowAction.Right, 2));

        AssertUnavailable(EchoSignatureActionParser.FromTelemetry(
            telemetry, null));
    }

    private static AIRunTelemetryData Telemetry(int runSequence,
        params AIRunEventSample[] events)
    {
        return new AIRunTelemetryData
        {
            runSequence = runSequence,
            completed = true,
            finishReason = AIRunTelemetry.CompletedTrainingReason,
            events = new List<AIRunEventSample>(events)
        };
    }

    private static EchoSignatureActionResult Parse(int runSequence,
        params AIRunEventSample[] events)
    {
        return EchoSignatureActionParser.FromTelemetry(
            Telemetry(runSequence, events), Identity(runSequence));
    }

    private static AIRunEventSample Event(string type, ShadowAction action,
        int laneBeforeAction)
    {
        return new AIRunEventSample
        {
            type = type,
            action = (int)action,
            lane = laneBeforeAction
        };
    }

    private static ActiveEchoIdentity Identity(int sourceRunSequence)
    {
        return new ActiveEchoIdentity
        {
            sourceRunSequence = sourceRunSequence
        };
    }

    private static void AssertUnavailable(EchoSignatureActionResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsFalse(result.available);
        Assert.AreEqual(ShadowAction.Keep, result.action);
        Assert.AreEqual(0, result.count);
        Assert.AreEqual(-1, result.laneBeforeAction);
        Assert.AreEqual(0, result.sourceRunSequence);
    }
}
