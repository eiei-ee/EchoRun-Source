using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator ShorterJumpPreservesRealCoinArcPickupsAndClearsGroundObstacle()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        float captureStep = Time.captureDeltaTime;
        int[] pickups = new int[2];
        var report = new List<string> { "case,height,duration,spawned,pickedUp,strikes,apexCenterError" };
        string output = Path.GetFullPath("TestResults/RunnerJumpRevision-20260928/arc-pickup.csv");
        try
        {
            Time.captureDeltaTime = 1f / 60f;
            for (int scenario = 0; scenario < 2; scenario++)
            {
                InstallIsolatedSave(new EchoRunSaveData());
                SceneManager.LoadScene("SampleScene");
                yield return null;
                yield return WaitForFreshRun(null, false);
                GameManager game = GameManager.Instance;
                TrackManager track = TrackManager.Instance;
                PlayerController player = Object.FindObjectOfType<PlayerController>();
                Assert.IsNotNull(player);
                Rigidbody body = player.GetComponent<Rigidbody>();
                Assert.IsNotNull(body);
                float originalHeight = player.jumpHeight, originalDuration = player.jumpDuration;
                bool trackEnabled = track.enabled;
                Coin[] arcCoins = null;
                GameObject obstacle = null;
                bool jumped = false, landed = false;
                Action<PlayerActionSignal> observe = signal =>
                {
                    if (signal.Edge == PlayerActionEdge.JumpStarted) jumped = true;
                    if (signal.Edge == PlayerActionEdge.Landed) landed = true;
                };
                player.ActionRaised += observe;
                try
                {
                    if (scenario == 0)
                    {
                        player.jumpHeight = 3f;
                        player.jumpDuration = .9f;
                    }
                    else
                    {
                        Assert.Less(player.jumpHeight, 3f, "The second run must exercise the current shorter jump.");
                        Assert.Less(player.jumpDuration, .9f);
                    }
                    game.StartGame();
                    yield return new WaitForSeconds(.2f);
                    Assert.AreEqual(GameState.Playing, game.State);
                    Assert.AreEqual(1, player.CurrentLane);

                    // Retain the actual initial road/physics. Pause further
                    // procedural spawning and pool unrelated interactive items.
                    track.enabled = false;
                    foreach (Coin coin in Object.FindObjectsOfType<Coin>()) track.ReleaseDynamic(coin.gameObject);
                    foreach (Obstacle other in Object.FindObjectsOfType<Obstacle>()) track.ReleaseDynamic(other.gameObject);
                    Vector3 forward = player.ForwardDirection.normalized;
                    const float arcCenterLocalZ = 10f;
                    TrackSegmentData owner = Object.FindObjectsOfType<TrackSegmentData>()
                        .Where(segment => segment.segmentType == TrackSegmentType.Straight
                            && Vector3.Dot(segment.transform.forward, forward) > .99f)
                        .Where(segment => Vector3.Dot(segment.transform.TransformPoint(
                            new Vector3(0f, 0f, arcCenterLocalZ)) - body.position, forward)
                            > game.CurrentSpeed * player.jumpDuration * .5f + 2f)
                        .OrderBy(segment => Vector3.Dot(segment.transform.position - body.position, forward))
                        .FirstOrDefault();
                    Assert.IsNotNull(owner, "A prepared straight segment must provide room for a complete jump reward arc.");
                    Vector3 center = owner.transform.TransformPoint(new Vector3(0f, 0f, arcCenterLocalZ));

                    MethodInfo spawnArc = typeof(TrackManager).GetMethod("SpawnJumpCoinArc",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo spawnObstacle = typeof(TrackManager).GetMethod("SpawnObstacleAtWithBinding",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(spawnArc);
                    Assert.IsNotNull(spawnObstacle);
                    spawnArc.Invoke(track, new object[] { owner.gameObject, 1, arcCenterLocalZ, player.jumpHeight, false, 0 });
                    arcCoins = Object.FindObjectsOfType<Coin>();
                    Assert.AreEqual(7, arcCoins.Length, "The actual spawn method must produce all seven ordinary reward coins.");
                    int highIndex = Array.FindIndex(track.obstaclePrefabs, prefab => prefab != null
                        && prefab.GetComponent<Obstacle>() != null
                        && prefab.GetComponent<Obstacle>().type == ObstacleType.High);
                    Assert.GreaterOrEqual(highIndex, 0);
                    obstacle = spawnObstacle.Invoke(track, new object[] { owner.gameObject, 1, arcCenterLocalZ,
                        highIndex, default(EchoChallengeObstacleBinding) }) as GameObject;
                    Assert.IsNotNull(obstacle);
                    Physics.SyncTransforms();

                    int coinsBefore = game.Coins, strikesBefore = game.CollisionStrikes;
                    float highestY = body.position.y, apexCenterError = float.PositiveInfinity;
                    bool queued = false, passedArc = false;
                    float endDistance = TrackSpawnRules.CoinSpacing * (TrackSpawnRules.JumpRewardCoinCount - 1) * .5f + 1.5f;
                    float deadline = Time.time + 6f;
                    while (Time.time < deadline && game.State == GameState.Playing)
                    {
                        float remaining = Vector3.Dot(center - body.position, forward);
                        if (!queued && remaining <= game.CurrentSpeed * player.jumpDuration * .5f)
                        {
                            InputManager.Instance.QueueSwipe(SwipeDirection.Up, InputIntentSource.Keyboard, Time.unscaledTime);
                            queued = true;
                        }
                        if (player.IsJumping && body.position.y > highestY)
                        {
                            highestY = body.position.y;
                            apexCenterError = Mathf.Abs(remaining);
                        }
                        if (remaining < -endDistance && landed)
                        {
                            passedArc = true;
                            break;
                        }
                        yield return null;
                    }
                    pickups[scenario] = game.Coins - coinsBefore;
                    int inactive = arcCoins.Count(coin => coin == null || !coin.gameObject.activeInHierarchy);
                    report.Add(string.Join(",", new[] { scenario == 0 ? "old" : "current",
                        player.jumpHeight.ToString("R", CultureInfo.InvariantCulture),
                        player.jumpDuration.ToString("R", CultureInfo.InvariantCulture), "7",
                        pickups[scenario].ToString(), (game.CollisionStrikes - strikesBefore).ToString(),
                        apexCenterError.ToString("R", CultureInfo.InvariantCulture) }));
                    Assert.IsTrue(queued && jumped && landed, "Real queued input must cause a complete jump and landing.");
                    Assert.IsTrue(passedArc, "The player must run beyond the final coin.");
                    Assert.AreEqual(GameState.Playing, game.State);
                    Assert.AreEqual(strikesBefore, game.CollisionStrikes, "The center ground obstacle must cause no injury.");
                    Assert.Less(apexCenterError, .8f, "The comparison must aim the real apex at the coin arc center.");
                    Assert.AreEqual(pickups[scenario], inactive, "Score must match the seven real trigger pickups.");
                    Assert.Greater(pickups[scenario], 0, "A broken trigger fixture must not pass as zero versus zero.");
                }
                finally
                {
                    if (player != null)
                    {
                        player.ActionRaised -= observe;
                        player.jumpHeight = originalHeight;
                        player.jumpDuration = originalDuration;
                    }
                    if (track != null)
                    {
                        if (arcCoins != null) foreach (Coin coin in arcCoins)
                            if (coin != null && coin.gameObject.activeInHierarchy) track.ReleaseDynamic(coin.gameObject);
                        if (obstacle != null && obstacle.activeInHierarchy) track.ReleaseDynamic(obstacle);
                        track.enabled = trackEnabled;
                    }
                    if (game != null) game.ReturnToMenu();
                }
                yield return null;
            }
            Assert.GreaterOrEqual(pickups[1], pickups[0],
                "The shorter live jump must not collect fewer reward coins than the old jump under the same input alignment.");
        }
        finally
        {
            Time.captureDeltaTime = captureStep;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllLines(output, report);
            RestoreSavePreferences(saved);
        }
    }
}
