using System;
using UnityEngine;

public sealed class SingleContractAcceleratingGateWindowFactory
    : ISingleContractGateWindowFactory
{
    public const float CommitOffsetSeconds = 1f;
    public const float ResolveOffsetSeconds = 2f;
    public const float ExitOffsetSeconds = 3f;

    private readonly float _startSpeed;
    private readonly float _maximumSpeed;
    private readonly float _acceleration;

    public SingleContractAcceleratingGateWindowFactory(float startSpeed,
        float maximumSpeed, float acceleration)
    {
        _startSpeed = Mathf.Max(0f, startSpeed);
        _maximumSpeed = Mathf.Max(_startSpeed, maximumSpeed);
        _acceleration = Mathf.Max(0f, acceleration);
    }

    public PredictionGateDistanceWindow[] CreateWindows(
        float courseDistance, float runDurationSeconds,
        float[] presentationTimesSeconds)
    {
        if (presentationTimesSeconds == null
            || presentationTimesSeconds.Length == 0)
            throw new ArgumentException(
                "At least one gate presentation time is required.",
                nameof(presentationTimesSeconds));
        if (!IsFinite(courseDistance) || courseDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(courseDistance));
        if (!IsFinite(runDurationSeconds) || runDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(runDurationSeconds));

        float expectedCourseDistance = EchoTimeRules.DistanceForAcceleratingRun(
            _startSpeed, _maximumSpeed, _acceleration, runDurationSeconds);
        float scale = expectedCourseDistance > 0.0001f
            ? courseDistance / expectedCourseDistance : 0f;
        var windows = new PredictionGateDistanceWindow[
            presentationTimesSeconds.Length];
        for (int index = 0; index < windows.Length; index++)
        {
            float presentationTime = presentationTimesSeconds[index];
            if (!IsFinite(presentationTime) || presentationTime < 0f
                || presentationTime > runDurationSeconds)
                throw new ArgumentOutOfRangeException(
                    nameof(presentationTimesSeconds));
            windows[index] = new PredictionGateDistanceWindow
            {
                presentationDistance = DistanceAt(presentationTime,
                    runDurationSeconds, scale),
                commitDistance = DistanceAt(
                    presentationTime + CommitOffsetSeconds,
                    runDurationSeconds, scale),
                resolveDistance = DistanceAt(
                    presentationTime + ResolveOffsetSeconds,
                    runDurationSeconds, scale),
                exitDistance = DistanceAt(
                    presentationTime + ExitOffsetSeconds,
                    runDurationSeconds, scale)
            };
        }
        return windows;
    }

    private float DistanceAt(float timeSeconds, float runDurationSeconds,
        float scale)
    {
        float time = Mathf.Clamp(timeSeconds, 0f, runDurationSeconds);
        return EchoTimeRules.DistanceForAcceleratingRun(
                   _startSpeed, _maximumSpeed, _acceleration, time)
               * scale;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
