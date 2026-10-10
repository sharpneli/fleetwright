#version 450
// Hitbox view: flat-shaded prism faces (solid, or blended over) and their outlines, coloured by kind (HitKinds),
// cut to the clip box, with the hovered and the selected prism lit up.
layout(location = 0) in vec3 fragWorld;
layout(location = 1) in vec3 fragNormal;
layout(location = 2) flat in uint fragKind;
layout(location = 3) flat in uint fragPrism;

layout(set = 3, binding = 0) uniform Params
{
    vec4 light;        // xyz: toward the light (unit), w: the pass (0 solid, 1 translucent, 2 lines)
    vec4 clipMin;      // the clip box, ship metres; w > 0.5: clipping on
    vec4 clipMax;      // w > 0.5: cut per pixel (else the vertex shader drops whole prisms; guide lines are cut here)
    uvec4 ids;         // x: the hovered prism, y: the selected prism
    vec4 colours[64];  // per kind: rgb, alpha
    vec4 edges[64];    // per kind: the outline's rgb, alpha
};

layout(location = 0) out vec4 outColour;

const uint NoPrism = 0xFFFFFFFFu;

void main()
{
    if (clipMin.w > 0.5 && (clipMax.w > 0.5 || fragPrism == NoPrism)
        && (any(lessThan(fragWorld, clipMin.xyz)) || any(greaterThan(fragWorld, clipMax.xyz))))
        discard;
    vec4 c;
    if (light.w > 1.5)
        c = fragPrism == NoPrism ? colours[fragKind] : edges[fragKind];   // guide lines take the kind's own colour
    else
    {
        c = colours[fragKind];
        c.rgb *= 0.5 + 0.5 * max(0.0, dot(normalize(fragNormal), light.xyz));
        if (light.w < 0.5)
            c.a = 1.0;
    }
    if (fragPrism != NoPrism && fragPrism == ids.y)
    {
        c.rgb = mix(c.rgb, vec3(1.0, 0.85, 0.2), 0.45);
        c.a = max(c.a, 0.6);
    }
    else if (fragPrism != NoPrism && fragPrism == ids.x)
    {
        c.rgb = mix(c.rgb, vec3(1.0), 0.35);
        c.a = max(c.a, 0.45);
    }
    outColour = c;
}
