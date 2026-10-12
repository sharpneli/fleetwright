#version 450
// The far side's mask (GlobeRenderer's coverage pass): the depth of a sphere 2 km inside the Earth, per pixel, so the
// baked land's fans (which sag under 0.4 km) pass in front and the far side's fail. The same ray-cast and depth as
// globe.frag writes for the coast.
layout(location = 0) in vec2 fragNdc;
layout(location = 0) out vec4 outColor;

layout(set = 3, binding = 0) uniform Globe
{
    vec4 right;
    vec4 up;
    vec4 fwd;
    vec4 eyeDir;
    vec4 sun;
    vec4 depth;    // x: |eye|² - r², r the masking sphere's radius, radii²; y: near, z: far, radii
};

void main()
{
    vec3 d = normalize(fwd.xyz + fragNdc.x * right.w * right.xyz + fragNdc.y * up.w * up.xyz);
    float b = fwd.w * dot(eyeDir.xyz, d), c = depth.x, disc = b * b - c;
    float z = 1.0;
    if (b < 0.0 && disc >= 0.0)
    {
        float t = c / (-b + sqrt(disc)), zv = t * dot(d, fwd.xyz);
        z = depth.z * (zv - depth.y) / (zv * (depth.z - depth.y));
    }
    gl_FragDepth = clamp(z, 0.0, 1.0);
    outColor = vec4(0.0);
}
