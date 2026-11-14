Shader "TheyWillDescend/Foliage/Tree Card Impostor"
{
    Properties
    {
        _ColorAtlas("Albedo and Coverage", 2D) = "white" {}
        _NormalAtlas("Object Normals", 2D) = "black" {}
        _CaptureCenter("Capture Center", Vector) = (0, 5, 0, 0)
        _Cutoff("Coverage Cutoff", Range(0,1)) = 0.5
        _Tint("Tint", Color) = (1,1,1,1)
        _UpperViewBrightness("Upper View Brightness", Range(1,2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_ColorAtlas);
        SAMPLER(sampler_ColorAtlas);
        TEXTURE2D(_NormalAtlas);
        SAMPLER(sampler_NormalAtlas);

        CBUFFER_START(UnityPerMaterial)
            float4 _CaptureCenter;
            half4 _Tint;
            half _Cutoff;
            half _UpperViewBrightness;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            nointerpolation half4 cardWeights : TEXCOORD1;
            half fog : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings CardVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.fog = ComputeFogFactor(output.positionCS.z);

            float3 view = GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(_CaptureCenter.xyz));
            float3 normal = TransformObjectToWorldNormal(input.normalOS);
            float3 up = TransformObjectToWorldNormal(float3(0, 1, 0));
            float elevation = dot(view, up);
            float topWeight = smoothstep(0.60, 0.81, elevation);
            float3 lateralView = SafeNormalize(view - elevation * up);
            float sideWeight = smoothstep(0.87, 0.974, dot(lateralView, normal));
            if (dot(lateralView, cross(up, normal)) < 0)
                sideWeight = -sideWeight;
            output.cardWeights = half4(topWeight, sideWeight, input.normalOS.y,
                lerp(1, _UpperViewBrightness, smoothstep(0.423, 0.819, elevation)));
            bool hidden = input.normalOS.y > 0.5 ? topWeight <= 0.0 : abs(sideWeight) <= 0.0 || topWeight >= 1.0;
            if (hidden)
                output.positionCS = float4(0, 0, 0, 1);
            return output;
        }

        half4 SampleColor(Varyings input)
        {
            // Apply the same visibility and coverage tests in the color and depth passes.
            float dither = max(InterleavedGradientNoise(floor(input.positionCS.xy), 0), 0.001);
            float topWeight = input.cardWeights.x;
            if (input.cardWeights.z > 0.5)
            {
                clip(topWeight - dither);
            }
            else
            {
                // Keep side coverage until the top has taken over most of the silhouette.
                float sideFade = smoothstep(0.55, 1.0, topWeight);
                clip(dither - sideFade);
                float sideDither = (dither - sideFade) / max(1.0 - sideFade, 0.001);
                float sideWeight = input.cardWeights.y;
                if (sideWeight < 0)
                    sideDither = 1.0 - sideDither;
                clip(abs(sideWeight) - max(sideDither, 0.001));
            }
            half4 color = SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, input.uv);
            clip(color.a - _Cutoff);
            return color;
        }

        half3 SampleNormal(Varyings input, half coverage)
        {
            half3 encoded = SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, input.uv).rgb;
            half3 normalOS = encoded * (2.0 / max(coverage, 0.01)) - 1.0;
            normalOS = dot(normalOS, normalOS) > 0.01 ? normalize(normalOS) : half3(0, 1, 0);
            return TransformObjectToWorldNormal(normalOS);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SampleColor(input);
                half3 normal = SampleNormal(input, color.a);
                Light sun = GetMainLight();
                half3 lighting = SampleSH(normal) + sun.color * saturate(dot(normal, sun.direction));
                half3 albedo = (color.rgb / max(color.a, 0.01)) * _Tint.rgb;
                return half4(MixFog(albedo * lighting * input.cardWeights.w, input.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            half DepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SampleColor(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SampleColor(input);
                half3 normal = SampleNormal(input, color.a);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
