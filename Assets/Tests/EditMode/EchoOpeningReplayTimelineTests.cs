using NUnit.Framework;

public sealed class EchoOpeningReplayTimelineTests
{
    [Test]
    public void LateralSignatureReplaysFromRecordedLaneThenReturnsToRuntimeLane()
    {
        float beforeAction = AIShadowRunner.ResolveSingleContractOpeningReplayLane(
            ShadowAction.Left, 2, 0.3f, 0);
        float afterAction = AIShadowRunner.ResolveSingleContractOpeningReplayLane(
            ShadowAction.Left, 2,
            AIShadowRunner.SingleContractOpeningReplayActionSeconds + 0.42f,
            0);
        float settled = AIShadowRunner.ResolveSingleContractOpeningReplayLane(
            ShadowAction.Left, 2,
            AIShadowRunner.SingleContractOpeningReplaySettleSeconds,
            0);

        Assert.AreEqual(2f, beforeAction, 0.0001f);
        Assert.AreEqual(1f, afterAction, 0.0001f);
        Assert.AreEqual(0f, settled, 0.0001f);
    }

    [Test]
    public void VerticalSignatureKeepsRecordedLaneUntilReturnWindow()
    {
        float duringAction = AIShadowRunner.ResolveSingleContractOpeningReplayLane(
            ShadowAction.Jump, 2, 1f, 0);
        float settled = AIShadowRunner.ResolveSingleContractOpeningReplayLane(
            ShadowAction.Jump, 2,
            AIShadowRunner.SingleContractOpeningReplaySettleSeconds,
            0);

        Assert.AreEqual(2f, duringAction, 0.0001f);
        Assert.AreEqual(0f, settled, 0.0001f);
    }

    [Test]
    public void EntranceGapReturnsToLiveRaceGapBeforeOpeningEnds()
    {
        const float liveGap = -1.25f;
        float held = AIShadowRunner.ResolveSingleContractOpeningReplayGap(
            1f, liveGap);
        float settled = AIShadowRunner.ResolveSingleContractOpeningReplayGap(
            AIShadowRunner.SingleContractOpeningReplaySettleSeconds,
            liveGap);

        Assert.AreEqual(3.2f, held, 0.0001f);
        Assert.AreEqual(liveGap, settled, 0.0001f);
        Assert.Less(
            AIShadowRunner.SingleContractOpeningReplaySettleSeconds,
            SingleContractFlow.OpeningMemoryDurationSeconds);
    }

    [Test]
    public void LateFrameStartsOnlyTheRemainingActionAndNeverReplaysPastEnd()
    {
        const float duration = 0.8f;
        float onTime = AIShadowRunner
            .CalculateSingleContractOpeningReplayActionRemaining(
                AIShadowRunner.SingleContractOpeningReplayActionSeconds,
                duration);
        float partial = AIShadowRunner
            .CalculateSingleContractOpeningReplayActionRemaining(
                AIShadowRunner.SingleContractOpeningReplayActionSeconds + 0.6f,
                duration);
        float skipped = AIShadowRunner
            .CalculateSingleContractOpeningReplayActionRemaining(
                AIShadowRunner.SingleContractOpeningReplayActionSeconds + 1.2f,
                duration);

        Assert.AreEqual(0.8f, onTime, 0.0001f);
        Assert.AreEqual(0.2f, partial, 0.0001f);
        Assert.AreEqual(0f, skipped, 0.0001f);
    }
}
