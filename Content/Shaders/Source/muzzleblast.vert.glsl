#version 450
// The muzzle blast on the water (docs/visuals/muzzle-flashes.md F5, BlastRenderer): one quad per mount fired, into the
// wake target, flat on the sea under the barrel tips (their middle in metres from the eye, formed in double on the
// CPU), spanned by the view's east and north and projected as the splashes' foam is. Its corners carry their place in
// metres east and north of the tips.

struct Blast
{
    vec4 base;    // xyz: the barrel tips' middle, metres from the eye; w: their height over the sea, m
    vec4 bore;    // xy: the bore's way over the sea; z: sin(elevation); w: cos(elevation)
    vec4 blast;   // x: lambda, m; y: age on the sea's clock, s; z: strength; w: seed
    vec4 quad;    // x: half size, m
};

layout(std430, set = 0, binding = 0) readonly buffer Blasts { Blast blasts[]; };

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
    Blast b = blasts[gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 loc = b.quad.x * corners[gl_VertexIndex];
    // on the sea, under the tips (their height down the view centre's vertical: close enough a few hundred metres out)
    vec3 r = b.base.xyz + east.xyz * loc.x + north.xyz * loc.y - normalize(cross(east.xyz, north.xyz)) * b.base.w;
    float z = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragLoc = loc;
    fragIndex = gl_InstanceIndex;
}
