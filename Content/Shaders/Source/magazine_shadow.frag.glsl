#version 450
#extension GL_GOOGLE_include_directive : require
// A magazine's shadow on the sea (docs/visuals/magazine-explosions.md X2): the true one, the body's smoke between the
// sea and the shadows' light (the stronger of sun and moon, as the ships' take). From a point of the sea the ray toward
// the light is marched through the one density (Include/magazine.glsl, its coarse octave); what it takes out darkens the
// sea and the decks under it, premultiplied black over them, as the splashes' shadows do. By day the strongest cue that
// the smoke is tall: a 1 km cap's shadow is over a kilometre long by 15 s.
#include "Include/magazine.glsl"

struct Mag
{
    vec4 base, quad, shade, body, bodyB, stem, time, misc;
};

layout(std430, set = 2, binding = 0) readonly buffer Mags { Mag mags[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;
    vec4 moonL;
    vec4 shadowL; // xy: the shadows' drift per metre of height, away from the light; z: opacity
    vec4 misc;    // y: metres per pixel; z: the steps' scale
    vec4 fireK;
    vec4 more;    // y: the mushroom
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outColor;

void main()
{
    Mag m = mags[fragIndex];
    MagState s;
    s.c = m.body.xyz;
    s.r = m.body.w;
    s.rv = m.bodyB.x;
    s.sigma = m.bodyB.y;
    s.heat = 0.0;
    s.skin = 0.0;
    s.deck = m.misc.x;
    s.drift = m.stem.xy;
    s.stemR = m.stem.z;
    s.stemSigma = m.stem.w;
    s.fire = 0.0;
    s.capShare = m.time.y;
    s.spin = m.time.z;
    s.mushroom = more.y;
    s.seed = m.base.w;

    vec2 c = -shadowL.xy;   // the ray toward the light climbs c per metre
    // only the stretch of the ray inside the body's box or the stem's
    float rb = 1.35 * s.r, rs = 1.4 * s.stemR;
    vec2 body = magCut(magCut(magLine(fragLoc.x, c.x, s.c.x - rb, s.c.x + rb), magLine(fragLoc.y, c.y, s.c.y - rb, s.c.y + rb)),
        vec2(s.c.z - 1.35 * s.rv, s.c.z + 1.35 * s.rv));
    vec2 stem = vec2(1.0, 0.0);
    if (s.stemSigma > 0.0)
        stem = magCut(magCut(magLine(fragLoc.x, c.x, min(0.0, s.drift.x) - rs, max(0.0, s.drift.x) + rs),
            magLine(fragLoc.y, c.y, min(0.0, s.drift.y) - rs, max(0.0, s.drift.y) + rs)), vec2(s.deck - 3.0, s.c.z));
    vec2 range = magCut(magJoin(body, stem), vec2(0.0, 1e6));
    if (range.x >= range.y)
        discard;
    float zTop = range.y, zBot = range.x;
    int n = int(clamp((zTop - zBot) * length(vec3(c, 1.0)) / misc.y / 6.0 * misc.z, 6.0, 20.0));
    float dz = (zTop - zBot) / float(n), ds = dz * sqrt(1.0 + dot(c, c));
    float jitter = splashHash(vec3(gl_FragCoord.xy, 13.0)), depth = 0.0, heat, relief;
    for (int i = 0; i < n; i++)
    {
        float z = zBot + (float(i) + jitter) * dz;
        depth += magDensity(s, vec3(fragLoc + c * z, z), -1.0, vec3(0.0), heat, relief) * ds;
    }
    float a = 0.7 * (1.0 - exp(-depth)) * shadowL.z * m.time.w;
    if (a < 0.002)
        discard;
    outColor = vec4(0.0, 0.0, 0.0, a);
}
