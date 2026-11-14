Shader "TheyWillDescend/Plateau Volumetric Fog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.63,0.70,0.76,1)
        _Density ("Density", Range(0, 0.3)) = 0.015
        [HideInInspector] _CullMode ("Cull Mode", Float) = 1
        [HideInInspector] _PlateauFogMask ("Plateau center XZ and radii", Vector) = (0,0,0,1)
        [HideInInspector] _PlateauFogStrength ("Plateau and outside density multipliers", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "PureVolumetricFog"
            Tags { "LightMode"="UniversalForward" }
            Cull [_CullMode]
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GroundFogVert
            #pragma fragment GroundFogFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // Soft-shadow variants are deliberately not compiled: the raymarch integrates
            // many shadow samples per pixel, so one hardware-PCF tap per step is enough.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "PlateauVolumetricFogCommon.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
