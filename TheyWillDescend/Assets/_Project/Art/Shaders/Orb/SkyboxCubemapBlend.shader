// Blends two cubemap skyboxes (day/night) with a single _Blend factor.
// Used by TimeLightController for smooth day/night skybox transitions.
Shader "Custom/Skybox/CubemapBlend"
{
    Properties
    {
        _Tex1 ("Cubemap Day", Cube) = "grey" {}
        _Tex2 ("Cubemap Night", Cube) = "black" {}
        _Exposure1 ("Exposure Day", Range(0, 8)) = 1.0
        _Exposure2 ("Exposure Night", Range(0, 8)) = 1.0
        _Blend ("Blend (0=Night, 1=Day)", Range(0, 1)) = 0
        [Gamma] _TintColor ("Tint Color", Color) = (0.5, 0.5, 0.5, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEXCUBE(_Tex1);
            UNITY_DECLARE_TEXCUBE(_Tex2);
            half4 _Tex1_HDR;
            half4 _Tex2_HDR;
            float4 _TintColor;
            half _Exposure1;
            half _Exposure2;
            half _Blend;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 c1 = DecodeHDR(UNITY_SAMPLE_TEXCUBE(_Tex1, i.texcoord), _Tex1_HDR) * _Exposure1;
                half3 c2 = DecodeHDR(UNITY_SAMPLE_TEXCUBE(_Tex2, i.texcoord), _Tex2_HDR) * _Exposure2;
                half3 c = lerp(c2, c1, _Blend);
                c *= _TintColor.rgb * unity_ColorSpaceDouble.rgb;
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
