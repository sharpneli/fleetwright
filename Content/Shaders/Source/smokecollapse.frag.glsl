#version 450
// The smoke field's levels collapsed (docs/battle/smoke.md Z5): drawn at one level of the collapsed chain, its own
// accumulation's texel plus the B-spline bicubic of the collapsed level above (none at the top), so the resolve reads
// one level, not every one. Each level's smoke is upsampled once per level it passes through (a B-spline widens it by
// ~0.6 of a texel of the level it comes from: small beside its puffs' 1.5 to 3).

layout(set = 2, binding = 0) uniform sampler2D field;
layout(set = 2, binding = 1) uniform sampler2D collapsed;

layout(set = 3, binding = 0) uniform Params
{
    vec4 info;   // x: this level; y: 1 at the top level (nothing above); zw: level 0's size, texels
};

layout(location = 0) in vec2 fragUv;
layout(location = 0) out vec4 outTau;

vec4 bspline(vec2 uv, float lod, vec2 size)
{
    vec2 st = uv * size - 0.5, i = floor(st), f = st - i;
    vec2 f2 = f * f, f3 = f2 * f;
    vec2 w0 = (-f3 + 3.0 * f2 - 3.0 * f + 1.0) / 6.0, w1 = (3.0 * f3 - 6.0 * f2 + 4.0) / 6.0;
    vec2 w2 = (-3.0 * f3 + 3.0 * f2 + 3.0 * f + 1.0) / 6.0, w3 = f3 / 6.0;
    vec2 g0 = w0 + w1, g1 = w2 + w3;
    vec2 h0 = (i - 0.5 + w1 / g0) / size, h1 = (i + 1.5 + w3 / g1) / size;
    return g0.y * (g0.x * textureLod(collapsed, vec2(h0.x, h0.y), lod) + g1.x * textureLod(collapsed, vec2(h1.x, h0.y), lod))
         + g1.y * (g0.x * textureLod(collapsed, vec2(h0.x, h1.y), lod) + g1.x * textureLod(collapsed, vec2(h1.x, h1.y), lod));
}

void main()
{
    int level = int(info.x + 0.5);
    vec4 own = texelFetch(field, ivec2(gl_FragCoord.xy), level);
    if (info.y > 0.5)
    {
        outTau = own;
        return;
    }
    vec2 above = max(floor(info.zw / exp2(float(level + 1))), vec2(1.0));
    outTau = own + bspline(fragUv, float(level + 1), above);
}
