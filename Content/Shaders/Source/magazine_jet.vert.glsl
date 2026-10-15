#version 450
// A magazine's flame jets (docs/visuals/magazine-explosions.md X3, MagazineRenderer): one quad per opening jetting
// (and one for the hit's flash), read from a storage buffer, 6 vertices from gl_VertexIndex. The event's origin (the sea
// under the magazine) is in metres from the eye; the quad lies in the sea's plane there, spanned by the view's east and
// north, over the jet's cone as the effects camera draws it (its ends placed on the CPU, heights up the screen).

struct Jet
{
    vec4 base;    // xyz: the event's origin, metres from the eye; w: seed
    vec4 seg;     // xy: the jet's root, zw: its tip, drawn (east, north of the origin), m
    vec4 shape;   // x: its radius at the root, y: at the tip, m; z: its heat at the root, w: at the tip (0-1)
    vec4 look;    // x: its strength (the growing in and the fading out); y: the flicker's clock, s; z: its length, m
    vec4 quad;    // xy: the quad's low corner, zw: its high
};

layout(std430, set = 0, binding = 0) readonly buffer Jets { Jet jets[]; };

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
    Jet j = jets[gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));
    vec2 loc = mix(j.quad.xy, j.quad.zw, corners[gl_VertexIndex]);
    vec3 r = j.base.xyz + east.xyz * loc.x + north.xyz * loc.y;
    float z = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
}
