using NUnit.Framework;

public sealed class ScopeClosurePresentationTests
{
    [Test]
    public void MemoryDescribesRecordedChoicesWithoutInventingPressureContext()
    {
        var memory = new EchoMemoryContract { preferredLane = 0, evidenceCount = 3, confidence = .6f };
        Assert.AreEqual("选路记录中，你更常选择左侧", memory.BuildMemoryText());
    }

    [Test]
    public void FormationSummaryUsesActualChoiceCountsAndLeavesSamplingCountersInDetails()
    {
        var progress = new SingleContractCalibrationProgress {
            formalChoices = 7, strongestRouteChoices = 4,
            preferredLane = 2, preferredLaneUnique = true };
        string evidence = EchoRunPresentation.BuildPlayerRouteEvidence(progress);
        Assert.AreEqual("本局 7 次选路中，有 4 次选择了右路", evidence);
        string full = "第2代回声已经形成\n它记住了：选路记录中，你更常选择右侧\n"
                      + evidence + "\n观察 81/24 · 主动 53/6";
        string summary = EchoRunPresentation.BuildSingleContractResultSummary(full);
        StringAssert.Contains(evidence, summary);
        StringAssert.Contains("第2代回声已经形成", summary);
        StringAssert.DoesNotContain("81/24", summary);
        StringAssert.Contains("81/24", EchoRunPresentation.BuildSingleContractResultDetails(full, ""));
    }

    [Test]
    public void TiedOrEmptyEvidenceDoesNotInventLanePreference()
    {
        StringAssert.Contains("尚无唯一偏向", EchoRunPresentation.BuildPlayerRouteEvidence(
            new SingleContractCalibrationProgress { formalChoices = 4 }));
        StringAssert.Contains("没有可用", EchoRunPresentation.BuildPlayerRouteEvidence(default));
    }
}
