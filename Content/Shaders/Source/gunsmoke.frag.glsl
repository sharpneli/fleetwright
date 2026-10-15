#version 450
#extension GL_GOOGLE_include_directive : require
// A gun smoke puff (docs/visuals/muzzle-flashes.md F6; research/vfx/muzzle-flash-smoke-research.md 5.4, 7.4): its
// vertical optical depth tau = A / (2 pi sigma^2) exp(-r^2 / 2 sigma^2), the same Gaussian a sight line would read,
// billowed by noise fixed to the puff (its mean kept, so the coverage stays honest) and drawn as alpha 1 - e^-tau.
//  - mode 0, the puff: lit by the sky, the sun and the moon (a dome seen from above, wrapped, its far side from the
//    light a little darker), and by the flashes' lights; white (salts, the water fog) or a little warm near the muzzle
//    for nitrocellulose powders, whitening over ~5 s. Premultiplied, through the ships' shoulder.
//  - mode 1, its shadow on the sea and the decks: black at the shadows' opacity times its (smooth) alpha.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

struct Puff
{
    vec4 centre, shape, light, tint, shadow, stem;
};

layout(std430, set = 2, binding = 0) readonly buffer Puffs { Puff puffs[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;    // xyz: toward the sun, east, north, up; w: metres per pixel
    vec4 moonL;   // xyz: toward the moon
    vec4 smokeLook;   // x: the opacity cap; y: the youth's time, s; z: the stretch along the bore; w: the young billows
    vec4 smokeMotion; // x: the churn; y: the warp
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;
layout(location = 2) flat in int fragMode;

layout(location = 0) out vec4 outColor;

const float TwoPi = 6.2831853;

// how young the puff is, 1 at its birth
float youth(Puff p)
{
    return exp(-p.light.y / smokeLook.y);
}

// the puff's smooth optical depth at d (east, north, from its centre), as drawn: while young stretched along the bore
// (the gas jet carried it there), its area kept; and capped, tau / (1 + tau / cap), so a thick young core isn't a solid
// ball (old, thin smoke is untouched). blur widens it (the shadow's penumbra).
float smoothTau(Puff p, vec2 d, float blur)
{
    float s = p.shape.x * blur;
    vec2 b = vec2(p.tint.w, p.shadow.w);
    float st = 1.0 + smokeLook.z * youth(p) * length(b);
    vec2 u = length(b) > 0.5 ? normalize(b) : vec2(1.0, 0.0);
    float sa = s * sqrt(st), sc = s / sqrt(st);
    float da = dot(d, u), dc = dot(d, vec2(-u.y, u.x));
    float peak = p.shape.y / (TwoPi * sa * sc);
    float tau = exp(-0.5 * (da * da / (sa * sa) + dc * dc / (sc * sc))) * peak;
    // the stem (an effect): while the jet runs, gas joins the puff back to the muzzle, tapering toward it
    if (p.stem.z > 0.0)
    {
        vec2 t = p.stem.xy;
        float f = clamp(dot(d, t) / max(dot(t, t), 1e-6), 0.0, 1.0);
        vec2 off = d - f * t;
        float r = s * mix(0.8, 0.15, f);
        tau = max(tau, 0.8 * p.stem.z * peak * exp(-0.5 * dot(off, off) / (r * r)));
    }
    return tau / (1.0 + tau / smokeLook.x);
}

// and billowed, their mean 1. The billows churn (an effect: the user, 2026-10-15, a fixed pattern read as a brick
// leaving the barrel): the noise turns over fast at first and slower as the puff ages, warped by a second noise, so they
// roll; while young they're smaller than the puff and grow slower than it, so it gains billows rather than magnifying
// them, and old smoke's are the size of the puff. The finer octave only where its cells are big enough on the screen.
float tauAt(Puff p, vec2 d)
{
    float s = p.shape.x, y = youth(p), age = p.light.y;
    float cell = mix(s / 1.3, 0.9 * pow(s, 0.8), y);
    float churn = p.centre.w * 17.0 + smokeMotion.x * log(1.0 + age / 0.4);
    vec2 q2 = d / cell + p.centre.w * 61.0;
    vec2 warp = vec2(splashNoise(vec3(q2 * 0.5, churn * 0.7 + 3.0)), splashNoise(vec3(q2 * 0.5 + 19.0, churn * 0.7 + 11.0))) - 0.5;
    q2 += smokeMotion.y * (0.3 + 0.7 * y) * warp;
    vec3 q = vec3(q2, churn);
    // two lookups turned 37 degrees against each other: value noise's grid hidden (alone, at a young puff's contrast,
    // its cells showed as blocks); their spread restored
    const mat2 Turn = mat2(0.7986, 0.6018, -0.6018, 0.7986);
    float fine = smoothstep(6.0, 16.0, cell / sunL.w);
    float n = 0.5 + 1.35 * (0.5 * (splashNoise(q) + splashNoise(vec3(Turn * q.xy * 1.13 + 7.0, q.z))) - 0.5);
    if (fine > 0.0)
        n = mix(n, 0.6 * n + 0.4 * splashNoise(vec3(Turn * q.xy * 2.7 + 5.0, q.z)), fine);
    // the billows' contrast, raised while young (it tears into lobes), mostly at the edge (lobes out of a cloud, only a
    // mottling inside), their mean kept where it can be; never quite empty (small round holes through a white puff
    // read as dark dirt from above)
    float k = (1.1 + smokeLook.w * y) * mix(0.35, 1.0, smoothstep(0.3, 1.6, length(d) / s));
    return smoothTau(p, d, 1.0) * max(1.0 + k * (n - 0.5), 0.3);
}

// how much of the smoke round d lies toward the light (h toward it) rather than away: 0 on the near side, up to 1 on
// the far side; a share, so a thick puff's centre isn't darkened by its depth. The smooth Gaussian alone (no billows:
// the noise is most of a puff's cost)
float farSide(Puff p, vec2 d, vec2 h)
{
    float s2 = 2.0 * p.shape.x * p.shape.x;
    float toward = exp(-dot(d + h, d + h) / s2), away = exp(-dot(d - h, d - h) / s2);
    return clamp((toward - away) / (toward + away + 1e-3), 0.0, 1.0);
}

void main()
{
    Puff p = puffs[fragIndex];
    if (fragMode == 1)
    {
        // the shadow: the smooth Gaussian is enough under a puff's billows, softened (a penumbra, and the light the
        // billows let through); faint while the puff is young (seen through its holes from above it read as dirt)
        float a = 0.7 * (1.0 - 0.8 * youth(p)) * (1.0 - exp(-smoothTau(p, fragLoc, 1.3) * (1.0 - smoothstep(0.7 * p.shape.w, p.shape.w, length(fragLoc))))) * p.shadow.z;
        if (a < 0.002)
            discard;
        outColor = vec4(0.0, 0.0, 0.0, a);
        return;
    }
    // faded to nothing inside the quad (it ends where the smooth depth is down to a few percent: cut there, a wide old
    // puff showed its box's edges)
    float tau = tauAt(p, fragLoc) * (1.0 - smoothstep(0.7 * p.shape.w, p.shape.w, length(fragLoc)));
    float alpha = 1.0 - exp(-tau);
    if (alpha < 0.002)
        discard;
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
