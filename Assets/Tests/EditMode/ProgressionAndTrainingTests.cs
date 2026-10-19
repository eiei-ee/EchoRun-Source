using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ProgressionAndTrainingTests
{
    [Test]
    public void RuntimeVersionMatchesProjectVersion()
    {
        Assert.AreEqual("0.2.0-alpha.1", EchoRunVersion.Current);
        Assert.AreEqual(EchoRunVersion.Current, Application.version);
    }

    [Test]
    public void BalanceDefinesExactlyFourPurchasablePowerUps()
    {
        GameBalanceData balance = GameBalanceConfig.Current;

        Assert.IsNotNull(balance.powerUps);
        Assert.AreEqual(4, balance.powerUps.Length);
        var ids = new HashSet<string>();
        foreach (PowerUpBalance powerUp in balance.powerUps)
        {
            Assert.IsNotNull(powerUp);
            Assert.Greater(powerUp.cost, 0);
            Assert.IsTrue(ids.Add(powerUp.id), "Power-up IDs must be unique.");
        }
    }

    [Test]
    public void BalanceNormalizationRepairsUnsafeNumericValues()
    {
        var balance = new GameBalanceData
        {
            gameplay = new GameplayBalance
            {
                startSpeed = -4f,
                maxSpeed = float.NaN,
                speedIncreaseRate = float.PositiveInfinity,
                coinScore = 0,
                magnetRadius = -2f,
                calibrationCourseDistance = float.NaN,
                challengeCourseDistance = -10f
            },
            track = new TrackBalance
            {
                obstacleChance = -1f,
                coinChance = 3f,
                turnChance = float.NaN
            },
            ai = new AIBalance
            {
                shadowLearningRate = float.NaN,
                minimumTrainingSamples = 0,
                minimumActiveSamples = 99,
                directorExploration = 2f
            }
        };

        NormalizeBalance(balance);

        Assert.Greater(balance.gameplay.startSpeed, 0f);
        Assert.GreaterOrEqual(balance.gameplay.maxSpeed, balance.gameplay.startSpeed);
        Assert.Greater(balance.gameplay.speedIncreaseRate, 0f);
        Assert.Greater(balance.gameplay.coinScore, 0);
        Assert.Greater(balance.gameplay.magnetRadius, 0f);
        Assert.Greater(balance.gameplay.calibrationCourseDistance, 0f);
        Assert.GreaterOrEqual(balance.gameplay.challengeCourseDistance,
            balance.gameplay.calibrationCourseDistance);
        Assert.AreEqual(0f, balance.track.obstacleChance);
        Assert.AreEqual(1f, balance.track.coinChance);
        Assert.That(balance.track.turnChance, Is.InRange(0f, 1f));
        Assert.That(balance.ai.shadowLearningRate, Is.InRange(0.001f, 0.5f));
        Assert.Greater(balance.ai.minimumTrainingSamples, 0);
        Assert.That(balance.ai.minimumActiveSamples,
            Is.InRange(1, balance.ai.minimumTrainingSamples));
        Assert.AreEqual(1f, balance.ai.directorExploration);
    }

    [Test]
    public void BalanceNormalizationOrdersPowerUpsByStableId()
    {
        var balance = new GameBalanceData
        {
            powerUps = new[]
            {
                PowerUp("magnet", 101),
                PowerUp("turboStart", 103),
                PowerUp("shield", 102),
                PowerUp("scoreBoost", 104)
            }
        };

        NormalizeBalance(balance);

        Assert.AreEqual("shield", balance.powerUps[(int)PowerUpId.Shield].id);
        Assert.AreEqual(102, balance.powerUps[(int)PowerUpId.Shield].cost);
        Assert.AreEqual("magnet", balance.powerUps[(int)PowerUpId.Magnet].id);
        Assert.AreEqual(101, balance.powerUps[(int)PowerUpId.Magnet].cost);
        Assert.AreEqual("scoreBoost",
            balance.powerUps[(int)PowerUpId.ScoreBoost].id);
        Assert.AreEqual(104, balance.powerUps[(int)PowerUpId.ScoreBoost].cost);
        Assert.AreEqual("turboStart",
            balance.powerUps[(int)PowerUpId.TurboStart].id);
        Assert.AreEqual(103, balance.powerUps[(int)PowerUpId.TurboStart].cost);
    }

    [Test]
    public void BalanceNormalizationRepairsInvalidPowerUpFields()
    {
        var balance = new GameBalanceData
        {
            powerUps = new[]
            {
                PowerUp("shield", 0, "", "", -1f, -1f),
                PowerUp("magnet", -1, "", "", -1f, 0f),
                PowerUp("scoreBoost", -1, "", "", 0f, 0f),
                PowerUp("turboStart", -1, "", "", float.NaN, -1f)
            }
        };

        NormalizeBalance(balance);

        foreach (PowerUpBalance powerUp in balance.powerUps)
        {
            Assert.IsNotEmpty(powerUp.displayName);
            Assert.IsNotEmpty(powerUp.description);
            Assert.Greater(powerUp.cost, 0);
            Assert.Greater(powerUp.value, 0f);
        }
        Assert.AreEqual(0f, balance.powerUps[(int)PowerUpId.Shield].duration);
        Assert.Greater(balance.powerUps[(int)PowerUpId.Magnet].duration, 0f);
        Assert.Greater(balance.powerUps[(int)PowerUpId.ScoreBoost].duration, 0f);
        Assert.Greater(balance.powerUps[(int)PowerUpId.TurboStart].duration, 0f);
    }

    [Test]
    public void TrainingReportShowsBeforeAfterAndDominantAction()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "finish_reached",
            shadowGenerationAtStart = 2,
            shadowGenerationAtEnd = 3,
            directorUpdatesAtStart = 10,
            directorUpdatesAtEnd = 14,
            playerSkillAtStart = 0.42f,
            playerSkillAtEnd = 0.55f,
            shadowWeightsAtStart = new[] { 0f, 0.2f },
            shadowWeightsAtEnd = new[] { 0.2f, 0.4f },
            directorWeightsAtStart = new[] { 0f },
            directorWeightsAtEnd = new[] { 0.3f }
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Jump });
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Jump });
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Left });

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.IsNotNull(report);
        Assert.AreEqual(2, report.generationBefore);
        Assert.AreEqual(3, report.generationAfter);
        Assert.AreEqual(4, report.directorUpdatesAfter - report.directorUpdatesBefore);
        Assert.AreEqual("跳跃", report.learnedAction);
        Assert.AreEqual(0.2f, report.shadowWeightDelta, 0.0001f);
        StringAssert.Contains("本代重点学习", report.summary);
    }

    [Test]
    public void TrainingReportRejectsIncompleteRun()
    {
        Assert.IsNull(AITrainingReportBuilder.FromTelemetry(
            new AIRunTelemetryData { completed = false }));
    }

    [Test]
    public void TrainingReportOnlyCountsPlayerTrainingSamples()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "finish_reached"
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample
        {
            action = (int)ShadowAction.Jump
        });
        for (int i = 0; i < 4; i++)
        {
            telemetry.shadowSamples.Add(new AIShadowTrainingSample
            {
                action = (int)ShadowAction.Left,
                opponentDecision = true
            });
        }

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.AreEqual("跳跃", report.learnedAction);
        Assert.AreEqual(1, report.actionSamples[(int)ShadowAction.Jump]);
        Assert.AreEqual(0, report.actionSamples[(int)ShadowAction.Left]);
    }

    [Test]
    public void TrainingReportExplainsWhenNoPlayerSampleWasLearned()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "finish_reached"
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample
        {
            action = (int)ShadowAction.Slide,
            opponentDecision = true
        });

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.AreEqual("暂无有效动作", report.learnedAction);
        StringAssert.Contains("没有采集到新的玩家动作样本", report.summary);
    }

    [Test]
    public void TrainingReportDoesNotPresentPassiveKeepSamplesAsLearningFocus()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "finish_reached"
        };
        for (int i = 0; i < 20; i++)
            telemetry.shadowSamples.Add(new AIShadowTrainingSample
            {
                action = (int)ShadowAction.Keep
            });
        telemetry.shadowSamples.Add(new AIShadowTrainingSample
        {
            action = (int)ShadowAction.Slide
        });

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.AreEqual("滑铲", report.learnedAction);
    }

    [TestCase("menu")]
    [TestCase("restart")]
    public void TrainingReportRejectsAbandonedRun(string finishReason)
    {
        Assert.IsNull(AITrainingReportBuilder.FromTelemetry(
            new AIRunTelemetryData
            {
                completed = true,
                finishReason = finishReason
            }));
    }

    [Test]
    public void SkillProfileOnlyCountsCompletedRuns()
    {
        var profile = new AIPlayerSkillProfile();

        profile.RecordRunEnd(120f, false);
        Assert.AreEqual(0, profile.completedRuns);
        Assert.AreEqual(0f, profile.bestDistance);

        profile.RecordRunEnd(95f, true);
        Assert.AreEqual(1, profile.completedRuns);
        Assert.AreEqual(95f, profile.bestDistance);
    }

    [Test]
    public void SaveNormalizationClearsSelectionWithoutInventory()
    {
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo normalize = typeof(EchoRunSaveSystem).GetMethod(
            "Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = dataField.GetValue(null);
        try
        {
            dataField.SetValue(null, new EchoRunSaveData
            {
                powerUpInventory = new[] { 0, 0, 0, 0 },
                selectedPowerUp = 2
            });

            normalize.Invoke(null, null);

            var normalized = (EchoRunSaveData)dataField.GetValue(null);
            Assert.AreEqual(-1, normalized.selectedPowerUp);
        }
        finally
        {
            dataField.SetValue(null, previous);
        }
    }

    [Test]
    public void LegacySaveDefaultsToStandardRunDifficulty()
    {
        bool hadPreference = PlayerPrefs.HasKey(
            RunDifficultySettings.PreferenceKey);
        int previousPreference = PlayerPrefs.GetInt(
            RunDifficultySettings.PreferenceKey, 0);
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo normalize = typeof(EchoRunSaveSystem).GetMethod(
            "Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = dataField.GetValue(null);
        try
        {
            PlayerPrefs.DeleteKey(RunDifficultySettings.PreferenceKey);
            dataField.SetValue(null, new EchoRunSaveData
            {
                version = 7,
                runDifficulty = (int)RunDifficultyLevel.Relaxed
            });

            normalize.Invoke(null, null);

            var normalized = (EchoRunSaveData)dataField.GetValue(null);
            Assert.AreEqual(EchoRunSaveSystem.CurrentVersion,
                normalized.version);
            Assert.AreEqual((int)RunDifficultyLevel.Standard,
                normalized.runDifficulty);
        }
        finally
        {
            if (hadPreference)
                PlayerPrefs.SetInt(RunDifficultySettings.PreferenceKey,
                    previousPreference);
            else PlayerPrefs.DeleteKey(RunDifficultySettings.PreferenceKey);
            dataField.SetValue(null, previous);
        }
    }

    [Test]
    public void LegacyAudioSaveDefaultsToFullMasterVolumeAndUnmuted()
    {
        bool hadMaster = PlayerPrefs.HasKey("MasterVolume");
        float previousMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bool hadMuted = PlayerPrefs.HasKey("AudioMuted");
        int previousMuted = PlayerPrefs.GetInt("AudioMuted", 0);
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo normalize = typeof(EchoRunSaveSystem).GetMethod(
            "Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = dataField.GetValue(null);
        try
        {
            PlayerPrefs.DeleteKey("MasterVolume");
            PlayerPrefs.DeleteKey("AudioMuted");
            dataField.SetValue(null, new EchoRunSaveData
            {
                version = 8,
                masterVolume = 0f,
                audioMuted = true
            });

            normalize.Invoke(null, null);

            var normalized = (EchoRunSaveData)dataField.GetValue(null);
            Assert.AreEqual(EchoRunSaveSystem.CurrentVersion,
                normalized.version);
            Assert.AreEqual(1f, normalized.masterVolume);
            Assert.IsFalse(normalized.audioMuted);
        }
        finally
        {
            if (hadMaster) PlayerPrefs.SetFloat("MasterVolume", previousMaster);
            else PlayerPrefs.DeleteKey("MasterVolume");
            if (hadMuted) PlayerPrefs.SetInt("AudioMuted", previousMuted);
            else PlayerPrefs.DeleteKey("AudioMuted");
            dataField.SetValue(null, previous);
        }
    }

    [Test]
    public void RestoringEmptyArchiveRemovesStaleLegacyShadowProfile()
    {
        const string legacyKey = "AIShadowProfileV1";
        bool hadValue = PlayerPrefs.HasKey(legacyKey);
        string previousValue = PlayerPrefs.GetString(legacyKey, "");
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo restore = typeof(EchoRunSaveSystem).GetMethod(
            "RestoreLegacyKeys", BindingFlags.Static | BindingFlags.NonPublic);
        object previousData = dataField.GetValue(null);
        try
        {
            PlayerPrefs.SetString(legacyKey, "stale-training-data");
            dataField.SetValue(null, new EchoRunSaveData { shadowProfileJson = "" });

            restore.Invoke(null, null);

            Assert.IsFalse(PlayerPrefs.HasKey(legacyKey));
        }
        finally
        {
            if (hadValue) PlayerPrefs.SetString(legacyKey, previousValue);
            else PlayerPrefs.DeleteKey(legacyKey);
            dataField.SetValue(null, previousData);
        }
    }

    private static void NormalizeBalance(GameBalanceData balance)
    {
        MethodInfo method = typeof(GameBalanceConfig).GetMethod(
            "Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "GameBalanceConfig.Normalize is missing.");
        method.Invoke(null, new object[] { balance });
    }

    private static PowerUpBalance PowerUp(string id, int cost,
        string displayName = "测试道具", string description = "测试描述",
        float duration = 10f, float value = 1f)
    {
        return new PowerUpBalance
        {
            id = id,
            displayName = displayName,
            description = description,
            cost = cost,
            duration = duration,
            value = value
        };
    }
}
