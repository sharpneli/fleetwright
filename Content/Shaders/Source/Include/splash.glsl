// A shell's splash (docs/visuals/splashes.md): its water as a density in the splash's own frame, the one model the
// column (seen through the effects camera), its shadow (the sun's ray) and the bounds of both are drawn from. Metres,
// x east, y north, z up, from the point of impact; horizontal offsets from the column's axis, which leans along the
// shell's travel at a shallow angle of fall. Its clock is tau, the column's age over its rise time sqrt(2H/g).
//
// The parts, after the research's anatomy (research/vfx/shell-splashes.md), each a smooth density:
//  - the impact's crown: a thin cone sheet flaring from the base, gone by tau 0.35;
//  - the shaft: a dense pillar 25 calibres wide, widening upward, rising as a thrown body H (1 - (1 - tau)^2);
//  - the blossom at the top: a ragged crown from half a width to two over tau 0.8-1.6;
//  - the fall: the top comes down the same parabola, the crown's water a curtain of rain round the shaft;
//  - the base surge: a low ring rolling out to 2.5 widths, gone by tau 4.
// Water this thick is nearly opaque: the noise (3D value noise in metres, seeded per splash; its fine octave fading
// out as its cells near a pixel) shapes the parts' outlines, ragged and lumpy, rather than thinning their insides (a
// translucent noisy volume, marched obliquely, smears into streaks). The crown breaks into clumps as it falls. The mist
// after the collapse waits for the drift downwind (docs/visuals/splashes.md "Later").

struct SplashShape
{
    float h;      // peak height, m (the angle of fall's share in)
    float w;      // shaft width, m (the size floor in)
    float tau;    // the column's age over its rise time
    float ta;     // the rise time, s
    vec2 lean;    // the axis's drift per metre of height (east, north)
    float seed;   // 0-1
};

const float SPLASH_GONE = 4.2;   // tau after which nothing of the column is left (the surge lasts longest)

float splashEnd(SplashShape s)
{
    return 2.0 + 1.0 / max(s.ta, 0.5);   // the column's fall: down by twice its rise and a second
}

// the top of the thrown water: up as a body thrown, down the same parabola by the column's end
float splashTop(SplashShape s)
{
    if (s.tau <= 1.0)
        return s.h * (1.0 - (1.0 - s.tau) * (1.0 - s.tau));
    float f = (s.tau - 1.0) / (splashEnd(s) - 1.0);
    return s.h * max(1.0 - f * f, 0.0);
}

float splashCrownR(SplashShape s)
{
    return s.w * (mix(0.5, 2.0, smoothstep(0.8, 1.6, s.tau)) + 0.3 * max(s.tau - 1.6, 0.0));
}

// the farthest any water reaches from the axis, and its highest point, now: the quads' bounds and the marches' ranges
float splashRadius(SplashShape s)
{
    float r = 1.4 * s.w;
    if (s.tau < 0.4)
        r = max(r, 2.2 * s.w);   // the impact's cone
    if (s.tau > 0.5)
        r = max(r, 1.2 * splashCrownR(s));
    if (s.tau > 0.15)
        r = max(r, 2.5 * s.w * min(s.tau / 2.5, 1.0) + 1.2 * s.w);   // the surge
    return r;
}

float splashHeight(SplashShape s)
{
    float rc = splashCrownR(s);
    return splashTop(s) + 0.45 * rc + 0.05 * s.h + 1.0;
}

// ---------------------------------------------------------------- noise

float splashHash(vec3 c)
{
    uvec3 u = uvec3(ivec3(c) & 1023);
    uint n = u.x * 73856093u ^ u.y * 19349663u ^ u.z * 83492791u;
    n = (n ^ (n >> 13)) * 1274126177u;
    return float(n & 0xffffffu) / 16777215.0;
}

float splashNoise(vec3 p)
{
    vec3 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = mix(mix(splashHash(i), splashHash(i + vec3(1, 0, 0)), f.x), mix(splashHash(i + vec3(0, 1, 0)), splashHash(i + vec3(1, 1, 0)), f.x), f.y);
    float b = mix(mix(splashHash(i + vec3(0, 0, 1)), splashHash(i + vec3(1, 0, 1)), f.x), mix(splashHash(i + vec3(0, 1, 1)), splashHash(i + vec3(1, 1, 1)), f.x), f.y);
    return mix(a, b, f.z);
}

// ---------------------------------------------------------------- the density

