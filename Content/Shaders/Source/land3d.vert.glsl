#version 450
// The baked land's fill (LandMesh): fans of triangles in Earth-centred metres, projected by the map's camera and
// counted by winding into the coverage target (landfill.frag). Floats suffice at the coarse levels it draws: a
// position is good to ~0.5 m against pixels of hundreds of metres.
layout(location = 0) in vec3 inPos;

layout(set = 1, binding = 0) uniform View
{
    vec4 eye;       // xyz: the eye, Earth-centred metres
    vec4 right;     // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;        // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;       // xyz: the camera's forward
    vec4 depth;     // x: near, y: far, metres
    vec4 viewport;  // x, y: the target's size in pixels; z: half a coast line's width, pixels
    vec4 color;     // the coast's colour
};

void main()
{
    vec3 r = inPos - eye.xyz;
    float z = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
}
