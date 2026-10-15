#version 450
#extension GL_GOOGLE_include_directive : require
// A magazine's flame jet (docs/visuals/magazine-explosions.md X3; research/vfx/magazine-explosion-vfx-research.md §2.5):
// the fuel-rich propellant gas leaving a failed opening near sonic speed and burning a second time as it meets the air,
// a narrow cone from the opening's root (radius 0.45 d) to its tip (+0.06 of its length), hot at the root and cooler at
// the tip, broken into tongues by noise that runs out along it. A zero-length jet is the hit's flash, a ball. Emitting
// only: its brightness by its temperature over the display's white deck (the body's rule), its hue whitened by it, and
// screen-blended as the flashes are (light wins over what's behind it).
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/magazine.glsl"

struct Jet
{
    vec4 base, seg, shape, look, quad;
};

layout(std430, set = 2, binding = 0) readonly buffer Jets { Jet jets[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;
    vec4 moonL;
    vec4 shadowL;
    vec4 misc;    // y: metres per pixel; w: display units per cd/m^2
    vec4 fireK;   // x: the cold temperature, K; y: the hot
    vec4 more;    // z: how far the hottest fire's hue whitens
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outColor;

const vec3 Warm = vec3(1.0, 0.86, 0.6) / 0.8992;

void main()
{
    Jet j = jets[fragIndex];
    vec2 a = j.seg.xy, b = j.seg.zw, ab = b - a;
    float len2 = dot(ab, ab);
    float s = len2 > 1e-6 ? clamp(dot(fragLoc - a, ab) / len2, 0.0, 1.0) : 0.0;
    float r = mix(j.shape.x, j.shape.y, s), d = length(fragLoc - (a + ab * s));
    // tongues: noise along the jet, running out with the gas, and across it
    float along = s * j.look.z / max(2.0 * j.shape.x, 0.5);
    vec2 across = len2 > 1e-6 ? vec2(-ab.y, ab.x) / sqrt(len2) : vec2(1.0, 0.0);
    float side = dot(fragLoc - (a + ab * s), across) / max(r, 0.1);
    float n = splashNoise(vec3(along - 9.0 * j.look.y, side * 1.5, j.base.w * 50.0));
    float rr = r * (0.75 + 0.5 * n);
    float core = exp(-2.2 * (d * d) / (rr * rr));
    // the tip frays, the root starts sharp at the opening
    core *= (1.0 - smoothstep(0.7, 1.0, s) * (0.6 + 0.4 * n)) * smoothstep(0.0, 0.03, s + 0.03 * step(len2, 1e-6));
    // hottest on its axis near the root, cooler out at its rim and toward the tip
    float heat = mix(j.shape.z, j.shape.w, s) * (1.0 - 0.35 * min(d * d / (rr * rr), 1.0)) * (0.85 + 0.3 * n);
    float w = core * j.look.x;
    if (w < 0.003)
        discard;
    float kel = mix(fireK.x, fireK.y, clamp(heat, 0.0, 1.0));
    vec3 ch = magChroma(kel);
    ch /= dot(ch, vec3(0.2126, 0.7152, 0.0722));
    ch = mix(ch, Warm, more.z * smoothstep(1350.0, 1950.0, kel));
    // its gas's brightness rolled off on luminance (the body's rule, its hue kept), then weighted by how much of the
    // flame the pixel sees: soft at its edges, however far past white the gas is
    float y = shoulder(vec3(min(misc.w * magLuminance(kel), 200.0)), look.z).x;
    vec3 rgb = ch * y;
    rgb /= max(max(rgb.r, max(rgb.g, rgb.b)), 1.0);
    outColor = vec4(linearToSrgb(rgb * w), 0.0);
}
