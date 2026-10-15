// The ships' light (docs/visuals/ship-lighting.md), included by the sprite shaders: a sprite's pixel shaded in linear
// light from the sun, the moon and a hemisphere's ambient (sky above, sea below), with a standard BRDF (Lambert; GGX,
// Schlick's Fresnel, Smith's height-correlated visibility; roughness widened by Toksvig from the mipped normal's
// length; an analytic sky for what gloss and metal reflect), then back to the frame's sRGB-coded values through a soft
// shoulder, so a glint rolls off instead of clipping. PBR-inspired: the lights are set by eye (ShipLight.cs,
// LightTable), not in physical units. Directions are in the sprite's frame: x along the quad's u (the bow), y along
// its v (starboard), z up; the including shader makes them per vertex. Define LIGHTS_BINDING (the fragment uniform
// slot) before including.

layout(set = 3, binding = LIGHTS_BINDING) uniform Lights
{
    vec4 sunRad;    // rgb: the sun's light, linear (0 when down)
    vec4 moonRad;   // rgb: the moon's
    vec4 skyAmb;    // rgb: the ambient from above; w: the light's level, sRGB-coded (a flat white deck), for unlit constants
    vec4 seaAmb;    // rgb: the ambient from below; w: the horizon's brightness over the zenith's (the analytic sky)
    vec4 look;      // x: 1 lit, 0 flat (the albedo as drawn), 2-5 debug views (normal, AO, roughness, metallic); y: a
                    // scale on the materials' roughness; z: the shoulder's knee; w: specular
    // the muzzle flashes' lights (docs/visuals/muzzle-flashes.md F4)
    vec4 flashInfo;     // x: how many (0-8)
    vec4 flashEast;     // xyz: east at the view's centre, Earth-fixed (for shaders in a local frame)
    vec4 flashNorth;    // xyz: north there
    vec4 flashUp;       // xyz: up there
    vec4 flashPos[8];   // xyz: metres from the eye, Earth-fixed; w: its size, m (the falloff softened inside it)
    vec4 flashRad[8];   // rgb: its intensity, display units x m^2 (a white surface facing it d away is lit by rgb / d^2)
};

int flashCount()
{
    return int(flashInfo.x + 0.5);
}

// flash light i at p (metres from the eye, Earth-fixed): toward it, l (unit, Earth-fixed), and its light there in the
// lights' units
vec3 flashAt(int i, vec3 p, out vec3 l)
{
    vec3 d = flashPos[i].xyz - p;
    float s = flashPos[i].w, d2 = dot(d, d);
    l = d * inversesqrt(max(d2, 1e-6));
    return flashRad[i].rgb / (d2 + s * s);
}

// a sprite's normal + AO texel (biased xyz, AO in a; ReliefMaps) as its normal, not renormalised: the mips average
// it, and how much shorter than 1 it came out is how varied the surface under the pixel is (Toksvig, in shipShade)
vec3 unpackNormal(vec4 t)
{
    return t.xyz * 2.0 - 1.0;
}

// a material texel (roughness, metallic premultiplied by the colour's coverage a; DrawList.WithMaterials) as the two
vec2 unpackMaterial(vec4 m, float a)
{
    return a > 0.0 ? clamp(m.rg / a, 0.0, 1.0) : vec2(1.0, 0.0);
}

// Karis's analytic fit of the split-sum environment BRDF: the specular's scale and bias on f0
vec2 envBrdf(float roughness, float nv)
{
    vec4 r = roughness * vec4(-1.0, -0.0275, -0.572, 0.022) + vec4(1.0, 0.0425, 1.04, -0.04);
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    return vec2(-1.04, 1.04) * a004 + r.zw;
}