// the water at q (xy: offset from the axis, z: height), as an extinction per metre; crown: how much of it is the
// blossom (for the light). fine: the fine octave's weight (0 when its cells are under a pixel).
float splashDensity(SplashShape s, vec3 q, float fine, out float crown)
{
    crown = 0.0;
    float w = s.w, tau = s.tau, top = splashTop(s), d = length(q.xy);
    // the noise rides up with the jet and down with the fall
    vec3 nq = vec3(q.xy, q.z - 0.6 * top) / (0.7 * w) + s.seed * 97.0;
    float n = splashNoise(nq);
    if (fine > 0.0)
        n = mix(n, 0.55 * n + 0.45 * splashNoise(nq * 2.9 + 13.0), fine);
    float rag = 0.62 + 0.76 * n;   // ragged, lumpy outlines: the radius each part reaches here
    float sum = 0.0;

    // the shaft, widening upward and flared at its root; thinning as it falls (the research's opacity)
    if (q.z < top)
    {
        float rs = 0.5 * w * (0.85 + 0.45 * q.z / max(top, 1.0)) + 0.45 * w * exp(-q.z / (0.06 * s.h + 0.5));
        float rho = tau < 1.0 ? 1.0 : exp(-2.0 * (tau - 1.0));
        sum += rho * (1.0 - smoothstep(0.85, 1.0, d / (rs * rag)));
    }
    // the impact's cone: a thin sheet flaring out, low, gone by tau 0.35
    if (tau < 0.35)
    {
        float hc = 0.22 * s.h * smoothstep(0.0, 0.12, tau);
        if (q.z < hc)
        {
            float rc = 0.5 * w + 1.5 * w * q.z / max(hc, 0.5), x = (d - rc) / (0.2 * w * rag);
            sum += 0.6 * exp(-x * x) * (1.0 - smoothstep(0.15, 0.35, tau));
        }
    }
    // the blossom: a flattened ball at the top, opening over the apex; falling, it breaks into clumps and thins
    float rcr = splashCrownR(s), amt = smoothstep(0.55, 0.95, tau) * (tau > 1.0 ? exp(-1.6 * (tau - 1.0)) : 1.0);
    if (amt > 0.01)
    {
        float rv = 0.35 * rcr + 0.04 * s.h;
        vec3 e = vec3(q.xy / rcr, (q.z - (top - 0.5 * rv)) / rv);
        float breakUp = clamp(tau - 1.0, 0.0, 1.0);
        float c = amt * (1.0 - smoothstep(0.85, 1.0, length(e) / rag)) * smoothstep(0.3 * breakUp, 0.3 * breakUp + 0.15, n);
        sum += c;
        crown = c;
    }
    // the curtain: the crown's water falling as rain round the shaft, in streaks
    if (tau > 1.0 && q.z < top)
    {
        float x = (d - 0.7 * rcr) / (0.3 * rcr + 0.2 * w);
        float streak = splashNoise(vec3(q.xy * 3.0 / w, (q.z + 2.0 * s.h * tau) / (2.0 * w)) + s.seed * 53.0);
        sum += 0.3 * exp(-x * x) * smoothstep(1.0, 1.3, tau) * exp(-1.5 * (tau - 1.0)) * smoothstep(0.45, 0.8, streak);
    }
    // the base surge: a low ring rolling out, ragged
    float surge = smoothstep(0.2, 0.5, tau) * (1.0 - smoothstep(2.5, 4.0, tau));
    if (surge > 0.01)
    {
        float rr = 2.5 * w * min(tau / 2.5, 1.0) + 0.3 * w, hs = 0.08 * s.h * (1.0 - 0.6 * smoothstep(1.5, 4.0, tau)) + 1.0;
        float x = (d - rr) / (0.4 * w);
        sum += 0.5 * surge * exp(-x * x) * (1.0 - smoothstep(0.5 * rag, rag, q.z / hs));
    }
    return sum * 6.0 / w;   // dense water: a shaft's width is ~6 optical depths
}

// ---------------------------------------------------------------- the rays

// the heights z in [lo, hi] where a + b z is within r of the axis (both components): a ray's stretch inside the
// splash's reach
vec2 splashRange(vec2 a, vec2 b, float r, float zmax)
{
    vec2 lo = vec2(0.0), hi = vec2(zmax);
    for (int i = 0; i < 2; i++)
    {
        if (abs(b[i]) < 1e-5)
        {
            if (abs(a[i]) >= r)
                return vec2(1.0, 0.0);
            continue;
        }
        float z0 = (-r - a[i]) / b[i], z1 = (r - a[i]) / b[i];
        lo[i] = max(0.0, min(z0, z1));
        hi[i] = min(zmax, max(z0, z1));
    }
    return vec2(max(lo.x, lo.y), min(hi.x, hi.y));
}
