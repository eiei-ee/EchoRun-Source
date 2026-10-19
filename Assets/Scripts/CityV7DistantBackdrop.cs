using UnityEngine;
using UnityEngine.SceneManagement;

// An authored distant silhouette ring. It follows translation only, so turns
// retain a consistent horizon; it never participates in track or collision.
public sealed class CityV7DistantBackdrop : MonoBehaviour
{
    private Transform _camera;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        SceneManager.sceneLoaded -= CreateForScene;
        SceneManager.sceneLoaded += CreateForScene;
        CreateForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    private static void CreateForScene(Scene scene, LoadSceneMode mode)
    {
        if (!CityLowerDistrict.CanCreateInScene<CityV7DistantBackdrop>(scene)) return;
        var prefab = Resources.Load<GameObject>("CityV7/ExperienceSkyline");
        if (prefab == null) return;
        var host = Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<CityV7DistantBackdrop>();
    }
    private void LateUpdate()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_camera == null) return;
        Vector3 p = _camera.position;
        transform.position = new Vector3(p.x, 0f, p.z);
    }
}
