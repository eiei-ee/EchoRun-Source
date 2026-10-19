using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Deterministic moving-camera samples of the actual assembled route. No image
// smoothing or compositing: each PNG is a fresh camera render at the saved pose.
public static class StackedCityFlickerReview
{
    public static void Before() { CityFlickerGeometryAudit.Audit(); StackedCityReview.FlickerCaptureBefore(); }
    public static void RepairAndBuild()
    {
        CityBridgeSurfaceRepair.Apply();
        CityBridgeSurfaceRepair.Apply();
        StackedCityReview.FlickerBuild();
    }
    public static void RefreshBridgeAndCapture()
    {
        CityBridgeSurfaceRepair.Apply();
        // A second pass must find no remaining faces to change.
        CityBridgeSurfaceRepair.Apply();
        StackedCityReview.FlickerCaptureAfter();
    }
    public static void ApplyAndCapture()
    {
        CityBridgeSurfaceRepair.Apply();
        CityBuildingSurfaceRepair.Install();
        CityFlickerGeometryAudit.Audit();
        Directory.CreateDirectory("TestResults/StackedCityFlicker/After");
        File.Copy("TestResults/StackedCityFlicker/geometry-audit.txt", "TestResults/StackedCityFlicker/After/geometry-audit.txt", true);
        StackedCityReview.FlickerCaptureAfter();
    }

    public static void Capture(Camera camera, GameObject player, GameObject route, Vector3 offset,
        float anchorY, int direction, string directory, StringBuilder report)
    {
        string side = direction < 0 ? "left" : "right";
        var sequences = new Dictionary<string, List<string>>();
        report.AppendLine("Flicker camera planes: " + camera.nearClipPlane + " / " + camera.farClipPlane);
        var run = new List<string>();
        for (int frame = 0; frame < 12; frame++)
        {
            string name = side + "-moving-approach-" + frame.ToString("00");
            StackedCityReview.CaptureView(camera, player, offset,
                new Vector3(-direction * 3f, anchorY, 65f + frame * .24f), Vector3.forward,
                4f + frame / 60f, directory, name, report);
            run.Add(name + ".png");
        }
        sequences.Add("Actual lane camera / corner approach", run);
        bool active = player.activeSelf;
        player.SetActive(false);
        try
        {
            Vector3 bridgeTarget = new Vector3(direction * 6f, -.95f, 70f);
            Sample(camera, new Vector3(direction * 13f, 2.8f, 53f), bridgeTarget,
                side + "-bridge-section-near", directory, report, sequences);
            Sample(camera, bridgeTarget + (new Vector3(direction * 13f, 2.8f, 53f) - bridgeTarget) * 3.4f,
                bridgeTarget, side + "-bridge-section-far", directory, report, sequences);

            // Examine the join between the 40 m and 60 m street chunks from
            // inside the corridor. This includes upper walls and their new base.
            var segment = route.GetComponentsInChildren<TrackSegmentData>()
                .Where(s => s.segmentType == TrackSegmentType.Straight)
                .OrderBy(s => Vector3.SqrMagnitude(s.transform.position - new Vector3(0, 0, 40))).First();
            var city = segment.transform.Find("CityV7Environment");
            if (city == null) throw new InvalidOperationException("Missing city for join review");
            var terrace = city.Cast<Transform>().Where(t => t.name == "V7_GroundedTerrace" && t.gameObject.activeInHierarchy)
                .OrderBy(t => Mathf.Abs(t.position.x)).FirstOrDefault();
            if (terrace != null)
            {
                var renderers = terrace.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                float sign = Mathf.Sign(bounds.center.x);
                float face = sign < 0 ? bounds.max.x : bounds.min.x;
                Vector3 target = new Vector3(face, bounds.min.y + .3f, 50f);
                Vector3 near = target + new Vector3(-sign * 16f, 9f, -12f);
                Sample(camera, near, target, side + "-building-join-near", directory, report, sequences);
                Sample(camera, target + (near - target) * 3.8f, target,
                    side + "-building-join-far", directory, report, sequences);
            }
        }
        finally { player.SetActive(active); }
        if (direction == 1) CaptureKnownInterfaces(camera, directory, report, sequences);
        WriteViewer(directory, side, sequences);
    }

