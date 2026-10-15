#version 450
// The foam lanes (docs/visuals/wakes.md K4): the lane's foam taken across it as a Gaussian, then broken up by a
// threshold over noise fixed to the water (the lane's own metres: along the trail, across it). Written in alpha (the most
// of the lanes crossing; aerated water and surface foam in one number, at the end), nothing in rgb: the wake target's
// slopes are the stamps'.
// The noise, four octaves:
//  - streaks: cells 20 m along and a share of the lane's width across, so as the water's lane spreads with age its
//    foam spreads with it;
//  - patches, 4 m;
//  - bubbles, 1.25 m and 0.5 m, square in metres: the speckle (the user, 2026-10-15: it looked low-pass filtered;
//    then too dense, so the bubbles' share is less than the streaks' and patches').
// Each octave fades to its mean as its cells near a pixel, so nothing shimmers zoomed out; the threshold is sharp, its
// softness the noise's own change across the pixel (antialiased, not blurred). Young water churns (the user: it looked
// painted on): the finer octaves slide through time slices on the sea's clock, the finest fastest, the churn fading
// over the first half-minute.
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

// an octave at p (cells), churning at rate slices a second by how young the water is: two time slices of offsets,
// blended so the contrast holds (weights cos and sin: their squares sum to 1; a plain mix of two unrelated patterns
// halves the variance midway, and through the sharp threshold the foam pulsed at each slice's rhythm); still water
// reads a fixed offset
float octave(vec2 p, float rate, float young, float salt)
{
    float z = fragClock * rate + salt, k = floor(z), u = (z - k) * 1.5707963;
    vec2 o0 = vec2(hash(vec2(k, salt)), hash(vec2(k, salt + 5.0))) * 512.0;
    vec2 o1 = vec2(hash(vec2(k + 1.0, salt)), hash(vec2(k + 1.0, salt + 5.0))) * 512.0;
    float churned = 0.5 + (valueNoise(p + o0) - 0.5) * cos(u) + (valueNoise(p + o1) - 0.5) * sin(u);
    return young > 0.01 ? mix(valueNoise(p + vec2(salt * 37.0, salt * 11.0)), churned, young) : valueNoise(p + vec2(salt * 37.0, salt * 11.0));
}

void main()
{
    float d = fragLane.y / max(fragWidth, 1e-3);
    float density = clamp(fragIntensity * exp(-d * d), 0.0, 1.5);

    // the lane in metres, along wrapped so the floats stay small (a multiple of every cell, and of the hash's period)
    float s = mod(fragLane.x, 81920.0), y = fragLane.y;
    float mpp = max(fwidth(s), fwidth(y));   // metres a pixel
    float young = exp(-fragAge / 30.0);

    // each octave's weight, its contrast faded out as its cells near a pixel
    vec4 cell = vec4(20.0, 4.0, 1.25, 0.5), full = vec4(0.30, 0.30, 0.22, 0.12);
    vec4 amp = full * (1.0 - smoothstep(vec4(0.3), vec4(0.8), mpp / cell));
    amp.x = full.x * (1.0 - smoothstep(0.3, 0.8, max(mpp / 20.0, fwidth(d * 1.6))));
    float fine = 0.5;   // the finest octave seen, for the white's own texture
    // what the faded octaves took away: a pixel then holds many specks, so its edge is that much softer (coverage, not
    // a solid lane: the speckle seen from further off)
    float lost = dot(full - amp, vec4(0.29));
    float n = 0.5 * (1.0 - (amp.x + amp.y + amp.z + amp.w));
    float streak = valueNoise(vec2(s / cell.x, d * 1.6));
    n += amp.x * streak;
    if (amp.y > 0.0)
        n += amp.y * octave(vec2(s, y) / cell.y, 0.4, young, 1.0);
    if (amp.z > 0.0)
        n += amp.z * octave(vec2(s, y) / cell.z, 1.0, young, 2.0);
    if (amp.w > 0.0)
    {
        fine = octave(vec2(s, y) / cell.w, 2.0, young, 3.0);
        n += amp.w * fine;
        fine = mix(0.5, fine, amp.w / full.w);
    }
    n = clamp((n - 0.5) * 1.6 + 0.5, 0.0, 1.0);   // summed octaves crowd their mean: spread them again

    // the threshold: foam where the noise clears 1 - 0.85·density (even the densest water keeps its gaps); its edge as
    // soft as the noise changes over the pixel, or as the specks too small to see spread it. The white itself is
    // textured by the finest octave (thinner and thicker foam), not a flat fill.
    // The youngest water, just behind the transom, clears it more easily down the middle, so it's solid white there for
    // a few seconds and breaks up from its edges (crisp: a translucent core read as a grey haze between the specks).
    float core = exp(-fragAge / 6.0) * exp(-2.0 * d * d);
    float t = 1.0 - 0.85 * density - 0.9 * core * density, soft = max(max(fwidth(n), 0.03), 1.6 * lost);
    float broken = smoothstep(t - soft, t + soft, n) * clamp(1.6 * density, 0.0, 1.0) * (0.85 + 0.3 * (fine - 0.5));
    // Two layers in the one channel (the user, 2026-10-15: solid white read as snow, not churning water): the aerated
    // water under the lane, broad and soft, its strength streaked, in 0-0.4; the surface foam's speckle over it in the
    // rest. seaShade lightens the water by the first and lays translucent white by the second.
    float aerated = clamp(density, 0.0, 1.0) * (0.75 + 0.5 * (mix(0.5, streak, amp.x / full.x) - 0.5));
    outWake = vec4(0.0, 0.0, 0.0, 0.4 * aerated + 0.6 * broken);
}
