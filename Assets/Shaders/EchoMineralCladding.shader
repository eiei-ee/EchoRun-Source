Shader "EchoRun/MineralCladding"
{
    Properties
    {
        _Color ("Mineral tint", Color) = (0.75,0.78,0.8,1)
        _MainTex ("Authored panel albedo", 2D) = "white" {}
        _SurfaceDetail ("Panel slope RG, occlusion B, smoothness A", 2D) = "gray" {}
        _DetailStrength ("Surface relief", Range(0,1)) = 0
        _PanelScale ("Tiles per metre", Float) = 0.18
        _Glossiness ("Smoothness", Range(0,1)) = 0.24
        _Metallic ("Metallic", Range(0,1)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _SurfaceDetail;
        fixed4 _Color;
        half _PanelScale, _Glossiness, _Metallic, _DetailStrength;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // World-scale projection keeps the authored tile consistent across
            // imported facade meshes whose UV scale varies between modules.
            float3 normal = normalize(WorldNormalVector(IN, float3(0,0,1)));
            float3 weights = pow(abs(normal), 8);
            weights /= max(dot(weights, float3(1,1,1)), 0.0001);
            float3 p = IN.worldPos * _PanelScale;
            fixed3 tile = tex2D(_MainTex, p.zy).rgb * weights.x
                        + tex2D(_MainTex, p.xz).rgb * weights.y
                        + tex2D(_MainTex, p.xy).rgb * weights.z;
            half4 dx = tex2D(_SurfaceDetail, p.zy);
            half4 dy = tex2D(_SurfaceDetail, p.xz);
            half4 dz = tex2D(_SurfaceDetail, p.xy);
            half4 detail = dx * weights.x + dy * weights.y + dz * weights.z;
            // Stored slopes are projected in world metres, so imported FBX
            // UV density and merged podium meshes share the same surface scale.
            float2 sx = dx.rg * 2 - 1, sy = dy.rg * 2 - 1, sz = dz.rg * 2 - 1;
            float3 gradient = float3(0,sx.y,sx.x) * weights.x
                            + float3(sy.x,0,sy.y) * weights.y
                            + float3(sz.x,sz.y,0) * weights.z;
            gradient -= normal * dot(normal, gradient);
            float3 detailedNormal = normalize(normal + gradient * _DetailStrength);
            float3 tangent = WorldNormalVector(IN, float3(1,0,0));
            float3 bitangent = WorldNormalVector(IN, float3(0,1,0));
            o.Normal = normalize(float3(dot(detailedNormal,tangent),
                dot(detailedNormal,bitangent),dot(detailedNormal,normal)));
            o.Albedo = tile * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness + (detail.a - .5) * .24 * _DetailStrength);
            o.Occlusion = lerp(1, detail.b, _DetailStrength);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
