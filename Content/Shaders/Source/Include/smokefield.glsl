// The smoke field's puffs as the sim keeps them (docs/battle/smoke.md Z5; Fleetwright.Sim.SmokeRecord, SmokeCopy), and
// their state at an age: the GLSL twin of SmokeModel.State (Fleetwright.Sim.Combat), held to it by a readback on the GPU
// (SmokeFieldTwinTests, through smokestate.comp). Closed form in the age: nothing is stepped.

struct FieldPuff
{
    vec4 at;       // xyz: its place on the sea in the air's frame, m from the copy's anchor; w: its birth, s from the copy
    vec4 rise;     // x: height at birth, m; y: the rise, m; z: its time, s; w: the extinction area at birth, m^2
    vec4 spread;   // x: sigma_h^2 at birth, m^2; y: sigma_z^2 at birth; z: sigma_w, m/s; w: its Lagrangian time, s
    vec4 misc;     // x: its decay time, s (0 none); y: its tint (SmokeTint); z: drawn (the drawing rule), 1 or 0;
                   // w: its drift along the wind against the air's frame, m/s
};

// a rising plume's spread for its rise (Pasquill's dh / 3.5)
const float BuoyantSpread = 1.0 / 3.5;

// the share of its rise a puff has made at u = age / rise time: (u^2 / (1 + u^2))^(1/3)
float fieldRiseShare(float u)
{
    return u > 0.0 ? pow(u * u / (1.0 + u * u), 1.0 / 3.0) : 0.0;
}

// Taylor's spread of sigma and Lagrangian time T_L after t s, m^2: 2 sigma^2 T_L^2 (t/T_L - 1 + e^(-t/T_L)); its series
// while t/T_L is small (in float the difference cancels)
float fieldTaylor(float t, float sigma, float lagrangian)
{
    if (!(t > 0.0) || !(sigma > 0.0) || !(lagrangian > 0.0))
        return 0.0;
    float u = t / lagrangian;
    return u < 0.05 ? sigma * sigma * t * t * (1.0 - u / 3.0 + u * u / 12.0)
                    : 2.0 * sigma * sigma * lagrangian * lagrangian * (u - 1.0 + exp(-u));
}

// its height over the sea, its variances across and up, m^2, and its extinction area, m^2, at an age, s, in air whose
// turbulence across is sigma_v with Lagrangian time T_L
void fieldState(FieldPuff p, float age, float sigmaV, float lagrangianH, out float z, out float h2, out float z2, out float area)
{
    float risen = p.rise.y * fieldRiseShare(age / max(p.rise.z, 0.1));
    z = p.rise.x + risen;
    float b = risen * BuoyantSpread;
    h2 = p.spread.x + fieldTaylor(age, sigmaV, lagrangianH) + b * b;
    z2 = p.spread.y + fieldTaylor(age, p.spread.z, p.spread.w) + b * b;
    area = p.misc.x > 0.0 ? p.rise.w * exp(-age / p.misc.x) : p.rise.w;
}

// the tints, linear (SmokeTint: coal's grey-brown, oil's light grey, powder's white, a fire's soot, a chemical screen's
// white) [EST: the map's placeholders]
vec3 fieldTint(float tint)
{
    int t = clamp(int(tint + 0.5), 0, 4);
    return t == 0 ? vec3(0.27, 0.25, 0.23) : t == 1 ? vec3(0.58, 0.58, 0.58) : t == 2 ? vec3(0.90, 0.89, 0.86)
         : t == 3 ? vec3(0.13, 0.115, 0.10) : vec3(0.92, 0.92, 0.92);
}
