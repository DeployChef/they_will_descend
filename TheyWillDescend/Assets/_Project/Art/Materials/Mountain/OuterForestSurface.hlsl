#ifndef TWD_OUTER_FOREST_SURFACE_INCLUDED
#define TWD_OUTER_FOREST_SURFACE_INCLUDED
#include "OuterForestBlend.hlsl"

float3 TWD_ForestHash3(float2 p)
{
    return frac(sin(float3(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)),dot(p,float2(419.2,371.9))))*43758.5453);
}
float2 TWD_ForestRotate(float2 p,float2 cs)
{
    return float2(cs.x*p.x-cs.y*p.y,cs.y*p.x+cs.x*p.y);
}
void TWD_ForestTile_float(UnityTexture2D ColorMap,UnityTexture2D NormalMap,
    float2 uv,float2 dx,float2 dy,float2 cell,float amount,out float3 color,out float3 normal)
{
    float3 hash=TWD_ForestHash3(cell);
    float angle=floor(hash.z*4.)*1.57079632679*saturate(amount);
    float sn,cs;sincos(angle,sn,cs);float2 rotation=float2(cs,sn);
    float2 sampleUV=TWD_ForestRotate(uv,rotation)+hash.xy*17.*saturate(amount);
    float2 gradX=TWD_ForestRotate(dx,rotation),gradY=TWD_ForestRotate(dy,rotation);
    color=SAMPLE_TEXTURE2D_GRAD(ColorMap.tex,ColorMap.samplerstate,sampleUV,gradX,gradY).rgb;
    normal=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(NormalMap.tex,NormalMap.samplerstate,sampleUV,gradX,gradY));
    normal.xy=TWD_ForestRotate(normal.xy,float2(cs,-sn));
}
// Three randomly offset/rotated projections blended on a continuous triangular
// lattice. Explicit gradients keep mip selection independent of cell boundaries.
// Shared world coordinates also keep renderer-sector boundaries identical.
void TWD_ForestLayer_float(UnityTexture2D ColorMap, UnityTexture2D NormalMap,
    float2 p,float2 positionDx,float2 positionDy,float Tiling,float AntiTile,float NormalAmount,
    out float3 Color, out float3 Normal)
{
    float2 uv=p*max(Tiling,.001);
    float2 dx=positionDx*max(Tiling,.001),dy=positionDy*max(Tiling,.001);
    if(AntiTile<.001)
    {
        Color=SAMPLE_TEXTURE2D_GRAD(ColorMap.tex,ColorMap.samplerstate,uv,dx,dy).rgb;
        Normal=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(NormalMap.tex,NormalMap.samplerstate,uv,dx,dy));
        Normal=normalize(float3(Normal.xy*max(NormalAmount,0),max(Normal.z,.05)));
        return;
    }
    float2 skew=float2(uv.x-.57735026919*uv.y,1.15470053838*uv.y);
    float2 cell=floor(skew),f=frac(skew);float2 c0,c1,c2;float3 weights;
    if(f.x+f.y<1.){c0=cell;c1=cell+float2(1,0);c2=cell+float2(0,1);weights=float3(1.-f.x-f.y,f.x,f.y);}
    else{c0=cell+1.;c1=cell+float2(0,1);c2=cell+float2(1,0);weights=float3(f.x+f.y-1.,1.-f.x,1.-f.y);}
    weights=weights*weights*weights;weights/=max(dot(weights,1.),.00001);
    float3 a,b,c,na,nb,nc;
    TWD_ForestTile_float(ColorMap,NormalMap,uv,dx,dy,c0,AntiTile,a,na);
    TWD_ForestTile_float(ColorMap,NormalMap,uv,dx,dy,c1,AntiTile,b,nb);
    TWD_ForestTile_float(ColorMap,NormalMap,uv,dx,dy,c2,AntiTile,c,nc);
    Color=a*weights.x+b*weights.y+c*weights.z;
    Normal=normalize(na*weights.x+nb*weights.y+nc*weights.z);
    Normal=normalize(float3(Normal.xy*max(NormalAmount,0),max(Normal.z,.05)));
}

