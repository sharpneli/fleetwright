#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's twin check (docs/battle/smoke.md Z5, SmokeFieldTwinTests): each puff's state at an age and its
// offset by the large eddies (Z5c) by smokefield.glsl, written back for the CPU to hold against SmokeModel.State and
// SmokeEddies.At. Not used in a frame.
#include "Include/smokefield.glsl"

layout(local_size_x = 64) in;

layout(std430, set = 0, binding = 0) readonly buffer Puffs { FieldPuff puffs[]; };
layout(std430, set = 1, binding = 0) writeonly buffer States { vec4 states[]; };

layout(set = 2, binding = 0) uniform Params
{
    vec4 info;   // x: the time, s from the copy; y: sigma_v; z: the Lagrangian time across; w: the count
    vec4 eddyK[FIELD_EDDY_MODES];   // the large eddies at that time (fieldEddy)
    vec4 eddyD[FIELD_EDDY_MODES];
};

void main()
{
    uint i = gl_GlobalInvocationID.x;
    if (i >= uint(info.w + 0.5))
        return;
    float z, h2, z2, area;
    fieldState(puffs[i], max(info.x - puffs[i].at.w, 0.0), info.y, info.z, z, h2, z2, area);
    states[2 * i] = vec4(z, h2, z2, area);
    states[2 * i + 1] = vec4(fieldEddy(puffs[i], eddyK, eddyD), 0.0);
}
