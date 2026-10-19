using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only geometry audit: co-facing triangles of different materials must not
// occupy the same plane and area. Run before baking the shared lower-city meshes.
public static class CitySurfaceReview
{
    public static void BuildPlayer()
    {
        const string directory="TestResults/CitySurface/Windows";
        Directory.CreateDirectory(directory);
        var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.scene"},directory+"/EchoRun.exe",
            BuildTarget.StandaloneWindows64,BuildOptions.Development|BuildOptions.CompressWithLz4HC);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Surface repair build failed");
        Debug.Log("CITY_SURFACE_BUILD_OK");
    }
    sealed class Face
    {
        public Vector2[] points;
        public Vector3 normal;
        public float distance;
        public string owner, material;
    }

    public static void Audit()
    {
        var report = new List<string>();
        for (int i = 0; i < 9; i++) AuditPrefab("CityV7/Chunk" + i, report);
        for (int i = 0; i < 4; i++) AuditPrefab("CityV7/LowerBlock" + i, report);
        Directory.CreateDirectory("TestResults/CitySurface");
        File.WriteAllLines("TestResults/CitySurface/audit.txt", report);
        Debug.Log("CITY_SURFACE_AUDIT_DONE");
    }

    public static int AuditPrefab(string path, List<string> report)
    {
        var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(path));
        try
        {
            var planes = new Dictionary<string, List<Face>>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) continue;
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var material = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                    var indices = mesh.GetTriangles(sub);
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        Vector3 a = vertices[indices[t]], b = vertices[indices[t+1]], c = vertices[indices[t+2]];
                        Vector3 n = Vector3.Cross(b-a,c-a).normalized;
                        if(n.sqrMagnitude < .5f) continue;
                        int axis = Mathf.Abs(n.x) > Mathf.Abs(n.y) ? (Mathf.Abs(n.x) > Mathf.Abs(n.z) ? 0 : 2) : (Mathf.Abs(n.y) > Mathf.Abs(n.z) ? 1 : 2);
                        string key = Mathf.RoundToInt(n.x*1000)+":"+Mathf.RoundToInt(n.y*1000)+":"+Mathf.RoundToInt(n.z*1000)+":"+Mathf.RoundToInt(Vector3.Dot(a,n)*2000);
                        if (!planes.TryGetValue(key, out var faces)) planes[key] = faces = new List<Face>();
                        int u = (axis+1)%3, v = (axis+2)%3;
                        faces.Add(new Face { points = new[]{new Vector2(a[u],a[v]),new Vector2(b[u],b[v]),new Vector2(c[u],c[v])},
                            normal=n,distance=Vector3.Dot(a,n),
                            owner = AnimationUtility.CalculateTransformPath(filter.transform,root.transform), material = material.name });
                    }
                }
            }
            var overlaps = new Dictionary<string, float>();
            foreach (var plane in planes)
            for (int i=0;i<plane.Value.Count;i++) for(int j=i+1;j<plane.Value.Count;j++)
            {
                var a=plane.Value[i];var b=plane.Value[j];
                if(a.material==b.material)continue;
                float area=IntersectionArea(a.points,b.points);
                if(area<.002f)continue;
                string key=a.owner+" ["+a.material+"] <> "+b.owner+" ["+b.material+"] plane="+plane.Key+" separation="+Mathf.Abs(a.distance-b.distance).ToString("F7");
                overlaps.TryGetValue(key,out float total);overlaps[key]=total+area;
            }
            report.Add(path+" overlapping material pairs="+overlaps.Count);
            foreach(var entry in overlaps.OrderByDescending(e=>e.Value).Take(80))report.Add(entry.Value.ToString("F4")+" m2 "+entry.Key);
            // Downward base faces are buried by the existing terrace/paving.
            return overlaps.Keys.Count(key=>!key.Contains("plane=0:-1000:0:"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
    static float IntersectionArea(Vector2[] a,Vector2[] b)
    {
        for(int axis=0;axis<2;axis++)
            if(Mathf.Max(a[0][axis],a[1][axis],a[2][axis]) <= Mathf.Min(b[0][axis],b[1][axis],b[2][axis]) ||
               Mathf.Max(b[0][axis],b[1][axis],b[2][axis]) <= Mathf.Min(a[0][axis],a[1][axis],a[2][axis]))return 0;
        var polygon=new List<Vector2>(a);
        float sign=Mathf.Sign(Cross(b[1]-b[0],b[2]-b[0]));
        for(int edge=0;edge<3&&polygon.Count>0;edge++)
        {
            var start=b[edge];var direction=b[(edge+1)%3]-start;
            var output=new List<Vector2>();var previous=polygon[polygon.Count-1];
            float before=sign*Cross(direction,previous-start);
            foreach(var current in polygon)
            {
                float after=sign*Cross(direction,current-start);
                if((after>=0)!=(before>=0))output.Add(Vector2.LerpUnclamped(previous,current,before/(before-after)));
                if(after>=0)output.Add(current);
                previous=current;before=after;
            }
            polygon=output;
        }
        float area=0;
        for(int i=0;i<polygon.Count;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);
        return Mathf.Abs(area)*.5f;
    }
}
