#version 450
// Ships on the map: the sun's shadow from the height map (as shipview_shadow.frag): a point is shadowed when, marching
// from it toward the sun, a column stands higher than the sun's ray. Grey x 0.25 m is the height above the waterline.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;
layout(location = 2) flat in vec4 fragMode;
layout(location = 3) flat in vec4 fragSun;     // xy: toward the sun, ship-local; z: tan(elevation); w: opacity
layout(location = 4) flat in vec4 fragMarch;   // x: step, m; y: steps; z: softness, m; w: the height map's mip level
layout(location = 5) flat in vec4 fragHmap;
layout(location = 6) flat in vec4 fragTint;

layout(set = 2, binding = 0) uniform sampler2D heightTex;

layout(location = 0) out vec4 outColour;

float H(vec2 q)
{
    vec2 huv = q * fragHmap.xy + fragHmap.zw;
    if (any(lessThan(huv, vec2(0.0))) || any(greaterThan(huv, vec2(1.0))))
        return 0.0;
    return textureLod(heightTex, huv, fragMarch.w).r * 63.75;
}

void main()
{
    float hp = H(fragShip);
    float shade = 0.0;
    int n = int(fragMarch.y);
    for (int i = 1; i <= n; i++)
    {
        float t = float(i) * fragMarch.x;
        float occ = H(fragShip + fragSun.xy * t);
        shade = max(shade, clamp((occ - (hp + t * fragSun.z)) / fragMarch.z, 0.0, 1.0));
    }
    outColour = vec4(0.0, 0.0, 0.0, shade * fragSun.w * fragMode.y);
}
