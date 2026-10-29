#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field over the scene (docs/battle/smoke.md Z5): the levels of the accumulation summed (each a B-spline
// bicubic of its level, four bilinear taps), alpha = 1 - e^(-tau), the tint the depth-weighted mean of the puffs'. The
// plain look: lit as the gun smoke is, a dome seen from above in the sky's and sea's ambient, the sun and the moon, and
// the point lights (flashes, fires, flares, beams: docs/visuals/night-lights.md Q10c), each from where the smoke under
// the pixel is: its place rebuilt from the view at a height (SmokeLitHeightM), lifted up the screen by the effects
// camera as the field draws it. Premultiplied, through the ships' shoulder.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"

layout(set = 2, binding = 0) uniform sampler2D field;

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;    // xyz: toward the sun, east, north, up
    vec4 moonL;   // xyz: toward the moon; w: the lowest level in use
    vec4 info;    // x: one past the highest level in use; y: an opacity gain; zw: level 0's size, texels
    vec4 pixView; // xy: 2 tan of the half fields over the target's pixels; zw: the tans (a pixel's ray off straight down)
    vec4 pixAt;   // x: the eye's altitude, m; y: the effects camera's screen metres north a metre up; z: the smoke's height, m
};

layout(location = 0) in vec2 fragUv;
layout(location = 0) out vec4 outColor;

// a level's B-spline bicubic at uv, from four bilinear taps
vec4 bspline(vec2 uv, float lod, vec2 size)
{
    vec2 st = uv * size - 0.5, i = floor(st), f = st - i;
    vec2 f2 = f * f, f3 = f2 * f;
    vec2 w0 = (-f3 + 3.0 * f2 - 3.0 * f + 1.0) / 6.0, w1 = (3.0 * f3 - 6.0 * f2 + 4.0) / 6.0;
    vec2 w2 = (-3.0 * f3 + 3.0 * f2 + 3.0 * f + 1.0) / 6.0, w3 = f3 / 6.0;
    vec2 g0 = w0 + w1, g1 = w2 + w3;
    vec2 h0 = (i - 0.5 + w1 / g0) / size, h1 = (i + 1.5 + w3 / g1) / size;
    return g0.y * (g0.x * textureLod(field, vec2(h0.x, h0.y), lod) + g1.x * textureLod(field, vec2(h1.x, h0.y), lod))
         + g1.y * (g0.x * textureLod(field, vec2(h0.x, h1.y), lod) + g1.x * textureLod(field, vec2(h1.x, h1.y), lod));
}

void main()
{
    vec4 sum = vec4(0.0);
    int levels = int(info.x + 0.5);
    for (int l = int(moonL.w + 0.5); l < levels; l++)
        sum += bspline(fragUv, float(l), max(floor(info.zw / exp2(float(l))), vec2(1.0)));
    float tau = sum.a * info.y;
    float alpha = 1.0 - exp(-tau);
    if (alpha < 0.002)
        discard;
    vec3 albedo = sum.rgb / max(sum.a, 1e-6);
    float sunLit = (max(sunL.z, 0.0) + 0.4) / 1.4, moonLit = (max(moonL.z, 0.0) + 0.4) / 1.4;
    vec3 lit = nightSee(mix(seaAmb.rgb, skyAmb.rgb, 0.7) + sunRad.rgb * sunLit + moonRad.rgb * moonLit);   // the rods' view at night
    int nf = flashCount();
    if (nf > 0)
    {
        // the smoke under the pixel: the sea there (straight down, the pixel's ray off it), less the lift the effects
        // camera gives its height, up at that height
        vec2 s = gl_FragCoord.xy * pixView.xy - pixView.zw;
        vec3 here = pixAt.x * (-flashUp.xyz + s.x * flashEast.xyz - s.y * flashNorth.xyz)
                  + flashUp.xyz * pixAt.z - flashNorth.xyz * (pixAt.z * pixAt.y);
        vec3 art = vec3(0.0);
        for (int i = 0; i < nf; i++)
        {
            vec3 lw;
            vec3 rad = flashAt(i, here, lw);
            art += rad * (max(dot(lw, flashUp.xyz), 0.0) + 0.4) / 1.4;
        }
        lit += lightResponse(art);   // the eye's response to artificial light
    }
    vec3 c = linearToSrgb(shoulder(albedo * lit, look.z));
    outColor = vec4(hazeCoded(c) * alpha, alpha);
}
