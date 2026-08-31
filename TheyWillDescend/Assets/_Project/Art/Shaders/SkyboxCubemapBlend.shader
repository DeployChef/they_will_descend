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

            samplerCUBE _Tex1;
            samplerCUBE _Tex2;
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
                half4 c1 = texCUBE(_Tex1, i.texcoord) * half4(_Exposure1, _Exposure1, _Exposure1, 1);
                half4 c2 = texCUBE(_Tex2, i.texcoord) * half4(_Exposure2, _Exposure2, _Exposure2, 1);
                half4 c = lerp(c2, c1, _Blend);
                c *= _TintColor * 2.0;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
