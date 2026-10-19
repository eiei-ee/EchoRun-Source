using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class EchoPostFxEffect : MonoBehaviour
{
    private Material _material;
    private bool _bloom = true;
    private bool _grading = true;
    private bool _vignette = true;

    public void Configure(bool bloom, bool grading, bool vignette)
    {
        _bloom = bloom;
        _grading = grading;
        _vignette = vignette;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        EnsureMaterial();
        if (_material == null || (!_bloom && !_grading && !_vignette))
        {
            Graphics.Blit(source, destination);
            return;
        }

        RenderTexture bloom = null;
        RenderTexture first = null;
        RenderTexture second = null;
        if (_bloom)
        {
            int width = Mathf.Max(1, source.width / 2);
            int height = Mathf.Max(1, source.height / 2);
            first = RenderTexture.GetTemporary(width, height, 0,
                source.format);
            second = RenderTexture.GetTemporary(width, height, 0,
                source.format);
            first.filterMode = FilterMode.Bilinear;
            second.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, first, _material, 0);
            _material.SetVector("_BlurDirection", new Vector2(1.5f, 0f));
            Graphics.Blit(first, second, _material, 1);
            _material.SetVector("_BlurDirection", new Vector2(0f, 1.5f));
            Graphics.Blit(second, first, _material, 1);
            bloom = first;
        }

        _material.SetTexture("_BloomTex", bloom != null
            ? (Texture)bloom : Texture2D.blackTexture);
        _material.SetFloat("_BloomEnabled", _bloom ? 1f : 0f);
        _material.SetFloat("_BloomIntensity", 0.24f);
        _material.SetFloat("_GradingEnabled", _grading ? 1f : 0f);
        _material.SetFloat("_VignetteEnabled", _vignette ? 1f : 0f);
        Graphics.Blit(source, destination, _material, 2);

        if (second != null) RenderTexture.ReleaseTemporary(second);
        if (first != null) RenderTexture.ReleaseTemporary(first);
    }

    private void EnsureMaterial()
    {
        if (_material != null) return;
        Shader shader = Shader.Find("Hidden/EchoRun/PostFx");
        if (shader != null)
        {
            _material = new Material(shader)
            {
                name = "EchoPostFx_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private void OnDestroy()
    {
        if (_material == null) return;
        if (Application.isPlaying) Destroy(_material);
        else DestroyImmediate(_material);
    }
}
