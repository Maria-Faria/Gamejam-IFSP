#ifndef ROGUELIKE_GHOST_COMMON_INCLUDED
#define ROGUELIKE_GHOST_COMMON_INCLUDED

// Shared by the Ghostly body and GhostSmoke shell: the slow breathing (expand/contract along the normals)
// and the low-frequency misshaping that make the monsters look unstable/ethereal. Everything is measured in
// units of the monster's size (_Size, metres) so small and boss monsters wobble alike. _Phase is set per
// instance (GhostlyLook) so a crowd of the same monster does not pulse in sync.

float3 GhostPivotWS()
{
    return float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
}

float GhostHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float GhostValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = GhostHash(i);
    float n100 = GhostHash(i + float3(1, 0, 0));
    float n010 = GhostHash(i + float3(0, 1, 0));
    float n110 = GhostHash(i + float3(1, 1, 0));
    float n001 = GhostHash(i + float3(0, 0, 1));
    float n101 = GhostHash(i + float3(1, 0, 1));
    float n011 = GhostHash(i + float3(0, 1, 1));
    float n111 = GhostHash(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

float GhostFbm(float3 p)
{
    float v = 0.0, a = 0.5;
    for (int o = 0; o < 3; o++)
    {
        v += a * GhostValueNoise(p);
        p = p * 2.03 + float3(1.7, 9.2, 3.1);
        a *= 0.5;
    }
    return v / 0.875;
}

// Offset (world space) for a vertex of the body: breathing + a wobbling bulge field that drifts upward.
float3 GhostWarpWS(float3 positionWS, float3 normalWS, float size, float phase,
                   float breatheAmount, float breatheSpeed, float warpAmount, float warpScale, float warpSpeed,
                   float hemSway)
{
    float t = _Time.y;
    float3 local = (positionWS - GhostPivotWS()) / max(size, 1e-3);
    float breathe = sin(t * breatheSpeed + phase) * breatheAmount
                  + sin(t * breatheSpeed * 2.37 + phase * 1.7) * breatheAmount * 0.25;
    float3 q = local * warpScale + float3(phase * 3.1, -t * warpSpeed, phase * 1.3);
    float bulge = GhostValueNoise(q) * 2.0 - 1.0;
    // a slow sideways sway that grows towards the bottom, like a hem in a draught
    float hem = saturate(0.6 - local.y);
    float3 sway = float3(sin(t * 1.3 + local.y * 5.0 + phase), 0, cos(t * 1.1 + local.y * 4.0 + phase * 1.3)) * hem;
    return (normalWS * (breathe + bulge * warpAmount) + sway * warpAmount * hemSway) * size;
}

#endif
