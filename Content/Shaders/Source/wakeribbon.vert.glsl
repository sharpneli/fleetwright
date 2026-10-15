#version 450
// The foam lanes (docs/visuals/wakes.md K4, FoamTrails): the trails' ribbons, corners in metres from the eye (formed in
// double on the CPU), projected as the ships' quads are (shipmap.vert), into the wake target.
layout(location = 0) in vec3 inRel;
layout(location = 1) in float inIntensity;
layout(location = 2) in vec2 inLane;
layout(location = 3) in float inWidth;
layout(location = 4) in float inAge;

layout(set = 1, binding = 0) uniform View
{
    vec4 right;   // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;      // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;     // xyz: the camera's forward
    vec4 depth;   // x: near, y: far, metres; w: the sea's clock, s (SeaView: game time, capped, still when paused)
    vec4 sun;
    vec4 moon;
};

layout(location = 0) out float fragIntensity;
layout(location = 1) out vec2 fragLane;
layout(location = 2) out float fragWidth;
layout(location = 3) out float fragAge;
layout(location = 4) flat out float fragClock;

void main()
{
    float z = dot(inRel, fwd.xyz);
    gl_Position = vec4(dot(inRel, right.xyz) / right.w, dot(inRel, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragIntensity = inIntensity;
    fragLane = inLane;
    fragWidth = inWidth;
    fragAge = inAge;
    fragClock = depth.w;
}
