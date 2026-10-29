#version 450
#extension GL_GOOGLE_include_directive : require
// Star shell in the world view (docs/visuals/night-lights.md Q10d):
//  - the trail (kind 0): the star's smoke, a Gaussian column about the segment from the star to the burst, its half width
//    growing toward the top (older smoke) and a rounder puff there (the burst's); its optical depth the thinner the wider
//    it's spread. Lit where it is: by the night (the rods' view) and by every light in the block, the star's own among
//    them from just under it, so it glows over the star and dims up its length, and keeps the others' after burn-out.
//    Premultiplied, through the haze.
//  - the star (kind 1): its core, its candle spread over a small disc, and its glow in the haze, sigma p I (pi/2 +
//    atan(h / d)) / d (the eye's ray from above down to the sea h under it), out to its reach with the air's
//    extinction; rolled to white on luminance as the flashes are (never per channel); added over the scene through the
//    haze.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"

struct Flare
{
    vec4 lo, hi, loAt, hiAt, look, colour;
};

layout(std430, set = 2, binding = 0) readonly buffer Flares { Flare flares[]; };

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;
layout(location = 0) out vec4 outColor;

const float TrailDepth = 1.2;     // a fresh trail's optical depth across its middle [EST]
const float TrailAlbedo = 0.8;    // white smoke (magnesium oxide)

vec3 rolled(vec3 col)
{
    float y = dot(col, vec3(0.2126, 0.7152, 0.0722));
    float yt = shoulder(vec3(y), look.z).x;
    vec3 rgb = col * (yt / max(y, 1e-6));
    float over = clamp(log2(max(y, 1e-6)) / 3.0, 0.0, 1.0);
    return mix(rgb / max(max(rgb.r, max(rgb.g, rgb.b)), 1.0), vec3(yt), over);
}

void main()
{
    Flare f = flares[fragIndex];
    if (f.lo.w < 0.5)
    {
        float len = length(f.hi.xyz - f.lo.xyz);
        float t = len > 1e-3 ? clamp(fragLoc.x / len, 0.0, 1.0) : 1.0;
        float s = mix(f.loAt.w, f.hi.w, t);
        float dy = fragLoc.x < 0.0 ? fragLoc.x : max(fragLoc.x - len, 0.0);
        float d2 = fragLoc.y * fragLoc.y + dy * dy;
        float tau = TrailDepth * (f.look.z / s) * exp(-0.5 * d2 / (s * s));
        // the burst's puff at the top: rounder and a little denser
        vec2 q = vec2(fragLoc.x - len, fragLoc.y);
        float sp = 1.6 * f.hi.w;
        tau += 0.8 * TrailDepth * (f.look.z / sp) * exp(-0.5 * dot(q, q) / (sp * sp));
        float a = (1.0 - exp(-tau)) * f.hiAt.w;
        if (a < 0.003)
            discard;
        vec3 here = mix(f.loAt.xyz, f.hiAt.xyz, t);
        vec3 lit = nightSee(mix(seaAmb.rgb, skyAmb.rgb, 0.7) + 0.6 * (sunRad.rgb + moonRad.rgb));
        int nf = flashCount();
        for (int i = 0; i < nf; i++)
        {
            vec3 lw;
            lit += 0.6 * flashAt(i, here, lw);
        }
        vec3 c = linearToSrgb(shoulder(TrailAlbedo * lit, look.z));
        outColor = hazePremul(vec4(c * a, a));
        return;
    }
    float d = length(fragLoc), core = f.look.z, reach = f.look.w, sigma = f.hiAt.w;
    // the eye's ray past it from above, through the air down to the sea its height under it
    float dd = max(d, core);
    float glow = f.look.y * (1.5708 + atan(f.colour.w, dd)) / dd * exp(-sigma * d) * (1.0 - smoothstep(0.5 * reach, reach, d));
    float star = f.look.x * exp(-0.5 * d * d / (core * core));
    vec3 col = f.colour.rgb * (star + glow);
    if (dot(col, vec3(0.2126, 0.7152, 0.0722)) < 1e-4)
        discard;
    outColor = vec4(linearToSrgb(rolled(col) * hazeThrough()), 0.0);
}
