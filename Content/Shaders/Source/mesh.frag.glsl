#version 450

// Inputs from vertex shader
layout(location = 0) in vec3 fragNormal;
layout(location = 1) in vec3 fragColor;
layout(location = 2) in vec2 fragUv;

// Samplers
layout(set = 2, binding = 0) uniform sampler2D colorTex;

// Output
layout(location = 0) out vec4 outColor;

// Spherical Harmonics coefficients for diffuse IBL
// Grace Cathedral environment (2nd order SH)
struct SHCoefficients {
    vec3 l00, l1m1, l10, l11, l2m2, l2m1, l20, l21, l22;
};

const SHCoefficients grace = SHCoefficients(
    vec3( 0.3623915,  0.2624130,  0.2326261),
    vec3( 0.1759131,  0.1436266,  0.1260569),
    vec3(-0.0247311, -0.0101254, -0.0010745),
    vec3( 0.0346500,  0.0223184,  0.0101350),
    vec3( 0.0198140,  0.0144073,  0.0043987),
    vec3(-0.0469596, -0.0254485, -0.0117786),
    vec3(-0.0898667, -0.0760911, -0.0740964),
    vec3( 0.0050194,  0.0038841,  0.0001374),
    vec3(-0.0818750, -0.0321501,  0.0033399)
);

vec3 calcIrradiance(vec3 nor) {
    const float c1 = 0.429043;
    const float c2 = 0.511664;
    const float c3 = 0.743125;
    const float c4 = 0.886227;
    const float c5 = 0.247708;

    return (
        c1 * grace.l22 * (nor.x * nor.x - nor.y * nor.y) +
        c3 * grace.l20 * nor.z * nor.z +
        c4 * grace.l00 -
        c5 * grace.l20 +
        2.0 * c1 * grace.l2m2 * nor.x * nor.y +
        2.0 * c1 * grace.l21  * nor.x * nor.z +
        2.0 * c1 * grace.l2m1 * nor.y * nor.z +
        2.0 * c2 * grace.l11  * nor.x +
        2.0 * c2 * grace.l1m1 * nor.y +
        2.0 * c2 * grace.l10  * nor.z
    );
}

void main() {
    float lightValue = max(dot(fragNormal, vec3(0.3, 1.0, 0.3)), 0.1);

    vec3 irradiance = calcIrradiance(fragNormal);

    vec3 color = fragColor * texture(colorTex, fragUv).xyz;

    outColor = vec4(color * lightValue + color * irradiance.x * vec3(0.2, 0.2, 0.2), 1.0);
}
