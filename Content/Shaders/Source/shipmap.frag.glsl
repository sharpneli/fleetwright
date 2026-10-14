#version 450
#extension GL_GOOGLE_include_directive : require
// Ships on the map: a baked sprite (premultiplied alpha, mipmapped, sampled by the hardware), a turret's silhouette
// as its shadow (only where the height map under it is lower than the turret's roof), the stand-in icon tinted, or a
// scorch over a wrecked part (on the ship only: where the height map has something). Damage (docs/battle/fighting.md
// 4.4): a turret's tint multiplies in by its alpha; with the sea's plane on, the hull shows tilted by its slopes (each
// point shifted by its height toward the low end and side: the trim and list, read back through the height map; the
// turrets' and scorches' quads are shifted on the CPU), and what's under the sea is drowned, faint and green-dark by its
// depth, with a pale wash along the new waterline. The hull, the turrets and the scorches are lit (Include/shiplight.glsl:
// the sprite's normal and AO from its relief, one material; a scorch flat); the silhouettes and the icons are not.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;
layout(location = 2) flat in vec4 fragMode;
layout(location = 3) flat in vec4 fragSun;
layout(location = 4) flat in vec4 fragMarch;
layout(location = 5) flat in vec4 fragHmap;
layout(location = 6) flat in vec4 fragTint;
layout(location = 7) flat in vec4 fragSea;
layout(location = 8) flat in vec3 fragLightSun;
layout(location = 9) flat in vec3 fragLightMoon;
layout(location = 10) in vec3 fragEye;

layout(set = 2, binding = 0) uniform sampler2D spriteTex;
layout(set = 2, binding = 1) uniform sampler2D heightTex;
layout(set = 2, binding = 2) uniform sampler2D normalTex;   // the sprite's normal + AO (an icon: unused)

layout(location = 0) out vec4 outColour;

#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"

const vec3 Up = vec3(0.0, 0.0, 1.0);

float heightAt(vec2 ship)
{
    vec2 huv = ship * fragHmap.xy + fragHmap.zw;
    if (any(lessThan(huv, vec2(0.0))) || any(greaterThan(huv, vec2(1.0))))
        return 0.0;
    return textureLod(heightTex, huv, 0.0).r * 63.75;
}

// premultiplied colour under the sea's plane: drowned by its depth there
vec4 drown(vec4 c, vec2 at, float h)
{
    if (fragSea.w < 0.5)
        return c;
    float depth = fragSea.x + fragSea.y * at.x + fragSea.z * at.y - h;
    if (depth > 0.0)
    {
        float k = exp(-depth / 1.5);
        vec3 rgb = mix(vec3(0.04, 0.10, 0.12) * skyAmb.w * c.a, c.rgb, 0.45 * k);
        return vec4(rgb, c.a) * (0.03 + 0.62 * k);
    }
    float wash = 1.0 - smoothstep(0.0, 0.35, -depth);
    return vec4(mix(c.rgb, vec3(0.85, 0.9, 0.9) * skyAmb.w * c.a, 0.45 * wash), c.a);
}

void main()
{
    float opacity = fragMode.y;
    if (fragMode.x > 2.5)
    {
        float h = heightAt(fragShip);
        if (h <= 0.05)
            discard;
        vec2 e = min(fragUv, 1.0 - fragUv);
        float edge = smoothstep(0.0, 0.18, min(e.x, e.y));
        // wreckage, rust and soot: mottled at a metre and a quarter-metre
        vec2 cellA = floor(fragShip), cellB = floor(fragShip * 4.0);
        float n = 0.6 * fract(sin(dot(cellA, vec2(12.9898, 78.233))) * 43758.5453) + 0.4 * fract(sin(dot(cellB, vec2(39.346, 11.135))) * 24634.6345);
        vec3 rgb = shipShade(fragTint.rgb * (0.55 + 0.9 * n), Up, normalize(fragEye), fragLightSun, fragLightMoon, 1.0, 0.0, 1.0);
        vec4 c = vec4(rgb, 1.0) * fragTint.a * edge * (0.7 + 0.3 * n);
        outColour = drown(c, fragShip, h) * opacity;
        return;
    }
    vec2 uv = fragUv, at = fragShip;
    if (fragMode.x < 0.5 && fragMode.z <= 0.0 && fragSea.w > 0.5)
    {
        // the hull tilted (its trim and list): a point h over the waterline shows h times the slope toward the low end and
        // side; the height map tells which point shows here, a few steps from this one
        vec2 slope = fragSea.yz;
        for (int i = 0; i < 4; i++)
            at = fragShip - heightAt(at) * slope;
        uv = at * fragHmap.xy + fragHmap.zw;
        if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0))))
            discard;
    }
    vec4 c = texture(spriteTex, uv);
    if (fragMode.x > 1.5)
        outColour = vec4(fragTint.rgb, 1.0) * fragTint.a * c.a * opacity;
    else if (fragMode.x > 0.5)
    {
        float h = heightAt(fragShip);
        if (h >= fragMode.z)
            discard;
        outColour = vec4(0.0, 0.0, 0.0, c.a * opacity);
    }
    else
    {
        vec3 rgb = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);   // the albedo as drawn, sRGB-coded
        if (fragTint.a > 0.0)
            rgb = mix(rgb, rgb * fragTint.rgb, fragTint.a);
        vec4 nt = texture(normalTex, uv);
        rgb = shipShade(rgb, unpackNormal(nt), normalize(fragEye), fragLightSun, fragLightMoon, look.y, 0.0, nt.a);
        vec4 col = vec4(rgb * c.a, c.a);
        if (fragSea.w > 0.5)
            col = drown(col, at, max(heightAt(at), fragMode.z));   // a turret stands as high as its roof
        outColour = col * opacity;
    }
}
