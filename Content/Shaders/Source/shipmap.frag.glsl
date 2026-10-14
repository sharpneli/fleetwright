#version 450
#extension GL_GOOGLE_include_directive : require
// Ships on the map: a baked sprite (premultiplied alpha, mipmapped, sampled by the hardware), a turret's shadow (its
// silhouette projected by the receiver's height: Include/shipshadow.glsl), the stand-in icon tinted, or a
// scorch over a wrecked part (on the ship only: where the height map has something). Damage (docs/battle/fighting.md
// 4.4, docs/visuals/ship-lighting.md L5): a turret's tint multiplies into its albedo by its alpha, and its soot makes it
// rough, bare of metal and darker in its hollows; with the sea's plane on, the hull shows tilted by its slopes (each
// point shifted by its height toward the low end and side: the trim and list, read back through the height map; the
// turrets' and scorches' quads are shifted on the CPU) and lit in its tilted frame (the vertex shader turns the
// lights), and what's under the sea is drowned, faint and green-dark by its depth and with no glint, with a pale wash
// along the new waterline. The hull, the turrets and the scorches are lit (Include/shiplight.glsl: the sprite's normal
// and AO from its relief, its material from the look's; a scorch on the hull's relief, matte and sooty); the
// silhouettes and the icons are not. A turret takes the hull's shadow at its roof's height (drawn after the hull's
// shadow pass, which it would otherwise get at the height under it). A casemate is under the deck over it: hidden where
// the hull stands over its cover. Shadows are by the heights over the sea's plane (Include/shipshadow.glsl).
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
layout(location = 11) flat in float fragSoot;

layout(set = 2, binding = 0) uniform sampler2D spriteTex;
layout(set = 2, binding = 1) uniform sampler2D heightTex;
layout(set = 2, binding = 2) uniform sampler2D normalTex;   // the sprite's normal + AO (an icon: unused)
layout(set = 2, binding = 3) uniform sampler2D materialTex; // its roughness and metallic, by the colour's coverage

layout(location = 0) out vec4 outColour;

#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/shipshadow.glsl"

float heightAt(vec2 ship)
{
    return shadowHeight(heightTex, fragHmap, ship, 0.0);
}

// how far under the sea's plane a point h over the waterline is (< 0: over it)
float depthAt(vec2 at, float h)
{
    return fragSea.w > 0.5 ? seaAt(fragSea, at) - h : -1.0;
}

// premultiplied colour under the sea's plane: drowned by its depth there
vec4 drown(vec4 c, float depth)
{
    if (fragSea.w < 0.5)
        return c;
    if (depth > 0.0)
    {
        float k = exp(-depth / 1.5);
        vec3 rgb = mix(vec3(0.04, 0.10, 0.12) * skyAmb.w * c.a, c.rgb, 0.45 * k);
        return vec4(rgb, c.a) * (0.03 + 0.62 * k);
    }
    float wash = 1.0 - smoothstep(0.0, 0.35, -depth);
    return vec4(mix(c.rgb, vec3(0.85, 0.9, 0.9) * skyAmb.w * c.a, 0.45 * wash), c.a);
}

// what's under the sea shows no glint: the specular gone over the first few centimetres down
float specAt(float depth)
{
    return 1.0 - smoothstep(0.0, 0.05, depth);
}

