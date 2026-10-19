using UnityEngine;

public enum ObstacleType { Low, High, Barrier }

public class Obstacle : MonoBehaviour
{
    public ObstacleType type;
}
