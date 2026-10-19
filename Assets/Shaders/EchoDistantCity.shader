Shader "EchoRun/DistantCity"
{
    Properties { _Color ("Silhouette", Color) = (.30,.38,.46,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 position:SV_POSITION; float3 world:TEXCOORD0; };
            v2f vert(appdata_base v)
            { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float distanceToView=distance(_WorldSpaceCameraPos.xz,i.world.xz);
                float haze=saturate(.22+distanceToView/650.0+saturate((12-i.world.y)/45)*.15);
                return fixed4(lerp(_Color.rgb,unity_FogColor.rgb,haze),1);
            }
            ENDCG
        }
    }
}
