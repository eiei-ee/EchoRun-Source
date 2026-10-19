using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Local QA artifacts only. Start the one validation Editor with -buildTarget WebGL;
// this entry point never switches targets, opens scenes, installs assets or deploys.
public static class ShadowEchoPlatformVerification
{
    private const string GameplayScene = "Assets/Scenes/SampleScene.scene";
    private const string WebGlOutput = "TestResults/ShadowEcho-20261008/CandidateWebGL";
    private const string QaProductName = "EchoRun-ShadowEcho-20261008-CandidateWebGL";

    public static void BuildCandidateWebGL()
    {
        string output = Path.GetFullPath(WebGlOutput);
        if (Directory.Exists(output) || File.Exists(output))
            throw new BuildFailedException("Preserve existing QA artifacts: " + output);
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new BuildFailedException("QA build requires an idle validation Editor.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new BuildFailedException(
                "Start the validation Editor with -buildTarget WebGL before this entry point.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScene) == null)
            throw new BuildFailedException("Gameplay scene is missing: " + GameplayScene);

        // This parameterized helper touches only the newly generated HTML shell.
        MethodInfo finalizeShell = typeof(BuildConfig).GetMethod("OptimizeWebGLShell",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            new[] { typeof(string) }, null);
        if (finalizeShell == null)
            throw new BuildFailedException("The parameterized WebGL shell finalizer is missing.");

        WebGlSettings before = WebGlSettings.Capture();
        var evidence = new BuildEvidence
        {
            startedUtc = DateTime.UtcNow.ToString("o"),
            scene = GameplayScene,
            output = WebGlOutput,
            product = QaProductName,
            engineVersion = Application.unityVersion,
            before = before
        };
        try
        {
            PlayerSettings.productName = QaProductName;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            evidence.builtWith = WebGlSettings.Capture();

            BuildReport report = BuildPipeline.BuildPlayer(new[] { GameplayScene }, output,
                BuildTarget.WebGL, BuildOptions.Development);
            evidence.buildResult = report == null ? "NoReport" : report.summary.result.ToString();
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Shadow Echo WebGL build failed: " + evidence.buildResult);

            try { finalizeShell.Invoke(null, new object[] { WebGlOutput }); }
            catch (TargetInvocationException error)
            {
                throw new BuildFailedException("WebGL shell finalization failed: " + error.InnerException);
            }
            if (!File.Exists(Path.Combine(output, "index.html")))
                throw new BuildFailedException("The WebGL build did not produce index.html.");
            evidence.completed = true;
        }
        catch (Exception error)
        {
            evidence.failure = error.ToString();
            throw;
        }
        finally
        {
            before.RestoreChangedSettings();
            evidence.after = WebGlSettings.Capture();
            evidence.settingsRestored = JsonUtility.ToJson(before) == JsonUtility.ToJson(evidence.after);
            evidence.finishedUtc = DateTime.UtcNow.ToString("o");
            if (Directory.Exists(output))
                File.WriteAllText(Path.Combine(output, "qa-build.json"),
                    JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));
            if (!evidence.settingsRestored)
                throw new BuildFailedException("QA settings did not restore; inspect qa-build.json.");
        }
        Debug.Log("SHADOW_ECHO_CANDIDATE_WEBGL_BUILD_OK " + output);
    }

    [Serializable]
    private sealed class BuildEvidence
    {
        public string startedUtc;
        public string finishedUtc;
        public string scene;
        public string output;
        public string product;
        public string engineVersion;
        public string buildOptions = "Development";
        public string scope = "Local browser QA only; not deployed and not WeChat or device acceptance.";
        public string buildResult = "NotStarted";
        public string failure;
        public bool completed;
        public bool settingsRestored;
        public WebGlSettings before;
        public WebGlSettings builtWith;
        public WebGlSettings after;
    }

    [Serializable]
    private sealed class WebGlSettings
    {
        public string productName;
        public string template;
        public WebGLCompressionFormat compression;
        public bool decompressionFallback;
        public bool dataCaching;
        public bool nameFilesAsHashes;
        public bool threadsSupport;
        public int width;
        public int height;
        // These values are recorded and checked, never changed by this bridge.
        public BuildTarget activeTarget;
        public string miniGameSubplatform;
        public int qualityLevel;
        public int vSyncCount;

        public static WebGlSettings Capture()
        {
            return new WebGlSettings
            {
                productName = PlayerSettings.productName,
                template = PlayerSettings.WebGL.template,
                compression = PlayerSettings.WebGL.compressionFormat,
                decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                dataCaching = PlayerSettings.WebGL.dataCaching,
                nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes,
                threadsSupport = PlayerSettings.WebGL.threadsSupport,
                width = PlayerSettings.defaultWebScreenWidth,
                height = PlayerSettings.defaultWebScreenHeight,
                activeTarget = EditorUserBuildSettings.activeBuildTarget,
                miniGameSubplatform = PlayerSettings.MiniGame.GetActiveSubplatform().ToString(),
                qualityLevel = QualitySettings.GetQualityLevel(),
                vSyncCount = QualitySettings.vSyncCount
            };
        }

        public void RestoreChangedSettings()
        {
            PlayerSettings.productName = productName;
            PlayerSettings.WebGL.template = template;
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;
            PlayerSettings.WebGL.threadsSupport = threadsSupport;
            PlayerSettings.defaultWebScreenWidth = width;
            PlayerSettings.defaultWebScreenHeight = height;
        }
    }
}
