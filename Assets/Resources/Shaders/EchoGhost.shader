Shader "EchoRun/GhostRunner"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _Color ("Transparent Body", Color) = (0.443, 0.722, 0.769, 0.42)
        [HDR] _RimColor ("Rim Color", Color) = (0.804, 0.914, 0.910, 1)
        _RimPower ("Rim Power", Range(1, 7)) = 2.0
        _EmissionStrength ("Rim Emission", Range(0, 1)) = 0.30
        _ScanStrength ("Scan Strength", Range(0, 0.5)) = 0.08
        _GlitchStrength ("Horizontal Glitch", Range(0, 0.04)) = 0.008
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 120
        Cull Back
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Lambert alpha:premul vertex:vert noforwardadd keepalpha
        #pragma target 2.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _RimColor;
        half _RimPower;
        half _EmissionStrength;
        half _ScanStrength;
        half _GlitchStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
        };

        void vert(inout appdata_full vertex)
        {
            half localBand = step(0.965h,
                sin(vertex.vertex.y * 29.0h + floor(_Time.y * 3.0h) * 1.73h));
            half eventGate = step(0.91h, frac(_Time.y * 0.23h));
            half direction = sin(floor(_Time.y * 3.0h) * 2.11h) >= 0.0h
                ? 1.0h : -1.0h;
            vertex.vertex.x += localBand * eventGate * direction * _GlitchStrength;
        }

        void surf(Input input, inout SurfaceOutput output)
        {
            fixed3 source = tex2D(_MainTex, input.uv_MainTex).rgb;
            half textureValue = dot(source, fixed3(0.30, 0.59, 0.11));
            half rim = pow(1.0h - saturate(dot(normalize(input.viewDir), output.Normal)),
                _RimPower);
            half scanWave = 0.5h + 0.5h * sin(
                input.worldPos.y * 34.0h - _Time.y * 7.5h);
            half scan = step(0.93h, scanWave) * _ScanStrength;
            half dropoutWave = 0.5h + 0.5h * sin(
                input.worldPos.y * 17.0h + floor(_Time.y * 2.0h) * 2.37h);
            half dropoutGate = step(0.945h, dropoutWave)
                * step(0.82h, frac(_Time.y * 0.31h))
                * saturate(_GlitchStrength / 0.014h);
            half detail = lerp(0.62h, 0.92h, textureValue);

            output.Albedo = _Color.rgb * detail;
            output.Emission = _RimColor.rgb
                * (rim * _EmissionStrength + scan * 0.34h);
            output.Alpha = saturate(
                (_Color.a * (0.78h + detail * 0.22h)
                 + rim * 0.58h + scan * 0.16h)
                * (1.0h - dropoutGate * 0.88h));
        }
        ENDCG
    }

    FallBack "Unlit/Transparent"
}
