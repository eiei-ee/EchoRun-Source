using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public sealed class EchoExternalIdentityTests
{
    [Test]
    public void ExistingIdentityRoundTripsWithoutMutatingSource()
    {
        ActiveEchoIdentity source = SingleContractValidationIdentity.Create();
        string json = source.ToJson();
        ActiveEchoIdentity localRoundTrip = ActiveEchoIdentity.FromJson(json);
        Assert.AreEqual(json, localRoundTrip.ToJson());
        Assert.IsTrue(ActiveEchoIdentity.TryFromExternalJson(json,
            out ActiveEchoIdentity imported, out string error), error);
        Assert.AreEqual(json, imported.ToJson());
        imported.policyWeights[0] += 0.25f;
        imported.sequenceTransitions[0] += 1;
        imported.memoryContract.preferredLane = 0;
        Assert.AreEqual(json, source.ToJson());
    }

    [TestCase("{\"version\":1", "{\"version\":2")]
    [TestCase("\"style\":{\"version\":3", "\"style\":{\"version\":4")]
    [TestCase("\"memoryContract\":{\"version\":1", "\"memoryContract\":{\"version\":2")]
    public void FutureVersionsAreRejectedBeforeNormalization(string from, string to)
    {
        string json = ValidJson();
        StringAssert.Contains(from, json);
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(json.Replace(from, to),
            out ActiveEchoIdentity result, out string error));
        Assert.IsNull(result);
        Assert.AreEqual("PAYLOAD_VERSION_UNSUPPORTED", error);
    }

    [TestCase("{\"version\":1,", "{")]
    [TestCase("\"generation\":1", "\"generation\":1.5")]
    [TestCase("\"generation\":1", "\"generation\":2147483648")]
    [TestCase("\"generation\":1", "\"generation\":\"1\"")]
    [TestCase("\"generation\":1", "\"generation\":1,\"generation\":2")]
    [TestCase("\"sequencePairCount\":0", "\"sequencePairCount\":-1")]
    public void MissingWrongTypedAndDuplicateFieldsAreRejected(string from, string to)
    {
        string json = ValidJson();
        StringAssert.Contains(from, json);
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(json.Replace(from, to),
            out ActiveEchoIdentity result, out _));
        Assert.IsNull(result);
    }

    [Test]
    public void SizeLimitUsesUtf8BytesAndIncludesWhitespace()
    {
        string json = ValidJson();
        string exact = json + new string(' ', EchoIdentityExternalPayload.MaximumBytes
            - Encoding.UTF8.GetByteCount(json));
        Assert.IsTrue(ActiveEchoIdentity.TryFromExternalJson(exact, out _, out string error), error);
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(exact + " ", out _, out error));
        Assert.AreEqual("PAYLOAD_TOO_LARGE", error);
        string multibyte = json.Substring(0, json.Length - 1)
                           + ",\"unused\":\"" + new string('影', 6000) + "\"}";
        Assert.Less(multibyte.Length, EchoIdentityExternalPayload.MaximumBytes);
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(multibyte, out _, out error));
        Assert.AreEqual("PAYLOAD_TOO_LARGE", error);
    }

    [Test]
    public void CorruptModelCannotSilentlyBecomeDefaultOpponent()
    {
        ActiveEchoIdentity value = SingleContractValidationIdentity.Create();
        value.policyWeights = new[] { 1f };
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.sequenceTransitions[0] = -1;
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.policyWeights[0] = 5;
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.policyWeights[0] = float.NaN;
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.memoryContract.identityId = "another-owner";
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.style.jumpActionSamples = 2;
        value.style.verticalActionSamples = 1;
        Assert.IsFalse(ReadRaw(value));
        value = SingleContractValidationIdentity.Create();
        value.style.jumpActionSamples = int.MaxValue;
        value.style.slideActionSamples = 1;
        Assert.IsFalse(ReadRaw(value));
    }

    [Test]
    public void ExactConfidenceThresholdIsChallengeReady()
    {
        ActiveEchoIdentity value = SingleContractValidationIdentity.Create();
        value.memoryContract.confidence = EchoMemoryContract.PreciseDescriptionConfidence;
        value.memoryContract.evidenceCount = 3;
        Assert.IsTrue(ReadRaw(value));
        value.memoryContract.evidenceCount = 2;
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(JsonUtility.ToJson(value),
            out _, out string error));
        Assert.AreEqual("IDENTITY_NOT_CHALLENGE_READY", error);
    }

    [Test]
    public void LocalCompatibilityReaderKeepsExistingBehavior()
    {
        string future = ValidJson().Replace("{\"version\":1", "{\"version\":99");
        Assert.IsNotNull(ActiveEchoIdentity.FromJson(future));
        Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(future, out _, out _));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("{bad}")]
    [TestCase("{\"version\":1} trailing")]
    public void MalformedInputNeverThrows(string json)
    {
        Assert.DoesNotThrow(() =>
            Assert.IsFalse(ActiveEchoIdentity.TryFromExternalJson(json, out _, out _)));
    }

    [TestCase(RunEndReason.FinishReached, true, "你跑赢了好友影子")]
    [TestCase(RunEndReason.FinishReached, false, "好友影子领先")]
    [TestCase(RunEndReason.Collision, false, "好友挑战中断")]
    public void AsyncResultTitleNeverClaimsPromotion(RunEndReason reason, bool won, string expected)
    {
        Assert.AreEqual(expected, UIManager.GetAsyncChallengeGameOverTitle(reason, won));
    }

    private static bool ReadRaw(ActiveEchoIdentity value)
    {
        return ActiveEchoIdentity.TryFromExternalJson(JsonUtility.ToJson(value), out _, out _);
    }

    private static string ValidJson() => SingleContractValidationIdentity.Create().ToJson();
}
