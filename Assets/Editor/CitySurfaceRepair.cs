using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Remove only the shared area of coplanar, co-facing authored surfaces. Material
// priority keeps structural trim and pale cladding over the underlying stone.
// Unlike a render offset this also works after the lower district is mesh-batched.
public static class CitySurfaceRepair
{
    struct Vertex
    {
        public Vector3 position, normal;
        public Vector4 tangent;
        public Vector2 uv, uv2;
        public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
            position=Vector3.LerpUnclamped(a.position,b.position,t),
            normal=Vector3.LerpUnclamped(a.normal,b.normal,t),
            tangent=Vector4.LerpUnclamped(a.tangent,b.tangent,t), uv=Vector2.LerpUnclamped(a.uv,b.uv,t),
            uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,t)};
    }
    sealed class Triangle
    {
        public Vector3 a,b,c,normal;
        public Bounds bounds;
        public MeshFilter owner;
        public int priority;
    }
    static int Priority(string name) => name == "WindowFrame" ? 6 : name.EndsWith("_Glass") || name == "WarmWindow" ? 5 :
        name.EndsWith("_Structure") ? 4 : name.EndsWith("_Pale") ? 3 : name.EndsWith("_Mineral") ? 2 : 1;

    public static void RestoreAuthoredMeshes(GameObject root)
    {
        // Rebuild from the unchanged FBX, never repeatedly clip an earlier bake.
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/Art/CityLayers/Clean_"))continue;
            int separator=filter.name.IndexOf("__CityV7_",StringComparison.Ordinal);
            if(separator<0)continue;
            string path="Assets/CityAfterimageV7/Models/"+filter.name.Substring(0,separator)+".fbx";
            var original=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m=>m.name==filter.name);
            if(original==null)throw new InvalidOperationException("Cannot resolve authored source: "+filter.name);
            filter.sharedMesh=original;
        }
    }

    public static void RepairChunks()
    {
        int removed=0;
        for(int index=0;index<9;index++)
        {
            string path="Assets/Resources/CityV7/Chunk"+index+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(Transform building in root.transform)
                {
                    var filters=building.GetComponentsInChildren<MeshFilter>().Where(f=>
                        f.transform.parent==building && f.name.Contains("__CityV7_")).ToArray();
                    var triangles=Triangles(building.GetComponentsInChildren<MeshFilter>());
                    foreach(var filter in filters)removed+=Repair(filter,triangles,index.ToString());
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Debug.Log("CITY_SURFACE_REPAIR clipped triangles="+removed);
    }

    public static void RepairLowerBlock(GameObject root)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>();
        var triangles=Triangles(filters);
        int removed=0;
        foreach(var filter in filters)removed+=Repair(filter,triangles,root.name);
        Debug.Log(root.name+" final-scale clipped triangles="+removed);
    }

    // A bridge girder extends through the concrete slab. Its end caps used to
    // share the slab's end plane, so the visible cross section alternated
    // between dark metal and pale concrete. Keep the concrete face and trim
    // only the intersecting area of the metal faces; exposed undersides stay.
    public static int RepairBridgeStructure(GameObject root, string segmentName)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>();
        var triangles = Triangles(filters);
        foreach (var triangle in triangles)
            triangle.priority = triangle.owner.GetComponent<Renderer>().sharedMaterial.name == "Concrete" ? 2 : 1;
        int removed = 0;
        foreach (var filter in filters)
            if (filter.GetComponent<Renderer>().sharedMaterial.name == "RoofMetal")
                // Float interpolation at the pier/crosshead edge can leave
                // sub-micrometre strips (~2.4e-8 m2). Do not treat those as new
                // overlaps on every invocation. One square millimetre is far
                // below a visible bridge feature; other city repairs keep
                // their existing tolerance.
                removed += Repair(filter, triangles, "Bridge_" + segmentName, 1e-6f);
        return removed;
    }

    static List<Triangle> Triangles(MeshFilter[] filters)
    {
        var result=new List<Triangle>();
        foreach(var filter in filters)
        {
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                Vector3 a=filter.transform.TransformPoint(vertices[indices[i]]),
                    b=filter.transform.TransformPoint(vertices[indices[i+1]]),
                    c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);bounds.Expand(.004f);
                result.Add(new Triangle{a=a,b=b,c=c,normal=Vector3.Cross(b-a,c-a).normalized,
                    bounds=bounds,owner=filter,priority=Priority(filter.GetComponent<Renderer>().sharedMaterial.name)});
            }
        }
        return result;
    }

    static int Repair(MeshFilter filter,List<Triangle> all,string chunk,float minimumArea=1e-8f)
    {
        var original=filter.sharedMesh;
        if(original.subMeshCount!=1)throw new InvalidOperationException("Expected material-separated city mesh: "+filter.name);
        var positions=original.vertices;var normals=original.normals;var tangents=original.tangents;var uv=original.uv;var uv2=original.uv2;
        bool hasUv2=positions.Length>0 && uv2.Length==positions.Length;
        var vertices=new List<Vertex>();var indices=new List<int>();int modified=0;
        var input=original.triangles;int priority=Priority(filter.GetComponent<Renderer>().sharedMaterial.name);
        var candidates=all.Where(t=>t.owner!=filter && t.priority>priority &&
            t.bounds.Intersects(filter.GetComponent<Renderer>().bounds)).ToArray();
        for(int i=0;i<input.Length;i+=3)
        {
            var polygon=new List<Vertex>();
            for(int j=0;j<3;j++)
            {
                int k=input[i+j];polygon.Add(new Vertex{position=positions[k],normal=normals[k],
                    tangent=tangents.Length>k?tangents[k]:Vector4.zero,uv=uv.Length>k?uv[k]:Vector2.zero,
                    uv2=hasUv2?uv2[k]:Vector2.zero});
            }
            Vector3 a=filter.transform.TransformPoint(polygon[0].position),
                b=filter.transform.TransformPoint(polygon[1].position),c=filter.transform.TransformPoint(polygon[2].position);
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);bounds.Expand(.004f);
            var pieces=new List<List<Vertex>>{polygon};bool changed=false;
            foreach(var other in candidates)
            {
                if(other.owner==filter||other.priority<=priority||!bounds.Intersects(other.bounds) ||
                    // Imported vertices can leave sub-millimetre gaps after
                    // lower-city scaling. Treat up to 2 mm as the same surface.
                    Vector3.Dot(normal,other.normal)<.99999f || Mathf.Abs(Vector3.Dot(other.a-a,normal))>.002f)continue;
                var next=new List<List<Vertex>>();
                foreach(var piece in pieces)
                {
                    var inside=piece;var outside=new List<List<Vertex>>();
                    var corners=new[]{other.a,other.b,other.c};
                    for(int edge=0;edge<3&&inside.Count>=3;edge++)
                    {
                        Vector3 edgeStart=corners[edge],direction=corners[(edge+1)%3]-edgeStart;
                        Split(inside,filter.transform,edgeStart,direction,normal,out var keep,out var cut);
                        if(Area(cut)>minimumArea)outside.Add(cut);
                        inside=keep;
                    }
                    if(Area(inside)>minimumArea){changed=true;next.AddRange(outside);}else next.Add(piece);
                }
                pieces=next;
            }
            if(changed)modified++;
            foreach(var piece in pieces)
            {
                int start=vertices.Count;vertices.AddRange(piece);
                for(int j=1;j<piece.Count-1;j++){indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);}
            }
        }
        if(modified==0)return 0;
        var mesh=new Mesh{name=original.name,indexFormat=IndexFormat.UInt32};
        mesh.SetVertices(vertices.Select(v=>v.position).ToList());mesh.SetNormals(vertices.Select(v=>v.normal).ToList());
        mesh.SetTangents(vertices.Select(v=>v.tangent).ToList());mesh.SetUVs(0,vertices.Select(v=>v.uv).ToList());
        if(hasUv2)mesh.SetUVs(1,vertices.Select(v=>v.uv2).ToList());
        mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        string path="Assets/Art/CityLayers/Clean_"+chunk+"_"+filter.name+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(asset==null){AssetDatabase.CreateAsset(mesh,path);asset=mesh;}
        else{EditorUtility.CopySerialized(mesh,asset);EditorUtility.SetDirty(asset);asset.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);}
        filter.sharedMesh=asset;
        return modified;
    }

    static float Area(List<Vertex> polygon)
    {
        float area=0;
        for(int i=1;i<polygon.Count-1;i++)area+=Vector3.Cross(polygon[i].position-polygon[0].position,polygon[i+1].position-polygon[0].position).magnitude*.5f;
        return area;
    }
    static void Split(List<Vertex> polygon,Transform transform,Vector3 start,Vector3 direction,Vector3 normal,
        out List<Vertex> inside,out List<Vertex> outside)
    {
        inside=new List<Vertex>();outside=new List<Vertex>();
        if(polygon.Count==0)return;
        var previous=polygon[polygon.Count-1];
        float before=Vector3.Dot(Vector3.Cross(direction,transform.TransformPoint(previous.position)-start),normal);
        foreach(var current in polygon)
        {
            float after=Vector3.Dot(Vector3.Cross(direction,transform.TransformPoint(current.position)-start),normal);
            if((after>=0)!=(before>=0))
            {
                var crossing=Vertex.Lerp(previous,current,before/(before-after));inside.Add(crossing);outside.Add(crossing);
            }
            if(after>=0)inside.Add(current);else outside.Add(current);
            previous=current;before=after;
        }
    }
}
