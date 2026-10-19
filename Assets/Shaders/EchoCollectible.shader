Shader "EchoRun/Collectible"
{
    Properties
    {
        _MainTex ("Vertex UV", 2D) = "white" {}
        _FrameColor ("Warm Frame", Color) = (0.85, 0.55, 0.20, 1)
        _FrameHighlight ("Warm Frame Highlight", Color) = (1.0, 0.92, 0.76, 1)
        _CoreColor ("Amber Memory Core", Color) = (1.0, 0.70, 0.29, 1)
        _CoreEdgeColor ("Warm Core Scan", Color) = (1.0, 0.95, 0.84, 1)
        _AccentColor ("Cyan Data Accent", Color) = (0.12, 0.91, 0.91, 1)
        _ContractColor ("Contract", Color) = (1.0, 0.34, 0.30, 1)
        _EmissionStrength ("Core Emission", Range(0, 4)) = 2.05
        _FrameEmissionStrength ("Frame Emission", Range(0, 2)) = 0.72
        _AccentEmissionStrength ("Accent Emission", Range(0, 2)) = 0.70
        _ScanPeriod ("Scan Period", Range(0.5, 4)) = 1.5
        _ContractMarker ("Contract Marker", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 180
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        fixed4 _FrameColor;
        fixed4 _FrameHighlight;
        fixed4 _CoreColor;
        fixed4 _CoreEdgeColor;
        fixed4 _AccentColor;
        fixed4 _ContractColor;
        half _EmissionStrength;
        half _FrameEmissionStrength;
        half _AccentEmissionStrength;
        half _ScanPeriod;
        half _ContractMarker;
        half _EchoVisualHigh;
        fixed4 _EchoPhaseTint;
        half _EchoPhaseIntensity;

        struct Input
        {
            float4 color : COLOR;
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            half frameWeight = saturate(input.color.r);
            half coreWeight = saturate(input.color.g);
            half accentWeight = saturate(input.color.b);
            half surfaceShade = lerp(0.22h, 1.0h,
                saturate(input.color.a));
            fixed3 frame = lerp(_FrameColor.rgb, _FrameHighlight.rgb,
                0.28h) * surfaceShade;
            fixed3 frameGlow = lerp(_FrameColor.rgb,
                _FrameHighlight.rgb, 0.68h)
                * lerp(0.58h, 1.0h, surfaceShade);
            fixed3 core = lerp(_CoreColor.rgb, _ContractColor.rgb,
                saturate(_ContractMarker));
            fixed3 accent = lerp(_AccentColor.rgb, _ContractColor.rgb,
                saturate(_ContractMarker) * 0.75h);
            fixed3 color = frame * frameWeight + core * coreWeight
                + accent * accentWeight;
            fixed3 phase = lerp(fixed3(1, 1, 1), _EchoPhaseTint.rgb,
                saturate(_EchoPhaseIntensity) * 0.12h);
            color *= phase;
            half fresnel = pow(1.0h - saturate(dot(normalize(input.viewDir),
                output.Normal)), 3.0h) * saturate(_EchoVisualHigh);
            half period = max(0.5h, _ScanPeriod);
            half scanPhase = frac(_Time.y / period);
            half scanDistance = abs(input.uv_MainTex.y - scanPhase);
            scanDistance = min(scanDistance, 1.0h - scanDistance);
            half scan = 1.0h - smoothstep(0.025h, 0.085h, scanDistance);
            output.Albedo = color * (0.78h + fresnel * 0.12h);
            output.Metallic = saturate(frameWeight * 0.78h
                + accentWeight * 0.52h);
            output.Smoothness = lerp(0.38h, 0.72h,
                saturate(frameWeight + _EchoVisualHigh * 0.35h));
            output.Emission = core * coreWeight * _EmissionStrength
                * (0.78h + scan * 0.22h)
                + _CoreEdgeColor.rgb * coreWeight * scan * 0.38h
                + frameGlow * frameWeight * _FrameEmissionStrength
                * (0.90h + fresnel * 0.10h)
                + accent * accentWeight * _AccentEmissionStrength;
            output.Alpha = 1.0h;
        }
        ENDCG
    }
    FallBack "Mobile/Diffuse"
}