    static void CaptureKnownInterfaces(Camera camera, string directory, StringBuilder report,
        Dictionary<string, List<string>> sequences)
    {
        // These are isolated copies of the two actual prefab interfaces found
        // by the geometry audit, rather than an arbitrary nearby facade.
        var states = UnityEngine.Object.FindObjectsOfType<Renderer>().ToDictionary(r => r, r => r.enabled);
        foreach (var renderer in states.Keys) renderer.enabled = false;
        try
        {
            foreach (int index in new[] { 5, 8 })
            {
                var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CityV7/Chunk" + index));
                try
                {
                    string footingName = "StackedDeepFooting_" + (index == 5 ? 2 : 1);
                    var footing = root.transform.Find(footingName);
                    Bounds lower = RendererBounds(footing);
                    var building = root.transform.Cast<Transform>()
                        .Where(t => t.name == "V7_GroundedTerrace")
                        .Single(t => { Bounds b = RendererBounds(t); return Mathf.Abs(b.center.x - lower.center.x) < .05f && Mathf.Abs(b.center.z - lower.center.z) < .05f; });
                    foreach (Transform child in root.transform)
                        child.gameObject.SetActive(child == footing || child == building);
                    Bounds body = building.GetComponent<Renderer>().bounds;
                    Vector3 target = new Vector3(body.center.x, body.min.y + .02f, body.max.z);
                    Sample(camera, target + new Vector3(4f, 3f, 12f), target,
                        "interface-chunk" + index + "-near", directory, report, sequences);
                    Sample(camera, target + new Vector3(4f, 3f, 12f) * 4.5f, target,
                        "interface-chunk" + index + "-far", directory, report, sequences);
                    report.AppendLine("interface-chunk" + index + " | isolated actual prefab copies; footing=" + footingName
                        + " | bodyBottom=" + body.min.y.ToString("F6") + " | baseTop=" + lower.max.y.ToString("F6"));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        finally { foreach (var state in states) if (state.Key != null) state.Key.enabled = state.Value; }
    }

    static Bounds RendererBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    static void Sample(Camera camera, Vector3 position, Vector3 target, string prefix,
        string directory, StringBuilder report, Dictionary<string, List<string>> sequences)
    {
        var names = new List<string>();
        for (int frame = 0; frame < 12; frame++)
        {
            // Sub-pixel-ish lateral motion exposes unstable depth ownership
            // without confusing it with a large change in scene composition.
            camera.transform.position = position + Vector3.right * ((frame - 5.5f) * .008f);
            camera.transform.LookAt(target);
            string name = prefix + "-" + frame.ToString("00");
            StackedCityReview.RenderImage(camera, directory, name);
            names.Add(name + ".png");
            report.AppendLine(name + " | inspection camera=" + camera.transform.position.ToString("F4") + " | target=" + target);
        }
        sequences.Add(prefix + " / inspection camera", names);
    }

    static void WriteViewer(string directory, string side, Dictionary<string, List<string>> sequences)
    {
        var html = new StringBuilder("<!doctype html><meta charset=utf-8><title>City surface motion review</title><style>body{background:#14191c;color:#eee;font:16px system-ui;margin:24px}img{display:block;width:min(100%,1600px);margin-top:16px}button,select{font:inherit;margin-right:12px}p{max-width:900px}</style><p>Raw Unity frames. Moving approach uses the game camera offset; all other sequences are inspection camera poses. No image processing. Pause and step to compare surface ownership.</p><select id=sequence></select><button id=play>Pause</button><input id=frame type=range min=0 max=11 value=0><span id=label></span><img id=picture><script>const sets=[");
        bool first = true;
        foreach (var pair in sequences)
        {
            if (!first) html.Append(',');
            first = false;
            html.Append("{name:'" + pair.Key + "',files:['" + string.Join("','", pair.Value) + "']}");
        }
        html.Append("];const q=document.querySelector('#sequence'),f=document.querySelector('#frame'),p=document.querySelector('#picture'),l=document.querySelector('#label'),b=document.querySelector('#play');let running=true;sets.forEach((s,i)=>q.add(new Option(s.name,i)));function show(){p.src=sets[q.value].files[+f.value];l.textContent=(+f.value+1)+'/12'}q.onchange=()=>{f.value=0;show()};f.oninput=()=>{running=false;b.textContent='Play';show()};b.onclick=()=>{running=!running;b.textContent=running?'Pause':'Play'};setInterval(()=>{if(running){f.value=(+f.value+1)%12;show()}},90);show();</script>");
        File.WriteAllText(Path.Combine(directory, side + "-motion-review.html"), html.ToString());
    }
}
