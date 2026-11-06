#ifndef TWD_NATURAL_GRASS_WIND_INCLUDED
#define TWD_NATURAL_GRASS_WIND_INCLUDED

#if defined(_WIND_ENABLED)
float3 TWD_GrassRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

#endif

// WindData: R = root-to-tip weight, G = blade phase, B = blade variation.
// Angles are in degrees at the material boundary; the field is world aligned.
void NaturalGrassWind_float(float3 PositionOS, float3 NormalOS, float3 TangentOS,
    float4 WindData, float4 BladePivot, float TimeSeconds, float DirectionDegrees, float Strength,
    float Speed, float DirectionVariation, float GustStrength, float SwayStrength,
    float BladeVariation, float PhaseOffset, float TuftSway, float TuftSwaySpeed,
    out float3 BentPositionOS, out float3 BentNormalOS, out float3 BentTangentOS)
{
#if !defined(_WIND_ENABLED)
    // A material keyword removes the entire wind path from static variants,
    // including ShadowCaster/DepthOnly passes. No runtime branch is needed.
    BentPositionOS = PositionOS;
    BentNormalOS = NormalOS;
    BentTangentOS = TangentOS;
#else
    float rootWeight = saturate(WindData.r);
    float strength = max(Strength, 0.0);
    float variation = saturate(BladeVariation);
    float t = TimeSeconds * max(Speed, 0.0) + PhaseOffset;
    float direction = radians(DirectionDegrees);
    float2 wind = float2(cos(direction), sin(direction));
    float2 side = float2(-wind.y, wind.x);
    float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float spatial = dot(origin.xz, wind) * 0.27;
    float across = dot(origin.xz, side) * 0.19;
    float seed = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
    float bladePhase = (WindData.g + seed) * 6.2831853 * variation;
    float bladeRate = lerp(1.0, 0.8 + 0.4 * WindData.b, variation);

    // Slow travelling gusts stay coherent between neighboring tufts.
    // Incommensurate frequencies avoid a single repeated hinge-like cycle.
    float gust = 0.5 + 0.5 * (
        0.62 * sin(t * 0.39 - spatial + across * 0.4) +
        0.38 * sin(t * 0.67 - spatial * 0.63 + 1.7));
    float longWave = 0.64 * sin(t * 1.35 * bladeRate - spatial + bladePhase) +
        0.36 * sin(t * 2.17 * bladeRate + across + bladePhase * 1.31 + 0.8);
    float crossWave = 0.67 * sin(t * 1.03 * bladeRate + across + bladePhase * 1.71) +
        0.33 * sin(t * 1.81 * bladeRate - spatial * 0.7 + bladePhase + 2.3);
    // A common phase for the entire tuft: no per-blade random phase here.
    // Wind Speed scales the weather; Tuft Sway Speed controls collective motion.
    float tuftTime = t * max(TuftSwaySpeed, 0.0);
    float collectiveWave = 0.72 * sin(tuftTime * 2.4 - spatial) +
        0.28 * sin(tuftTime * 3.73 + across + 0.9);
    float collectiveCross = 0.78 * sin(tuftTime * 2.1 + across + 0.6) +
        0.22 * sin(tuftTime * 3.37 - spatial * 0.6);
    float wander = radians(clamp(DirectionVariation, 0.0, 180.0)) * (
        0.82 * sin(tuftTime * 3.0 - spatial * 0.5) +
        0.18 * sin(tuftTime * 1.7 + across + 1.1));
    float sw, cw;
    sincos(wander, sw, cw);
    float2 gustDirection = wind * cw + side * sw;
    float along = 0.35 + 0.75 * saturate(GustStrength) * gust +
        0.75 * max(TuftSway, 0.0) * collectiveWave +
        0.65 * saturate(SwayStrength) * longWave;
    float crossBend = 0.45 * max(TuftSway, 0.0) * collectiveCross +
        0.32 * saturate(SwayStrength) * crossWave;
    float2 bend = strength * (gustDirection * along + side * crossBend);
    float bendLength = length(bend);
    float3 axis = float3(bend.y, 0.0, -bend.x) / max(bendLength, 1e-5);
    // Smooth response: increasing strength does not clip the tops of gusts.
    float angle = 1.55 * bendLength * rsqrt(1.0 + bendLength * bendLength);
    angle *= rootWeight * (0.4 + 0.6 * rootWeight);
    angle += strength * variation * rootWeight * rootWeight * 0.035 *
        sin(t * 3.73 * bladeRate + bladePhase * 2.1);

    float3 pivotWS = TransformObjectToWorld(BladePivot.xyz);
    float3 positionWS = TransformObjectToWorld(PositionOS);
    float3 normalWS = TransformObjectToWorldNormal(NormalOS);
    float3 tangentWS = TransformObjectToWorldDir(TangentOS);
    float3 relative = positionWS - pivotWS;
    // Each blade bends around its own root. Approach the ground smoothly so
    // already splayed leaves never swing through the ground during a storm.
    float signedCrossY = cross(axis, relative).y * sign(angle);
    float groundLimit = 0.96 * atan2(max(relative.y, 1e-5), max(-signedCrossY, 1e-5));
    float ratio = angle / max(groundLimit, 1e-5);
    angle *= rsqrt(1.0 + ratio * ratio);
    // Curved rotation lowers tips as they bend instead of stretching ribbons.
    positionWS = pivotWS + TWD_GrassRotate(relative, axis, angle);
    normalWS = TWD_GrassRotate(normalWS, axis, angle);
    tangentWS = TWD_GrassRotate(tangentWS, axis, angle);
    BentPositionOS = TransformWorldToObject(positionWS);
    BentNormalOS = TransformWorldToObjectNormal(normalWS);
    BentTangentOS = TransformWorldToObjectDir(tangentWS);
#endif
}

void NaturalGrassWind_half(half3 PositionOS, half3 NormalOS, half3 TangentOS,
    half4 WindData, half4 BladePivot, half TimeSeconds, half DirectionDegrees, half Strength,
    half Speed, half DirectionVariation, half GustStrength, half SwayStrength,
    half BladeVariation, half PhaseOffset, half TuftSway, half TuftSwaySpeed,
    out half3 BentPositionOS, out half3 BentNormalOS, out half3 BentTangentOS)
{
    float3 p, n, t;
    NaturalGrassWind_float(PositionOS, NormalOS, TangentOS, WindData, BladePivot, TimeSeconds,
        DirectionDegrees, Strength, Speed, DirectionVariation, GustStrength,
        SwayStrength, BladeVariation, PhaseOffset, TuftSway, TuftSwaySpeed, p, n, t);
    BentPositionOS = p;
    BentNormalOS = n;
    BentTangentOS = t;
}

#endif
