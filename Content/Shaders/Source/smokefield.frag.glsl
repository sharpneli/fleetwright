#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's accumulation (docs/battle/smoke.md Z5): a puff's column optical depth here,
// tau = A / (2 pi sigma_l sigma_s) e^(-l^2 / 2 sigma_l^2 - s^2 / 2 sigma_s^2) along and across its stretch (the sim's
// vertical tau), added into its level: rgb its tint times tau, a
// tau. Summing tau is Beer-Lambert's exactly and needs no sorting. Its grain (Z5d): times a noise about 1 fixed in the
// air's frame (it drifts with the smoke, never swims), its octaves by the puff's short sigma in metres (no change with
// the zoom), so a small puff gets small billows and a big one big lumps; below the sim's resolution, its mean kept.
#include "Include/smokefield.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragPuff;
layout(location = 2) in vec2 fragGrain;            // the place in the air's frame on the grain's plane, m (wrapped)
layout(location = 3) flat in vec4 fragGrainInfo;   // xyz: its three octaves' weights (the gain in them); w: the first's cell, m

layout(location = 0) out vec4 outTau;

float grainHash(ivec2 c)
{
    uint h = uint(c.x) * 0x27d4eb2du ^ uint(c.y) * 0x165667b1u;
    h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
    return float(h) * (2.0 / 4294967295.0) - 1.0;
}

// value noise in [-1, 1] on a lattice of cells this wide, wrapped to the grain's period (a power of two of cells: a mask)
float grainNoise(vec2 x, float cell)
{
    vec2 q = x / cell, i = floor(q), f = q - i;
    f = f * f * (3.0 - 2.0 * f);
    int mask = int(GRAIN_PERIOD / cell + 0.5) - 1;
    ivec2 c0 = ivec2(i) & mask, c1 = (c0 + 1) & mask;
    return mix(mix(grainHash(c0), grainHash(ivec2(c1.x, c0.y)), f.x), mix(grainHash(ivec2(c0.x, c1.y)), grainHash(c1), f.x), f.y);
}

void main()
{
    float tau = fragPuff.x * exp(-fragLoc.x * fragLoc.x * fragPuff.y - fragLoc.y * fragLoc.y * fragPuff.w);
    if (fragGrainInfo.x + fragGrainInfo.y + fragGrainInfo.z > 0.0 && tau > 1e-4)
    {
        float cell = fragGrainInfo.w;
        float n = fragGrainInfo.x * grainNoise(fragGrain, cell) + fragGrainInfo.y * grainNoise(fragGrain, 2.0 * cell)
                + fragGrainInfo.z * grainNoise(fragGrain, 4.0 * cell);
        tau *= max(1.0 + n, 0.0);
    }
    outTau = vec4(fieldTint(fragPuff.z) * tau, tau);
}
