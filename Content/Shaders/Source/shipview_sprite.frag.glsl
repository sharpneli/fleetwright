#version 450
#extension GL_GOOGLE_include_directive : require
// Ship viewer: a baked sprite (premultiplied alpha), lit as on the map (Include/shiplight.glsl; the eye straight
// above), or a turret's shadow, its silhouette projected by the receiver's height (Include/shipshadow.glsl). A turret
// takes the hull's shadow at its roof's height (drawn after the hull's shadow pass).
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;

layout(set = 2, binding = 0) uniform sampler2D spriteTex;
layout(set = 2, binding = 1) uniform sampler2D heightTex;
layout(set = 2, binding = 2) uniform sampler2D normalTex;   // the sprite's normal + AO

layout(set = 3, binding = 0) uniform Params
{
    vec4 p;       // x: mip level (< 0: by the hardware), y: 1 = a turret's shadow, z: opacity, w: the turret's roof, m (0: the hull)
    vec4 hmap;    // height map uv = ship metres * xy + zw
    vec4 sunL;    // xyz: toward the sun, in the quad's frame (x along u, y along v, z up)
    vec4 moonL;   // xyz: toward the moon
    vec4 shadow;  // the shadows' light: xy toward it (ship-local), z tan(elevation), w opacity
    vec4 march;   // the hull's shadow: x step, m; y steps (0: none); z softness, m; w the height map's mip level
    vec4 frame;   // a turret's shadow: the turret's uv (0, 0) corner and u edge, ship metres
};

#define LIGHTS_BINDING 1
#include "Include/shiplight.glsl"
#include "Include/shipshadow.glsl"

layout(location = 0) out vec4 outColour;

void main()
{
    if (p.y > 0.5)
    {
        // a turret's shadow: the quad is a box down-light of it; the mip by the turret's own uv
        vec2 own = turretUv(fragShip, frame);
        vec2 gx = dFdx(own), gy = dFdy(own);
        float h = shadowHeight(heightTex, hmap, fragShip, march.w);
        if (h >= p.w)
            discard;
        vec2 tuv = turretShadowUv(fragShip, h, p.w, shadow.xy, shadow.z, frame);
        if (any(lessThan(tuv, vec2(0.0))) || any(greaterThan(tuv, vec2(1.0))))
            discard;
        float a = p.x < 0.0 ? textureGrad(spriteTex, tuv, gx, gy).a : textureLod(spriteTex, tuv, p.x).a;
        if (a <= 0.0)
            discard;
        float hs = hullShadow(heightTex, hmap, fragShip, h, shadow.xy, shadow.z, march);
        outColour = vec4(0.0, 0.0, 0.0, shadowOver(a, hs, shadow.w) * p.z);
        return;
    }
    vec4 c = p.x < 0.0 ? texture(spriteTex, fragUv) : textureLod(spriteTex, fragUv, p.x);
    vec3 rgb = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);
    const vec3 up = vec3(0.0, 0.0, 1.0);
    vec4 nt = p.x < 0.0 ? texture(normalTex, fragUv) : textureLod(normalTex, fragUv, p.x);
    rgb = shipShade(rgb, unpackNormal(nt), up, sunL.xyz, moonL.xyz, look.y, 0.0, nt.a);
    if (p.w > 0.0 && march.y > 0.0)   // a turret, in the hull's shadow at its roof's height
        rgb *= 1.0 - hullShadow(heightTex, hmap, fragShip, p.w, shadow.xy, shadow.z, march) * shadow.w;
    outColour = vec4(rgb * c.a, c.a) * p.z;
}
