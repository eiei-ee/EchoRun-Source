#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class CityLayerRuntimeReview : MonoBehaviour
{
    string directory;
    float started;
    int captureIndex,frames,turns,missingRoadFrames,missingCityFrames;
    Vector3 previousForward;
    readonly float[] distances={20,65,110,180,260,350,450,500};
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-echo-city-layer-review")>=0)
            new GameObject("CityLayerRuntimeReview").AddComponent<CityLayerRuntimeReview>();
    }
    void Start()
    {
        directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"CityCaptures");
        Directory.CreateDirectory(directory);started=Time.realtimeSinceStartup;
    }
    void LateUpdate()
    {
        var gm=GameManager.Instance;
        if(gm==null)return;
        if(!gm.ActiveSingleContractValidationConfig.enabled)
        {
            if(Time.realtimeSinceStartup-started>20)Finish("Validation did not start");
            return;
        }
        // Dedicated fixed-seed visual test only: no input injection or normal-play changes.
        foreach(var obstacle in FindObjectsOfType<Obstacle>())foreach(var collider in obstacle.GetComponentsInChildren<Collider>())collider.enabled=false;
        if(gm.State==GameState.Playing)
        {
            frames++;
            var city=FindObjectOfType<CityLowerDistrict>();
            if(city==null||city.transform.childCount!=25)missingCityFrames++;
            foreach(var data in FindObjectsOfType<TrackSegmentData>())
            {
                if(data.segmentType!=TrackSegmentType.Straight)continue;
                var ground=data.transform.Find("GroundPlane");
                if(ground==null||!ground.GetComponent<Renderer>().enabled){missingRoadFrames++;break;}
            }
            var player=FindObjectOfType<PlayerController>();
            if(player!=null)
            {
                var forward=player.ForwardDirection;
                if(previousForward.sqrMagnitude>.1f&&Vector3.Angle(previousForward,forward)>30)
                {
                    string direction=Vector3.Cross(previousForward,forward).y<0?"left":"right";
                    turns++;Capture("turn-"+turns+"-"+direction);
                }
                previousForward=forward;
            }
            if(captureIndex<distances.Length&&gm.Distance>=distances[captureIndex])
            {Capture("distance-"+distances[captureIndex]);captureIndex++;}
            if(captureIndex==distances.Length){Finish("Completed 500m visual route");return;}
        }
        if(Time.realtimeSinceStartup-started>110||gm.State==GameState.GameOver)Finish("Route ended");
    }
    void Capture(string name)
    {EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(directory,name+".png"));}
    void Finish(string reason)
    {
        string report=reason+"\nVisual diagnostics only; obstacle colliders disabled.\nframes="+frames+"\nturns="+turns
            +"\ndistance="+GameManager.Instance.Distance+"\nmissingRoadFrames="+missingRoadFrames+"\nmissingCityFrames="+missingCityFrames;
        File.WriteAllText(Path.Combine(directory,"report.txt"),report);Debug.Log("CITY_LAYER_RUNTIME_REVIEW "+report);enabled=false;Application.Quit();
    }
}
#endif
