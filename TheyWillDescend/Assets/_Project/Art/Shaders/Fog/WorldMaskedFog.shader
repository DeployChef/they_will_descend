Shader "TheyWillDescend/Environment/World Masked Fog"
{
    Properties
    {
        [HideInInspector] _WorldFogDepth("Scene depth", 2D) = "white" {}
        [HideInInspector] _WorldFogColor("Fog color", Color) = (0.38, 0.44, 0.43, 1)
        [HideInInspector] _WorldFogDistance("Start, end, density, mode", Vector) = (140, 650, 0.0015, 1)
        [HideInInspector] _WorldFogMask("Centre XZ, radius, width", Vector) = (0, 0, 100, 100)
        [HideInInspector] _WorldFogStrength("Inside, outside, maximum opacity", Vector) = (1, 1, 0.95, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "World Masked Fog"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X_FLOAT(_WorldFogDepth);
            CBUFFER_START(UnityPerMaterial)
                float4 _WorldFogColor;
                float4 _WorldFogDistance;
                float4 _WorldFogMask;
                float4 _WorldFogStrength;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float rawDepth = SAMPLE_TEXTURE2D_X(_WorldFogDepth, sampler_PointClamp, uv).r;
                // Sky has no surface position. Keep the existing skybox untouched.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0000001) return half4(0, 0, 0, 0);
                    float depth = rawDepth;
                #else
                    if (rawDepth >= 0.9999999) return half4(0, 0, 0, 0);
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float eyeDepth = max(0.0, -TransformWorldToView(positionWS).z);
                float amount;
                if (_WorldFogDistance.w < 1.5) // FogMode.Linear = 1
                {
                    amount = saturate((eyeDepth - _WorldFogDistance.x)
                        / max(0.01, _WorldFogDistance.y - _WorldFogDistance.x));
                }
                else
                {
                    float opticalDepth = max(0.0, _WorldFogDistance.z) * eyeDepth;
                    if (_WorldFogDistance.w > 2.5) opticalDepth *= opticalDepth;
                    amount = 1.0 - exp2(-1.442695041 * opticalDepth);
                }
                float radius = distance(positionWS.xz, _WorldFogMask.xy);
                float mask = smoothstep(_WorldFogMask.z,
                    _WorldFogMask.z + max(0.01, _WorldFogMask.w), radius);
                float strength = lerp(_WorldFogStrength.x, _WorldFogStrength.y, mask);
                float opacity = min(saturate(amount * strength), _WorldFogStrength.z);
                return half4(_WorldFogColor.rgb, opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
