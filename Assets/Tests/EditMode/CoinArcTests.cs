using NUnit.Framework;
using UnityEngine;

public class CoinArcTests
{
    [TestCase(2.4f)]
    [TestCase(3f)]
    public void JumpRewardCoinsFollowThePlayerJumpHeight(float jumpHeight)
    {
        const float groundCoinHeight = 1f;

        Assert.AreEqual(groundCoinHeight,
            TrackSpawnRules.JumpCoinHeight(
                0f, groundCoinHeight, jumpHeight), 0.0001f);
        Assert.AreEqual(groundCoinHeight + jumpHeight,
            TrackSpawnRules.JumpCoinHeight(
                0.5f, groundCoinHeight, jumpHeight), 0.0001f);
        Assert.AreEqual(groundCoinHeight,
            TrackSpawnRules.JumpCoinHeight(
                1f, groundCoinHeight, jumpHeight), 0.0001f);
    }

    [Test]
    public void CoinTrailOverlapChecksTheWholeTrailInsteadOfOnlyItsStart()
    {
        Assert.IsTrue(TrackSpawnRules.CoinTrailOverlapsObstacle(
            6f, 7, 1.8f, 11f, 0.35f),
            "An obstacle inside the later half of a coin trail must be detected.");
        Assert.IsFalse(TrackSpawnRules.CoinTrailOverlapsObstacle(
            6f, 7, 1.8f, 19f, 0.35f),
            "A distant obstacle must not convert an unrelated coin trail.");
    }

    [Test]
    public void JumpRewardArcFitsCompletelyInsideTheStraightSegment()
    {
        float center = TrackSpawnRules.ClampJumpRewardCenter(
            2f, 20f, TrackSpawnRules.JumpRewardCoinCount,
            TrackSpawnRules.CoinSpacing,
            TrackSpawnRules.CoinSegmentMargin);
        float halfSpan = (TrackSpawnRules.JumpRewardCoinCount - 1)
                         * TrackSpawnRules.CoinSpacing * 0.5f;

        Assert.GreaterOrEqual(center - halfSpan,
            TrackSpawnRules.CoinSegmentMargin);
        Assert.LessOrEqual(center + halfSpan,
            20f - TrackSpawnRules.CoinSegmentMargin);
        Assert.AreEqual(6.4f, center, 0.0001f);
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void TurnGuideCoinsFormOneEvenlySpacedPathThroughTheCorner(
        int turnDirection)
    {
        Vector3 previous = TrackSpawnRules.TurnGuideCoinLocalPosition(
            20f, turnDirection, 0);
        Assert.AreEqual(0f, previous.x, 0.0001f);

        for (int index = 1;
             index < TrackSpawnRules.TurnGuideCoinCount; index++)
        {
            Vector3 current = TrackSpawnRules.TurnGuideCoinLocalPosition(
                20f, turnDirection, index);
            Assert.AreEqual(TrackSpawnRules.CoinSpacing,
                Vector3.Distance(previous, current), 0.0001f,
                "Turn guide coins must not bunch up around the corner.");
            previous = current;
        }

        Vector3 corner = TrackSpawnRules.TurnGuideCoinLocalPosition(
            20f, turnDirection, TrackSpawnRules.TurnGuideCoinsPerArm);
        Vector3 exit = TrackSpawnRules.TurnGuideCoinLocalPosition(
            20f, turnDirection, TrackSpawnRules.TurnGuideCoinCount - 1);
        Assert.AreEqual(new Vector3(0f, TrackSpawnRules.GroundCoinHeight, 10f),
            corner);
        Assert.AreEqual(turnDirection * TrackSpawnRules.CoinSpacing
                        * TrackSpawnRules.TurnGuideCoinsPerArm,
            exit.x, 0.0001f);
        Assert.AreEqual(10f, exit.z, 0.0001f);
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void TurnGuideCoinRootsFollowTheRouteTangent(int turnDirection)
    {
        Quaternion segmentRotation = Quaternion.Euler(0f, 90f, 0f);
        for (int index = 0;
             index < TrackSpawnRules.TurnGuideCoinCount; index++)
        {
            Vector3 localTangent = TrackSpawnRules.TurnGuideCoinLocalTangent(
                20f, turnDirection, index);
            Quaternion routeRotation = TrackSpawnRules.CoinRouteRotation(
                segmentRotation, localTangent);
            Vector3 expectedLocalTangent = index
                < TrackSpawnRules.TurnGuideCoinsPerArm
                ? Vector3.forward
                : index == TrackSpawnRules.TurnGuideCoinsPerArm
                    ? new Vector3(turnDirection, 0f, 1f).normalized
                    : new Vector3(turnDirection, 0f, 0f);
            Vector3 expectedWorldTangent =
                segmentRotation * expectedLocalTangent;

            Assert.Less(Vector3.Angle(localTangent, expectedLocalTangent),
                0.01f, "The authored turn tangent is reversed or misplaced.");
            Assert.Less(Vector3.Angle(routeRotation * Vector3.forward,
                    expectedWorldTangent), 0.01f,
                "The root trigger width must stay across the route after a turn.");
        }
    }
}
