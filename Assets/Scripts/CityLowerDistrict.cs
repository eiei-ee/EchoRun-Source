using UnityEngine;
using UnityEngine.SceneManagement;

// Visual-only authored blocks remain fixed in world space until recycled
// beyond the fog. Unlike the horizon, nearby rooftops must retain parallax.
public sealed class CityLowerDistrict : MonoBehaviour
{
    public const float BlockSize=96f;
    const int Width=5;
    readonly Transform[,] blocks=new Transform[Width,Width];
    Transform viewer;
    Vector2Int center=new Vector2Int(int.MinValue,int.MinValue);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        // This callback runs once per player session. The roots themselves are
        // scene-owned, so Restart and ReturnToMenu need the sceneLoaded hook.
        SceneManager.sceneLoaded -= CreateForScene;
        SceneManager.sceneLoaded += CreateForScene;
        CreateForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    static void CreateForScene(Scene scene, LoadSceneMode mode)
    {
        if (!CanCreateInScene<CityLowerDistrict>(scene)) return;
        if (Resources.Load<GameObject>("CityV7/StackedBlock0") == null
            && Resources.Load<GameObject>("CityV7/LowerBlock0") == null) return;
        var host = new GameObject("CityLowerDistrict");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<CityLowerDistrict>();
    }
    // All three city layers use the gameplay scene as their lifetime boundary.
    // An additive preview/UI scene must neither receive nor duplicate a grid.
    internal static bool CanCreateInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        bool hasGameplay = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<T>(true) != null) return false;
            if (root.GetComponentInChildren<GameManager>() != null)
                hasGameplay = true;
        }
        return hasGameplay;
    }
    void Start()
    {
        for(int x=0;x<Width;x++)for(int z=0;z<Width;z++)
        {
            int variant=(x+z*3)%4;
            var prefab=Resources.Load<GameObject>("CityV7/StackedBlock"+variant)
                ??Resources.Load<GameObject>("CityV7/LowerBlock"+variant);
            blocks[x,z]=Instantiate(prefab,transform).transform;
        }
        LateUpdate();
    }
    void LateUpdate()
    {
        if(viewer==null&&Camera.main!=null)viewer=Camera.main.transform;
        if(viewer==null)return;
        var next=new Vector2Int(Mathf.FloorToInt(viewer.position.x/BlockSize),Mathf.FloorToInt(viewer.position.z/BlockSize));
        if(next==center)return;
        center=next;
        for(int x=next.x-2;x<=next.x+2;x++)for(int z=next.y-2;z<=next.y+2;z++)
        {
            Transform block=blocks[((x%Width)+Width)%Width,((z%Width)+Width)%Width];
            if(block!=null)block.position=new Vector3((x+.5f)*BlockSize,0,(z+.5f)*BlockSize);
        }
    }
}
