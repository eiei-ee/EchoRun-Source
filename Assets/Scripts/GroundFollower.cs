using UnityEngine;

public class GroundFollower : MonoBehaviour
{
    public Transform player;
    public float colliderLength = 200f;
    public float updateThreshold = 30f;

    void Start()
    {
        // Hide ground plane mesh — track segments provide visuals.
        // Renderer would otherwise block camera view of the character.
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        if (player == null)
        {
            GameObject p = GameObject.Find("player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

        float dx = transform.position.x - player.position.x;
        float dz = transform.position.z - player.position.z;
        float sqrDist = dx * dx + dz * dz;

        if (sqrDist > updateThreshold * updateThreshold)
        {
            transform.position = new Vector3(player.position.x, -0.1f, player.position.z);
        }
    }
}
