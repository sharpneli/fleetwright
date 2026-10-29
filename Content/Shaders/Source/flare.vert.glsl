#version 450
// Star shell in the world view (docs/visuals/night-lights.md Q10d, FlareRenderer): one quad per part, read from a storage
// buffer, 6 vertices from gl_VertexIndex, projected as the ships' quads are. Its ends come lifted by the effects camera
// (on the sea's plane, metres from the eye). The trail (kind 0): a ribbon from the star up to the burst, as wide as it
// spreads, a little past both ends. The star (kind 1): a square on the screen's axes about it, as wide as its glow
// reaches. Its corners carry their place in metres: the trail's along it from the star and across; the star's from it.

struct Flare
{
    vec4 lo, hi, loAt, hiAt, look, colour;
};

layout(std430, set = 0, binding = 0) readonly buffer Flares { Flare flares[]; };

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
    Flare f = flares[gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(-1, -1), vec2(1, -1), vec2(1, 1), vec2(-1, -1), vec2(1, 1), vec2(-1, 1));
    vec2 c = corners[gl_VertexIndex];
    vec3 p;
    if (f.lo.w < 0.5)
    {
        // the trail: along from the star (0) to the burst (len), its widest a few half widths either side
        vec3 d = f.hi.xyz - f.lo.xyz;
        float len = length(d);
        vec3 axis = len > 1e-3 ? d / len : up.xyz;
        vec3 side = normalize(cross(fwd.xyz, axis));
        float w = 3.0 * max(f.loAt.w, f.hi.w) * 1.6;
        float along = c.x < 0.0 ? -3.0 * f.loAt.w : len + w;
        fragLoc = vec2(along, c.y * w);
        p = f.lo.xyz + axis * fragLoc.x + side * fragLoc.y;
    }
    else
    {
        fragLoc = c * f.look.w;
        p = f.lo.xyz + right.xyz * fragLoc.x + up.xyz * fragLoc.y;
    }
    float z = dot(p, fwd.xyz);
    gl_Position = vec4(dot(p, right.xyz) / right.w, dot(p, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragIndex = gl_InstanceIndex;
}
