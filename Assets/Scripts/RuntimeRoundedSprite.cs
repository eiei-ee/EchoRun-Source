using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class RuntimeRoundedSprite : IDisposable
{
    private Sprite _sprite;
    private Texture2D _texture;

    public void Apply(Image image)
    {
        if (image == null) return;
        if (_sprite == null) Create();
        image.sprite = _sprite;
        image.type = Image.Type.Sliced;
    }

    public void Dispose()
    {
        if (_sprite != null) UnityEngine.Object.Destroy(_sprite);
        if (_texture != null) UnityEngine.Object.Destroy(_texture);
        _sprite = null;
        _texture = null;
    }

    private void Create()
    {
        const int size = 64;
        const float radius = 15f;
        _texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeRoundedUI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2 inner = new Vector2(center.x - radius, center.y - radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(
                    Mathf.Abs(x - center.x) - inner.x,
                    Mathf.Abs(y - center.y) - inner.y);
                float outside = new Vector2(
                    Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                float distance = outside + inside - radius;
                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(16f, 16f, 16f, 16f));
        _sprite.name = "RuntimeRoundedUISprite";
    }
}
