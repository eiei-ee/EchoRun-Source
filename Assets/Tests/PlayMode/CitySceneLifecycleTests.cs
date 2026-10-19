using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CitySceneLifecycleTests
{
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator RestartAndReturnToMenuRecreateAllThreeCityLayers()
    {
        using (var save = new SaveSnapshot())
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            AssertCity();
            Assert.That(GameManager.Instance.TryConfigureGameplayFlow(
                GameplayFlowMode.SingleContract,
                new SingleContractValidationConfig { enabled = true, fixedSeed = 1337,
                    freezeDirector = true, disablePowerUps = true,
                    forceStandardDifficulty = true }), Is.True);

            // These are the same entry points as the result/retry and menu UI.
            for (int reload = 0; reload < 2; reload++)
            {
                var oldLower = Object.FindObjectOfType<CityLowerDistrict>();
                var oldUpper = Object.FindObjectOfType<CityUpperTransit>();
                var oldSkyline = Object.FindObjectOfType<CityV7DistantBackdrop>();
                var oldCamera = Camera.main;
                GameManager.Instance.Restart();
                yield return null;
                yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                Assert.That(oldLower == null && oldUpper == null
                    && oldSkyline == null && oldCamera == null, Is.True,
                    "Reload must destroy old scene roots and their cached camera.");
                AssertCity();
            }

            GameManager.Instance.ReturnToMenu();
            yield return null;
            yield return null;
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Menu));
            AssertCity();
            GameManager.Instance.StartGame();
            yield return null;
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            AssertCity();
            GameManager.Instance.ReturnToMenu();
            yield return null;
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator RepeatedBootstrapDoesNotDuplicateCityAndIgnoresNonGameplayScene()
    {
        using (var save = new SaveSnapshot())
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            Type[] layers = { typeof(CityLowerDistrict), typeof(CityUpperTransit),
                typeof(CityV7DistantBackdrop) };
            foreach (Type layer in layers)
            for (int pass = 0; pass < 3; pass++)
                layer.GetMethod("Create", StaticPrivate).Invoke(null, null);
            yield return null;
            AssertCity();

            Scene preview = SceneManager.CreateScene("CityLifecycleNonGameplay");
            try
            {
                foreach (Type layer in layers)
                    layer.GetMethod("CreateForScene", StaticPrivate).Invoke(null,
                        new object[] { preview, LoadSceneMode.Additive });
                Assert.That(preview.GetRootGameObjects(), Is.Empty,
                    "An additive non-gameplay scene must not receive city roots.");
                AssertCity();
            }
            finally
            {
                SceneManager.UnloadSceneAsync(preview);
            }
            yield return null;
        }
    }

    static void AssertCity()
    {
        var lower = OnlyInActiveScene<CityLowerDistrict>();
        var upper = OnlyInActiveScene<CityUpperTransit>();
        var skyline = OnlyInActiveScene<CityV7DistantBackdrop>();
        Assert.That(lower.transform.childCount, Is.EqualTo(25));
        Assert.That(upper.transform.childCount, Is.EqualTo(9));
        Assert.That(skyline.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
        Assert.That(typeof(CityLowerDistrict).GetField("viewer", InstancePrivate)
            .GetValue(lower), Is.SameAs(Camera.main.transform));
        Assert.That(typeof(CityUpperTransit).GetField("viewer", InstancePrivate)
            .GetValue(upper), Is.SameAs(Camera.main.transform));
        Assert.That(typeof(CityV7DistantBackdrop).GetField("_camera", InstancePrivate)
            .GetValue(skyline), Is.SameAs(Camera.main.transform));
    }

    static T OnlyInActiveScene<T>() where T : Component
    {
        T[] roots = Object.FindObjectsOfType<T>(true);
        Assert.That(roots.Length, Is.EqualTo(1), typeof(T).Name);
        Assert.That(roots[0].gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
        return roots[0];
    }

    // Reload uses normal save/telemetry hooks. Restore their preferences and
    // in-memory archive after the regression instead of altering player history.
    sealed class SaveSnapshot : IDisposable
    {
        static readonly string[] StringKeys = {
            EchoRunSaveSystem.SaveKey, EchoRunSaveSystem.SaveSlotAKey,
            EchoRunSaveSystem.SaveSlotBKey, EchoRunSaveSystem.TelemetryKey,
            EchoRunSaveSystem.SingleContractSaveSlotAKey,
            EchoRunSaveSystem.SingleContractSaveSlotBKey, "AIShadowProfileV1"
        };
        static readonly string[] IntKeys = {
            EchoRunSaveSystem.ActiveSaveSlotKey,
            EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
            EchoRunSaveSystem.TrainingResetPendingKey, "HighScore", "TotalCoins",
            "TargetFrameRate", "AudioMuted", "CharacterPreset"
        };
        static readonly string[] FloatKeys = { "MasterVolume", "MusicVolume", "SfxVolume" };
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<FieldInfo, object> fields = new Dictionary<FieldInfo, object>();

        public SaveSnapshot()
        {
            foreach (string key in StringKeys)
                if (PlayerPrefs.HasKey(key)) strings[key] = PlayerPrefs.GetString(key);
            foreach (string key in IntKeys)
                if (PlayerPrefs.HasKey(key)) ints[key] = PlayerPrefs.GetInt(key);
            foreach (string key in FloatKeys)
                if (PlayerPrefs.HasKey(key)) floats[key] = PlayerPrefs.GetFloat(key);
            foreach (FieldInfo field in typeof(EchoRunSaveSystem).GetFields(StaticPrivate))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                object value = field.GetValue(null);
                if (value != null && !field.FieldType.IsValueType && value is not string)
                    value = JsonUtility.FromJson(JsonUtility.ToJson(value), field.FieldType);
                fields.Add(field, value);
            }
        }

        public void Dispose()
        {
            Time.timeScale = 1f;
            foreach (string key in StringKeys) PlayerPrefs.DeleteKey(key);
            foreach (string key in IntKeys) PlayerPrefs.DeleteKey(key);
            foreach (string key in FloatKeys) PlayerPrefs.DeleteKey(key);
            foreach (var pair in strings) PlayerPrefs.SetString(pair.Key, pair.Value);
            foreach (var pair in ints) PlayerPrefs.SetInt(pair.Key, pair.Value);
            foreach (var pair in floats) PlayerPrefs.SetFloat(pair.Key, pair.Value);
            foreach (var pair in fields) pair.Key.SetValue(null, pair.Value);
            PlayerPrefs.Save();
        }
    }
}
