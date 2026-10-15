#version 450
// The torpedoes under the water (docs/visuals/torpedoes.md R3, TorpedoRenderer): one quad per torpedo running, read from
// a storage buffer, 6 vertices from gl_VertexIndex. Its centre is in metres from the eye (formed in double on the CPU);
// the quad lies in the sea's plane there along its heading, a little longer and wider than the body so the soft edge
// fits, and is projected as the ships' quads are (shipmap.vert). Its corners carry their place along and across the
// body, metres.

struct Torpedo
{
    vec4 base;    // xyz: its centre on the surface over it, metres from the eye; w: seed
    vec4 axis;    // xyz: its heading, unit, Earth-fixed; w: half its drawn length, m
    vec4 side;    // xyz: to starboard, unit; w: its drawn radius, m
    vec4 look;    // x: opacity; y: the blur its depth gives, m; z: the world's share
};

layout(std430, set = 0, binding = 0) readonly buffer Torpedoes { Torpedo torpedoes[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;   // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;      // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;     // xyz: the camera's forward
    vec4 depth;   // x: near, y: far, metres
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out vec4 fragSize;   // x: half length, y: radius, z: blur
layout(location = 2) flat out vec4 fragLook;

void main()
{
    Torpedo t = torpedoes[gl_InstanceIndex];
    float hl = t.axis.w, r = t.side.w, blur = t.look.y;
    vec2 hi = vec2(hl + 2.0 * blur, r + 2.0 * blur);
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 loc = corners[gl_VertexIndex] * hi;
    vec3 p = t.base.xyz + t.axis.xyz * loc.x + t.side.xyz * loc.y;
    float z = dot(p, fwd.xyz);
    gl_Position = vec4(dot(p, right.xyz) / right.w, dot(p, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragSize = vec4(hl, r, blur, 0.0);
    fragLook = t.look;
}
