using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class P0InvisibleExperienceTests
{
    private readonly Dictionary<string, string> _stringValues =
        new Dictionary<string, string>();
    private readonly Dictionary<string, int> _intValues =
        new Dictionary<string, int>();
    private readonly Dictionary<string, float> _floatValues =
        new Dictionary<string, float>();

    private static readonly string[] StringKeys =
    {
        EchoRunSaveSystem.SaveKey,
        EchoRunSaveSystem.SaveSlotAKey,
        EchoRunSaveSystem.SaveSlotBKey,
        EchoRunSaveSystem.TelemetryKey,
        "AIShadowProfileV1"
    };

    private static readonly string[] IntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey,
        "HighScore",
        "TotalCoins",
        "TargetFrameRate",
        "AudioMuted",
        "CharacterPreset"
    };

    private static readonly string[] FloatKeys =
    {
        "MasterVolume",
        "MusicVolume",
        "SfxVolume"
    };

    [SetUp]
    public void SetUp()
    {
        CapturePreferences();
        ClearPreferences();
    }

    [TearDown]
    public void TearDown()
    {
        ClearPreferences();
        foreach (KeyValuePair<string, string> pair in _stringValues)
            PlayerPrefs.SetString(pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in _intValues)
            PlayerPrefs.SetInt(pair.Key, pair.Value);
        foreach (KeyValuePair<string, float> pair in _floatValues)
            PlayerPrefs.SetFloat(pair.Key, pair.Value);
        PlayerPrefs.Save();
        ResetSaveSystemCache();
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void EarlyVerticalIntentExecutesWithinTheSameWindowAtEachFrameRate(
        int frameRate)
    {
        var buffer = new InputIntentBuffer();
        BufferedSwipeCommand queued = buffer.Enqueue(
            SwipeDirection.Up, InputIntentSource.Touch, 0f, out _);
        float frameStep = 1f / frameRate;
        const float groundedAt = 0.075f;
        float executedAt = -1f;

        for (float now = frameStep;
             now <= InputIntentBuffer.VerticalLifetime + frameStep;
             now += frameStep)
        {
            Assert.IsFalse(buffer.TryPopExpired(now, out _),
                "The command expired before its configured grace window.");
            if (now < groundedAt) continue;
            Assert.IsTrue(buffer.TryPeek(out BufferedSwipeCommand pending));
            Assert.AreEqual(queued.sequence, pending.sequence);
            Assert.IsTrue(buffer.TryResolveHead(pending.sequence, out _));
            executedAt = now;
            break;
        }

        Assert.GreaterOrEqual(executedAt, groundedAt);
        Assert.LessOrEqual(executedAt,
            InputIntentBuffer.VerticalLifetime + frameStep);
        Assert.AreEqual(0, buffer.Count);
    }

    [Test]
    public void ExpiredIntentCannotReplayLater()
    {
        var buffer = new InputIntentBuffer();
        BufferedSwipeCommand queued = buffer.Enqueue(
            SwipeDirection.Down, InputIntentSource.Keyboard, 4f, out _);

        Assert.IsFalse(buffer.TryPopExpired(queued.expiresAt, out _));
        Assert.IsTrue(buffer.TryPopExpired(
            queued.expiresAt + 0.001f, out BufferedSwipeCommand expired));
        Assert.AreEqual(queued.sequence, expired.sequence);
        Assert.IsFalse(buffer.TryPeek(out _));
    }

    [Test]
    public void DeferredVerticalIntentDoesNotBlockFollowingLaneChange()
    {
        var buffer = new InputIntentBuffer();
        BufferedSwipeCommand vertical = buffer.Enqueue(
            SwipeDirection.Up, InputIntentSource.Touch, 1f, out _);
        BufferedSwipeCommand laneChange = buffer.Enqueue(
            SwipeDirection.Left, InputIntentSource.Touch, 1.01f, out _);

        Assert.IsTrue(buffer.TryDeferHead(vertical.sequence));
        Assert.IsTrue(buffer.TryPeek(out BufferedSwipeCommand next));
        Assert.AreEqual(laneChange.sequence, next.sequence);
    }

    [Test]
    public void CorruptNewestSaveFallsBackToPreviousVerifiedSlot()
    {
        var legacy = new EchoRunSaveData
        {
            highScore = 110,
            totalCoins = 33,
            directorWeights = new[] { 0.1f, 0.2f, 0.3f },
            directorModelUpdateCount = 4
        };
        PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey,
            JsonUtility.ToJson(legacy));
        ResetSaveSystemCache();
        EchoRunSaveSystem.EnsureInitialized();

        EchoRunSaveSystem.SaveProgress(220, 45);
        string activeKey = GetActiveSlotKey();
        PlayerPrefs.SetString(activeKey, "{\"schemaVersion\":1");
        PlayerPrefs.Save();
        ResetSaveSystemCache();

        EchoRunSaveSystem.EnsureInitialized();

        Assert.IsTrue(EchoRunSaveSystem.RecoveredFromBackup);
        Assert.AreEqual(110, PlayerPrefs.GetInt("HighScore"));
        Assert.AreEqual(33, EchoRunSaveSystem.TotalCoins);
        Assert.AreEqual(4, EchoRunSaveSystem.DirectorModelUpdateCount);
        CollectionAssert.AreEqual(legacy.directorWeights,
            EchoRunSaveSystem.GetDirectorWeights());
    }

    [Test]
    public void ChecksumMismatchCannotBecomeTheActiveSave()
    {
        PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey,
            JsonUtility.ToJson(new EchoRunSaveData
            {
                highScore = 10,
                totalCoins = 5
            }));
        ResetSaveSystemCache();
        EchoRunSaveSystem.EnsureInitialized();
        EchoRunSaveSystem.SaveProgress(20, 9);

        string activeKey = GetActiveSlotKey();
        EchoRunSaveEnvelope envelope = JsonUtility.FromJson<EchoRunSaveEnvelope>(
            PlayerPrefs.GetString(activeKey));
        StringAssert.Contains("\"highScore\":20", envelope.payload);
        envelope.payload = envelope.payload.Replace(
            "\"highScore\":20", "\"highScore\":9999");
        PlayerPrefs.SetString(activeKey, JsonUtility.ToJson(envelope));
        PlayerPrefs.Save();
        ResetSaveSystemCache();

        EchoRunSaveSystem.EnsureInitialized();

        Assert.AreEqual(10, PlayerPrefs.GetInt("HighScore"));
        Assert.AreEqual(5, EchoRunSaveSystem.TotalCoins);
    }

    [Test]
    public void LegacyArchiveMigratesAndTelemetryStaysOutsideProgressSlots()
    {
        PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey,
            JsonUtility.ToJson(new EchoRunSaveData
            {
                highScore = 73,
                totalCoins = 12,
                lastRunTelemetryJson = "{\"oldTrace\":true}"
            }));
        ResetSaveSystemCache();

        EchoRunSaveSystem.EnsureInitialized();

        Assert.IsFalse(PlayerPrefs.HasKey(EchoRunSaveSystem.SaveKey));
        Assert.IsTrue(PlayerPrefs.HasKey(EchoRunSaveSystem.SaveSlotAKey)
                      || PlayerPrefs.HasKey(EchoRunSaveSystem.SaveSlotBKey));
        StringAssert.Contains("oldTrace",
            EchoRunSaveSystem.GetLastRunTelemetryJson());
        string slotBefore = PlayerPrefs.GetString(GetActiveSlotKey());

        EchoRunSaveSystem.SaveLastRunTelemetry("{\"newTrace\":true}");

        Assert.AreEqual(slotBefore,
            PlayerPrefs.GetString(GetActiveSlotKey()));
        StringAssert.Contains("newTrace",
            PlayerPrefs.GetString(EchoRunSaveSystem.TelemetryKey));
    }

    [Test]
    public void RunCapsuleRoundTripKeepsFingerprintsAndInputResolution()
    {
        float[] shadowWeights = { 0.1f, 0.2f };
        float[] directorWeights = { 0.3f, 0.4f, 0.5f };
        AIRunTelemetry.BeginRun(77, 12, 7904, 22, 48,
            shadowWeights, directorWeights, "director-state",
            "sequence-state");
        float issuedAt = Time.unscaledTime + 0.025f;
        var command = new BufferedSwipeCommand(7, SwipeDirection.Up,
            InputIntentSource.Touch, issuedAt,
            issuedAt + InputIntentBuffer.VerticalLifetime);
        AIRunTelemetry.RecordInputQueued(command);
        AIRunTelemetry.RecordInputResolved(command,
            InputIntentOutcome.Executed, 1, issuedAt + 0.05f);

        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson());

        Assert.IsNotNull(restored.runCapsule);
        Assert.AreEqual(77, restored.runCapsule.seed);
        Assert.AreEqual("0000004D-000012", restored.runCapsule.runId);
        Assert.IsNotEmpty(restored.runCapsule.balanceFingerprint);
        Assert.IsNotEmpty(restored.runCapsule.shadowModelFingerprint);
        Assert.IsNotEmpty(restored.runCapsule.directorModelFingerprint);
        Assert.AreEqual(1, restored.runCapsule.inputs.Count);
        Assert.AreEqual((int)InputIntentOutcome.Executed,
            restored.runCapsule.inputs[0].outcome);
        Assert.AreEqual(1, restored.runCapsule.inputs[0].lane);
        Assert.Greater(restored.runCapsule.inputs[0].resolvedAt,
            restored.runCapsule.inputs[0].issuedAt);
    }

    private string GetActiveSlotKey()
    {
        return PlayerPrefs.GetInt(EchoRunSaveSystem.ActiveSaveSlotKey, 0) == 0
            ? EchoRunSaveSystem.SaveSlotAKey
            : EchoRunSaveSystem.SaveSlotBKey;
    }

    private void CapturePreferences()
    {
        _stringValues.Clear();
        _intValues.Clear();
        _floatValues.Clear();
        foreach (string key in StringKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            _stringValues[key] = PlayerPrefs.GetString(key);
        }
        foreach (string key in IntKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            _intValues[key] = PlayerPrefs.GetInt(key);
        }
        foreach (string key in FloatKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            _floatValues[key] = PlayerPrefs.GetFloat(key);
        }
    }

    private void ClearPreferences()
    {
        foreach (string key in StringKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in IntKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in FloatKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        ResetSaveSystemCache();
    }

    private static void ResetSaveSystemCache()
    {
        SetStaticField("_data", null);
        SetStaticField("_initialized", false);
        SetStaticField("_activeSlot", -1);
        SetStaticField("_generation", 0L);
    }

    private static void SetStaticField(string name, object value)
    {
        FieldInfo field = typeof(EchoRunSaveSystem).GetField(
            name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing save-system field: " + name);
        field.SetValue(null, value);
    }
}
