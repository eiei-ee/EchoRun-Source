Shader "EchoRun/OrangeEchoSurface"
{
    Properties
    {
        _Color ("Material Color", Color) = (0.8,0.23,0.065,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Grain ("Surface Grain", Range(0,0.2)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _Smoothness, _Metallic, _Grain;
        struct Input { float3 worldPos; };
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            // Restrained grain fades with distance to avoid mobile shimmer.
            float2 cell = floor(i.worldPos.xz * 85.0);
            half grain = frac(sin(dot(cell, float2(12.9898,78.233))) * 43758.5453);
            float distanceToCamera = distance(_WorldSpaceCameraPos, i.worldPos);
            half fade = 1.0 - smoothstep(3.0, 12.0, distanceToCamera);
            o.Albedo = _Color.rgb * (1.0 + (grain - .5) * _Grain * fade);
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Mobile/Diffuse"
}
