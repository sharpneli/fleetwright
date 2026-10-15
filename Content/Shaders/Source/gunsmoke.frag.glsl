#version 450
#extension GL_GOOGLE_include_directive : require
// A gun smoke puff (docs/visuals/muzzle-flashes.md F6; research/vfx/muzzle-flash-smoke-research.md 5.4, 7.4): its
// vertical optical depth tau = A / (2 pi sigma^2) exp(-r^2 / 2 sigma^2), the same Gaussian a sight line would read,
// billowed by noise fixed to the puff (its mean kept, so the coverage stays honest) and drawn as alpha 1 - e^-tau.
//  - mode 0, the puff: lit by the sky, the sun and the moon (a dome seen from above, wrapped, its far side from the
//    light a little darker), and by the flashes' lights; white (salts, the water fog) or a little warm near the muzzle
//    for nitrocellulose powders, whitening over ~5 s. Premultiplied, through the ships' shoulder.
//  - mode 1, its shadow on the sea and the decks: black at the shadows' opacity times its alpha.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

struct Puff
{
    vec4 centre, shape, light, tint, shadow;
};

layout(std430, set = 2, binding = 0) readonly buffer Puffs { Puff puffs[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;    // xyz: toward the sun, east, north, up; w: metres per pixel
    vec4 moonL;   // xyz: toward the moon
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;
layout(location = 2) flat in int fragMode;

layout(location = 0) out vec4 outColor;

const float TwoPi = 6.2831853;

// the puff's optical depth at d (east, north, from its centre), billowed
float tauAt(Puff p, vec2 d)
{
    float s = p.shape.x;
    float g = exp(-0.5 * dot(d, d) / (s * s)) * p.shape.y / (TwoPi * s * s);
    // billows the size of the puff and smaller, fixed to it, drifting slowly as it ages; their mean is 1
    vec3 q = vec3(d / s * 1.3 + p.centre.w * 61.0, p.light.y * 0.02 + p.centre.w * 17.0);
    float n = 0.6 * splashNoise(q) + 0.4 * splashNoise(q * 2.7 + 5.0);
    return g * (0.45 + 1.1 * n);
}

// how much of the smoke round d lies toward the light (h toward it) rather than away: 0 on the near side, up to 1 on
// the far side; a share, so a thick puff's centre isn't darkened by its depth
float farSide(Puff p, vec2 d, vec2 h)
{
    float toward = tauAt(p, d + h), away = tauAt(p, d - h);
    return clamp((toward - away) / (toward + away + 0.2), 0.0, 1.0);
}

void main()
{
    Puff p = puffs[fragIndex];
    float tau = tauAt(p, fragLoc);
    float alpha = 1.0 - exp(-tau);
    if (alpha < 0.002)
        discard;
    if (fragMode == 1)
    {
        float a = alpha * p.shadow.z;
        outColor = vec4(0.0, 0.0, 0.0, a);
        return;
    }
    // the light: the sky's and sea's ambient, and the sun and the moon on the puff as a dome seen from above (its
    // normal tilting out with the distance from its centre, wrapped), its far side from the light a little darker (two
    // taps, a sigma apart); the flashes' lights from where they are
    float s = p.shape.x;
    vec3 nrm = normalize(vec3(fragLoc / s * 0.6, 1.0));
    vec2 sunH = normalize(sunL.xy + vec2(1e-5)) * s * 0.5, moonH = normalize(moonL.xy + vec2(1e-5)) * s * 0.5;
    float sunLit = (max(dot(nrm, sunL.xyz), 0.0) + 0.4) / 1.4 * (1.0 - 0.4 * (1.0 - sunL.z) * farSide(p, fragLoc, sunH));
    float moonLit = (max(dot(nrm, moonL.xyz), 0.0) + 0.4) / 1.4 * (1.0 - 0.4 * (1.0 - moonL.z) * farSide(p, fragLoc, moonH));
    vec3 lit = mix(seaAmb.rgb, skyAmb.rgb, 0.7) + sunRad.rgb * sunLit + moonRad.rgb * moonLit;
    int nf = flashCount();
    if (nf > 0)
    {
        vec3 here = p.centre.xyz + flashEast.xyz * fragLoc.x + flashNorth.xyz * fragLoc.y + flashUp.xyz * p.light.x;
        for (int i = 0; i < nf; i++)
        {
            vec3 lw;
            lit += 0.5 * flashAt(i, here, lw);
        }
    }
    vec3 albedo = mix(mix(p.tint.rgb, vec3(0.9), p.light.z), vec3(0.92), p.light.w);
    vec3 c = linearToSrgb(shoulder(albedo * lit, look.z));
    outColor = vec4(c * alpha, alpha);
}
