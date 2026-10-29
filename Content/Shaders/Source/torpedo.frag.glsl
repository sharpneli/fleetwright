#version 450
#extension GL_GOOGLE_include_directive : require
// A torpedo under the water (docs/visuals/torpedoes.md R3): its body seen down through a few metres of sea, a dark,
// soft-edged shape (blunt head, tapering tail) the colour of deep water, lit as a level surface by the shared lights.
// Darker than its physical contrast by TorpedoLook.Body (decision 2: where it is matters to the player). Drawn in the
// main pass after the sea and before the ships, premultiplied over the water.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragSize;
layout(location = 2) flat in vec4 fragLook;

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;    // z: the sun's height, its sine
    vec4 moonL;   // z: the moon's
};

layout(location = 0) out vec4 outColor;

void main()
{
    float hl = fragSize.x, r = fragSize.y, blur = max(fragSize.z, 1e-3);
    // along: -1 the tail, +1 the head; the radius there: full over the head and the middle, tapering over the tail
    float u = fragLoc.x / hl;
    float rad = r * (u > 0.0 ? sqrt(max(1.0 - pow(u, 6.0), 0.0)) : 1.0 - 0.75 * smoothstep(-0.35, -1.0, u));
    float d = max(abs(fragLoc.y) - rad, 0.0) + max(abs(fragLoc.x) - hl, 0.0);
    float a = fragLook.x * fragLook.z * (1.0 - smoothstep(0.0, 2.0 * blur, d));
    if (a < 0.002)
        discard;
    // deep water's colour, lit as a level surface
    vec3 light = skyAmb.rgb + sunRad.rgb * max(sunL.z, 0.0) + moonRad.rgb * max(moonL.z, 0.0);
    vec3 c = linearToSrgb(shoulder(nightSee(vec3(0.004, 0.012, 0.02) * light), look.z));   // the rods' view at night
    outColor = vec4(hazeCoded(c) * a, a);
}
