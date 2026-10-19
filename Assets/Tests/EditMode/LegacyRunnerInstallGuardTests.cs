using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LegacyRunnerInstallGuardTests
{
    private const string EntryPoint = "InstallEchoRunnerPhaseOne.Install";

    [TestCase("-allowLegacyRunnerInstall", false)]
    [TestCase("-allowLegacyRunnerInstall=*", false)]
    [TestCase("-allowLegacyRunnerInstall=RunnerSilhouetteRefinement.Restore", false)]
    [TestCase("-allowLegacyRunnerInstall=InstallEchoRunnerPhaseOne.Install", true)]
    public void ReconstructionOptInIsScopedToOneExactInstaller(string option, bool allowed)
    {
        Type guard = Type.GetType("LegacyRunnerInstallGuard, TempleRun.Editor", true);
        MethodInfo policy = guard.GetMethod("HasExactOptIn", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(policy);
        Assert.AreEqual(allowed, policy.Invoke(null, new object[] { new[] { "-batchmode", option }, EntryPoint }));
    }

    [Test]
    public void BatchGuardRejectsWithoutExplicitOptInBeforeRunningAnyInstaller()
    {
        if (!Application.isBatchMode)
            Assert.Ignore("The batch rejection path is exercised by the batch EditMode gate.");
        string[] args = Environment.GetCommandLineArgs();
        Assert.IsFalse(Array.Exists(args, a => a == "-allowLegacyRunnerInstall=" + EntryPoint),
            "Do not run the test suite with a legacy reconstruction opt-in.");
        Type guard = Type.GetType("LegacyRunnerInstallGuard, TempleRun.Editor", true);
        MethodInfo allow = guard.GetMethod("Allow", BindingFlags.Public | BindingFlags.Static);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
            allow.Invoke(null, new object[] { EntryPoint }));
        Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
        StringAssert.Contains("No changes were made", error.InnerException.Message);
        StringAssert.Contains("-allowLegacyRunnerInstall=" + EntryPoint, error.InnerException.Message);
    }
}
