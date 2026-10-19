using System;
using UnityEngine;

[Serializable]
public struct PredictionGateObstacleBinding
{
    public int runId;
    public int gateId;
    public int physicalLane;
    public ObstacleType obstacleType;

    public bool IsBound => runId > 0 && gateId > 0
                           && physicalLane >= 0 && physicalLane <= 2;
}

public sealed class PredictionGateObstacleTag : MonoBehaviour
{
    public PredictionGateObstacleBinding Binding { get; private set; }

    public void Configure(PredictionGateObstacleBinding binding)
    {
        Binding = binding;
    }

    public void Clear()
    {
        Binding = default;
    }

    void OnDisable()
    {
        Clear();
    }
}
