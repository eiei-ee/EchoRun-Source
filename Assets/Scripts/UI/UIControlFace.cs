using UnityEngine;
using UnityEngine.UI;

// A quiet vertical material response on the existing UI mesh. This adds no
// texture, blur pass or duplicate geometry to the mobile control surface.
[DisallowMultipleComponent]
public sealed class UIControlFace : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || mesh.currentVertCount == 0) return;
        Rect rect = ((RectTransform)transform).rect;
        UIVertex vertex = default;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            float height = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            Color tint = vertex.color;
            float value = Mathf.Lerp(.90f, 1.04f, height);
            tint.r *= value;
            tint.g *= value;
            tint.b *= value;
            vertex.color = tint;
            mesh.SetUIVertex(vertex, i);
        }
    }
}
