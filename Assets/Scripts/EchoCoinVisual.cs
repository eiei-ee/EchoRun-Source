using UnityEngine;

[DisallowMultipleComponent]
public sealed class EchoCoinVisual : MonoBehaviour
{
    private const string FormalVisualPath =
        "Art/Pickups/MemoryPulseShard_B";
    private const string MaterialPath = "Materials/EchoCollectible";
    private const float PresentationScale = 0.72f;
    private static readonly int ContractMarkerId =
        Shader.PropertyToID("_ContractMarker");
    private static Mesh _formalMesh;
    private static Quaternion _formalRotation = Quaternion.identity;
    private static Vector3 _formalScale = Vector3.one;
    private static Material _sharedMaterial;
    private static bool _ownsMaterial;

    private MeshRenderer _renderer;

    public void Initialize()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = GetFormalMesh();
        transform.localPosition = Vector3.zero;
        transform.localRotation = _formalRotation;
        // Scale the authored mesh only; collection triggers and route spacing
        // retain their gameplay size and positions.
        transform.localScale = _formalScale * PresentationScale;
        _renderer.sharedMaterial = GetOrCreateMaterial();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    public void ApplyViewFacingRotation(Quaternion facingRotation)
    {
        transform.rotation = facingRotation * _formalRotation;
    }

    public void ResetPresentation()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = _formalRotation;
    }

    public void SetContractMarker(bool marker)
    {
        if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null) return;
        var properties = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(properties);
        properties.SetFloat(ContractMarkerId, marker ? 1f : 0f);
        _renderer.SetPropertyBlock(properties);
    }

    private static Mesh GetFormalMesh()
    {
        if (_formalMesh != null) return _formalMesh;
        GameObject visual = Resources.Load<GameObject>(FormalVisualPath);
        MeshFilter source = visual != null
            ? visual.GetComponentInChildren<MeshFilter>(true)
            : null;
        _formalMesh = source != null ? source.sharedMesh : null;
        if (source != null)
        {
            _formalRotation = source.transform.localRotation;
            _formalScale = source.transform.localScale;
        }
        if (_formalMesh == null)
            Debug.LogError("Missing formal memory pulse shard visual at Resources/"
                + FormalVisualPath + ".");
        return _formalMesh;
    }

    private static Material GetOrCreateMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        _sharedMaterial = Resources.Load<Material>(MaterialPath);
        if (_sharedMaterial != null) return _sharedMaterial;
        Shader shader = Shader.Find("EchoRun/Collectible");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        _sharedMaterial = new Material(shader)
        {
            name = "EchoCollectible_RuntimeFallback"
        };
        _ownsMaterial = true;
        return _sharedMaterial;
    }

    private void OnDestroy()
    {
        if (_ownsMaterial && _sharedMaterial != null)
        {
            if (Application.isPlaying) Destroy(_sharedMaterial);
            else DestroyImmediate(_sharedMaterial);
            _sharedMaterial = null;
            _ownsMaterial = false;
        }
    }
}
