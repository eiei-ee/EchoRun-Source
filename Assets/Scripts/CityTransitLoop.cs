using UnityEngine;

// Presentation only. Points and vehicle motion use this root's local space so
// a recycled lower-city block carries its traffic without resetting the phase.
public sealed class CityTransitLoop : MonoBehaviour
{
    public Transform[] vehicles;
    public Vector3[] pathPoints;
    [Min(0f)] public float speed = 12f;
    [Min(0f)] public float spacing = 8f;
    public float phaseOffset;

    private float _elapsedSeconds;
    private const float MinimumLength = 0.0001f;

    private void OnEnable()
    {
        Sample(_elapsedSeconds);
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    // Pass scaled delta time; a paused frame does not move any vehicle.
    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;
        Sample(_elapsedSeconds + deltaSeconds);
    }

    // Absolute time makes review captures repeatable and also sets the phase
    // from which runtime playback continues. Vehicle models face local +Z.
    public void Sample(float elapsedSeconds)
    {
        _elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        if (vehicles == null || pathPoints == null || pathPoints.Length < 2) return;

        float length = 0f;
        for (int i = 0; i < pathPoints.Length; i++)
            length += Vector3.Distance(pathPoints[i], pathPoints[(i + 1) % pathPoints.Length]);
        if (length <= MinimumLength) return;

        float travel = (_elapsedSeconds + phaseOffset) * Mathf.Max(0f, speed);
        float headingSpan = Mathf.Min(1.5f, length * 0.02f);
        for (int i = 0; i < vehicles.Length; i++)
        {
            Transform vehicle = vehicles[i];
            if (vehicle == null) continue;
            float distance = travel - i * Mathf.Max(0f, spacing);
            Vector3 segmentDirection;
            Vector3 position = PositionAt(distance, length, out segmentDirection);
            Vector3 unused;
            Vector3 direction = PositionAt(distance + headingSpan, length, out unused)
                - PositionAt(distance - headingSpan, length, out unused);
            // A two-point return route has a zero tangent exactly at its ends.
            if (direction.sqrMagnitude <= MinimumLength * MinimumLength)
                direction = segmentDirection;
            vehicle.SetPositionAndRotation(transform.TransformPoint(position),
                transform.rotation * Quaternion.LookRotation(direction, Vector3.up));
        }
    }

    private Vector3 PositionAt(float distance, float totalLength, out Vector3 direction)
    {
        float remaining = Mathf.Repeat(distance, totalLength);
        direction = Vector3.forward;
        for (int i = 0; i < pathPoints.Length; i++)
        {
            Vector3 start = pathPoints[i];
            Vector3 delta = pathPoints[(i + 1) % pathPoints.Length] - start;
            float length = delta.magnitude;
            if (length <= MinimumLength) continue;
            if (remaining < length)
            {
                direction = delta / length;
                return start + direction * remaining;
            }
            remaining -= length;
        }
        return pathPoints[0];
    }
}