void main()
{
    float opacity = fragMode.y;
    vec2 px = fwidth(fragShip);   // ship metres a pixel: shownAt's step
    float pxM = max(px.x, px.y);
    if (fragMode.x > 2.5)
    {
        // a scorch: on the hull's point that shows here (tilted, as the hull), its relief, matte and sooty
        vec2 at = shownAt(heightTex, fragHmap, fragShip, fragSea, pxM);
        float h = heightAt(at);
        if (h <= 0.05)
            discard;
        vec2 e = min(fragUv, 1.0 - fragUv);
        float edge = smoothstep(0.0, 0.18, min(e.x, e.y));
        // wreckage, rust and soot: mottled at a metre and a quarter-metre
        vec2 cellA = floor(at), cellB = floor(at * 4.0);
        float n = 0.6 * fract(sin(dot(cellA, vec2(12.9898, 78.233))) * 43758.5453) + 0.4 * fract(sin(dot(cellB, vec2(39.346, 11.135))) * 24634.6345);
        vec4 nt = texture(normalTex, at * fragHmap.xy + fragHmap.zw);
        float depth = depthAt(at, h);
        vec3 rgb = shipShade(fragTint.rgb * (0.55 + 0.9 * n), unpackNormal(nt), normalize(fragEye), fragLightSun, fragLightMoon, 1.0, 0.0,
            nt.a * (0.45 + 0.35 * n), specAt(depth));
        vec4 c = vec4(rgb, 1.0) * fragTint.a * edge * (0.7 + 0.3 * n);
        outColour = drown(c, depth) * opacity;
        return;
    }
    if (fragMode.x > 0.5 && fragMode.x < 1.5)
    {
        // a turret's shadow: the quad is a box down-light of it, the tint its frame, the roof its height; the mip by
        // the turret's own uv (the projected one jumps at every deck edge). The receiver is the hull's point that shows
        // here, the turret where it stands untilted, both by their heights over the sea's plane.
        vec2 own = turretUv(fragShip, fragTint);
        vec2 gx = dFdx(own), gy = dFdy(own);
        vec2 at = shownAt(heightTex, fragHmap, fragShip, fragSea, pxM);
        vec4 frame = vec4(fragTint.xy - fragMode.z * fragSea.yz, fragTint.zw);
        float h = shadowHeight(heightTex, fragHmap, at, fragMarch.w);
        vec2 mid = frame.xy + 0.5 * (frame.zw + vec2(-frame.w, frame.z));
        float over = overSea(fragSea, at, h), roof = fragMode.z - seaAt(fragSea, mid);
        if (over >= roof)
            discard;
        vec2 tuv = turretShadowUv(at, over, roof, fragSun.xy, fragSun.z, frame);
        if (any(lessThan(tuv, vec2(0.0))) || any(greaterThan(tuv, vec2(1.0))))
            discard;
        float a = textureGrad(spriteTex, tuv, gx, gy).a;
        if (a <= 0.0)
            discard;
        float hs = hullShadow(heightTex, fragHmap, at, h, fragSun.xy, fragSun.z, fragMarch, fragSea);
        outColour = vec4(0.0, 0.0, 0.0, shadowOver(a, hs, fragSun.w) * opacity);
        return;
    }
    vec2 uv = fragUv, at = fragShip;
    if (fragMode.x < 0.5 && fragMode.z <= 0.0 && fragSea.w > 0.5)
    {
        // the hull tilted (its trim and list): the point that shows here
        at = shownAt(heightTex, fragHmap, fragShip, fragSea, pxM);
        uv = at * fragHmap.xy + fragHmap.zw;
        if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0))))
            discard;
    }
    if (fragMode.x < 0.5 && fragMode.w > 0.0 && heightAt(shownAt(heightTex, fragHmap, fragShip, fragSea, pxM)) >= fragMode.w)
        discard;   // a casemate, under the deck over it: hidden where the hull that shows here stands over its cover
    vec4 c = texture(spriteTex, uv);
    if (fragMode.x > 1.5)
        outColour = vec4(fragTint.rgb, 1.0) * fragTint.a * c.a * opacity;
    else
    {
        vec3 rgb = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);   // the albedo as drawn, sRGB-coded
        if (fragTint.a > 0.0)
            rgb = mix(rgb, rgb * fragTint.rgb, fragTint.a);
        vec4 nt = texture(normalTex, uv);
        vec2 mat = unpackMaterial(texture(materialTex, uv), c.a);
        mat = mix(mat, vec2(1.0, 0.0), fragSoot);
        // a turret stands as high as its roof, where it stands untilted
        vec2 own = fragMode.z > 0.0 ? fragShip - fragMode.z * fragSea.yz : at;
        float depth = depthAt(own, max(heightAt(at), fragMode.z));
        rgb = shipShade(rgb, unpackNormal(nt), normalize(fragEye), fragLightSun, fragLightMoon, mat.x, mat.y, nt.a * (1.0 - 0.4 * fragSoot),
            specAt(depth));
        if (fragMode.z > 0.0 && fragMarch.y > 0.0)   // a turret, in the hull's shadow at its roof's height
            rgb *= 1.0 - hullShadow(heightTex, fragHmap, own, fragMode.z, fragSun.xy, fragSun.z, fragMarch, fragSea) * fragSun.w;
        outColour = drown(vec4(rgb * c.a, c.a), depth) * opacity;
    }
}
