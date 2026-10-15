#version 450
// Magazine explosions (docs/visuals/magazine-explosions.md X2, MagazineRenderer): one quad per event and mode, read from
// a storage buffer, 6 vertices from gl_VertexIndex. The event's origin (the sea under the magazine) is in metres from
// the eye (formed in double on the CPU); the quad lies in the sea's plane there, spanned by the view's east and north,
// and is projected as the splashes' are (splash.vert). Its bounds come from the CPU: mode 0 the body through the effects
// camera (heights drawn up the screen), mode 1 its shadow down-sun. Its corners carry their
// place in metres east and north of the origin.

struct Mag
{
    vec4 base;    // xyz: the origin, metres from the eye; w: seed
    vec4 quad;    // the body's quad: xy its low corner, zw its high
    vec4 shade;   // the shadow's quad
    vec4 body;    // xyz: the body's centre; w: its radius across
    vec4 bodyB;   // x: its radius up; y: its extinction per metre; z: its heat; w: the skin
    vec4 stem;    // xy: the cap's drift; z: the stem's radius; w: its extinction per metre
    vec4 time;    // x: the stem's fires; y: the cap's share; z: the ring's turn, radians; w: opacity (the world's share, the fade)
    vec4 misc;    // x: the deck's height; y: the fire's light on the smoke, display units
};

layout(std430, set = 0, binding = 0) readonly buffer Mags { Mag mags[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;    // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;       // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;      // xyz: the camera's forward
    vec4 depth;    // x: near, y: far, metres; w: the mode
    vec4 east;     // xyz: east at the view's centre
    vec4 north;    // xyz: north there
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out int fragIndex;

void main()
{
    Mag m = mags[gl_InstanceIndex];
    vec4 q = depth.w < 0.5 ? m.quad : m.shade;
    vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));
    vec2 loc = mix(q.xy, q.zw, corners[gl_VertexIndex]);
    vec3 r = m.base.xyz + east.xyz * loc.x + north.xyz * loc.y;
    float z = dot(r, fwd.xyz);
    gl_Position = q.z <= q.x ? vec4(2.0, 2.0, 2.0, 1.0)
        : vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
}
