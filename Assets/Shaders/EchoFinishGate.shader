Shader "EchoRun/FinishGate"
{
    Properties
    {
        _StructureColor ("Structure", Color) = (0.055, 0.09, 0.14, 1)
        _SignalColor ("Signal", Color) = (0.08, 0.82, 1.0, 1)
        _CoreColor ("Core", Color) = (1.0, 0.34, 0.30, 1)
        _GateRole ("Gate Role", Float) = 0
        _GateProgress ("Gate Progress", Range(0, 1)) = 0
        _PulseAmount ("Signal Pulse", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 160
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _StructureColor;
            fixed4 _SignalColor;
            fixed4 _CoreColor;
            half _GateRole;
            half _GateProgress;
            half _PulseAmount;
            half _EchoVisualHigh;
            fixed4 _EchoPhaseTint;
            half _EchoPhaseIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half signalRole = step(0.5h, _GateRole);
                half coreRole = step(1.5h, _GateRole);
                fixed3 color = lerp(_StructureColor.rgb, _SignalColor.rgb,
                    signalRole);
                color = lerp(color, _CoreColor.rgb, coreRole);
                half pulse = lerp(1.0h, 0.76h + 0.24h * sin(_Time.y * 4.0h
                    + i.worldPos.y * 1.4h), saturate(_PulseAmount));
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                half fresnel = pow(1.0h - saturate(dot(normalize(i.worldNormal),
                    viewDirection)), 3.0h) * saturate(_EchoVisualHigh);
                fixed3 phase = lerp(fixed3(1, 1, 1), _EchoPhaseTint.rgb,
                    saturate(_EchoPhaseIntensity) * 0.15h);
                color *= phase;
                fixed3 emission = color * signalRole
                    * lerp(0.22h, 1.15h, saturate(_GateProgress))
                    * (coreRole > 0.5h ? pulse : 0.68h);
                fixed4 result = fixed4(color * (0.62h + fresnel * 0.18h)
                    + emission, 1.0h);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
