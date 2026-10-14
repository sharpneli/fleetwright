// The ships' light (docs/visuals/ship-lighting.md), included by the sprite shaders: a sprite's pixel shaded in linear
// light from the sun, the moon and a hemisphere's ambient (sky above, sea below), with a standard BRDF (Lambert; GGX,
// Schlick's Fresnel, Smith's height-correlated visibility), then back to the frame's sRGB-coded values through a soft
// shoulder, so a glint rolls off instead of clipping. PBR-inspired: the lights are set by eye (ShipLight.cs,
// LightTable), not in physical units. Directions are in the sprite's frame: x along the quad's u (the bow), y along
// its v (starboard), z up; the including shader makes them per vertex. Define LIGHTS_BINDING (the fragment uniform
// slot) before including.

layout(set = 3, binding = LIGHTS_BINDING) uniform Lights
{
    vec4 sunRad;    // rgb: the sun's light, linear (0 when down)
    vec4 moonRad;   // rgb: the moon's
    vec4 skyAmb;    // rgb: the ambient from above; w: the light's level, sRGB-coded (a flat white deck), for unlit constants
    vec4 seaAmb;    // rgb: the ambient from below
    vec4 look;      // x: 1 lit, 0 flat (the albedo as drawn); y: the default roughness; z: the shoulder's knee; w: specular
};

vec3 srgbToLinear(vec3 c)
{
    c = clamp(c, 0.0, 1.0);
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

vec3 linearToSrgb(vec3 c)
{
    c = clamp(c, 0.0, 1.0);
    return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

// identity up to the knee k, then an exponential approach to 1 (the slope continuous at k)
vec3 shoulder(vec3 x, float k)
{
    return min(x, vec3(k)) + (1.0 - k) * (1.0 - exp(-max(x - k, 0.0) / (1.0 - k)));
}

// the specular BRDF times pi (the lights are in units where a matte white surface facing one returns it)
vec3 specularPi(vec3 n, vec3 v, vec3 l, float nl, float a, vec3 f0)
{
    vec3 h = normalize(v + l);
    float nv = max(dot(n, v), 1e-4), nh = max(dot(n, h), 0.0), vh = max(dot(v, h), 0.0);
    float a2 = a * a, d = nh * nh * (a2 - 1.0) + 1.0;
    float vis = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2) + 1e-6);
    vec3 f = f0 + (1.0 - f0) * pow(1.0 - vh, 5.0);
    return (a2 / (d * d)) * vis * f;   // D = a2 / (pi d^2), times pi
}

// A pixel's colour, sRGB-coded and not premultiplied, from its albedo as drawn (sRGB-coded): n its normal, v toward
// the eye, lSun and lMoon toward the lights (all unit, in the sprite's frame); roughness, metallic, ambient occlusion.
// Flat (look.x 0): the albedo as drawn.
vec3 shipShade(vec3 albedoSrgb, vec3 n, vec3 v, vec3 lSun, vec3 lMoon, float roughness, float metallic, float ao)
{
    if (look.x < 0.5)
        return albedoSrgb;
    vec3 albedo = srgbToLinear(albedoSrgb);
    vec3 diffuse = albedo * (1.0 - metallic);
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    float a = max(roughness * roughness, 0.002);
    vec3 c = diffuse * mix(seaAmb.rgb, skyAmb.rgb, n.z * 0.5 + 0.5) * ao;
    float nl = dot(n, lSun);
    if (nl > 0.0)
        c += (diffuse + look.w * specularPi(n, v, lSun, nl, a, f0)) * sunRad.rgb * nl;
    nl = dot(n, lMoon);
    if (nl > 0.0)
        c += (diffuse + look.w * specularPi(n, v, lMoon, nl, a, f0)) * moonRad.rgb * nl;
    return linearToSrgb(shoulder(c, look.z));
}
