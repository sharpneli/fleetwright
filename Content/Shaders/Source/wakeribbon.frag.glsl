#version 450
// The foam lanes (docs/visuals/wakes.md K4): the lane's foam taken across it as a Gaussian, then broken up by the
// research's soft threshold (§6) over streaky value noise in the lane's own metres (along the trail, across it), so it
// stays fixed to the water: cells ~2.5 m across, stretched 4x along. Where a cell nears a pixel the noise's contrast
// fades to its mean, so the lane never shimmers at 4 m/px. Written as coverage in alpha (the most of the lanes
// crossing), nothing in rgb: the wake target's slopes are the stamps'.
layout(location = 0) in float fragIntensity;
layout(location = 1) in vec2 fragLane;
layout(location = 2) in float fragWidth;

layout(location = 0) out vec4 outWake;

float hash(vec2 c)
{
    uvec2 u = uvec2(ivec2(c) & 4095);
    uint n = u.x * 73856093u ^ u.y * 19349663u;
    n = (n ^ (n >> 13)) * 1274126177u;
    return float(n & 0xffffffu) / 16777215.0;
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void main()
{
    float d = fragLane.y / max(fragWidth, 1e-3);
    float density = clamp(fragIntensity * exp(-d * d), 0.0, 1.5);

    // the lane's metres in noise cells; the trail's distance wrapped so the floats stay small
    vec2 q = vec2(mod(fragLane.x, 40960.0) / 10.0, fragLane.y / 2.5);
    vec2 cellsPerPx = fwidth(q);
    float n = 0.65 * valueNoise(q) + 0.35 * valueNoise(q * 2.3 + vec2(17.0, 5.0));
    n = mix(n, 0.5, smoothstep(0.25, 0.6, max(cellsPerPx.x, cellsPerPx.y)));

    float t = 1.0 - density;
    float broken = clamp((n - t + 0.18) / 0.36, 0.0, 1.0);
    outWake = vec4(0.0, 0.0, 0.0, broken * clamp(1.6 * density, 0.0, 1.0));
}
