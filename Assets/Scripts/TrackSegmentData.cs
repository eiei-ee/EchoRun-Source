using UnityEngine;

public enum TrackSegmentType { Straight, TurnLeft, TurnRight }

public class TrackSegmentData : MonoBehaviour
{
    public TrackSegmentType segmentType;
    public float routeDistance;
    public Vector3 entryDirection;
    public Vector3 exitDirection;
    public Vector3 turnPointWorld;
    [System.NonSerialized] public AITrackPlan trackPlan;
    [System.NonSerialized] public bool contentSpawned;
    [System.NonSerialized] public bool isFinishSegment;
}
