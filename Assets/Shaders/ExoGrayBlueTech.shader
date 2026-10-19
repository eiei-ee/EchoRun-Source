Shader "EchoRun/ExoGrayBlueTech"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _DarkColor ("Dark Blue Gray", Color) = (0.035, 0.075, 0.12, 1)
        _LightColor ("Light Blue Gray", Color) = (0.32, 0.46, 0.60, 1)
        [HDR] _EmissionColor ("Cyan Emission", Color) = (0, 1.3, 2.1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 4)) = 1.6
        _AccentThreshold ("Warm Accent Threshold", Range(0, 0.5)) = 0.08
        _Metallic ("Metallic", Range(0, 1)) = 0.22
        _Smoothness ("Smoothness", Range(0, 1)) = 0.38
        _ToneScale ("Material Tone Scale", Range(0.35, 1.5)) = 1
        _ToneOffset ("Material Tone Offset", Range(-0.25, 0.25)) = 0
        [HDR] _IdentityColor ("Identity Rim Color", Color) = (1.25, 0.92, 0.48, 1)
        _RimStrength ("Identity Rim Strength", Range(0, 1.5)) = 0.28
        _RimPower ("Identity Rim Power", Range(1, 8)) = 3.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 180

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0

        sampler2D _MainTex;
        fixed4 _DarkColor;
        fixed4 _LightColor;
        half4 _EmissionColor;
        half _EmissionStrength;
        half _AccentThreshold;
        half _Metallic;
        half _Smoothness;
        half _ToneScale;
        half _ToneOffset;
        half4 _IdentityColor;
        half _RimStrength;
        half _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed3 source = tex2D(_MainTex, input.uv_MainTex).rgb;
            half luminance = dot(source, half3(0.299, 0.587, 0.114));
            half warmAccent = saturate((source.r - max(source.g, source.b) - _AccentThreshold) * 5.0);
            // Imported meshes need not have a valid tangent basis. Evaluate the
            // rim in world space so turning cannot flood the suit with emission.
            half facing = abs(dot(normalize(input.worldNormal),
                normalize(UnityWorldSpaceViewDir(input.worldPos))));
            half rim = pow(saturate(1.0 - facing),
                max(1.0h, _RimPower));

            fixed3 blueGray = saturate(
                lerp(_DarkColor.rgb, _LightColor.rgb, luminance)
                * _ToneScale + _ToneOffset);
            output.Albedo = lerp(blueGray, _EmissionColor.rgb * 0.16, warmAccent);
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Emission = _EmissionColor.rgb * warmAccent * _EmissionStrength
                + _IdentityColor.rgb * rim * _RimStrength;
            output.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
