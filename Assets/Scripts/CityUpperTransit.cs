using UnityEngine;
using UnityEngine.SceneManagement;

// A sparse, world-anchored upper rail network. The authored deck stays above
// the complete gameplay/camera corridor, including turns and jumps.
public sealed class CityUpperTransit : MonoBehaviour
{
    public const float BlockSize = 192f;
    const int Width = 3;
    readonly Transform[,] blocks = new Transform[Width, Width];
    Transform viewer;
    Vector2Int center = new Vector2Int(int.MinValue, int.MinValue);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        SceneManager.sceneLoaded -= CreateForScene;
        SceneManager.sceneLoaded += CreateForScene;
        CreateForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void CreateForScene(Scene scene, LoadSceneMode mode)
    {
        if (!CityLowerDistrict.CanCreateInScene<CityUpperTransit>(scene)
            || Resources.Load<GameObject>("CityV7/UpperTransit") == null) return;
        var host = new GameObject("CityUpperTransit");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<CityUpperTransit>();
    }

    void Start()
    {
        var prefab = Resources.Load<GameObject>("CityV7/UpperTransit");
        if (prefab == null) return;
        for (int x = 0; x < Width; x++)
        for (int z = 0; z < Width; z++)
        {
            var block = Instantiate(prefab, transform).transform;
            blocks[x, z] = block;
            foreach (var loop in block.GetComponentsInChildren<CityTransitLoop>())
            {
                loop.phaseOffset += x * 7f + z * 11f;
                loop.Sample(0f);
            }
        }
        LateUpdate();
    }

    void LateUpdate()
    {
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        if (viewer == null) return;
        var next = new Vector2Int(Mathf.FloorToInt((viewer.position.x + BlockSize * .5f) / BlockSize),
            Mathf.FloorToInt((viewer.position.z - 64f + BlockSize * .5f) / BlockSize));
        if (next == center) return;
        center = next;
        for (int x = next.x - 1; x <= next.x + 1; x++)
        for (int z = next.y - 1; z <= next.y + 1; z++)
        {
            var block = blocks[(x % Width + Width) % Width, (z % Width + Width) % Width];
            if (block != null)
                block.position = new Vector3(x * BlockSize, 0f, z * BlockSize + 64f);
        }
    }
}
