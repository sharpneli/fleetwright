#version 450
#extension GL_GOOGLE_include_directive : require
// Shell splashes (docs/visuals/splashes.md, SplashRenderer): one quad per splash and mode, read from a storage buffer,
// 6 vertices from gl_VertexIndex. The splash's base is in metres from the eye (formed in double on the CPU); the quad
// lies in the sea's plane there, spanned by the view's east and north, and is projected as the ships' quads are
// (shipmap.vert). Its corners carry their place in the splash's frame (metres east and north of the impact).
//  - mode 0, the column through the effects camera: a height z is drawn z tan(pitch) up the screen (north), so the
//    quad reaches up from the base by the column's height times that;
//  - mode 1, its shadow on the sea, away from the shadows' light;
//  - mode 2, the foam (into the wake target), and mode 3, the dye's stain: discs on the water.
// A splash with nothing to draw in a mode gets a quad off the screen.
#include "Include/splash.glsl"

struct Splash
{
    vec4 base;    // xyz: the impact, metres from the eye; w: seed
    vec4 shape;   // x: peak height, m; y: width, m (floored); z: tau (the column's clock); w: rise time, s
    vec4 lean;    // xy: the axis's drift per metre of height; z: opacity (the world's share, the floor); w: 1 the column shows
    vec4 dye;     // rgb: the dye, sRGB-coded; w: its strength (0 none)
    vec4 foam;    // x: age on the foam's clock, s; y: the foam's strength; z: the true width, m; w: the world's share
};

layout(std430, set = 0, binding = 0) readonly buffer Splashes { Splash splashes[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;    // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;       // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;      // xyz: the camera's forward
    vec4 depth;    // x: near, y: far, metres; z: the first splash of this draw; w: the mode
    vec4 east;     // xyz: east at the view's centre; w: tan of the effects camera's pitch
    vec4 north;    // xyz: north there
    vec4 shadow;   // xy: the shadows' drift per metre of height, away from the light (east, north); z: on
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out vec4 fragShape;
layout(location = 2) flat out vec4 fragLean;
layout(location = 3) flat out vec4 fragDye;
layout(location = 4) flat out vec4 fragFoam;
layout(location = 5) flat out float fragSeed;

void main()
{
    Splash p = splashes[int(depth.z) + gl_InstanceIndex];
    int mode = int(depth.w + 0.5);
    SplashShape s = SplashShape(p.shape.x, p.shape.y, p.shape.z, p.shape.w, p.lean.xy, p.base.w);
    float k = east.w;
    vec2 lo, hi;
    bool none = false;
    if (mode == 0 || mode == 1)
    {
        none = p.lean.w < 0.5 || s.tau < 0.0 || s.tau >= SPLASH_GONE || (mode == 1 && shadow.z < 0.5);
        float r = splashRadius(s), zmax = splashHeight(s);
        // where the volume's points land: the axis drifts lean z, and the column draws z k north (mode 0) or its
        // shadow falls drift z (mode 1)
        vec2 b = mode == 0 ? s.lean + vec2(0.0, k) : s.lean + shadow.xy;
        lo = min(vec2(0.0), b * zmax) - r;
        hi = max(vec2(0.0), b * zmax) + r;
    }
    else
    {
        float r = mode == 2 ? 3.4 * p.foam.z : 2.6 * p.foam.z * (1.0 + p.foam.x / 60.0);
        none = mode == 3 && p.dye.w <= 0.0 || p.foam.y <= 0.0 || p.foam.x < 0.0;   // nothing before its (delayed) start
        lo = vec2(-r);
        hi = vec2(r);
    }
    vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));
    vec2 loc = mix(lo, hi, corners[gl_VertexIndex]);
    vec3 r = p.base.xyz + east.xyz * loc.x + north.xyz * loc.y;
    float z = dot(r, fwd.xyz);
    gl_Position = none ? vec4(2.0, 2.0, 2.0, 1.0)
        : vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragShape = p.shape;
    fragLean = p.lean;
    fragDye = p.dye;
    fragFoam = p.foam;
    fragSeed = p.base.w;
}
