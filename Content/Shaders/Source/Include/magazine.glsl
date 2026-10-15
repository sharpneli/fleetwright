// A magazine going up (docs/visuals/magazine-explosions.md X2): the fireball and the low cloud of smoke it becomes as
// one analytic density, the one model the body (seen through the effects camera) and its shadow (the sun's ray) are
// marched through. Metres, x east, y north, z up from the sea under the magazine; the state (where the body is, how
// big, how hot) comes from the CPU each frame (MagazineBlast.BodyAt), the shape's detail from here:
//  - the body: an ellipsoid (flattening as it billows out), its outline ragged by value noise in its own units, which
//    rolls round its edge as the cloud turns over (the circulation imitated by advected noise, decision 2);
//  - the heat (0-1, MagazineLook's temperature scale): hot in the core, cooled at the top and outside first (the soot
//    skin), stirred by the noise.

#include "splash.glsl"

struct MagState
{
    vec3 c;        // the body's centre (its drift downwind in xy)
    float r;       // its radius across, m
    float rv;      // its radius up, m
    float sigma;   // its extinction per metre
    float heat;    // its core's heat, 0-1
    float skin;    // the soot skin's share of its cooling, 0-1 (times the knob)
    float deck;    // the deck's height over the sea, m
    float roll;    // how far the cloud turns over at its edge, 0 the young fireball to 1
    float churn;   // how far its billows have boiled on, in noise cells
    float spin;    // its turn so far, radians
    float seed;
};

// the smoke's extinction at q, and its heat there (0-1); fine: the finest octave's weight (below 0: no noise at all,
// the smooth shape, for the shadow). relief: the billows' slope toward l (unit; zero for none), -1 to 1: positive
// where the smoke rises toward the light (a billow's lit side)
float magDensity(MagState s, vec3 q, float fine, vec3 l, out float heat, out float relief)
{
    heat = 0.0;
    relief = 0.0;
    vec3 d = q - s.c;
    float rho = length(d.xy);
    vec3 e3 = vec3(d.xy / s.r, d.z / s.rv);
    float e = length(e3);

    // the noise rolls round a ring at 0.55 r (the cloud turning over at its edge): the point turned about the ring's
    // core in its own vertical plane, near the ring only (blended back to the plain point away from it: turned
    // everywhere, the coordinates pinch toward the axis and the top shows ribs)
    float ring = 0.55 * s.r;
    vec2 pol = vec2(rho - ring, d.z * s.r / s.rv);
    float near = exp(-dot(pol, pol) / (0.3 * s.r * 0.3 * s.r));
    float a = s.spin * s.roll;
    vec2 rot = vec2(cos(a) * pol.x - sin(a) * pol.y, sin(a) * pol.x + cos(a) * pol.y);
    vec2 dir = rho > 1e-3 ? d.xy / rho : vec2(1.0, 0.0);
    vec3 turned = vec3(dir * (ring + rot.x), rot.y * s.rv / s.r);
    vec3 adv = mix(d, turned, near) / (0.28 * s.r) + s.seed * 97.0;
    // the billows boil on as it rises, up through it, fast at first: fixed to the body, it looked rigid moving
    adv.z -= s.churn;
    float n = 0.5;
    if (fine >= 0.0)
    {
        // billows: swirled (so they roll rather than sit on value noise's grid; a closed-form swirl, two lookups
        // cheaper than a noise warp), two octaves, a third where it shows
        adv.xy += 0.45 * vec2(sin(1.3 * adv.y + 0.7 * adv.z), sin(1.1 * adv.x - 0.9 * adv.z));
        float n1 = splashNoise(adv);
        n = 0.62 * n1 + 0.38 * splashNoise(adv * 2.3 + 13.0);
        if (fine > 0.0)
            n = mix(n, 0.8 * n + 0.2 * splashNoise(adv * 5.3 + 29.0), fine);
        if (dot(l, l) > 0.0)   // the coarse octave's slope toward the light
            relief = clamp(2.6 * (n1 - splashNoise(adv - 0.35 * l)), -1.0, 1.0);
    }

    float sum = 0.0;
    // the body: ragged at its outline
    float rag = 0.62 + 0.76 * n;
    if (e < 1.35)
    {
        float edge = 1.0 - smoothstep(0.86, 1.0, e / rag);
        // lumpy inside too, so the light finds billows; the young fireball's hot gas thinner than its soot
        float body = edge * (0.7 + 0.6 * n) * s.sigma;
        body *= mix(0.3, 1.0, clamp(1.2 - s.heat, 0.0, 1.0));
        sum += body;
        // hot in the core, cooled at its top and outside first, and in patches where the soot has formed
        float up = max(d.z / s.rv, 0.0);
        float h = s.heat * (1.0 - 0.25 * e * e) - s.skin * (0.6 * up + 0.4 * e);
        h *= 0.55 + 0.9 * n;
        h *= mix(1.0, smoothstep(0.38, 0.62, n), clamp(s.skin * 2.0, 0.0, 1.0));
        heat = clamp(h, 0.0, 1.0) * edge;
    }
    return sum;
}

// the distance from q (inside the body) out of it toward l (unit): the body's own shadow on the light
float magChord(MagState s, vec3 q, vec3 l)
{
    vec3 sc = vec3(s.r, s.r, s.rv);
    vec3 p = (q - s.c) / sc, ls = l / sc;
    float a = dot(ls, ls), b = dot(p, ls), c = dot(p, p) - 1.0;
    float disc = b * b - a * c;
    if (disc <= 0.0)
        return 0.0;
    return max((-b + sqrt(disc)) / a, 0.0);
}

// the heights z where an effects-camera ray from the draw plane's y meets drawn ys in [lo, hi] (the ray's y falls as
// k (z - deck)): an interval, empty (x > y) when it misses
vec2 magRayY(float y, float lo, float hi, float k, float deck)
{
    return vec2(deck + (y - hi) / k, deck + (y - lo) / k);
}

// the heights z where a straight ray a + b z stays within [lo, hi] on one axis
vec2 magLine(float a, float b, float lo, float hi)
{
    if (abs(b) < 1e-6)
        return a >= lo && a <= hi ? vec2(-1e6, 1e6) : vec2(1.0, 0.0);
    float z0 = (lo - a) / b, z1 = (hi - a) / b;
    return vec2(min(z0, z1), max(z0, z1));
}

vec2 magCut(vec2 a, vec2 b)
{
    return vec2(max(a.x, b.x), min(a.y, b.y));
}

// the hull of two intervals, either possibly empty
vec2 magJoin(vec2 a, vec2 b)
{
    if (a.x >= a.y)
        return b;
    if (b.x >= b.y)
        return a;
    return vec2(min(a.x, b.x), max(a.y, b.y));
}

// a blackbody's luminance, cd/m^2, by its temperature (FlashPhotometry's table: log10 L = 10.902 - 10466 / K within
// a few percent from 1000 to 2000 K) and its chroma, linear, the largest channel 1
float magLuminance(float kelvin)
{
    return pow(10.0, 10.902 - 10466.0 / max(kelvin, 500.0));
}

vec3 magChroma(float kelvin)
{
    float u = clamp((kelvin - 1000.0) / 1000.0, 0.0, 2.0);
    return vec3(1.0, 0.029 + 0.237 * u, max(u - 1.0, 0.0) * 0.15);
}
