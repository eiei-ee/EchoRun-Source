Shader "EchoRun/Road"
{
    Properties
    {
        _Color ("Road Base", Color) = (0.052, 0.057, 0.061, 1)
        [NoScaleOffset] _RoadAtlas ("Road Atlas", 2D) = "gray" {}
        [NoScaleOffset] _NormalMap ("Road Normal", 2D) = "bump" {}
        _AtlasTiling ("Atlas Tiling", Vector) = (1, 4, 0, 0)
        _LaneColor ("Inset Neutral", Color) = (0.28, 0.31, 0.32, 1)
        _EdgeColor ("Edge Neutral", Color) = (0.62, 0.66, 0.67, 1)
        _FlowSpeed ("Scan Speed", Range(0, 1)) = 0.08
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.46
        _Wetness ("Wetness", Range(0, 1)) = 0.18
        _ReflectionStrength ("Fake Reflection", Range(0, 1)) = 0.05
        _RoadRole ("Road Role", Float) = 0
        _RoadUvScale ("Role UV Scale", Float) = 1
        _RoadSeamStrength ("Seam Strength", Float) = 0
        _RoadStartDeckBoost ("Start Deck Boost", Float) = 0
        _RoadLaneDensity ("Lane Density", Float) = 1
        _RoadSafeLaneHint ("Safe Lane Hint", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ECHO_NORMALMAP
            #pragma shader_feature_local _ECHO_FAKE_REFLECTION
            #pragma shader_feature_local _ECHO_WET_SURFACE
            #pragma shader_feature_local _ECHO_PLANAR_REFLECTION

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _RoadAtlas;
            sampler2D _NormalMap;
            fixed4 _Color;
            fixed4 _LaneColor;
            fixed4 _EdgeColor;
            float4 _AtlasTiling;
            half _FlowSpeed;
            half _NormalStrength;
            half _Wetness;
            half _ReflectionStrength;
            half _RoadRole;
            half _RoadUvScale;
            half _RoadSeamStrength;
            half _RoadStartDeckBoost;
            half _RoadLaneDensity;
            half _RoadSafeLaneHint;
            fixed4 _EchoPhaseTint;
            half _EchoPhaseIntensity;
            half _EchoPhaseCoral;
            sampler2D _EchoPlanarReflectionTex;
            float4x4 _EchoReflectionVP;
            half _EchoPlanarReflectionStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                half3 worldTangent : TEXCOORD3;
                half3 worldBinormal : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                float4 reflectionPosition : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            inline half Band(float value, float center, float width, float softness)
            {
                return 1.0h - smoothstep(width, width + softness,
                    abs(value - center));
            }

            inline half Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * tangentSign;
                o.reflectionPosition = mul(_EchoReflectionVP,
                    float4(o.worldPos, 1.0));
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 atlasUv = i.uv * _AtlasTiling.xy
                    * max(0.1h, _RoadUvScale);
                fixed3 atlas = tex2D(_RoadAtlas, atlasUv).rgb;
                fixed luminance = dot(atlas, fixed3(0.299, 0.587, 0.114));
                fixed3 surface = _Color.rgb * lerp(0.88h, 1.08h, luminance);

                half3 normalDirection = normalize(i.worldNormal);
                #if defined(_ECHO_NORMALMAP)
                    half3 tangentNormal = UnpackNormal(tex2D(_NormalMap, atlasUv));
                    tangentNormal.xy *= _NormalStrength;
                    tangentNormal.z = sqrt(saturate(1.0h
                        - dot(tangentNormal.xy, tangentNormal.xy)));
                    normalDirection = normalize(i.worldTangent * tangentNormal.x
                        + i.worldBinormal * tangentNormal.y
                        + i.worldNormal * tangentNormal.z);
                #endif

                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                half ndl = saturate(dot(normalDirection,
                    normalize(_WorldSpaceLightPos0.xyz)));
                surface *= 0.82h + ndl * 0.16h;

                half centerGuide = Band(i.uv.x, 0.5h, 0.004h, 0.006h);
                half leftEdge = Band(i.uv.x, 0.055h, 0.005h, 0.009h);
                half rightEdge = Band(i.uv.x, 0.945h, 0.005h, 0.009h);
                half laneDivider = max(Band(i.uv.x, 0.333h, 0.0025h, 0.005h),
                    Band(i.uv.x, 0.667h, 0.0025h, 0.005h));
                half seamRole = step(1.5h, _RoadRole) * (1.0h - step(2.5h, _RoadRole));
                half regularRoad = 1.0h - seamRole;
                centerGuide *= regularRoad * saturate(_RoadLaneDensity);
                half edges = max(leftEdge, rightEdge) * regularRoad;
                laneDivider *= regularRoad;

                // One restrained transverse memory node per 20 m road surface.
                // It gives speed scale without painting circuit graphics everywhere.
                half memoryNode = Band(frac(i.uv.y), 0.5h, 0.008h, 0.012h)
                    * regularRoad;

                half flowPhase = frac(i.uv.y
                    - _Time.y * max(0.01h, _FlowSpeed));
                half scanPulse = smoothstep(0.91h, 0.97h, flowPhase)
                    * (1.0h - smoothstep(0.97h, 1.0h, flowPhase));
                half phaseAmount = saturate(_EchoPhaseIntensity);
                fixed3 phaseColor = max(_EchoPhaseTint.rgb,
                    fixed3(0.001h, 0.001h, 0.001h));

                // Lane recognition stays neutral. Stage color is reserved for the
                // narrow edge flow, scan pulse and memory node.
                fixed3 emission = _LaneColor.rgb * laneDivider * 0.045h;
                emission += _EdgeColor.rgb * edges * 0.025h;
                emission += phaseColor * edges
                    * (0.025h + phaseAmount * 0.12h);
                emission += phaseColor * centerGuide * phaseAmount
                    * (0.035h + scanPulse * 0.16h);
                emission += phaseColor * memoryNode
                    * (0.018h + phaseAmount * 0.075h);
                emission += _LaneColor.rgb * seamRole
                    * saturate(_RoadSeamStrength) * 0.08h;

                half viewHighlight = pow(1.0h
                    - saturate(dot(normalDirection, viewDirection)), 3.0h);
                surface += _EdgeColor.rgb * viewHighlight * 0.018h;

                #if defined(_ECHO_WET_SURFACE)
                    half rainNoise = Hash21(floor(i.worldPos.xz * 1.7h));
                    half rainStain = smoothstep(0.68h, 0.94h, rainNoise) * _Wetness;
                    surface = lerp(surface, surface * 0.78h, rainStain * 0.08h);
                    emission += _EdgeColor.rgb * rainStain * viewHighlight * 0.012h;
                #endif

                #if defined(_ECHO_FAKE_REFLECTION)
                    half3 reflectionDirection = reflect(-viewDirection, normalDirection);
                    half4 encodedReflection = UNITY_SAMPLE_TEXCUBE(
                        unity_SpecCube0, reflectionDirection);
                    half3 reflection = DecodeHDR(encodedReflection, unity_SpecCube0_HDR);
                    half fresnel = pow(1.0h
                        - saturate(dot(normalDirection, viewDirection)), 4.0h);
                    surface += reflection * (_ReflectionStrength
                        * (0.18h + fresnel * 0.82h) * _Wetness);
                #endif

                #if defined(_ECHO_PLANAR_REFLECTION)
                    float2 reflectionUv = i.reflectionPosition.xy
                        / max(0.001h, i.reflectionPosition.w) * 0.5h + 0.5h;
                    half inside = step(0.0h, reflectionUv.x)
                        * step(reflectionUv.x, 1.0h)
                        * step(0.0h, reflectionUv.y)
                        * step(reflectionUv.y, 1.0h);
                    fixed3 planar = tex2D(_EchoPlanarReflectionTex,
                        reflectionUv).rgb;
                    surface += planar * _EchoPlanarReflectionStrength
                        * inside * _Wetness;
                #endif

                // The graphite road never receives a full-surface phase wash.
                // Coral/relearn energy stays a local hairline, not a red floor.
                emission += phaseColor * centerGuide
                    * saturate(_EchoPhaseCoral) * 0.035h;

                surface += _Color.rgb * saturate(_RoadStartDeckBoost) * 0.06h;
                surface += _LaneColor.rgb * saturate(_RoadSafeLaneHint) * 0.015h;
                fixed4 color = fixed4(surface + emission, 1.0h);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
