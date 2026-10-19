#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Explicit visual-route review only. This never runs in a release player or
// ordinary play, and its captures are not collision/playtest acceptance.
public sealed class ExperienceSliceReviewProbe : MonoBehaviour
{
    string _directory;
    Vector3 _previousForward;
    int _turns;
    int _frames;
    int _missingRoadFrames;
    int _disabledHazards;
    float _start;
    bool _finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-echo-slice-review") < 0) return;
        new GameObject("ExperienceSliceReviewProbe").AddComponent<ExperienceSliceReviewProbe>();
    }

    IEnumerator Start()
    {
        _directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SliceCaptures");
        Directory.CreateDirectory(_directory);
        _start = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            yield return null;
        if (!GameManager.Instance.ActiveSingleContractValidationConfig.enabled)
            throw new InvalidOperationException("Slice review requires isolated fixed-seed validation.");
        float[] distances = {12, 45, 90, 130, 150, 220, 350, 500, 700};
        foreach (float target in distances)
        {
            while (GameManager.Instance.Distance < target && !_finished)
                yield return null;
            if (_finished) yield break;
            yield return new WaitForEndOfFrame();
            Capture("distance-" + target.ToString("000"));
        }
        Finish();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (_finished || gm == null || !gm.ActiveSingleContractValidationConfig.enabled) return;
        // Only the diagnostic route disables hazards, leaving their visual
        // meshes, gate positions, track pool, player and camera code intact.
        foreach (var obstacle in FindObjectsOfType<Obstacle>())
            foreach (var collider in obstacle.GetComponentsInChildren<Collider>())
                if (collider.enabled) { collider.enabled = false; _disabledHazards++; }
        var player = FindObjectOfType<PlayerController>();
        if (player != null && gm.State == GameState.Playing)
        {
            _frames++;
            Vector3 forward = player.ForwardDirection;
            if (_previousForward.sqrMagnitude > .1f && Vector3.Angle(_previousForward, forward) > 30f)
            {
                _turns++;
                string direction = Vector3.Cross(_previousForward, forward).y < 0 ? "left" : "right";
                StartCoroutine(CaptureTurn(direction, _turns));
            }
            _previousForward = forward;
            foreach (var city in FindObjectsOfType<CityV7ChunkIdentity>())
            {
                // Turn city dressing retains the turn prefab's own road meshes.
                if (city.index < 0) continue;
                Transform ground = city.transform.parent.Find("GroundPlane");
                if (ground == null || !ground.GetComponent<Renderer>().enabled)
                { _missingRoadFrames++; break; }
            }
        }
        if (Time.realtimeSinceStartup - _start > 150f || gm.State == GameState.GameOver)
            Finish();
    }

    IEnumerator CaptureTurn(string direction, int index)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForEndOfFrame();
            Capture("turn-" + index + "-" + direction + "-" + i);
            yield return new WaitForSeconds(.22f);
        }
    }

    void Capture(string name)
    {
        EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(_directory, name + ".png"));
        Debug.Log("SLICE_FRAME " + name + " distance=" + GameManager.Instance.Distance);
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        string report = "Visual route only; obstacle colliders disabled. No input or human-play acceptance.\n"
            + "distance=" + GameManager.Instance.Distance + "\nframes=" + _frames
            + "\nturns=" + _turns + "\nmissingRoadFrames=" + _missingRoadFrames
            + "\ndisabledHazardColliders=" + _disabledHazards;
        File.WriteAllText(Path.Combine(_directory, "route-report.txt"), report);
        Debug.Log("EXPERIENCE_SLICE_REVIEW_COMPLETE " + report);
        Application.Quit();
    }
}
#endif
