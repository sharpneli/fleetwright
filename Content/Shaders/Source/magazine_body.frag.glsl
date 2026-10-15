#version 450
#extension GL_GOOGLE_include_directive : require
// A magazine's fireball, cap and stem through the effects camera (docs/visuals/magazine-explosions.md X2): the oblique
// view from the south, a height z over the deck drawn k z up the screen. A pixel at (x, y) of the draw plane sees the
// points (x, y - k (z - deck), z): its ray is marched
// from the top down (front to back) through the one density (Include/magazine.glsl). Each step:
//  - the smoke, soot (albedo ~0.07), lit by the sky's and sea's ambient, the sun and the moon (wrapped round the body,
//    dimmed by the body's own depth toward the light: the chord out of its ellipsoid, softened for the light it
//    scatters), the flashes' lights, and from inside by the fire (its glow on the smoke near the core);
//  - the fire: the hot gas emits by its temperature (a blackbody, MagazineLook's scale), as many times brighter than
//    the display's white deck as the real fire is than the real one (FlashPhotometry's contrast, the flashes' rule).
// Rolled to white on luminance (as the flashes), premultiplied out.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/magazine.glsl"

struct Mag
{
    vec4 base, quad, shade, body, bodyB, stem, time, misc;
};

layout(std430, set = 2, binding = 0) readonly buffer Mags { Mag mags[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;    // xyz: toward the sun (east, north, up)
    vec4 moonL;   // xyz: toward the moon
    vec4 shadowL; // xy: the shadows' drift per metre of height, away from the light; z: opacity
    vec4 misc;    // x: k; y: metres per pixel; z: the steps' scale; w: display units per cd/m^2 (the fire's scale)
    vec4 fireK;   // x: the cold temperature, K; y: the hot; z: the soot's albedo
    vec4 more;    // y: the mushroom; z: how far the hottest fire's hue whitens
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outColor;

const vec3 Warm = vec3(1.0, 0.86, 0.6) / 0.8992;   // unit luminance

MagState stateOf(Mag m)
{
    MagState s;
    s.c = m.body.xyz;
    s.r = m.body.w;
    s.rv = m.bodyB.x;
    s.sigma = m.bodyB.y;
    s.heat = m.bodyB.z;
    s.skin = m.bodyB.w;
    s.deck = m.misc.x;
    s.drift = m.stem.xy;
    s.stemR = m.stem.z;
    s.stemSigma = m.stem.w;
    s.fire = m.time.x;
    s.capShare = m.time.y;
    s.spin = m.time.z;
    s.mushroom = more.y;
    s.seed = m.base.w;
    return s;
}

void main()
{
    Mag m = mags[fragIndex];
    MagState s = stateOf(m);
    float k = misc.x, mpp = misc.y;
    // only the stretch of this pixel's ray that can meet the body or the stem: x is the same all along it, y falls as
    // its drawn height climbs (an interval of heights for each part's box)
    float rb = 1.35 * s.r, rs = 1.4 * s.stemR;
    vec2 body = vec2(1.0, 0.0), stem = vec2(1.0, 0.0);
    if (abs(fragLoc.x - s.c.x) <= rb)
        body = magCut(magRayY(fragLoc.y, s.c.y - rb, s.c.y + rb, k, s.deck), vec2(s.c.z - 1.35 * s.rv, s.c.z + 1.35 * s.rv));
    if (s.stemSigma > 0.0 && fragLoc.x >= min(0.0, s.drift.x) - rs && fragLoc.x <= max(0.0, s.drift.x) + rs)
        stem = magCut(magRayY(fragLoc.y, min(0.0, s.drift.y) - rs, max(0.0, s.drift.y) + rs, k, s.deck), vec2(s.deck - 3.0, s.c.z));
    vec2 range = magCut(magJoin(body, stem), vec2(0.0, 1e6));
    if (range.x >= range.y)
        discard;
    float zTop = range.y, zBot = range.x;
    float px = (k + 0.2) * (zTop - zBot) / mpp;
    int n = int(clamp(px / 3.0 * misc.z, 8.0, 40.0));
    float dz = (zTop - zBot) / float(n);
    float jitter = splashHash(vec3(gl_FragCoord.xy, 5.0));
    float fine = 1.0 - smoothstep(0.5, 1.5, mpp / (0.1 * s.r));

    vec3 amb = mix(seaAmb.rgb, skyAmb.rgb, 0.65);
    vec3 col = vec3(0.0);
    float trans = 1.0;
    float albedo = fireK.z;
    vec3 glow = m.misc.y * magChroma(mix(fireK.x, fireK.y, max(s.heat, 0.3)));
    int nf = flashCount();
    for (int i = 0; i < n && trans > 0.01; i++)
    {
        float z = zTop - (float(i) + jitter) * dz;
        vec3 q = vec3(fragLoc.x, fragLoc.y - k * (z - s.deck), z);
        float heat, relief;
        float sigma = magDensity(s, q, fine, sunL.z > moonL.z ? sunL.xyz : moonL.xyz, heat, relief);
        if (sigma <= 1e-5)
            continue;
        // the step along the ray: dz up, k dz across the screen
        float ds = dz * sqrt(1.0 + k * k);
        float alpha = 1.0 - exp(-sigma * ds);
        // the light on the smoke: a normal out of the ellipsoid, wrapped; the body's depth toward the light
        vec3 d = q - s.c;
        vec3 nrm = normalize(vec3(d.xy / (s.r * s.r), d.z / (s.rv * s.rv)) + vec3(0.0, 0.0, 1e-6));
        float tSun = exp(-0.3 * s.sigma * magChord(s, q, sunL.xyz)), tMoon = exp(-0.3 * s.sigma * magChord(s, q, moonL.xyz));
        // the billows: lit on their sides toward the light, shaded on the far ones
        float bump = max(1.0 + 1.1 * relief, 0.1);
        vec3 light = amb * (0.6 + 0.4 * tSun) * (1.0 + 0.3 * relief);
        light += sunRad.rgb * (max(dot(nrm, sunL.xyz), 0.0) + 0.2) / 1.2 * tSun * bump;
        light += moonRad.rgb * (max(dot(nrm, moonL.xyz), 0.0) + 0.2) / 1.2 * tMoon * bump;
        // the fire's light from inside and below: on the smoke near the core, most under it
        float e = length(vec3(d.xy / s.r, d.z / s.rv));
        light += glow * exp(-1.5 * e) * (1.0 + 0.6 * clamp(-d.z / s.rv, 0.0, 1.0));
        for (int f = 0; f < nf; f++)
        {
            vec3 lw;
            light += 0.5 * flashAt(f, m.base.xyz + flashEast.xyz * q.x + flashNorth.xyz * q.y + flashUp.xyz * q.z, lw);
        }
        // the fire: emitting by its temperature
        // (its hue whitened by its temperature, toward a warm white: the eye's hue shifts toward yellow as a fire
        // brightens, and a blackbody's own chroma, whitened, reads salmon, as the flashes' research found; by its
        // temperature, not its brightness, so a fire far past white still shows its hot core and cooler rim)
        vec3 emit = vec3(0.0);
        if (heat > 0.01)
        {
            float kel = mix(fireK.x, fireK.y, heat);
            vec3 ch = magChroma(kel);
            ch /= dot(ch, vec3(0.2126, 0.7152, 0.0722));
            ch = mix(ch, Warm, more.z * smoothstep(1350.0, 1950.0, kel));
            emit = ch * min(misc.w * magLuminance(kel), 200.0);
        }
        col += trans * alpha * (albedo * light + emit);
        trans *= 1.0 - alpha;
    }
    float cover = 1.0 - trans;
    if (cover < 0.002)
        discard;
    vec3 c = col / cover;
    // rolled off on luminance (the shoulder on Y), its hue kept: the fire never goes flat white (at night a physical
    // fireball is thousands of times the moonlit deck; rolled to white as the flashes are, it read as a blank blob)
    float y = dot(c, vec3(0.2126, 0.7152, 0.0722));
    if (y > 1e-5)
    {
        vec3 rgb = c * (shoulder(vec3(y), look.z).x / y);
        c = rgb / max(max(rgb.r, max(rgb.g, rgb.b)), 1.0);
    }
    float o = cover * m.time.w;
    outColor = vec4(linearToSrgb(c) * o, o);
}
