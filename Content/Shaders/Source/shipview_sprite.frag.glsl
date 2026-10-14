#version 450
#extension GL_GOOGLE_include_directive : require
// Ship viewer: a baked sprite (premultiplied alpha), lit as on the map (Include/shiplight.glsl; the eye straight
// above), or its silhouette as a turret's shadow, kept only where the height map under it is lower than the turret's
// roof (shadow.below_mask).
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;

layout(set = 2, binding = 0) uniform sampler2D spriteTex;
layout(set = 2, binding = 1) uniform sampler2D heightTex;
layout(set = 2, binding = 2) uniform sampler2D normalTex;   // the sprite's normal + AO

layout(set = 3, binding = 0) uniform Params
{
    vec4 p;      // x: mip level (< 0: by the hardware), y: 1 = silhouette, z: opacity, w: the turret roof, m
    vec4 hmap;   // height map uv = ship metres * xy + zw
    vec4 sunL;   // xyz: toward the sun, in the quad's frame (x along u, y along v, z up)
    vec4 moonL;  // xyz: toward the moon
};

#define LIGHTS_BINDING 1
#include "Include/shiplight.glsl"

layout(location = 0) out vec4 outColour;

void main()
{
    vec4 c = p.x < 0.0 ? texture(spriteTex, fragUv) : textureLod(spriteTex, fragUv, p.x);
    if (p.y > 0.5)
    {
        vec2 huv = fragShip * hmap.xy + hmap.zw;
        float h = 0.0;
        if (all(greaterThanEqual(huv, vec2(0.0))) && all(lessThanEqual(huv, vec2(1.0))))
            h = textureLod(heightTex, huv, 0.0).r * 63.75;
        if (h >= p.w)
            discard;
        outColour = vec4(0.0, 0.0, 0.0, c.a * p.z);
    }
    else
    {
        vec3 rgb = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);
        const vec3 up = vec3(0.0, 0.0, 1.0);
        vec4 nt = p.x < 0.0 ? texture(normalTex, fragUv) : textureLod(normalTex, fragUv, p.x);
        rgb = shipShade(rgb, unpackNormal(nt), up, sunL.xyz, moonL.xyz, look.y, 0.0, nt.a);
        outColour = vec4(rgb * c.a, c.a) * p.z;
    }
}
