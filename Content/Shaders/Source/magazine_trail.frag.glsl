#version 450
#extension GL_GOOGLE_include_directive : require
// A magazine's debris (docs/visuals/magazine-explosions.md X4): a burning piece's smoke trail as soft soot ribbons (the
// "octopus" tendrils of big explosions), one segment between two moments of its arc, spreading and fading as it ages
// and drifting downwind; and a cold piece as a dark fleck (a zero-length segment). Read through the jets' vertex
// shader (the same five vec4s: the segment's ends drawn through the effects camera, its radii and opacities at its
// ends). Lit flat by the sky, the sea, the sun and the moon; premultiplied over what's behind.
#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/splash.glsl"

struct Seg
{
    vec4 base, seg, shape, look, quad;   // shape: x, y the radii at the ends, m; z, w the opacities there; look.x the overall opacity
};

layout(std430, set = 2, binding = 0) readonly buffer Segs { Seg segs[]; };

layout(set = 3, binding = 1) uniform Params
{
    vec4 sunL;
    vec4 moonL;
    vec4 shadowL;
    vec4 misc;
    vec4 fireK;   // z: the soot's albedo
    vec4 more;
};

layout(location = 0) in vec2 fragLoc;
layout(location = 1) flat in int fragIndex;

layout(location = 0) out vec4 outColor;

void main()
{
    Seg g = segs[fragIndex];
    vec2 a = g.seg.xy, b = g.seg.zw, ab = b - a;
    float len2 = dot(ab, ab);
    float s = len2 > 1e-6 ? clamp(dot(fragLoc - a, ab) / len2, 0.0, 1.0) : 0.0;
    float r = mix(g.shape.x, g.shape.y, s), d = length(fragLoc - (a + ab * s));
    float n = splashNoise(vec3(fragLoc / max(r, 0.5) * 0.8, g.base.w * 40.0));
    float alpha = mix(g.shape.z, g.shape.w, s) * (1.0 - smoothstep(0.45, 1.0, d / (r * (0.8 + 0.4 * n)))) * g.look.x;
    if (alpha < 0.003)
        discard;
    vec3 light = mix(seaAmb.rgb, skyAmb.rgb, 0.7) + 0.8 * (sunRad.rgb * max(sunL.z, 0.0) + moonRad.rgb * max(moonL.z, 0.0));
    vec3 c = linearToSrgb(shoulder(vec3(max(fireK.z, 0.12)) * light, look.z));
    outColor = vec4(hazeCoded(c) * alpha, alpha);
}
