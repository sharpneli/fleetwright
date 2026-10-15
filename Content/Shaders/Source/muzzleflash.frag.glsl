#version 450
#extension GL_GOOGLE_include_directive : require
// A mount's muzzle flash (docs/visuals/muzzle-flashes.md, FlashRenderer, FlashEmitters): its parts as Gaussians with
// their integrals fixed on the CPU (brightness in display units × m², so a flash keeps its energy from a sprite to a
// point), each in the bore's frame:
//  - per barrel, the primary core and the Mach disc, side by side at the barrels' spacing;
//  - the secondary fireball, ragged: its outline broken by a few lobes and value noise that grow with it, its inside
//    mottled, and redder toward its rim, where it is cooler;
//  - the halo for the bloom pass we don't have: the parts' compressed brightness, widened by 1.5, 6 and 18 px.
// The sum is in display units (1 a lit white deck); rolled to white on its luminance (the ships' shoulder, then a
// desaturation above white), never clipped per channel, which would turn every flash flat yellow. Screen-blended.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

struct Flash
{
    vec4 base, bore, quad, coreA, coreB, discA, discB, fireA, fireB, halo, haloColour;
};

layout(std430, set = 2, binding = 0) readonly buffer Flashes { Flash flashes[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 misc;   // x: metres per pixel; y: the halo's scale; z: its gain; w: the compression's knee
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outColor;

const float TwoPi = 6.2831853;

// a normalised elliptical Gaussian at d (east, north) from its centre, its axes along u and across it
float gauss(vec2 d, vec2 u, vec2 sigma)
{
    vec2 q = vec2(dot(d, u), dot(d, vec2(-u.y, u.x))) / sigma;
    return exp(-0.5 * dot(q, q)) / (TwoPi * sigma.x * sigma.y);
}

void main()
{
    Flash f = flashes[fragIndex];
    vec2 p = fragLoc, u = f.bore.xy, across = vec2(-u.y, u.x);
    int n = int(f.bore.z + 0.5);
    vec3 col = vec3(0.0);

    // the barrels' cores and discs
    for (int i = 0; i < n; i++)
    {
        vec2 o = across * ((float(i) - 0.5 * float(n - 1)) * f.bore.w);
        if (f.coreB.w > 0.0)
            col += f.coreB.rgb * f.coreB.w * gauss(p - f.coreA.xy - o, u, f.coreA.zw);
        if (f.discB.w > 0.0)
            col += f.discB.rgb * f.discB.w * gauss(p - f.discA.xy - o, u, f.discA.zw);
    }

    // the fireball: lobes and noise on its outline (the integral's mean kept by 1.03, as the reference's), mottled
    // inside, cooler at the rim
    if (f.fireB.w > 0.0)
    {
        vec2 d = p - f.fireA.xy;
        vec2 q = vec2(dot(d, u), dot(d, across)) / f.fireA.zw;
        float seed = f.base.w, life = f.haloColour.w;
        float ang = atan(q.y, q.x);
        float rmod = 1.0 + 0.18 * sin(3.0 * ang + seed * 40.0) + 0.12 * sin(5.0 * ang + seed * 73.0);
        // noise in the fireball's own units, so it grows with it; drifting outward as it burns
        vec2 nq = q * 1.6 + seed * 97.0;
        float nz = splashNoise(vec3(nq, life * 3.0)) * 0.65 + splashNoise(vec3(nq * 2.3, life * 5.0 + 11.0)) * 0.35;
        rmod *= 0.8 + 0.4 * nz;
        float r2 = dot(q, q) / (rmod * rmod);
        float g = exp(-0.5 * r2) / (TwoPi * f.fireA.z * f.fireA.w * 1.03);
        float mottle = 0.7 + 0.6 * splashNoise(vec3(nq * 3.1, life * 7.0 + 23.0));
        float rim = clamp(r2 / 4.0, 0.0, 1.0);
        vec3 tint = f.fireB.rgb * vec3(1.0, 1.0 - 0.35 * rim, 1.0 - 0.7 * rim);
        col += tint * f.fireB.w * g * mottle;
    }

    // the halo: the compressed source widened by the bloom's radii (a Gaussian blurred by a Gaussian)
    if (f.halo.w > 0.0)
    {
        vec2 d = p - f.halo.xy;
        float s0 = f.halo.z * f.halo.z, d2 = dot(d, d), h = 0.0;
        vec3 radii = vec3(1.5, 6.0, 18.0) * misc.x * misc.y, share = vec3(0.2, 0.1, 0.05);
        for (int i = 0; i < 3; i++)
        {
            float s2 = s0 + radii[i] * radii[i];
            h += share[i] * exp(-0.5 * d2 / s2) / (TwoPi * s2);
        }
        col += f.haloColour.rgb * f.halo.w * h * misc.z;
    }

    // rolled to white on luminance: the shoulder on Y, then toward white as it passes it
    float y = dot(col, vec3(0.2126, 0.7152, 0.0722));
    if (y < 1e-4)
        discard;
    float yt = shoulder(vec3(y), look.z).x;
    vec3 rgb = col * (yt / y);
    float over = clamp(log2(max(y, 1e-6)) / 3.0, 0.0, 1.0);
    rgb = mix(rgb / max(max(rgb.r, max(rgb.g, rgb.b)), 1.0), vec3(yt), over);
    outColor = vec4(linearToSrgb(rgb), 0.0);
}