void OuterForestSurface_float(float3 PositionWS,
    UnityTexture2D MudColor,UnityTexture2D MudNormal,UnityTexture2D LeafColor,UnityTexture2D LeafNormal,
    float MudTiling,float LeafTiling,float4 MudTint,float4 LeafTint,
    float LeafCoverage,float PatchSize,float ForestStart,float ForestEnd,float EdgeBreakup,
    float AntiTile,float MudNormalAmount,float LeafNormalAmount,float MudSmoothness,float LeafSmoothness,
    float3 NormalWS,float3 TangentWS,float3 BitangentWS,
    out float3 Color,out float3 NormalTS,out float Roughness,out float LeafWeight)
{
    float2 p=PositionWS.xz;
    float2 positionDx=ddx(p),positionDy=ddy(p);
    float3 mud=0,mudN=float3(0,0,1),leaf=0,leafN=float3(0,0,1);
    LeafWeight=0;
    // Material-wide branch: a disabled secondary layer does not sample its maps.
    if(LeafCoverage>.001)
    {
        float patch=TWD_OuterNoise(p/max(PatchSize,.5)+float2(8.2,13.1));
        float offset=(patch-.5)*abs(EdgeBreakup);
        float coast=smoothstep(min(ForestStart,ForestEnd-.5)+offset,ForestEnd+offset,length(p));
        // Full forest coverage after End; noise breaks the boundary, not the whole floor.
        LeafWeight=coast*saturate(LeafCoverage);
    }
    // Large single-surface areas sample only the layer they actually display.
    // Explicit gradients above remain valid across the branch boundary.
    if(LeafWeight<.9999)
        TWD_ForestLayer_float(MudColor,MudNormal,p,positionDx,positionDy,MudTiling,AntiTile,MudNormalAmount,mud,mudN);
    if(LeafWeight>.0001)
        TWD_ForestLayer_float(LeafColor,LeafNormal,p+float2(23.7,-9.2),positionDx,positionDy,LeafTiling,AntiTile,LeafNormalAmount,leaf,leafN);
    Color=lerp(mud*MudTint.rgb,leaf*LeafTint.rgb,LeafWeight);
    float3 n=normalize(lerp(mudN,leafN,LeafWeight));
    // Planar XZ whiteout mapping, then convert to the existing graph's tangent space.
    float3 geom=normalize(NormalWS);
    float3 world=normalize(float3(n.x+geom.x,max(n.z,.05)*geom.y,n.y+geom.z));
    NormalTS=normalize(float3(dot(world,normalize(TangentWS)),dot(world,normalize(BitangentWS)),dot(world,geom)));
    Roughness=1.-saturate(lerp(MudSmoothness,LeafSmoothness,LeafWeight));
}

void OuterForestRock_float(float3 PositionWS,float3 NormalWS,float3 TangentWS,float3 BitangentWS,
    float DirtWeight,UnityTexture2D RockColor,UnityTexture2D RockNormal,UnityTexture2D RockRoughness,
    float Tiling,out float3 Color,out float3 NormalTS,out float Roughness)
{
    float3 uv=PositionWS*max(Tiling,.001);
    // Derivatives are evaluated before divergent flow. Texture samples are skipped
    // on fully covered ground, while coastline quads retain stable mip selection.
    float3 dx=ddx(uv),dy=ddy(uv);
    Color=0;NormalTS=float3(0,0,1);Roughness=1;
    if(DirtWeight>=.9999)return;
    float3 n=normalize(NormalWS),w=pow(abs(n),4.);w/=max(dot(w,1.),.00001);
    float3 cx=SAMPLE_TEXTURE2D_GRAD(RockColor.tex,RockColor.samplerstate,uv.zy,dx.zy,dy.zy).rgb;
    float3 cy=SAMPLE_TEXTURE2D_GRAD(RockColor.tex,RockColor.samplerstate,uv.xz,dx.xz,dy.xz).rgb;
    float3 cz=SAMPLE_TEXTURE2D_GRAD(RockColor.tex,RockColor.samplerstate,uv.xy,dx.xy,dy.xy).rgb;
    float rx=SAMPLE_TEXTURE2D_GRAD(RockRoughness.tex,RockRoughness.samplerstate,uv.zy,dx.zy,dy.zy).r;
    float ry=SAMPLE_TEXTURE2D_GRAD(RockRoughness.tex,RockRoughness.samplerstate,uv.xz,dx.xz,dy.xz).r;
    float rz=SAMPLE_TEXTURE2D_GRAD(RockRoughness.tex,RockRoughness.samplerstate,uv.xy,dx.xy,dy.xy).r;
    float3 nx=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(RockNormal.tex,RockNormal.samplerstate,uv.zy,dx.zy,dy.zy));
    float3 ny=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(RockNormal.tex,RockNormal.samplerstate,uv.xz,dx.xz,dy.xz));
    float3 nz=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(RockNormal.tex,RockNormal.samplerstate,uv.xy,dx.xy,dy.xy));
    nx=float3(nx.xy+n.zy,abs(nx.z)*n.x);ny=float3(ny.xy+n.xz,abs(ny.z)*n.y);nz=float3(nz.xy+n.xy,abs(nz.z)*n.z);
    float3 world=normalize(nx.zyx*w.x+ny.xzy*w.y+nz.xyz*w.z);
    Color=cx*w.x+cy*w.y+cz*w.z;Roughness=rx*w.x+ry*w.y+rz*w.z;
    NormalTS=normalize(float3(dot(world,normalize(TangentWS)),dot(world,normalize(BitangentWS)),dot(world,n)));
}
#endif
