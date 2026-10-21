#version 450
#extension GL_GOOGLE_include_directive : require
// The smoke field's accumulation (docs/battle/smoke.md Z5): a puff's column optical depth here,
// tau = A / (2 pi sigma^2) e^(-r^2 / 2 sigma^2) (the sim's vertical tau), added into its level: rgb its tint times tau, a
// tau. Summing tau is Beer-Lambert's exactly and needs no sorting.
#include "Include/smokefield.glsl"

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in vec4 fragPuff;

layout(location = 0) out vec4 outTau;

void main()
{
    float tau = fragPuff.x * exp(-dot(fragLoc, fragLoc) * fragPuff.y);
    outTau = vec4(fieldTint(fragPuff.z) * tau, tau);
}
