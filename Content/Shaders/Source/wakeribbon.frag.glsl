#version 450
// The foam lanes (docs/visuals/wakes.md K4): the lane's foam taken across it as a Gaussian, then broken up by the
// research's soft threshold (§6) over streaky value noise in the lane's own metres (along the trail, across it), so it
// stays fixed to the water: cells a share of the lane's width across, 20 m along (streaks, not blotches). Where a cell nears a pixel the noise's contrast
// fades to its mean, so the lane never shimmers at 4 m/px. Written as coverage in alpha (the most of the lanes
// crossing), nothing in rgb: the wake target's slopes are the stamps'.
// It lives (the user, 2026-10-15: still, it looked painted on): across the lane the noise is in the lane's width, so as
// the water's lane spreads with age its foam spreads with it; and young water churns, the noise sliding through time
// slices on the sea's clock, the churn fading over the first half-minute.
layout(location = 0) in float fragIntensity;
layout(location = 1) in vec2 fragLane;
layout(location = 2) in float fragWidth;
layout(location = 3) in float fragAge;
layout(location = 4) flat in float fragClock;

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

    // the lane in noise cells: along, its metres (wrapped so the floats stay small); across, its width's share
    vec2 q = vec2(mod(fragLane.x, 81920.0) / 20.0, d * 1.6);
    vec2 cellsPerPx = fwidth(q);
    float still = 0.65 * valueNoise(q) + 0.35 * valueNoise(q * 2.3 + vec2(17.0, 5.0));
    // the churn: the fine octave through time slices, blended, fastest in the youngest water
    float z = fragClock * 0.8, k = floor(z), u = smoothstep(0.0, 1.0, z - k);
    vec2 o0 = vec2(hash(vec2(k, 3.0)), hash(vec2(k, 7.0))) * 64.0, o1 = vec2(hash(vec2(k + 1.0, 3.0)), hash(vec2(k + 1.0, 7.0))) * 64.0;
    float churned = 0.65 * valueNoise(q) + 0.35 * mix(valueNoise(q * 2.3 + o0), valueNoise(q * 2.3 + o1), u);
    float n = mix(still, churned, exp(-fragAge / 30.0));
    n = clamp((n - 0.5) * 1.35 + 0.5, 0.0, 1.0);   // the value noise is narrow round its mean: spread for gaps in the core
    n = mix(n, 0.5, smoothstep(0.25, 0.6, max(cellsPerPx.x, cellsPerPx.y)));

    float t = 1.0 - density;
    float broken = clamp((n - t + 0.18) / 0.36, 0.0, 1.0) * clamp(1.6 * density, 0.0, 1.0);
    // the youngest water, just behind the transom, is solid white down its middle for a few seconds
    float core = density * exp(-fragAge / 6.0) * exp(-2.0 * d * d);
    outWake = vec4(0.0, 0.0, 0.0, max(broken, clamp(core * 1.5, 0.0, 1.0)));
}
