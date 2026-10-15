#version 450
// Muzzle flashes (docs/visuals/muzzle-flashes.md, FlashRenderer): one quad per mount fired, read from a storage buffer,
// 6 vertices from gl_VertexIndex. The barrel tips' middle is in metres from the eye (formed in double on the CPU); the
// quad lies in the sea's plane there, spanned by the view's east and north, and is projected as the splashes' are
// (splash.vert). The parts' offsets come through the effects camera already (height above the tips drawn k up the
// screen), so the quad is flat. Its corners carry their place in metres east and north of the tips.

struct Flash
{
    vec4 base;    // xyz: the barrel tips' middle, metres from the eye; w: seed
    vec4 bore;    // xy: the bore's way (east, north); z: barrels; w: their spacing, m
    vec4 quad;    // xy: the quad's centre (offset); z: its half size, m
    vec4 coreA, coreB, discA, discB, fireA, fireB;   // A: centre xy, sigma along, sigma across; B: colour, brightness
    vec4 halo;    // xy: centre; z: sigma, m; w: compressed brightness
    vec4 haloColour;   // rgb: colour; w: the fireball's life gone
};

layout(std430, set = 0, binding = 0) readonly buffer Flashes { Flash flashes[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;    // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;       // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;      // xyz: the camera's forward
    vec4 depth;    // x: near, y: far, metres
    vec4 east;     // xyz: east at the view's centre
    vec4 north;    // xyz: north there
};

layout(location = 0) out vec2 fragLoc;
layout(location = 1) flat out int fragIndex;

void main()
{
    Flash f = flashes[gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 loc = f.quad.xy + f.quad.z * corners[gl_VertexIndex];
    vec3 r = f.base.xyz + east.xyz * loc.x + north.xyz * loc.y;
    float z = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
}