// the analytic sky round a ship, by the reflected ray's z: zenith, the brighter haze at the horizon, the sea below,
// blurred toward the hemisphere's average as roughness spreads the reflection
vec3 skyAt(vec3 r, vec3 n, float roughness)
{
    vec3 horizon = mix(skyAmb.rgb, vec3(dot(skyAmb.rgb, vec3(0.3333))), 0.5) * seaAmb.w;
    vec3 env = r.z >= 0.0 ? mix(horizon, skyAmb.rgb, smoothstep(0.0, 0.7, r.z)) : mix(horizon, seaAmb.rgb, smoothstep(0.0, 0.25, -r.z));
    return mix(env, mix(seaAmb.rgb, skyAmb.rgb, n.z * 0.5 + 0.5), roughness);
}

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

// A pixel's colour, sRGB-coded and not premultiplied, from its albedo as drawn (sRGB-coded): n its normal as
// unpacked (a mip's shortened average: Toksvig widens the roughness by it), v toward the eye, lSun and lMoon toward
// the lights (unit, in the sprite's frame); its material's roughness and metallic; ambient occlusion. Flat (look.x
// 0): the albedo as drawn; the debug views show one input as grey (the normal biased, as colour; the roughness as
// shaded, Toksvig's and the scale's included). spec scales the specular and the sky's reflection (0: under the sea).
// extra: more light on the diffuse alone, already by its cosine (the flashes' lights).
vec3 shipShade(vec3 albedoSrgb, vec3 n, vec3 v, vec3 lSun, vec3 lMoon, float roughness, float metallic, float ao, float spec, vec3 extra)
{
    if (look.x < 0.5)
        return albedoSrgb;
    // Toksvig, by the von Mises-Fisher fit (Han et al.): the normals' spread under the pixel, kappa from the average's
    // length, adds 2 / kappa to alpha squared. The 8-bit encoding's own error (under half a percent) is let through.
    float len = min(length(n) + 0.004, 1.0);
    n = len > 1e-3 ? n / length(n) : vec3(0.0, 0.0, 1.0);
    float a2 = pow(clamp(roughness * look.y, 0.0, 1.0), 4.0);
    if (len < 1.0)
        a2 += 2.0 * (1.0 - len * len) / (3.0 * len - len * len * len);
    a2 = clamp(a2, 4e-6, 1.0);
    float rough = sqrt(sqrt(a2));
    if (look.x > 1.5)
    {
        int view = int(look.x + 0.5);
        return view == 2 ? n * 0.5 + 0.5 : vec3(view == 3 ? ao : view == 4 ? rough : metallic);
    }
    vec3 albedo = srgbToLinear(albedoSrgb);
    vec3 diffuse = albedo * (1.0 - metallic);
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    float a = sqrt(a2);
    vec3 c = diffuse * mix(seaAmb.rgb, skyAmb.rgb, n.z * 0.5 + 0.5) * ao;
    // the sky's reflection: what keeps metal from going black with no cube map
    float nv = max(dot(n, v), 1e-4);
    vec2 ab = envBrdf(rough, nv);
    c += spec * look.w * skyAt(reflect(-v, n), n, rough) * (f0 * ab.x + ab.y) * ao;
    float nl = dot(n, lSun);
    if (nl > 0.0)
        c += (diffuse + spec * look.w * specularPi(n, v, lSun, nl, a, f0)) * sunRad.rgb * nl;
    nl = dot(n, lMoon);
    if (nl > 0.0)
        c += (diffuse + spec * look.w * specularPi(n, v, lMoon, nl, a, f0)) * moonRad.rgb * nl;
    c += diffuse * extra;
    return linearToSrgb(shoulder(c, look.z));
}

vec3 shipShade(vec3 albedoSrgb, vec3 n, vec3 v, vec3 lSun, vec3 lMoon, float roughness, float metallic, float ao, float spec)
{
    return shipShade(albedoSrgb, n, v, lSun, lMoon, roughness, metallic, ao, spec, vec3(0.0));
}

vec3 shipShade(vec3 albedoSrgb, vec3 n, vec3 v, vec3 lSun, vec3 lMoon, float roughness, float metallic, float ao)
{
    return shipShade(albedoSrgb, n, v, lSun, lMoon, roughness, metallic, ao, 1.0);
}
