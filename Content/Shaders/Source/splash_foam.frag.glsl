#version 450
#extension GL_GOOGLE_include_directive : require
// A splash's foam on the sea (docs/visuals/splashes.md), into the wake target's foam (max-blend; nothing in rgb, the
// slopes are the stamps'), in the foam lanes' encoding (wakeribbon.frag): aerated water in 0-0.4, surface foam's
// speckle over it in the rest, so seaShade lights it as it lights the wakes. Flat on the water: never tilted. On the
// foam's own clock (the sea's: the world's speed, capped), in the splash's metres:
//  - the impact's white, where the shell went in, over the rise;
//  - the base surge's ring rolling out to 2.5 widths and fading;
//  - after the collapse, the white disc of fallen water ~1.5 widths across, its surface foam gone in ~15 s, the
//    aerated patch under it in ~40, all of it by a minute.
// Broken up by a sharp threshold over value noise fixed to the water, each octave fading to its mean as its cells
// near a pixel (as the lanes do), so nothing shimmers zoomed out. The foam's strength (fainter at high compression,
// where many more patches lie on the water at once) scales it all.
#include "Include/splash.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragShape;
layout(location = 2) flat in vec4 fragLean;
layout(location = 3) flat in vec4 fragDye;
layout(location = 4) flat in vec4 fragFoam;
layout(location = 5) flat in float fragSeed;

layout(location = 0) out vec4 outWake;

float noise2(vec2 p, float salt)
{
    return splashNoise(vec3(p, salt));
}

void main()
{
    float w = fragFoam.z, age = fragFoam.x, ta = max(fragShape.w, 0.5), tf = age / ta;
    float d = length(fragLoc);
    vec2 p = fragLoc + fragSeed * 911.0;
    float mpp = max(fwidth(fragLoc.x), fwidth(fragLoc.y));

    // the noise: patches 3 m, bubbles 1.2 m and 0.5 m, each faded to its mean as its cells near a pixel
    vec3 cell = vec3(3.0, 1.2, 0.5), full = vec3(0.4, 0.35, 0.25);
    vec3 amp = full * (1.0 - smoothstep(vec3(0.3), vec3(0.8), mpp / cell));
    float lost = dot(full - amp, vec3(0.29));
    float n = 0.5 * (1.0 - (amp.x + amp.y + amp.z));
    n += amp.x * noise2(p / cell.x, 1.0) + amp.y * noise2(p / cell.y, 2.0) + amp.z * noise2(p / cell.z, 3.0);
    n = clamp((n - 0.5) * 1.6 + 0.5, 0.0, 1.0);
    float rag = 0.8 + 0.4 * noise2(p / (0.8 * w), 4.0);   // ragged outlines

    // where the water is white (surface) and churned (aerated)
    float white = 0.0, aerated = 0.0;
    // the impact's white over the rise
    white = max(white, (1.0 - smoothstep(0.5, 1.0, d / (0.9 * w * rag))) * (1.0 - smoothstep(0.6, 1.6, tf)));
    // the surge's ring rolling out, the water inside it churned
    float rr = (2.5 * w * min(tf / 2.5, 1.0) + 0.3 * w) * rag, x = (d - rr) / (0.35 * w);
    float ring = smoothstep(0.15, 0.5, tf) * (1.0 - smoothstep(2.5, 6.0, tf));
    white = max(white, 0.7 * ring * exp(-x * x));
    aerated = max(aerated, 0.35 * ring * (1.0 - smoothstep(0.6, 1.05, d / max(rr, 1e-3))));
    // the fallen water's disc
    float disc = (1.0 - smoothstep(0.6, 1.0, d / (1.5 * w * rag * (1.0 + age / 120.0)))) * smoothstep(0.8, 2.0, tf);
    white = max(white, disc * exp(-age / 15.0));
    aerated = max(aerated, disc * exp(-age / 40.0));
    float gone = 1.0 - smoothstep(30.0, 60.0, age);
    white *= gone * fragFoam.y;
    aerated *= gone * fragFoam.y;

    float t = 1.0 - 0.85 * white, soft = max(max(fwidth(n), 0.03), 1.6 * lost);
    float broken = smoothstep(t - soft, t + soft, n) * clamp(1.6 * white, 0.0, 1.0);
    float a = (0.4 * clamp(aerated, 0.0, 1.0) + 0.6 * broken) * fragFoam.w;
    if (a < 0.002)
        discard;
    outWake = vec4(0.0, 0.0, 0.0, a);
}
