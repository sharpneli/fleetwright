#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's accumulation (docs/battle/smoke.md Z5, SmokeFieldRenderer): one quad per puff and level, read from
// the copy's storage buffer, 6 vertices from gl_VertexIndex. The puff's state at the frame's time is worked out here
// (smokefield.glsl, the sim's closed form); its place on the sea is the air frame's rotation of its offset from the
// anchor, plus the anchor's place from the eye (formed in double on the CPU). It's drawn into the level where its
// sigma is 1.5 to 3 texels (never under a level-0 texel: a smaller puff is widened, its area kept), so a kilometre-wide
// puff writes a few dozen texels; in the world view lifted its height up the screen (the effects camera). Its quad
// reaches out to where its column's optical depth falls to the edge's.
#include "Include/smokefield.glsl"

layout(std430, set = 0, binding = 0) readonly buffer Puffs { FieldPuff puffs[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;    // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;       // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;      // xyz: the camera's forward; w: the lift, screen metres north a metre up (the world view's share in)
    vec4 depth;    // x: near, y: far, metres; z: this level; w: the top level
    vec4 east;     // xyz: east at the view's centre; w: a level-0 texel, m
    vec4 north;    // xyz: north there; w: the frame's time, s from the copy
    vec4 m0;       // xyz: the air frame's rotation's first row (world = M air); w: sigma_v, m/s
    vec4 m1;       // xyz: its second row; w: the Lagrangian time across, s
    vec4 m2;       // xyz: its third row; w: the edge's optical depth
    vec4 anchor;   // xyz: the copy's anchor on the sea, m from the eye; w: a gain on the extinction
    vec4 wind;     // xyz: the way the wind blows at the view's centre
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out vec4 fragPuff;   // x: the peak column depth; y: 1 / (2 sigma^2); z: the tint

void main()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);   // nothing, unless it's drawn at this level
    FieldPuff p = puffs[gl_InstanceIndex];
    if (p.misc.z < 0.5)
        return;
    float z, h2, z2, area;
    float age = max(north.w - p.at.w, 0.0);
    fieldState(p, age, m0.w, m1.w, z, h2, z2, area);
    float s = max(sqrt(h2), east.w);
    float peak = area * anchor.w / (6.2831853 * s * s);
    if (!(peak > m2.w))
        return;
    int level = clamp(int(floor(log2(s / (1.5 * east.w)))), 0, int(depth.w + 0.5));
    if (level != int(depth.z + 0.5))
        return;
    float reach = max(s * sqrt(2.0 * log(peak / m2.w)), s);
    // carried by the air's frame, and on along the wind by its own drift against it (low smoke lags, high runs ahead)
    vec3 rel = anchor.xyz + vec3(dot(m0.xyz, p.at.xyz), dot(m1.xyz, p.at.xyz), dot(m2.xyz, p.at.xyz)) + wind.xyz * (p.misc.w * age);
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 loc = reach * corners[gl_VertexIndex];
    vec3 r = rel + east.xyz * loc.x + north.xyz * (loc.y + fwd.w * z);
    float d = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (d - depth.x) / (depth.y - depth.x), d);
    fragLoc = loc;
    fragPuff = vec4(peak, 0.5 / (s * s), p.misc.y, 0.0);
}
