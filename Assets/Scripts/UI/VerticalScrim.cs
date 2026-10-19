using UnityEngine;
using UnityEngine.UI;

// A single transparent quad keeps home titles readable over the live skyline.
// It uses the normal UI material and adds no texture to the download.
public sealed class VerticalScrim : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Color bottom = color;
        bottom.a = 0f;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin, 0f), bottom, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin, 0f), bottom, Vector2.right);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax, 0f), color, Vector2.one);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax, 0f), color, Vector2.up);
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }
}
