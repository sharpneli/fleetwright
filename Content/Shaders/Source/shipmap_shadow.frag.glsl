#version 450
#extension GL_GOOGLE_include_directive : require
// Ships on the map: the hull's shadow from the height map (Include/shipshadow.glsl), on the hull and the sea; drawn
// under the turrets, which take it in their own shader at their roof's height. A ship heeled and trimmed takes it at
// the point that shows here (tilted, as the hull), by the heights over the sea's plane.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;
layout(location = 2) flat in vec4 fragMode;
layout(location = 3) flat in vec4 fragSun;     // xy: toward the light, ship-local; z: tan(elevation); w: opacity
layout(location = 4) flat in vec4 fragMarch;   // x: step, m; y: steps; z: softness, m; w: the height map's mip level
layout(location = 5) flat in vec4 fragHmap;
layout(location = 6) flat in vec4 fragTint;
layout(location = 7) flat in vec4 fragSea;     // the sea's plane over the waterline (z, slopes, the tallest column)

layout(set = 2, binding = 0) uniform sampler2D heightTex;

layout(location = 0) out vec4 outColour;

#define LIGHTS_BINDING 0
#include "Include/shiplight.glsl"
#include "Include/shipshadow.glsl"

void main()
{
    vec2 px = fwidth(fragShip);
    vec2 at = shownAt(heightTex, fragHmap, fragShip, fragSea, max(px.x, px.y));
    float hp = shadowHeight(heightTex, fragHmap, at, fragMarch.w);
    float shade = hullShadow(heightTex, fragHmap, at, hp, fragSun.xy, fragSun.z, fragMarch, fragSea);
    outColour = hazePremul(vec4(0.0, 0.0, 0.0, shade * fragSun.w * fragMode.y));   // the haze over it (docs/visuals/haze.md)
}
