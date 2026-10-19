Shader "EchoRun/VertexColor"
{
    Properties
    {
        _EmissionStrength ("Cyan Emission Strength", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 180

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0

        half _EmissionStrength;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = input.color.rgb;
            output.Metallic = 0.04;
            output.Smoothness = 0.24;
            output.Emission = input.color.rgb * input.color.a * _EmissionStrength;
            output.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
