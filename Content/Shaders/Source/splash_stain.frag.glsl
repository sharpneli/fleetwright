#version 450
#extension GL_GOOGLE_include_directive : require
// A dyed shell's stain on the water (docs/visuals/splashes.md "Dye"): the dye that came down with the column, a soft
// broken patch spreading slowly and fading over most of a minute on the foam's clock. Drawn in the main pass after the
// sea, before the ships, premultiplied: the dye's colour lit as a level surface by the shared lights, laid thinly over
// the water (tinted water, not paint). Flat on the water: never tilted.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragShape;
layout(location = 2) flat in vec4 fragLean;
layout(location = 3) flat in vec4 fragDye;
layout(location = 4) flat in vec4 fragFoam;
layout(location = 5) flat in float fragSeed;

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;
    vec4 moonL;
    vec4 shadowL;
    vec4 misc;      // w: vivid
};

layout(location = 0) out vec4 outColor;

void main()
{
    float w = fragFoam.z, age = fragFoam.x, ta = max(fragShape.w, 0.5);
    vec2 p = fragLoc + fragSeed * 577.0;
    float n = 0.6 * splashNoise(vec3(p / (0.7 * w), 5.0)) + 0.4 * splashNoise(vec3(p / (0.25 * w), 6.0));
    float rad = 1.4 * w * (1.0 + age / 50.0) * (0.75 + 0.5 * n);
    float cover = (1.0 - smoothstep(0.4, 1.0, length(fragLoc) / rad)) * smoothstep(0.8 * ta, 2.0 * ta, age) * exp(-age / 40.0);
    cover *= (0.6 + 0.8 * n) * (1.0 - smoothstep(40.0, 60.0, age));
    float a = clamp(0.4 * fragDye.w * misc.w * cover, 0.0, 0.6) * fragFoam.w * fragFoam.y;
    if (a < 0.002)
        discard;
    vec3 light = skyAmb.rgb + sunRad.rgb * max(sunL.z, 0.0) + moonRad.rgb * max(moonL.z, 0.0);
    vec3 c = linearToSrgb(shoulder(srgbToLinear(fragDye.rgb) * 0.7 * light, look.z));
    outColor = vec4(c * a, a);
}
