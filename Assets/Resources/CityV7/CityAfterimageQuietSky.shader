Shader "CityAfterimage/QuietSky"
{
    Properties
    {
        _Zenith ("Blue grey zenith", Color) = (0.565,0.663,0.722,1)
        _Horizon ("Pale horizon", Color) = (0.749,0.792,0.816,1)
        _Ground ("Lower hemisphere", Color) = (0.682,0.725,0.729,1)
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
            #include "UnityCG.cginc"
            fixed4 _Zenith, _Horizon, _Ground;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.direction = v.vertex.xyz; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float height = normalize(i.direction).y;
                float4 upper = lerp(_Horizon, _Zenith, smoothstep(0, .8, max(0,height)));
                return height >= 0 ? upper : lerp(_Horizon, _Ground, saturate(-height * 3));
            }
            ENDCG
        }
    }
}
