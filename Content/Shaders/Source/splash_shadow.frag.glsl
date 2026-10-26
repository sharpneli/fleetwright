#version 450
#extension GL_GOOGLE_include_directive : require
// A splash's shadow on the sea (docs/visuals/splashes.md): the true one, the column's water between the sea and the
// shadows' light (the stronger of sun and moon, as the ships' shadows take). From a point of the sea the ray toward the
// light is marched through the splash's density (Include/splash.glsl); what it takes out darkens the sea and the decks
// under it, premultiplied black over them, as the ships' shadows do. Spray lets some light through, so even a dense
// column's shadow isn't black.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragShape;
layout(location = 2) flat in vec4 fragLean;
layout(location = 3) flat in vec4 fragDye;
layout(location = 4) flat in vec4 fragFoam;
layout(location = 5) flat in float fragSeed;

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;
    vec4 moonL;
    vec4 shadowL;   // xy: the shadows' drift per metre of height, away from the light; z: opacity
    vec4 misc;      // y: metres per pixel; z: steps' scale
};

layout(location = 0) out vec4 outColor;

void main()
{
    SplashShape s = SplashShape(fragShape.x, fragShape.y, fragShape.z, fragShape.w, fragLean.xy, fragSeed);
    float r = splashRadius(s), zmax = splashHeight(s);
    vec2 c = -shadowL.xy, b = c - s.lean;   // the ray toward the light climbs c per metre
    vec2 range = splashRange(fragLoc, b, r, zmax);
    if (range.y <= range.x)
        discard;
    int n = int(clamp((range.y - range.x) * length(vec3(c, 1.0)) / misc.y / 4.0 * misc.z, 4.0, 16.0));
    float dz = (range.y - range.x) / float(n), ds = dz * sqrt(1.0 + dot(c, c));
    float jitter = splashHash(vec3(gl_FragCoord.xy, 11.0)), depth = 0.0, crown;
    for (int i = 0; i < n; i++)
    {
        float z = range.x + (float(i) + jitter) * dz;
        depth += splashDensity(s, vec3(fragLoc + b * z, z), 0.0, crown) * ds;
    }
    float a = 0.85 * (1.0 - exp(-depth)) * shadowL.z * fragLean.z;
    if (a < 0.002)
        discard;
    outColor = hazePremul(vec4(0.0, 0.0, 0.0, a));   // the haze over the shadow (docs/visuals/haze.md)
}
