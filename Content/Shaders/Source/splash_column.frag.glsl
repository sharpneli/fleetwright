#version 450
#extension GL_GOOGLE_include_directive : require
// A splash's column through the effects camera (docs/visuals/splashes.md decision 1): the oblique view from the south,
// the pitch's tangent k up the screen per metre up. A pixel at (x, y) of the splash's draw plane sees the points
// (x, y - k z, z): its ray is marched from the top down (front to back) through the splash's density
// (Include/splash.glsl), lit by the shared lights (shiplight.glsl's Lights: the sun, the moon, the sky's and sea's
// ambient), with a wrap diffuse round the column, the blossom shading the shaft under it, and a forward-scatter lobe so a
// column against the light glows at its rim. A dyed shell tints the upper column and the crown, more as it thins.
// Premultiplied out; the light through the ships' shoulder, display-referred.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragShape;
layout(location = 2) flat in vec4 fragLean;
layout(location = 3) flat in vec4 fragDye;
layout(location = 4) flat in vec4 fragFoam;
layout(location = 5) flat in float fragSeed;
layout(location = 6) flat in vec3 fragBase;

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;      // xyz: toward the sun, in east, north, up
    vec4 moonL;     // xyz: toward the moon
    vec4 shadowL;   // xy: the shadows' drift per metre of height, away from the light; z: opacity
    vec4 misc;      // x: k, tan of the effects camera's pitch; y: metres per pixel; z: steps' scale; w: vivid
};

layout(location = 0) out vec4 outColor;

const vec3 Spray = vec3(0.86, 0.9, 0.93);   // the water's albedo, sRGB-coded: white with a little of the sea in it

void main()
{
    SplashShape s = SplashShape(fragShape.x, fragShape.y, fragShape.z, fragShape.w, fragLean.xy, fragSeed);
    float k = misc.x, mpp = misc.y;
    float r = splashRadius(s), zmax = splashHeight(s);
    vec2 a = fragLoc, c = vec2(0.0, -k), b = c - s.lean;
    vec2 range = splashRange(a, b, r, zmax);
    if (range.y <= range.x)
        discard;

    // steps: about one every 3 px of the column's height on the screen, 6 to 40, jittered per pixel against banding
    float px = (range.y - range.x) * k / mpp;
    int n = int(clamp(px / 3.0 * misc.z, 6.0, 40.0));
    float dz = (range.y - range.x) / float(n), ds = dz * sqrt(1.0 + dot(c, c));
    float jitter = splashHash(vec3(gl_FragCoord.xy, 7.0));
    float fine = 1.0 - smoothstep(0.5, 1.5, mpp / (0.22 * s.w));

    vec3 v = normalize(vec3(0.0, -k, 1.0));   // toward the effects camera
    vec3 amb = mix(seaAmb.rgb, skyAmb.rgb, 0.65);
    float top = splashTop(s), rcr = splashCrownR(s);
    // the forward-scatter lobe (Henyey-Greenstein, g 0.55) toward the eye
    float g = 0.55, ct = dot(-sunL.xyz, v), hg = (1.0 - g * g) / pow(1.0 + g * g - 2.0 * g * ct, 1.5) / 12.566;
    float ctm = dot(-moonL.xyz, v), hgm = (1.0 - g * g) / pow(1.0 + g * g - 2.0 * g * ctm, 1.5) / 12.566;
    vec3 albedo = srgbToLinear(Spray), dye = srgbToLinear(fragDye.rgb);

    vec3 col = vec3(0.0);
    float trans = 1.0;
    int nf = flashCount();
    for (int i = 0; i < n && trans > 0.01; i++)
    {
        float z = range.y - (float(i) + jitter) * dz;
        vec3 q = vec3(a + b * z, z);
        float crown;
        float sigma = splashDensity(s, q, fine, crown);
        if (sigma <= 1e-4)
            continue;
        float alpha = 1.0 - exp(-sigma * ds);
        // a normal for the light: out from the axis, turned up where it's the blossom
        float d = length(q.xy);
        vec2 radial = d > 1e-3 ? q.xy / d : vec2(0.0);
        float share = clamp(crown * 2.0, 0.0, 1.0);
        vec3 nrm = normalize(vec3(radial * (1.0 - 0.6 * share), 0.25 + 1.2 * share));
        // the blossom shades the shaft under it from a high light
        float under = q.z < top - 0.2 * rcr && d < rcr ? smoothstep(0.55, 1.1, s.tau) * (1.0 - 0.6 * smoothstep(0.0, 1.0, d / rcr)) : 0.0;
        float lit = 1.0 - 0.55 * under;
        vec3 light = amb;
        light += sunRad.rgb * (max(dot(nrm, sunL.xyz) + 0.3, 0.0) / 1.3 * lit + 1.2 * hg * (1.0 - share));
        light += moonRad.rgb * (max(dot(nrm, moonL.xyz) + 0.3, 0.0) / 1.3 * lit + 1.2 * hgm);
        light = nightSee(light);   // the rods' view of the natural light at night; the flashes' in colour
        // the flashes' lights (docs/visuals/muzzle-flashes.md F4), from where this water is
        for (int f = 0; f < nf; f++)
        {
            vec3 lw;
            vec3 rad = flashAt(f, fragBase + flashEast.xyz * q.x + flashNorth.xyz * q.y + flashUp.xyz * q.z, lw);
            vec3 ll = vec3(dot(lw, flashEast.xyz), dot(lw, flashNorth.xyz), dot(lw, flashUp.xyz));
            light += rad * max(dot(nrm, ll) + 0.3, 0.0) / 1.3;
        }
        // the dye: little at the base and in the falling curtain, most in the upper column and the crown, more as
        // the column thins
        float t = fragDye.w * (0.1 + 0.4 * clamp(q.z / max(s.h, 1.0), 0.0, 1.0) + 0.2 * share) * (0.5 + 0.5 * smoothstep(0.5, 1.5, s.tau));
        vec3 alb = mix(albedo, dye, clamp(t * misc.w, 0.0, 0.85));
        col += trans * alpha * alb * light;
        trans *= 1.0 - alpha;
    }
    float cover = 1.0 - trans;
    if (cover < 0.002)
        discard;
    vec3 c3 = linearToSrgb(shoulder(col / cover, look.z));
    float o = cover * fragLean.z;
    outColor = vec4(hazeCoded(c3) * o, o);
}
