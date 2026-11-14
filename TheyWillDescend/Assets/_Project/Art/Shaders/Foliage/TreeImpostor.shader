Shader "TheyWillDescend/Foliage/Tree Impostor"
{
    Properties
    {
        _ColorAtlas("Albedo and Coverage", 2D) = "white" {}
        _SurfaceAtlas("Object Normals and Depth", 2D) = "black" {}
        _Capture("Capture Center and Half Size", Vector) = (0,5,0,7)
        _Cutoff("Coverage Cutoff", Range(0,1)) = 0.4
        _Tint("Tint", Color) = (1,1,1,1)
        _UpperViewBrightness("Upper View Brightness", Range(1,2)) = 1
        [Toggle(_IMPOSTOR_LITE)] _ImpostorLite("Two-View Sampling", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_ColorAtlas);
        SAMPLER(sampler_ColorAtlas);
        TEXTURE2D(_SurfaceAtlas);
        SAMPLER(sampler_SurfaceAtlas);

        CBUFFER_START(UnityPerMaterial)
            float4 _Capture;
            half4 _Tint;
            half _Cutoff;
            half _UpperViewBrightness;
            float _ImpostorLite;
        CBUFFER_END

        static const float Columns = 12.0;
        static const float Rows = 5.0;
        static const float TileInset = 8.0 / 256.0;
        static const float TileInterior = 1.0 - 2.0 * TileInset;

        struct Attributes
        {
            float3 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 projectionA : TEXCOORD1;
            float3 projectionB : TEXCOORD2;
            nointerpolation float4 frameA : TEXCOORD4;
            nointerpolation float4 frameB : TEXCOORD5;
            nointerpolation float4 weightsAndBrightness : TEXCOORD7;
            #if !defined(_IMPOSTOR_LITE)
                float3 projectionC : TEXCOORD3;
                nointerpolation float4 frameC : TEXCOORD6;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        void CaptureAxes(float azimuth, float elevation, out float3 view, out float3 right, out float3 up)
        {
            float sinAzimuth, cosAzimuth, sinElevation, cosElevation;
            sincos(azimuth, sinAzimuth, cosAzimuth);
            sincos(elevation, sinElevation, cosElevation);
            view = float3(sinAzimuth * cosElevation, sinElevation, cosAzimuth * cosElevation);
            right = float3(-cosAzimuth, 0, sinAzimuth);
            up = cross(right, view);
        }

        void ProjectFrame(float2 tile, float3 planePoint, float3 viewRay, out float3 projection, out float4 frame)
        {
            float3 view, right, up;
            CaptureAxes(tile.x * (TWO_PI / Columns), tile.y * (HALF_PI / (Rows - 1.0)), view, right, up);
            projection = float3(dot(planePoint, right) * 0.5 + 0.5,
                                dot(planePoint, up) * 0.5 + 0.5, dot(planePoint, view));
            float alignment = max(dot(viewRay, view), 0.3);
            frame = float4(float2(dot(viewRay, right), dot(viewRay, up)) * (0.5 / alignment),
                           fmod(tile.x, Columns), tile.y);
        }

        Varyings ImpostorVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 centerWS = TransformObjectToWorld(_Capture.xyz);
            float3 view = TransformWorldToObjectDir(GetWorldSpaceNormalizeViewDir(centerWS), true);
            float azimuth = dot(view.xz, view.xz) > 1e-8 ? atan2(view.x, view.z) : 0.0;
            float elevation = asin(clamp(view.y, -1.0, 1.0));
            float3 basisView, right, up;
            CaptureAxes(azimuth, elevation, basisView, right, up);
            float3 planePoint = right * input.positionOS.x + up * input.positionOS.y;
            output.positionWS = TransformObjectToWorld(_Capture.xyz + planePoint * _Capture.w);
            output.positionCS = TransformWorldToHClip(output.positionWS);

            float2 grid = float2(frac(azimuth / TWO_PI + 1.0) * Columns,
                                saturate(elevation / HALF_PI) * (Rows - 1.0));
            float2 cell = floor(grid);
            cell.y = min(cell.y, Rows - 2.0);
            float2 fraction = grid - cell;
            bool lowerTriangle = fraction.x >= fraction.y;
            float2 middle = lowerTriangle ? float2(1, 0) : float2(0, 1);
            output.weightsAndBrightness.xyz = float3(1.0 - max(fraction.x, fraction.y),
                                                     abs(fraction.x - fraction.y), min(fraction.x, fraction.y));
            // Blend the lighting correction in as the camera rises from 25 to 55 degrees.
            output.weightsAndBrightness.w = lerp(1.0, _UpperViewBrightness, smoothstep(0.423, 0.819, view.y));
            float2 tileA = cell;
            float2 tileB = cell + middle;
            float2 tileC = cell + 1.0;
            #if defined(_IMPOSTOR_LITE)
                // Drop the frame with the lowest weight and renormalize the other two.
                float3 weights = output.weightsAndBrightness.xyz;
                if (weights.x <= min(weights.y, weights.z))
                {
                    tileA = tileC;
                    weights.x = weights.z;
                }
                else if (weights.y <= weights.z)
                {
                    tileB = tileC;
                    weights.y = weights.z;
                }
                output.weightsAndBrightness.xy = weights.xy / (weights.x + weights.y);
            #else
                ProjectFrame(tileC, planePoint, view, output.projectionC, output.frameC);
            #endif
            ProjectFrame(tileA, planePoint, view, output.projectionA, output.frameA);
            ProjectFrame(tileB, planePoint, view, output.projectionB, output.frameB);
            return output;
        }

        float2 AtlasUV(float2 uv, float2 tile)
        {
            return (tile + TileInset + saturate(uv) * TileInterior) / float2(Columns, Rows);
        }

        float2 Reproject(float3 projection, float4 frame)
        {
            float2 atlasUV = AtlasUV(projection.xy, frame.zw);
            float packedDepth = SAMPLE_TEXTURE2D(_SurfaceAtlas, sampler_SurfaceAtlas, atlasUV).a;
            float viewDepth = packedDepth > 0.12 ? (packedDepth - 0.5) / 0.38 : projection.z;
            float2 offset = clamp(frame.xy * (viewDepth - projection.z), -0.12, 0.12);
            return AtlasUV(projection.xy + offset, frame.zw);
        }

        struct ImpostorSamples
        {
            float2 uvA, uvB;
            #if !defined(_IMPOSTOR_LITE)
                float2 uvC;
            #endif
            half4 albedo;
        };

        ImpostorSamples SampleImpostor(Varyings input)
        {
            ImpostorSamples result;
            #if defined(_IMPOSTOR_LITE)
                result.uvA = AtlasUV(input.projectionA.xy, input.frameA.zw);
                result.uvB = AtlasUV(input.projectionB.xy, input.frameB.zw);
            #else
                result.uvA = Reproject(input.projectionA, input.frameA);
                result.uvB = Reproject(input.projectionB, input.frameB);
                result.uvC = Reproject(input.projectionC, input.frameC);
            #endif
            result.albedo =
                SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, result.uvA) * input.weightsAndBrightness.x +
                SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, result.uvB) * input.weightsAndBrightness.y;
            #if !defined(_IMPOSTOR_LITE)
                result.albedo += SAMPLE_TEXTURE2D(_ColorAtlas, sampler_ColorAtlas, result.uvC) * input.weightsAndBrightness.z;
            #endif
            clip(result.albedo.a - _Cutoff);
            return result;
        }

        half3 ImpostorNormalWS(Varyings input, ImpostorSamples samples)
        {
            half3 encoded =
                SAMPLE_TEXTURE2D(_SurfaceAtlas, sampler_SurfaceAtlas, samples.uvA).rgb * input.weightsAndBrightness.x +
                SAMPLE_TEXTURE2D(_SurfaceAtlas, sampler_SurfaceAtlas, samples.uvB).rgb * input.weightsAndBrightness.y;
            #if !defined(_IMPOSTOR_LITE)
                encoded += SAMPLE_TEXTURE2D(_SurfaceAtlas, sampler_SurfaceAtlas, samples.uvC).rgb * input.weightsAndBrightness.z;
            #endif
            half3 normalOS = encoded * (2.0 / max(samples.albedo.a, 0.01)) - 1.0;
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
            #pragma vertex ImpostorVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _IMPOSTOR_LITE

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ImpostorSamples samples = SampleImpostor(input);
                half3 normal = ImpostorNormalWS(input, samples);
                // The sector-wide proxy should shade the ground without darkening the billboard.
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normal, sun.direction));
                half3 ambient = SampleSH(normal);
                half3 lighting = ambient + sun.color * diffuse;
                half3 albedo = (samples.albedo.rgb / max(samples.albedo.a, 0.01)) * _Tint.rgb;
                half fog = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
                return half4(MixFog(albedo * lighting * input.weightsAndBrightness.w, fog), 1);
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
            #pragma vertex ImpostorVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _IMPOSTOR_LITE

            half DepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SampleImpostor(input);
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
            #pragma vertex ImpostorVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _IMPOSTOR_LITE

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ImpostorSamples samples = SampleImpostor(input);
                half3 normal = ImpostorNormalWS(input, samples);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    half3 packed = PackFloat2To888(saturate(octNormal * 0.5 + 0.5));
                    return half4(packed, 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}

