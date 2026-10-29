#version 450
// The searchlights' beams in the air (docs/visuals/night-lights.md Q10c, BeamRenderer): one quad per beam, read from a
// storage buffer, 6 vertices from gl_VertexIndex. The quad lies in the sea's plane under the beam, from its lamp out
// along its axis to where its glow fades, as wide as the beam's spread (3.5 sigma either side of the axis, a little more
// near the lamp), and is projected as the ships' quads are (shipmap.vert). Its corners carry their place along and across
// the beam, metres.

struct Beam
{
    vec4 base;    // xyz: the lamp's place on the sea under it, metres from the eye; w: the lamp's height, m
    vec4 axis;    // xyz: along the beam, unit, Earth-fixed; w: the quad's length, m
    vec4 side;    // xyz: across it, unit; w: its 1-sigma spread, radians
    vec4 light;   // x: its glow's scale, display units x m; y: the air's extinction, 1/m; z: the lamp's softening, m; w: the
                  // gain on it as shown (after the eye's response)
    vec4 shadow;  // x: where her target stands along it, m (< 0: none); y: across; z: the half width she covers; w: her top, m
    vec4 colour;  // rgb: the arc's; w: half her length along the beam, m (the ship it's held on)
    vec4 hull;    // x: the glow's share left over her
};

layout(std430, set = 0, binding = 0) readonly buffer Beams { Beam beams[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;   // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;      // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;     // xyz: the camera's forward
    vec4 depth;   // x: near, y: far, metres
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out int fragIndex;

void main()
{
    Beam b = beams[gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(0, -1), vec2(1, -1), vec2(1, 1), vec2(0, -1), vec2(1, 1), vec2(0, 1));
    vec2 c = corners[gl_VertexIndex];
    float along = c.x * b.axis.w;
    float wide = 3.5 * b.side.w * max(along, b.light.z) + 4.0;
    vec2 loc = vec2(along - (1.0 - c.x) * 8.0, c.y * wide);   // a little behind the lamp, for its glare
    vec3 p = b.base.xyz + b.axis.xyz * loc.x + b.side.xyz * loc.y;
    float z = dot(p, fwd.xyz);
    gl_Position = vec4(dot(p, right.xyz) / right.w, dot(p, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
}
