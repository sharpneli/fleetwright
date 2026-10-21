#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's accumulation (docs/battle/smoke.md Z5): a puff's column optical depth here,
// tau = A / (2 pi sigma_l sigma_s) e^(-l^2 / 2 sigma_l^2 - s^2 / 2 sigma_s^2) along and across its stretch (the sim's
// vertical tau), added into its level: rgb its tint times tau, a
// tau. Summing tau is Beer-Lambert's exactly and needs no sorting.
#include "Include/smokefield.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragPuff;

layout(location = 0) out vec4 outTau;

void main()
{
    float tau = fragPuff.x * exp(-fragLoc.x * fragLoc.x * fragPuff.y - fragLoc.y * fragLoc.y * fragPuff.w);
    outTau = vec4(fieldTint(fragPuff.z) * tau, tau);
}
