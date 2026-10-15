#version 450
#extension GL_GOOGLE_include_directive : require
// A mount's muzzle blast on the water (docs/visuals/muzzle-flashes.md F5; research/vfx/muzzle-blast-water-vfx.md 3.2),
// into the wake target: rgb added (z: the slope variance), alpha by the most (the foam, in the lanes' encoding). Per
// pixel of the sea, d from the barrel tips (their height in):
//  - the overpressure dp/p0 = 0.11 x + 0.0061 x^2, x = lambda'/r, lambda' = lambda beta(theta) (Fansler's directivity,
//    mu 0.78: ~8 times stronger along the bore than behind);
//  - the front's arrival from the table (Rankine-Hugoniot), so the mark grows at the speed of the shock;
//  - its strength s from p_vis to p_sat on a log scale; behind the front a thin dark edge (the ripples laid down, the
//    variance taken off) and then the frost (roughened, added), fading over tau_frost: the slopes' spread scaled by
//    1 + 2.5 frost - 0.7 edge on the wind's own (Cox-Munk);
//  - the scour foam within the scaled distance k_foam (and a jet lobe along the bore at low elevation), broken as
//    the splashes' foam is and streaked radially, fading over tau_foam.
#include "Include/splash.glsl"

struct Blast
{
    vec4 base, bore, blast, quad;
};

layout(std430, set = 2, binding = 0) readonly buffer Blasts { Blast blasts[]; };

layout(set = 3, binding = 0) uniform Params
{
    vec4 arr[4];   // the front's arrival, units of lambda'/c0, at rho = rhoMin stepped by rhoStep
    vec4 misc0;    // x: p_vis/p0; y: p_sat/p0; z: tau_frost, s; w: tau_foam, s
    vec4 misc1;    // x: the wind's mean-square slope; y: k_foam; z: ln rhoMin; w: ln rhoStep
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outWake;

const float C0 = 343.0, Mu = 0.78;

float arrivalAt(float rho)
{
    float f = (log(max(rho, 1e-6)) - misc1.z) / misc1.w;
    if (f <= 0.0)
        return arr[0].x * rho / exp(misc1.z);
    if (f >= 15.0)
        return arr[3].w + (rho - exp(misc1.z + 15.0 * misc1.w));
    // the node below by its log, then linear in rho between the two (the arrival is nearly linear in rho)
    int i = int(f);
    float a = arr[i / 4][i % 4], b = arr[(i + 1) / 4][(i + 1) % 4];
    float r0 = exp(misc1.z + float(i) * misc1.w);
    return mix(a, b, (rho - r0) / (r0 * (exp(misc1.w) - 1.0)));
}

void main()
{
    Blast b = blasts[fragIndex];
    float lam = b.blast.x, age = b.blast.y, strength = b.blast.z, seed = b.blast.w;
    float mpp = max(fwidth(fragLoc.x), fwidth(fragLoc.y));   // here, before any discard
    vec2 u = b.bore.xy;
    vec3 d = vec3(fragLoc, -b.base.w);
    float r = max(length(d), 1e-3);
    vec3 bore = vec3(u * b.bore.w, b.bore.z);
    float ct = dot(d, bore) / r, st2 = max(1.0 - ct * ct, 0.0);
    float lp = lam * (Mu * ct + sqrt(1.0 - Mu * Mu * st2));
    float x = lp / r, dp = 0.11 * x + 0.0061 * x * x;
    float since = age - arrivalAt(r / lp) * lp / C0;
    if (since < 0.0)
        discard;

    // the frost and the leading edge
    float s = clamp(log(dp / misc0.x) / log(misc0.y / misc0.x), 0.0, 1.0);
    float band = C0 * 0.0009 * lam, behind = since * C0;
    float edge = s * exp(-(behind / band) * (behind / band));
    float frost = s * (1.0 - exp(-(behind / (2.0 * band)) * (behind / (2.0 * band)))) * exp(-since / misc0.z);
    // (the edge takes off at most half the wind's variance: the wake target can't know the sea's own)
    float k = max(1.0 + 2.5 * frost - 0.7 * edge, 0.71);
    float dvar = misc1.x * (k * k - 1.0) * strength;

    // the scour foam: by scaled distance, and the jet's lobe along the bore's track at low elevation
    float zf = r / lp;
    float dens = pow(clamp(1.3 - zf / max(misc1.y, 1e-3), 0.0, 1.0), 1.5);
    float along = dot(fragLoc, u), across = dot(fragLoc, vec2(-u.y, u.x));
    float low = smoothstep(0.17, 0.05, b.bore.z);   // under ~10 degrees
    float lobe = low * step(0.0, along) * clamp(1.0 - along / (4.0 * lam * b.bore.w), 0.0, 1.0) * exp(-(across * across) / (0.16 * lam * lam));
    dens = max(dens, lobe) * exp(-since / misc0.w);
    if (dens <= 0.0 && abs(dvar) < 1e-6)
        discard;   // no mark here: skip the noise
    // broken as the splashes' foam is (splash_foam.frag): patches 4 m, clumps 1.5 m and bubbles 0.6 m fixed to the
    // water, each faded to its mean as its cells near a pixel, with the blast's radial streaks (noise in angle and
    // scaled distance round the muzzle) through them; a sharp threshold over that, the churned water under it patchy
    vec2 pw = fragLoc + seed * 911.0;
    vec3 cell = vec3(4.0, 1.5, 0.6), full = vec3(0.4, 0.35, 0.25);
    vec3 amp = full * (1.0 - smoothstep(vec3(0.3), vec3(0.8), mpp / cell));
    float lost = dot(full - amp, vec3(0.29));
    float n = 0.5 * (1.0 - (amp.x + amp.y + amp.z));
    n += amp.x * splashNoise(vec3(pw / cell.x, 1.0)) + amp.y * splashNoise(vec3(pw / cell.y, 2.0)) + amp.z * splashNoise(vec3(pw / cell.z, 3.0));
    float ang = atan(fragLoc.y, fragLoc.x);
    float streak = splashNoise(vec3(ang * 24.0 + seed * 50.0, zf * 9.0, seed * 7.0));
    n = clamp((0.75 * n + 0.25 * streak - 0.5) * 2.4 + 0.5, 0.0, 1.0);
    float white = smoothstep(0.0, 0.6, dens);
    float t = 1.0 - 0.55 * white, soft = max(0.05, 1.6 * lost);
    float broken = smoothstep(t - soft, t + soft, n) * clamp(1.6 * white, 0.0, 1.0);
    float aerated = clamp(1.5 * dens, 0.0, 1.0) * smoothstep(0.4, 0.8, n);
    float foam = (0.4 * aerated + 0.6 * broken) * strength;
    outWake = vec4(0.0, 0.0, dvar, foam);
}
