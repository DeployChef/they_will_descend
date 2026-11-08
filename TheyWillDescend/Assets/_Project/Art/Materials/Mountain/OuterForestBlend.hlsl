#ifndef TWD_OUTER_FOREST_BLEND_INCLUDED
#define TWD_OUTER_FOREST_BLEND_INCLUDED
float TWD_OuterHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float TWD_OuterNoise(float2 p)
{
    float2 i=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);
    return lerp(lerp(TWD_OuterHash(i),TWD_OuterHash(i+float2(1,0)),f.x),
                lerp(TWD_OuterHash(i+float2(0,1)),TWD_OuterHash(i+1),f.x),f.y);
}
void OuterForestBlend_float(float3 PositionWS,float CoastStart,float CoastEnd,float Breakup,float4 ForestTint,
    out float CoastWeight,out float3 SoilTint)
{
    float2 p=PositionWS.xz;
    float offset=(TWD_OuterNoise(p*.035)-.5)*Breakup+(TWD_OuterNoise(p*.13)-.5)*3.0;
    float radius=length(p);
    // End always remains the requested radius, even if the user moves it below Start.
    // Previously clamping End to Start+1 made the End control appear frozen.
    CoastWeight=smoothstep(min(CoastStart,CoastEnd-.5)+offset,CoastEnd+offset,radius);
    float forest=smoothstep(CoastEnd-5.0,CoastEnd+32.0,radius);
    float macro=TWD_OuterNoise(p/28.0);
    SoilTint=lerp(float3(1,1,1),ForestTint.rgb,forest)*lerp(.85,1.08,macro);
}

void OuterForestRockMask_float(float SurfaceMask,float CoastWeight,float NormalHeight,out float DirtWeight)
{
    // Source paint varies the transition, but cannot pin rock patches beyond End.
    DirtWeight=saturate(NormalHeight)*saturate(CoastWeight)*
        (1.-saturate(SurfaceMask)*.7*(1.-saturate(CoastWeight)));
}
#endif
