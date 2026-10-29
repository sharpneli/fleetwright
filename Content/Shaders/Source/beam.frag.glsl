#version 450
#extension GL_GOOGLE_include_directive : require
// A searchlight's beam seen from above (docs/visuals/night-lights.md Q10c): the light the air scatters out of it toward
// the eye. Looking straight down, the eye's ray crosses the beam across its height, so the glow is closed form for a
// Gaussian beam (its 1-sigma spread s, its axis at the lamp's height h, level) cut off by the sea under it:
//
//     L(r, x) = I sigma_s p sqrt(2 pi) s / r * e^(-x^2 / 2 s^2 r^2) * Phi(h / s r) * e^(-sigma r)
//
// r along it from the lamp (softened near the lamp to the mirror's glare), x across it, the air's scattering sigma_s,
// its phase p (all round, as the sim's beam seen from beside), the extinction sigma on its way out. Past the ship it's
// held on, her shadow in it: the air behind her unlit, filling in as the beam spreads over her top. The scale, physical
// constants and the night eye (display units a cd/m^2) are folded on the CPU into light.x. Rolled to white on its
// luminance as the flashes are (never per channel), through the haze, screen-blended.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"

struct Beam
{
    vec4 base, axis, side, light, shadow, colour, hull;
};

layout(std430, set = 2, binding = 0) readonly buffer Beams { Beam beams[]; };

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;
layout(location = 0) out vec4 outColor;

// the standard normal's cumulative share below x (a logistic fit, within 1%)
float phi(float x)
{
    return 1.0 / (1.0 + exp(-1.702 * x));
}

void main()
{
    Beam b = beams[fragIndex];
    float h = b.base.w, s = b.side.w;
    float r = sqrt(fragLoc.x * fragLoc.x + b.light.z * b.light.z);
    float x = fragLoc.y, w = s * r;
    float g = exp(-0.5 * x * x / (w * w)) * phi(h / w) * exp(-b.light.y * r) / r;
    // behind the lamp only its glare: the mirror's spill, a few metres
    if (fragLoc.x < 0.0)
        g *= exp(-fragLoc.x * fragLoc.x / 8.0);
    // over her: the glow thinned (a look's departure: her lit hull is what the beam is for), soft at her edges
    if (b.shadow.x > 0.0)
    {
        float over = (1.0 - smoothstep(0.7, 1.2, abs(fragLoc.x - b.shadow.x) / max(b.colour.w, 1.0)))
                   * (1.0 - smoothstep(0.7, 1.2, abs(x - b.shadow.y) / max(b.shadow.z, 1.0)));
        g *= mix(1.0, b.hull.x, over);
    }
    // her shadow: the air behind her unlit up to her top, filling in as the beam's spread grows past it
    if (b.shadow.x > 0.0 && fragLoc.x > b.shadow.x)
    {
        float k = fragLoc.x / b.shadow.x;   // her silhouette's shadow widens with the distance, as the beam does
        float across = 1.0 - smoothstep(0.8, 1.2, abs(x - b.shadow.y * k) / (b.shadow.z * k));
        float below = clamp(b.shadow.w * k / max(h + 2.0 * w, 1.0), 0.0, 1.0);   // the share of the beam's height she hides
        g *= 1.0 - across * below;
    }
    vec3 col = lightResponse(b.colour.rgb * b.light.x * g) * b.light.w;   // through the eye's response, then the look's gain
    float y = dot(col, vec3(0.2126, 0.7152, 0.0722));
    if (y < 1e-4)
        discard;
    float yt = shoulder(vec3(y), look.z).x;
    vec3 rgb = col * (yt / y);
    float over = clamp(log2(max(y, 1e-6)) / 3.0, 0.0, 1.0);
    rgb = mix(rgb / max(max(rgb.r, max(rgb.g, rgb.b)), 1.0), vec3(yt), over);
    // fade the quad's far end and sides out, so no edge shows
    rgb *= 1.0 - smoothstep(0.85, 1.0, fragLoc.x / max(b.axis.w, 1.0));
    outColor = vec4(linearToSrgb(rgb * hazeThrough()), 0.0);
}
