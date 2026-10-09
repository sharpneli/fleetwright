#version 450
// Ship viewer: the sun's shadow from the height map (shadow.py): a point is shadowed when, marching from it toward
// the sun, a column stands higher than the sun's ray. Grey x 0.25 m is the height above the waterline.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;

layout(set = 2, binding = 0) uniform sampler2D heightTex;

layout(set = 3, binding = 0) uniform Params
{
    vec4 sun;    // xy: unit vector toward the sun (ship-local), z: tan(elevation), w: opacity
    vec4 hmap;   // height map uv = ship metres * xy + zw
    vec4 march;  // x: step in metres, y: steps, z: softness in metres, w: mip level of the height map
};

layout(location = 0) out vec4 outColour;

float H(vec2 q)
{
    vec2 huv = q * hmap.xy + hmap.zw;
    if (any(lessThan(huv, vec2(0.0))) || any(greaterThan(huv, vec2(1.0))))
        return 0.0;
    return textureLod(heightTex, huv, march.w).r * 63.75;
}

void main()
{
    float hp = H(fragShip);
    float shade = 0.0;
    int n = int(march.y);
    for (int i = 1; i <= n; i++)
    {
        float t = float(i) * march.x;
        float occ = H(fragShip + sun.xy * t);
        shade = max(shade, clamp((occ - (hp + t * sun.z)) / march.z, 0.0, 1.0));
    }
    outColour = vec4(0.0, 0.0, 0.0, shade * sun.w);
}
