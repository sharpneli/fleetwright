#version 450
// Hitbox view: the hitbox mesh (HitboxMesh) in ship-local metres. A vertex whose kind is hidden, or whose prism
// lies outside the clip box (in "whole prisms" clipping), is moved outside the clip volume, so the whole prism drops
// out without rebuilding the index buffers. Lines are pulled a little toward the camera, so outlines win against the
// faces they border.
layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in uint inKind;
layout(location = 3) in uint inPrism;

layout(std430, set = 0, binding = 0) readonly buffer Prisms
{
    vec4 bounds[];   // per prism: min, max
};

layout(set = 1, binding = 0) uniform View
{
    mat4 viewProj;
    vec4 misc;       // x: line depth bias (NDC)
    uvec4 kindMask;  // x: kinds 0-31, y: kinds 32-63
    vec4 clipMin;    // w > 0.5: clipping on
    vec4 clipMax;    // w > 0.5: cut per pixel (the fragment shader), else whole prisms here
};

layout(location = 0) out vec3 fragWorld;
layout(location = 1) out vec3 fragNormal;
layout(location = 2) flat out uint fragKind;
layout(location = 3) flat out uint fragPrism;

const uint NoPrism = 0xFFFFFFFFu;

void main()
{
    uint word = inKind < 32u ? kindMask.x : kindMask.y;
    bool hidden = (word & (1u << (inKind & 31u))) == 0u;
    if (!hidden && clipMin.w > 0.5 && clipMax.w < 0.5 && inPrism != NoPrism)
    {
        vec3 lo = bounds[2u * inPrism].xyz, hi = bounds[2u * inPrism + 1u].xyz;
        hidden = any(greaterThan(lo, clipMax.xyz)) || any(lessThan(hi, clipMin.xyz));
    }
    if (hidden)
    {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        return;
    }
    vec4 p = viewProj * vec4(inPosition, 1.0);
    p.z -= misc.x * p.w;
    gl_Position = p;
    fragWorld = inPosition;
    fragNormal = inNormal;
    fragKind = inKind;
    fragPrism = inPrism;
}
