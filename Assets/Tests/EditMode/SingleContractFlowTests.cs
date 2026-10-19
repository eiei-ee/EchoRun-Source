using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SingleContractFlowTests
{
    private GameObject _managerObject;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (_managerObject != null)
            Object.DestroyImmediate(_managerObject);
    }

    [Test]
    public void ActiveGameplayFlowSelectionIsFrozenForOneRun()
    {
        GameManager manager = CreateManager();
        var validation = new SingleContractValidationConfig
        {
            enabled = true,
            fixedSeed = 424242,
            freezeDirector = true,
            disablePowerUps = true,
            forceStandardDifficulty = true
        };
        Assert.IsTrue(manager.TryConfigureGameplayFlow(
            GameplayFlowMode.SingleContract, validation));

        FreezeConfiguration(manager);
        SetState(manager, GameState.Playing);
        validation.fixedSeed = 7;

        Assert.AreEqual(GameplayFlowMode.SingleContract,
            manager.ActiveGameplayFlowMode);
        Assert.AreEqual(424242,
            manager.ActiveSingleContractValidationConfig.fixedSeed);
        Assert.IsFalse(manager.TryConfigureGameplayFlow(
            GameplayFlowMode.SixPhaseLegacy));
        Assert.AreEqual(GameplayFlowMode.SingleContract,
            manager.ActiveGameplayFlowMode);

        SingleContractValidationConfig exposedSnapshot =
            manager.ActiveSingleContractValidationConfig;
        exposedSnapshot.fixedSeed = 99;
        Assert.AreEqual(424242,
            manager.ActiveSingleContractValidationConfig.fixedSeed);
    }

    [Test]
    public void ShippingDefaultIsSingleContractAndLegacyRequiresExplicitOptIn()
    {
        GameManager first = CreateManager();
        Assert.IsFalse(first.ConfiguredSingleContractValidationConfig.enabled,
            "A normal player launch must not silently enable the fixed validation profile.");
        Assert.IsFalse(first.ConfiguredSingleContractValidationConfig
            .useFixedIdentity);
        Assert.IsTrue(first.TryConfigureGameplayFlow(
            GameplayFlowMode.SingleContract));
        FreezeConfiguration(first);
        Assert.AreEqual(GameplayFlowMode.SingleContract,
            first.ActiveGameplayFlowMode);

        Object.DestroyImmediate(_managerObject);
        _managerObject = null;

        GameManager second = CreateManager();
        Assert.AreEqual(GameplayFlowMode.SingleContract,
            second.ConfiguredGameplayFlowMode);
        Assert.IsTrue(second.TryConfigureGameplayFlow(
            GameplayFlowMode.SixPhaseLegacy));
        FreezeConfiguration(second);

        Assert.AreEqual(GameplayFlowMode.SixPhaseLegacy,
            second.ActiveGameplayFlowMode);
    }

    [Test]
    public void ValidationLaunchOptionsRequireExplicitOptInAndValidSeed()
    {
        Assert.IsFalse(SingleContractValidationLaunchOptions.TryParse(
            new[] { "-echo-single-contract-seed=424242" },
            out SingleContractValidationConfig absent, out string noError));
        Assert.IsNull(absent);
        Assert.IsEmpty(noError);

        Assert.IsFalse(SingleContractValidationLaunchOptions.TryParse(
            new[]
            {
                "-echo-single-contract-seed=invalid",
                SingleContractValidationLaunchOptions.AutoStartArgument,
                SingleContractValidationLaunchOptions.FixedIdentityArgument
            },
            out SingleContractValidationConfig ignored,
            out string ignoredError));
        Assert.IsNull(ignored);
        Assert.IsEmpty(ignoredError);

        Assert.IsFalse(SingleContractValidationLaunchOptions.TryParse(
            new[]
            {
                SingleContractValidationLaunchOptions.EnableArgument,
                "-echo-single-contract-seed=invalid"
            }, out SingleContractValidationConfig invalid,
            out string invalidError));
        Assert.IsNull(invalid);
        Assert.IsNotEmpty(invalidError);

        Assert.IsTrue(SingleContractValidationLaunchOptions.TryParse(
            new[]
            {
                "-ECHO-SINGLE-CONTRACT-VALIDATION",
                "-echo-single-contract-seed=424242",
                "-ECHO-SINGLE-CONTRACT-AUTOSTART",
                "-ECHO-SINGLE-CONTRACT-FIXED-IDENTITY"
            }, out SingleContractValidationConfig validation,
            out string error));
        Assert.IsEmpty(error);
        Assert.IsTrue(validation.enabled);
        Assert.AreEqual(424242, validation.fixedSeed);
        Assert.IsTrue(validation.freezeDirector);
        Assert.IsTrue(validation.disablePowerUps);
        Assert.IsTrue(validation.forceStandardDifficulty);
        Assert.IsTrue(validation.autoStart);
        Assert.IsTrue(validation.useFixedIdentity);
    }

    [Test]
    public void GameManagerAppliesValidationLaunchWithoutChangingShippingDefault()
    {
        GameManager manager = CreateManager();
        Assert.IsFalse(manager.ConfiguredSingleContractValidationConfig.enabled);

        MethodInfo method = typeof(GameManager).GetMethod(
            "ApplySingleContractValidationLaunchOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(manager, new object[]
        {
            new[]
            {
                SingleContractValidationLaunchOptions.EnableArgument,
                "-echo-single-contract-seed=424242",
                SingleContractValidationLaunchOptions.AutoStartArgument,
                SingleContractValidationLaunchOptions.FixedIdentityArgument
            }
        });

        SingleContractValidationConfig configured =
            manager.ConfiguredSingleContractValidationConfig;
        Assert.AreEqual(GameplayFlowMode.SingleContract,
            manager.ConfiguredGameplayFlowMode);
        Assert.IsTrue(configured.enabled);
        Assert.AreEqual(424242, configured.fixedSeed);
        Assert.IsTrue(configured.freezeDirector);
        Assert.IsTrue(configured.disablePowerUps);
        Assert.IsTrue(configured.forceStandardDifficulty);
        Assert.IsTrue(configured.autoStart);
        Assert.IsTrue(configured.useFixedIdentity);
    }

    private GameManager CreateManager()
    {
        _managerObject = new GameObject("GameManager_ModeTest");
        return _managerObject.AddComponent<GameManager>();
    }

    private static void FreezeConfiguration(GameManager manager)
    {
        MethodInfo method = typeof(GameManager).GetMethod(
            "FreezeGameplayFlowConfiguration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(manager, null);
    }

    private static void SetState(GameManager manager, GameState state)
    {
        FieldInfo field = typeof(GameManager).GetField(
            "<State>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(manager, state);
    }
}
