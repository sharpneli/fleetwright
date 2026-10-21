#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's accumulation (docs/battle/smoke.md Z5, SmokeFieldRenderer): one quad per puff and level, read from
// the copy's storage buffer, 6 vertices from gl_VertexIndex. The puff's state at the frame's time is worked out here
// (smokefield.glsl, the sim's closed form); its place on the sea is the air frame's rotation of its offset from the
// anchor, plus the anchor's place from the eye (formed in double on the CPU). It's drawn into the level where its
// short sigma is 1.5 to 3 texels (never under a level-0 texel: a smaller puff is widened, its area kept), so a
// kilometre-wide puff writes a few dozen texels; in the world view lifted its height up the screen (the effects camera).
// Its quad lies along its stretch (Z5b) and reaches out to where its column's optical depth falls to the edge's.
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
layout(location = 1) flat out vec4 fragPuff;   // x: the peak column depth; y: 1 / (2 sigma_long^2); z: the tint; w: 1 / (2 sigma_short^2)

void main()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);   // nothing, unless it's drawn at this level
    FieldPuff p = puffs[gl_InstanceIndex];
    if (p.misc.z < 0.5)
        return;
    float z, h2, z2, area;
    float age = max(north.w - p.at.w, 0.0);
    fieldState(p, age, m0.w, m1.w, z, h2, z2, area);
    // its stretch on the screen's plane: the axis carried by the air's frame, its share in the plane (Z5b)
    vec3 axisW = vec3(dot(m0.xyz, p.axis.xyz), dot(m1.xyz, p.axis.xyz), dot(m2.xyz, p.axis.xyz));
    vec2 ax = vec2(dot(axisW, east.xyz), dot(axisW, north.xyz));
    float al = dot(ax, ax);
    vec2 dirL = al > 1e-6 ? ax * inversesqrt(al) : vec2(1.0, 0.0);
    float s = max(sqrt(h2), east.w), sl = max(sqrt(h2 + p.axis.w * al), east.w);
    float peak = area * anchor.w / (6.2831853 * s * sl);
    if (!(peak > m2.w))
        return;
    int level = clamp(int(floor(log2(s / (1.5 * east.w)))), 0, int(depth.w + 0.5));
    if (level != int(depth.z + 0.5))
        return;
    float edge = sqrt(2.0 * log(peak / m2.w));
    vec2 reach = vec2(max(sl * edge, sl), max(s * edge, s));
    // carried by the air's frame, and on along the wind by its own drift against it since the copy (folded in up to it:
    // low smoke lags, high runs ahead)
    vec3 rel = anchor.xyz + vec3(dot(m0.xyz, p.at.xyz), dot(m1.xyz, p.at.xyz), dot(m2.xyz, p.at.xyz)) + wind.xyz * (p.misc.w * max(north.w, 0.0));
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 loc = reach * corners[gl_VertexIndex];   // along its long axis, across it
    vec2 plane = dirL * loc.x + vec2(-dirL.y, dirL.x) * loc.y;
    vec3 r = rel + east.xyz * plane.x + north.xyz * (plane.y + fwd.w * z);
    float d = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (d - depth.x) / (depth.y - depth.x), d);
    fragLoc = loc;
    fragPuff = vec4(peak, 0.5 / (sl * sl), p.misc.y, 0.5 / (s * s));
}
