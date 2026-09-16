Shader "TheyWillDescend/FootprintOutline"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        _CityCenter ("City Center", Vector) = (0, 0, 0, 0)
        _InnerRadius ("Inner Radius", Float) = 1
        _OuterRadius ("Outer Radius", Float) = 2
        _Angle0 ("Angle 0", Float) = 0
        _AngleSpan ("Angle Span", Float) = 0.5
        _LineWidth ("Line Width", Float) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CityCenter;
                float _InnerRadius;
                float _OuterRadius;
                float _Angle0;
                float _AngleSpan;
                float _LineWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Wrap02Pi(float angle)
            {
                const float twoPi = 6.28318530718;
                angle = angle - twoPi * floor(angle / twoPi);
                return angle < 0.0 ? angle + twoPi : angle;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 delta = float2(
                    input.positionWS.x - _CityCenter.x,
                    input.positionWS.z - _CityCenter.z);
                float radius = length(delta);
                float angle = Wrap02Pi(atan2(delta.x, delta.y) - _Angle0);
                float span = max(_AngleSpan, 1e-4);
                float width = max(_LineWidth, 0.05);
                float inner = _InnerRadius;
                float outer = _OuterRadius;

                bool inRing = radius >= inner && radius <= outer;
                bool inWedge = angle >= 0.0 && angle <= span;
                if (!inRing || !inWedge)
                    discard;

                float shrinkAngle = width / max(radius, 0.001);
                bool innerRing = radius >= inner + width && radius <= outer - width;
                bool innerWedge = angle >= shrinkAngle && angle <= span - shrinkAngle;
                if (innerRing && innerWedge)
                    discard;

                return _Color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
