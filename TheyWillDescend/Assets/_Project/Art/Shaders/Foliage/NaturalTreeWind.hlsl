#ifndef TWD_NATURAL_TREE_WIND_INCLUDED
#define TWD_NATURAL_TREE_WIND_INCLUDED

// Mesh contract (root-local, Y up): UV1.xyz = card hinge, UV1.w = foliage flag;
// UV2 = (whole-tree height, crown start / height, flexibility, reserved).
// COLOR.r = card root-to-tip, g = card phase, b = tint. UV0 remains untouched.
// All renderers, including shadow-only geometry, use the same root coordinates.
// No texture reads, bones, per-tree Update or material property blocks required.

float3 TWD_TreeRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

void NaturalTreeWind_float(float3 PositionOS, float3 NormalOS, float3 TangentOS,
    float4 FoliageData, float4 CardHinge, float4 TreeData, float TimeSeconds,
    float Strength, float Speed, float DirectionDegrees, float DirectionVariation,
    float GustStrength, float TreeSwaySpeed, float TrunkBend, float BranchSway,
    float LeafFlutter, float LeafFlutterSpeed, float PhaseOffset, float PreviewTime,
    out float3 BentPositionOS, out float3 BentNormalOS, out float3 BentTangentOS)
{
    BentPositionOS = PositionOS;
    BentNormalOS = NormalOS;
    BentTangentOS = TangentOS;
#if defined(_TREE_WIND_ENABLED)
    // Missing metadata is safely static; it never guesses a leaf card's hinge.
    if (TreeData.x <= 0.0001 || Strength <= 0.0) return;
    float height = TreeData.x;
    float strength = clamp(Strength, 0.0, 8.0) * clamp(TreeData.z, 0.0, 2.0);
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float direction = radians(DirectionDegrees);
    float2 windWS = float2(cos(direction), sin(direction));
    float2 sideWS = float2(-windWS.y, windWS.x);
    float spatial = dot(originWS.xz, windWS) * 0.09;
    float across = dot(originWS.xz, sideWS) * 0.07;
    float seed = frac(sin(dot(originWS.xz, float2(12.9898, 78.233))) * 43758.5453);
    float phase = PhaseOffset + seed * 6.2831853;
    float seconds = PreviewTime >= 0.0 ? PreviewTime : TimeSeconds;
    float t = seconds * max(Speed, 0.0);
    float slow = t * max(TreeSwaySpeed, 0.0);

    // Travelling broad gusts plus slower tree response; no common hinge cycle.
    float gust = 0.5 + 0.5 * (0.63 * sin(t * 0.37 - spatial) +
        0.37 * sin(t * 0.61 - spatial * 0.71 + across + 1.7));
    float alongWave = 0.67 * sin(slow * 0.83 + phase - spatial) +
        0.33 * sin(slow * 1.31 + phase * 1.37 + 0.9);
    float crossWave = 0.71 * sin(slow * 0.63 + phase + across + 1.1) +
        0.29 * sin(slow * 1.17 + phase * 1.73);
    float wander = radians(clamp(DirectionVariation, 0.0, 120.0)) *
        (0.73 * sin(slow * 0.41 + phase) + 0.27 * sin(slow * 0.73 + across));
    float sw, cw;
    sincos(wander, sw, cw);
    float2 directionWS = windWS * cw + sideWS * sw;
    float3 directionOS = TransformWorldToObjectDir(float3(directionWS.x, 0, directionWS.y));
    directionOS.y = 0;
    directionOS *= rsqrt(max(dot(directionOS, directionOS), 1e-6));
    float3 sideOS = float3(-directionOS.z, 0, directionOS.x);
    float pressure = 0.55 + saturate(GustStrength) * gust * 0.75 + alongWave * 0.28;
    float3 bend = directionOS * pressure + sideOS * crossWave * 0.22;
    float bendLength = max(length(bend), 1e-5);
    float3 bendDirection = bend / bendLength;
    float3 axis = float3(bendDirection.z, 0, -bendDirection.x);
    float3 p = PositionOS;
    float3 n = NormalOS;
    float3 tangent = TangentOS;

    // Card-local motion. Root vertices stay at the hinge. Phase is shared by
    // every vertex of a card; unrelated leaves cannot pull its corners apart.
    if (CardHinge.w > 0.5)
    {
        float leafPhase = phase + FoliageData.g * 6.2831853;
        float leafTime = t * max(LeafFlutterSpeed, 0.0);
        float flutterWave = 0.66 * sin(leafTime * 1.91 + leafPhase) +
            0.34 * sin(leafTime * 3.07 + leafPhase * 1.61 + 0.4);
        float weight = saturate(FoliageData.r);
        float amplitude = radians(clamp(LeafFlutter, 0.0, 45.0)) * strength;
        amplitude = 0.7 * amplitude * rsqrt(0.49 + amplitude * amplitude);
        float angle = amplitude * (0.45 + gust * 0.55) * flutterWave * weight * weight;
        float3 leafAxis = normalize(axis + float3(0, 0.22 * sin(leafPhase), 0));
        p = CardHinge.xyz + TWD_TreeRotate(p - CardHinge.xyz, leafAxis, angle);
        n = TWD_TreeRotate(n, leafAxis, angle);
        tangent = TWD_TreeRotate(tangent, leafAxis, angle);
    }

    // A smooth spatial crown field, shared by wood, leaves and shadow proxy.
    // It needs no species-specific bone hierarchy. Inner trunk stays stiff;
    // lateral branches flex gradually. This is broad flex, not skeletal joints.
    float crown = smoothstep(clamp(TreeData.y, 0.05, 0.9), 1.0, p.y / height);
    float lateral = smoothstep(0.025, 0.24, length(p.xz) / height);
    float branchWave = 0.65 * sin(slow * 1.43 + phase + p.y / height * 2.1) +
        0.35 * sin(slow * 2.13 + phase * 1.37 + 1.5);
    float branchAngle = radians(clamp(BranchSway, 0.0, 20.0)) * strength;
    branchAngle = 0.45 * branchAngle * rsqrt(0.2025 + branchAngle * branchAngle);
    branchAngle *= crown * lateral * branchWave;
    p = TWD_TreeRotate(p, float3(0, 1, 0), branchAngle);
    n = TWD_TreeRotate(n, float3(0, 1, 0), branchAngle);
    tangent = TWD_TreeRotate(tangent, float3(0, 1, 0), branchAngle);

    // Constant-curvature centreline: top lowers as it bends, roots are fixed.
    // Cross sections rotate with the curve instead of rigidly tipping the tree.
    float topAngle = radians(clamp(TrunkBend, 0.0, 25.0)) * strength * bendLength;
    topAngle = 0.6 * topAngle * rsqrt(0.36 + topAngle * topAngle);
    float phi = topAngle * max(p.y, 0.0) / height;
    float sinPhi, cosPhi;
    sincos(phi, sinPhi, cosPhi);
    // Taylor terms avoid cancellation at zero and preserve the exact static case.
    float sinc = abs(phi) < 0.01 ? 1.0 - phi * phi / 6.0 : sinPhi / phi;
    float cosc = abs(phi) < 0.01 ? phi * 0.5 - phi * phi * phi / 24.0 : (1.0 - cosPhi) / phi;
    float3 centre = float3(0, p.y * sinc, 0) + bendDirection * p.y * cosc;
    p = centre + TWD_TreeRotate(float3(p.x, 0, p.z), axis, phi);
    n = TWD_TreeRotate(n, axis, phi);
    tangent = TWD_TreeRotate(tangent, axis, phi);
    BentPositionOS = p;
    BentNormalOS = normalize(n);
    BentTangentOS = normalize(tangent);
#endif
}

#endif
