#version 450
#extension GL_GOOGLE_include_directive : require
// Ship viewer: the hull's shadow from the height map (Include/shipshadow.glsl), on the hull and the sea; drawn under
// the turrets, which take it in their own shader at their roof's height.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;

layout(set = 2, binding = 0) uniform sampler2D heightTex;

layout(set = 3, binding = 0) uniform Params
{
    vec4 sun;    // xy: unit vector toward the light (ship-local), z: tan(elevation), w: opacity
    vec4 hmap;   // height map uv = ship metres * xy + zw
    vec4 march;  // x: step in metres, y: steps, z: softness in metres, w: mip level of the height map
};

layout(location = 0) out vec4 outColour;

#include "Include/shipshadow.glsl"

void main()
{
    float hp = shadowHeight(heightTex, hmap, fragShip, march.w);
    outColour = vec4(0.0, 0.0, 0.0, hullShadow(heightTex, hmap, fragShip, hp, sun.xy, sun.z, march) * sun.w);
}
