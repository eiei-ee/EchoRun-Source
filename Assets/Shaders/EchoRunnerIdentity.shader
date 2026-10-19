Shader "EchoRun/RunnerIdentity"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.025, 0.045, 0.065, 1)
        [HDR] _IdentityColor ("Identity Color", Color) = (1.35, 0.88, 0.32, 1)
        _IdentityStrength ("Identity Strength", Range(0, 4)) = 0
        _PulseAmount ("Core Pulse Amount", Range(0, 0.35)) = 0
        _PulseSpeed ("Core Pulse Speed", Range(0, 6)) = 2.2
        _Metallic ("Metallic", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.48
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 120

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0

        fixed4 _BaseColor;
        half4 _IdentityColor;
        half _IdentityStrength;
        half _PulseAmount;
        half _PulseSpeed;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = lerp(
                _BaseColor.rgb, _IdentityColor.rgb,
                saturate(_IdentityStrength * 0.11));
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            half pulse = 1.0h + sin(_Time.y * _PulseSpeed) * _PulseAmount;
            output.Emission = _IdentityColor.rgb * _IdentityStrength * pulse;
            output.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
