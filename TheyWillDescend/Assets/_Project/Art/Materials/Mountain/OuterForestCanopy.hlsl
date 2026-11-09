#ifndef TWD_OUTER_FOREST_CANOPY_INCLUDED
#define TWD_OUTER_FOREST_CANOPY_INCLUDED
#include "OuterForestSurface.hlsl"

// An editor-baked, world-XZ canopy coverage mask. It represents broad shelter
// under foliage, not a sun-direction shadow or individual leaf silhouettes.
// One shared lookup across all ground sectors; no per-tree runtime work.
void OuterForestSurfaceCanopy_float(float3 PositionWS,
    UnityTexture2D MudColor, UnityTexture2D MudNormal, UnityTexture2D LeafColor, UnityTexture2D LeafNormal,
    float MudTiling, float LeafTiling, float4 MudTint, float4 LeafTint,
    float LeafCoverage, float PatchSize, float ForestStart, float ForestEnd, float EdgeBreakup,
    float AntiTile, float MudNormalAmount, float LeafNormalAmount, float MudSmoothness, float LeafSmoothness,
    float3 NormalWS, float3 TangentWS, float3 BitangentWS,
    UnityTexture2D CanopyMask, float4 CanopyWorldRect, float CanopyShadeStrength,
    out float3 Color, out float3 NormalTS, out float Roughness, out float LeafWeight)
{
    OuterForestSurface_float(PositionWS, MudColor, MudNormal, LeafColor, LeafNormal,
        MudTiling, LeafTiling, MudTint, LeafTint, LeafCoverage, PatchSize, ForestStart, ForestEnd,
        EdgeBreakup, AntiTile, MudNormalAmount, LeafNormalAmount, MudSmoothness, LeafSmoothness,
        NormalWS, TangentWS, BitangentWS, Color, NormalTS, Roughness, LeafWeight);

    // Strength zero reproduces the previous surface and skips the mask lookup.
    if (CanopyShadeStrength > 0.0001 && min(CanopyWorldRect.z, CanopyWorldRect.w) > 0.001)
    {
        float2 uv = (PositionWS.xz - CanopyWorldRect.xy) / CanopyWorldRect.zw;
        float inside = step(0., uv.x) * step(0., uv.y) * step(uv.x, 1.) * step(uv.y, 1.);
        float cover = SAMPLE_TEXTURE2D(CanopyMask.tex, CanopyMask.samplerstate, saturate(uv)).r;
        // The final bank graph already separates rock from soil. This additional
        // slope gate protects steep surfaces near the edge of the forest floor.
        float ground = smoothstep(0.45, 0.85, normalize(NormalWS).y);
        Color *= 1. - saturate(CanopyShadeStrength) * saturate(cover) * inside * ground;
    }
}
#endif
