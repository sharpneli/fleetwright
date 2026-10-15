#version 450
// Gun smoke (docs/visuals/muzzle-flashes.md F6, SmokeRenderer): one quad per puff and mode, read from a storage buffer,
// 6 vertices from gl_VertexIndex. The puff's centre at its anchor height is in metres from the eye (formed in double
// on the CPU); the quad lies in that plane, spanned by the view's east and north, and is projected as the splashes'
// are. Mode 0: the puff, drawn its lift up the screen (the effects camera); mode 1: its shadow, laid down-light on the
// sea. Its corners carry their place in metres east and north of the puff's (or shadow's) centre.

struct Puff
{
    vec4 centre;   // xyz: the centre at its anchor height, metres from the eye; w: seed
    vec4 shape;    // x: sigma_h, m; y: extinction area, m^2; z: lift up the screen, m; w: half size, m
    vec4 light;    // x: height over its anchor, m; y: age, s; z: tint gone to white; w: the fog's share
    vec4 tint;     // rgb: tint near the muzzle, linear
    vec4 shadow;   // xy: the shadow's offset, m; z: its opacity
};

layout(std430, set = 0, binding = 0) readonly buffer Puffs { Puff puffs[]; };

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
layout(location = 2) flat out int fragMode;

void main()
{
    Puff p = puffs[gl_InstanceIndex];
    int mode = int(depth.w + 0.5);
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 at = mode == 0 ? vec2(0.0, p.shape.z) : p.shadow.xy;
    vec2 loc = p.shape.w * corners[gl_VertexIndex];
    vec3 r = p.centre.xyz + east.xyz * (at.x + loc.x) + north.xyz * (at.y + loc.y);
    float z = dot(r, fwd.xyz);
    bool none = mode == 1 && p.shadow.z <= 0.0;
    gl_Position = none ? vec4(2.0, 2.0, 2.0, 1.0)
        : vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
    fragMode = mode;
}
