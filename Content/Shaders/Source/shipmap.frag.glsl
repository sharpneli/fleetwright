#version 450
// Ships on the map: a baked sprite (premultiplied alpha, mipmapped, sampled by the hardware), a turret's silhouette
// as its shadow (only where the height map under it is lower than the turret's roof), or the stand-in icon tinted.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;
layout(location = 2) flat in vec4 fragMode;
layout(location = 3) flat in vec4 fragSun;
layout(location = 4) flat in vec4 fragMarch;
layout(location = 5) flat in vec4 fragHmap;
layout(location = 6) flat in vec4 fragTint;

layout(set = 2, binding = 0) uniform sampler2D spriteTex;
layout(set = 2, binding = 1) uniform sampler2D heightTex;

layout(location = 0) out vec4 outColour;

void main()
{
    vec4 c = texture(spriteTex, fragUv);
    float opacity = fragMode.y;
    if (fragMode.x > 1.5)
        outColour = vec4(fragTint.rgb, 1.0) * fragTint.a * c.a * opacity;
    else if (fragMode.x > 0.5)
    {
        vec2 huv = fragShip * fragHmap.xy + fragHmap.zw;
        float h = 0.0;
        if (all(greaterThanEqual(huv, vec2(0.0))) && all(lessThanEqual(huv, vec2(1.0))))
            h = textureLod(heightTex, huv, 0.0).r * 63.75;
        if (h >= fragMode.z)
            discard;
        outColour = vec4(0.0, 0.0, 0.0, c.a * opacity);
    }
    else
        outColour = vec4(c.rgb * fragMode.w, c.a) * opacity;
}
