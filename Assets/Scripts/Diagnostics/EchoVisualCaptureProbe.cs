using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public sealed class EchoVisualCaptureProbe : MonoBehaviour
{
    public const string CaptureDistancesArgumentPrefix =
        "-echo-qa-capture-distances=";
    public const string OffscreenCaptureArgument = "-echo-qa-offscreen-capture";
    public const string MenuCaptureArgument = "-echo-qa-capture-menu";

    private const string CaptureDirectoryName = "VisualCaptures";
    private const float DistanceTolerance = 0.0001f;

    private float[] _targetDistances;
    private bool _offscreenCapture;
    private bool _captureMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateWhenExplicitlyRequested()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        float[] distances = ParseCaptureDistances(arguments);
        bool captureMenu = Array.IndexOf(arguments, MenuCaptureArgument) >= 0;
        if (distances.Length == 0 && !captureMenu) return;

        GameObject host = new GameObject("EchoVisualCaptureProbe_Runtime");
        DontDestroyOnLoad(host);
        EchoVisualCaptureProbe probe =
            host.AddComponent<EchoVisualCaptureProbe>();
        probe._targetDistances = distances;
        probe._offscreenCapture = UsesOffscreenCapture(arguments);
        probe._captureMenu = captureMenu;
    }

    private IEnumerator Start()
    {
        if (_captureMenu)
        {
            // Read-only diagnostic: wait for the normal menu and its text/sky
            // initialization. Do not enter a run, alter cosmetics, or touch saves.
            float deadline = Time.realtimeSinceStartup + 15f;
            while (GameManager.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Menu)
            {
                string directory = CaptureOutputDirectory();
                Directory.CreateDirectory(directory);
                CaptureOffscreen(Path.Combine(directory, "menu-" + Screen.width + "x" + Screen.height + ".png"));
                Debug.Log("ECHO_MENU_CAPTURE_COMPLETE");
            }
        }
        if (_targetDistances == null || _targetDistances.Length == 0)
        {
            yield break;
        }

        int targetIndex = 0;
        string outputDirectory = null;

        while (targetIndex < _targetDistances.Length)
        {
            GameManager gameManager = GameManager.Instance;
            float actualDistance = gameManager != null
                ? gameManager.Distance
                : 0f;
            GameState state = gameManager != null
                ? gameManager.State
                : GameState.Menu;
            float targetDistance = _targetDistances[targetIndex];

            Debug.Log("ECHO_VISUAL_CAPTURE_FRAME target="
                      + FormatDistance(targetDistance)
                      + " actual=" + FormatDistance(actualDistance)
                      + " state=" + state);

            if (gameManager == null || state != GameState.Playing
                || actualDistance + DistanceTolerance < targetDistance)
            {
                yield return null;
                continue;
            }

            yield return new WaitForEndOfFrame();

            gameManager = GameManager.Instance;
            actualDistance = gameManager != null
                ? gameManager.Distance
                : actualDistance;
            outputDirectory = outputDirectory ?? CaptureOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            string fileName = BuildCaptureFileName(targetDistance,
                actualDistance, Screen.width, Screen.height);
            string capturePath = Path.Combine(outputDirectory, fileName);
            if (_offscreenCapture)
                CaptureOffscreen(capturePath);
            else
                ScreenCapture.CaptureScreenshot(capturePath);
            targetIndex++;

            // Give Unity a frame to submit the screenshot request before
            // advancing to the next target or freezing the finished run.
            yield return null;
        }

        Debug.Log("ECHO_VISUAL_CAPTURE_COMPLETE count="
                  + _targetDistances.Length
                  + " directory=" + outputDirectory);
        Time.timeScale = 0f;
        enabled = false;
    }

    public static float[] ParseCaptureDistances(string[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
            return Array.Empty<float>();

        SortedSet<float> parsed = new SortedSet<float>();
        for (int argumentIndex = 0;
             argumentIndex < arguments.Length;
             argumentIndex++)
        {
            string argument = arguments[argumentIndex];
            if (string.IsNullOrEmpty(argument)
                || !argument.StartsWith(CaptureDistancesArgumentPrefix,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            string values = argument.Substring(
                CaptureDistancesArgumentPrefix.Length);
            string[] tokens = values.Split(',');
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                if (!float.TryParse(tokens[tokenIndex].Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float distance)
                    || float.IsNaN(distance)
                    || float.IsInfinity(distance)
                    || distance < 0f)
                    continue;

                parsed.Add(distance);
            }
        }

        if (parsed.Count == 0) return Array.Empty<float>();
        float[] result = new float[parsed.Count];
        parsed.CopyTo(result);
        return result;
    }

    public static bool UsesOffscreenCapture(string[] arguments)
    {
        if (arguments == null) return false;
        foreach (string argument in arguments)
        {
            if (string.Equals(argument, OffscreenCaptureArgument,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    internal static void CaptureOffscreen(string path)
    {
        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException(
                "Offscreen visual capture requires the active gameplay camera.");

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        int previousCullingMask = camera.cullingMask;
        var overlayStates = new List<OverlayCanvasState>();
        var overlayLayers = new Dictionary<GameObject, int>();
        Camera overlayCamera = null;
        // Collect every state before changing a parent canvas, since a child
        // can report a different effective render mode after that change.
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                overlayStates.Add(new OverlayCanvasState(canvas));
        }

        RenderTexture target = null;
        Texture2D image = null;
        try
        {
            target = RenderTexture.GetTemporary(width, height, 24,
                RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            // Overlay UI is drawn after the game's post-processing. Rendering
            // it through the gameplay camera falsely adds bloom/color grading
            // to buttons and text, so composite it in a separate clean pass.
            if (overlayStates.Count > 0)
            {
                overlayCamera = new GameObject("EchoCaptureOverlayCamera").AddComponent<Camera>();
                overlayCamera.enabled = false;
                overlayCamera.clearFlags = CameraClearFlags.Depth;
                overlayCamera.cullingMask = 1 << 31;
                overlayCamera.targetTexture = target;
                overlayCamera.nearClipPlane = 0.01f;
                overlayCamera.farClipPlane = 10f;
                overlayCamera.allowHDR = false;
                overlayCamera.allowMSAA = false;
                camera.cullingMask &= ~(1 << 31);
            }
            foreach (OverlayCanvasState state in overlayStates)
            {
                Canvas canvas = state.canvas;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = overlayCamera;
                canvas.planeDistance = 1f;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (!overlayLayers.ContainsKey(child.gameObject))
                        overlayLayers.Add(child.gameObject, child.gameObject.layer);
                    child.gameObject.layer = 31;
                }
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            if (overlayCamera != null) overlayCamera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("ECHO_VISUAL_CAPTURE_OFFSCREEN camera=" + camera.name
                      + " size=" + width + "x" + height + " path=" + path);
        }
        finally
        {
            for (int index = overlayStates.Count - 1; index >= 0; index--)
                overlayStates[index].Restore();
            foreach (var entry in overlayLayers)
                if (entry.Key != null) entry.Key.layer = entry.Value;
            if (overlayCamera != null) Destroy(overlayCamera.gameObject);
            camera.targetTexture = previousTarget;
            camera.cullingMask = previousCullingMask;
            RenderTexture.active = previousActive;
            if (image != null) Destroy(image);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            Canvas.ForceUpdateCanvases();
        }
    }

    private readonly struct OverlayCanvasState
    {
        public readonly Canvas canvas;
        private readonly RenderMode _renderMode;
        private readonly Camera _worldCamera;
        private readonly float _planeDistance;

        public OverlayCanvasState(Canvas canvas)
        {
            this.canvas = canvas;
            _renderMode = canvas.renderMode;
            _worldCamera = canvas.worldCamera;
            _planeDistance = canvas.planeDistance;
        }

        public void Restore()
        {
            if (canvas == null) return;
            canvas.renderMode = _renderMode;
            canvas.worldCamera = _worldCamera;
            canvas.planeDistance = _planeDistance;
        }
    }

    private static string CaptureOutputDirectory()
    {
#if UNITY_EDITOR
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Builds", "Windows", CaptureDirectoryName));
#else
        string executableDirectory = Path.GetDirectoryName(
            Application.dataPath);
        return Path.GetFullPath(Path.Combine(
            string.IsNullOrEmpty(executableDirectory)
                ? Application.dataPath
                : executableDirectory,
            CaptureDirectoryName));
#endif
    }

    private static string BuildCaptureFileName(float targetDistance,
        float actualDistance, int width, int height)
    {
        return "target-" + FileSafeDistance(targetDistance)
               + "m_actual-" + FileSafeDistance(actualDistance)
               + "m_" + Mathf.Max(0, width)
               + "x" + Mathf.Max(0, height) + ".png";
    }

    private static string FileSafeDistance(float distance)
    {
        return FormatDistance(distance).Replace('.', 'p');
    }

    private static string FormatDistance(float distance)
    {
        return distance.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
