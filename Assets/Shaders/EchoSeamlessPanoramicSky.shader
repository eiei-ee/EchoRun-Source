Shader "EchoRun/SeamlessPanoramicSky"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "grey" {}
        _Tint ("Tint Color", Color) = (.5, .5, .5, 1)
        _Exposure ("Exposure", Range(0, 8)) = 0.54
        _Saturation ("Saturation", Range(0, 1)) = 0.22
        _Rotation ("Rotation", Range(0, 360)) = 0
        _SeamBlend ("Seam Blend", Range(0.001, 0.1)) = 0.07
        _HorizonTexY ("Source Horizon Height", Range(0.1, 0.5)) = 0.24
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half4 _Tint;
            half _Exposure;
            half _Saturation;
            float _Rotation;
            float _SeamBlend;
            float _HorizonTexY;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float3 RotateAroundYInDegrees(float3 value, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float sine;
                float cosine;
                sincos(radians, sine, cosine);
                float2x2 rotationMatrix = float2x2(cosine, -sine, sine, cosine);
                return float3(mul(rotationMatrix, value.xz), value.y).xzy;
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = RotateAroundYInDegrees(input.vertex.xyz, _Rotation);
                return output;
            }

            float2 ToRadialCoords(float3 direction)
            {
                direction = normalize(direction);
                float latitude = acos(direction.y);
                float longitude = atan2(direction.z, direction.x);
                float2 sphereCoords = float2(longitude, latitude)
                    * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
            }

            half4 frag(v2f input) : SV_Target
            {
                float2 uv = ToRadialCoords(input.direction);
                uv.x = frac(uv.x);

                // EchoSky is a perspective concept painting, not a true
                // equirectangular panorama. Map the painting's authored
                // horizon to the skybox horizon, then use its own blue ground
                // below it. This avoids the previous hard dark hemisphere.
                float upperAmount = saturate((uv.y - 0.5) * 2.0);
                float lowerAmount = saturate(uv.y * 2.0);
                uv.y = uv.y >= 0.5
                    ? lerp(_HorizonTexY, 1.0, upperAmount)
                    : lerp(0.0, _HorizonTexY, lowerAmount);
                uv.y = saturate(uv.y);

                // The source artwork is not authored as a seamless panorama.
                // Pair samples from both edges only inside a narrow blend band,
                // making the 0/1 wrap continuous without washing out the city.
                float halfTexel = max(_MainTex_TexelSize.x * 0.5, 0.00001);
                float leftRightU = clamp(uv.x, halfTexel, 1.0 - halfTexel);
                float pairedU = clamp(1.0 - uv.x, halfTexel, 1.0 - halfTexel);
                half4 city = tex2D(_MainTex, float2(leftRightU, uv.y));
                half4 oppositeEdge = tex2D(_MainTex, float2(pairedU, uv.y));

                float edgeDistance = min(uv.x, 1.0 - uv.x);
                float seamWeight = 1.0 - smoothstep(
                    0.0, max(_SeamBlend, 0.001), edgeDistance);
                half4 color = lerp(city, (city + oppositeEdge) * 0.5, seamWeight);
                half luminance = dot(color.rgb,
                    half3(0.2126h, 0.7152h, 0.0722h));
                color.rgb = lerp(luminance.xxx, color.rgb,
                    saturate(_Saturation));
                color.rgb *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;

                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
