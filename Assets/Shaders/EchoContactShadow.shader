Shader "EchoRun/ContactShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0.012, 0.018, 0.026, 0.34)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent-10"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        LOD 50
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centered = (input.uv - 0.5) * 2.0;
                float radiusSquared = dot(centered, centered);
                float feather = 1.0 - smoothstep(0.12, 1.0, radiusSquared);
                return fixed4(_Color.rgb, _Color.a * feather);
            }
            ENDCG
        }
    }

    FallBack Off
}
