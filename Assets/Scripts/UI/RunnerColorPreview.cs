using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Show the actual dressed runner. Preview changes never rotate the gameplay root.
[RequireComponent(typeof(RawImage))]
public sealed class RunnerColorPreview : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private const int PreviewLayer = 30;
    private readonly Dictionary<GameObject, int> _layers = new Dictionary<GameObject, int>();
    private readonly List<Light> _worldLights = new List<Light>();
    private readonly List<int> _lightMasks = new List<int>();
    private Transform _model, _head;
    private Camera _camera;
    private Camera _gameCamera;
    private bool _gameCameraHadPreviewLayer;
    private Light _key, _fill;
    private RenderTexture _texture;
    private Bounds _localPose;
    private float _yaw = 8f;
    public bool PortraitMode { get; private set; }

    public void SetPortraitMode(bool portrait) { PortraitMode = portrait; }
    public void OnBeginDrag(PointerEventData data) { }
    public void OnDrag(PointerEventData data)
    {
        float width = Mathf.Max(1f, ((RectTransform)transform).rect.width * transform.lossyScale.x);
        _yaw = Mathf.Clamp(_yaw - data.delta.x / width * 150f, -65f, 65f);
    }

    void LateUpdate()
    {
        if (_model == null && !Initialize()) return;
        float height = Mathf.Max(.8f, _localPose.size.y * _model.lossyScale.y);
        Vector3 center = _model.TransformPoint(_localPose.center);
        if (PortraitMode && _head != null)
            center = _head.position + _model.up * height * .015f;
        float frameHeight = PortraitMode ? height * .30f : height * 1.12f;
        Rect rect = ((RectTransform)transform).rect;
        float aspect = rect.width / Mathf.Max(1f, rect.height);
        _camera.aspect = aspect;
        float frameWidth = PortraitMode ? frameHeight * .8f : _localPose.size.x * _model.lossyScale.x * 1.15f;
        float distance = Mathf.Max(frameHeight, frameWidth / aspect) * .5f /
            Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f);
        Vector3 direction = Quaternion.AngleAxis(_yaw, _model.up) * _model.forward;
        _camera.transform.position = center + direction * distance;
        _camera.transform.LookAt(center, _model.up);
        _key.transform.rotation = _camera.transform.rotation * Quaternion.Euler(18f, -28f, 0f);
        _fill.transform.rotation = _camera.transform.rotation * Quaternion.Euler(5f, 38f, 0f);

        // Existing city lights otherwise double-light the face in this manual camera.
        _worldLights.Clear(); _lightMasks.Clear();
        foreach (Light light in Light.GetLights(LightType.Directional, PreviewLayer))
        {
            if (light == _key || light == _fill) continue;
            _worldLights.Add(light); _lightMasks.Add(light.cullingMask);
            light.cullingMask &= ~(1 << PreviewLayer);
        }
        try { _camera.Render(); }
        finally
        {
            for (int i = 0; i < _worldLights.Count; i++)
                if (_worldLights[i] != null) _worldLights[i].cullingMask = _lightMasks[i];
        }
    }

    private bool Initialize()
    {
        GameObject player = GameObject.Find("player");
        _model = player != null ? player.transform.Find("CharacterModel") : null;
        if (_model == null) return false;
        Animator animator = _model.GetComponent<Animator>();
        _head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        // Culling boxes are oversized. Measure visible geometry once, avoiding camera breathing.
        var mesh = new Mesh();
        bool first = true;
        var vertices = new List<Vector3>();
        foreach (SkinnedMeshRenderer skin in _model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.sharedMesh == null) continue;
            skin.BakeMesh(mesh); mesh.GetVertices(vertices);
            foreach (Vector3 vertex in vertices)
            {
                Vector3 p = _model.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                if (first) { _localPose = new Bounds(p, Vector3.zero); first = false; }
                else _localPose.Encapsulate(p);
            }
        }
        Destroy(mesh);
        if (first) _localPose = new Bounds(Vector3.up, new Vector3(.8f, 2f, .5f));
        foreach (Transform child in _model.GetComponentsInChildren<Transform>(true))
        {
            _layers[child.gameObject] = child.gameObject.layer;
            child.gameObject.layer = PreviewLayer;
        }
        _gameCamera = Camera.main;
        if (_gameCamera != null)
        {
            _gameCameraHadPreviewLayer = (_gameCamera.cullingMask & (1 << PreviewLayer)) != 0;
            _gameCamera.cullingMask &= ~(1 << PreviewLayer);
        }
        _texture = new RenderTexture(512, 640, 24);
        RawImage image = GetComponent<RawImage>();
        image.texture = _texture;
        image.raycastTarget = true;
        _camera = new GameObject("RunnerPreviewCamera").AddComponent<Camera>();
        _camera.enabled = false;
        _camera.cullingMask = 1 << PreviewLayer;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = EchoRunUITheme.PageRaised;
        _camera.targetTexture = _texture;
        _camera.fieldOfView = 28f;
        _camera.nearClipPlane = .05f; _camera.farClipPlane = 30f;
        _key = MakeLight("RunnerPreviewKey", .82f, new Color(1f, .94f, .86f));
        _fill = MakeLight("RunnerPreviewFill", .30f, new Color(.78f, .87f, 1f));
        return true;
    }

    private static Light MakeLight(string name, float intensity, Color color)
    {
        Light light = new GameObject(name).AddComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << PreviewLayer;
        light.intensity = intensity; light.color = color;
        light.shadows = LightShadows.None;
        return light;
    }

    void OnDisable()
    {
        foreach (var entry in _layers)
            if (entry.Key != null) entry.Key.layer = entry.Value;
        _layers.Clear(); _model = null; _head = null;
        if (_gameCamera != null && _gameCameraHadPreviewLayer)
            _gameCamera.cullingMask |= 1 << PreviewLayer;
        _gameCamera = null;
        if (_camera != null) Destroy(_camera.gameObject);
        if (_key != null) Destroy(_key.gameObject);
        if (_fill != null) Destroy(_fill.gameObject);
        GetComponent<RawImage>().texture = null;
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
    }
}
