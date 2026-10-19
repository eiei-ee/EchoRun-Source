Shader "Hidden/EchoRun/PostFx"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragExtract
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 fragExtract(v2f_img i) : SV_Target
            {
                fixed3 color = tex2D(_MainTex, i.uv).rgb;
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                half contribution = saturate((luminance - 0.62h) * 2.8h);
                return fixed4(color * contribution, 1.0h);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragBlur
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float2 _BlurDirection;
            fixed4 fragBlur(v2f_img i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurDirection;
                fixed3 color = tex2D(_MainTex, i.uv).rgb * 0.40h;
                color += tex2D(_MainTex, i.uv + offset).rgb * 0.24h;
                color += tex2D(_MainTex, i.uv - offset).rgb * 0.24h;
                color += tex2D(_MainTex, i.uv + offset * 2.0h).rgb * 0.06h;
                color += tex2D(_MainTex, i.uv - offset * 2.0h).rgb * 0.06h;
                return fixed4(color, 1.0h);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragComposite
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _BloomTex;
            half _BloomEnabled;
            half _BloomIntensity;
            half _GradingEnabled;
            half _VignetteEnabled;
            half _EchoPhaseBloomBoost;
            half _EchoPhaseContrast;
            fixed4 _EchoPhaseTint;
            half _EchoPhaseIntensity;

            fixed4 fragComposite(v2f_img i) : SV_Target
            {
                fixed3 source = tex2D(_MainTex, i.uv).rgb;
                fixed3 bloom = tex2D(_BloomTex, i.uv).rgb;
                source += bloom * _BloomEnabled
                    * (_BloomIntensity + _EchoPhaseBloomBoost);

                half luminance = dot(source, half3(0.2126h, 0.7152h, 0.0722h));
                fixed3 shadowTint = fixed3(0.88h, 0.95h, 1.08h);
                fixed3 highlightTint = fixed3(0.98h, 1.035h, 1.04h);
                fixed3 graded = source * lerp(shadowTint, highlightTint,
                    saturate(luminance));
                graded = (graded - 0.5h) * (1.035h + _EchoPhaseContrast) + 0.5h;
                source = lerp(source, graded, _GradingEnabled * 0.55h);
                fixed3 phaseMultiplier = fixed3(0.55h, 0.55h, 0.55h)
                    + max(_EchoPhaseTint.rgb,
                        fixed3(0.001h, 0.001h, 0.001h)) * 0.90h;
                source = lerp(source, source * phaseMultiplier,
                    saturate(_EchoPhaseIntensity) * 0.18h);

                float2 centered = i.uv * 2.0h - 1.0h;
                half vignette = smoothstep(1.18h, 0.36h, dot(centered, centered));
                source *= lerp(1.0h, lerp(0.82h, 1.0h, vignette),
                    _VignetteEnabled);
                return fixed4(source, 1.0h);
            }
            ENDCG
        }
    }
    FallBack Off
}
