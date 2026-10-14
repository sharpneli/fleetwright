#version 450
// Shipgen bake: one flat colour per draw (premultiplied for the colour layers; grey in .r for the height map), or for
// the relief layer (colour.w < 0) a height in metres by the draw's profile (Lower's ReliefPx: plateau, cylinder, dome,
// slope), evaluated at the pixel's canvas position.
layout(location = 0) in vec2 fragPx;

layout(set = 3, binding = 0) uniform Paint
{
    vec4 colour;   // relief: x the profile's kind, w -1
    vec4 p0;
    vec4 p1;
};

layout(location = 0) out vec4 outColour;

void main()
{
    if (colour.w >= 0.0)
    {
        outColour = colour;
        return;
    }
    int kind = int(colour.x + 0.5);
    vec2 d = fragPx - p0.xy;
    float h = p0.z;
    if (kind == 2)        // cylinder: across the axis, in radii
    {
        float t = dot(d, p1.xy);
        h += p0.w * sqrt(max(1.0 - t * t, 0.0));
    }
    else if (kind == 3)   // dome: the offset into the unit disc
    {
        vec2 q = vec2(dot(p1.xy, d), dot(p1.zw, d));
        h += p0.w * sqrt(max(1.0 - dot(q, q), 0.0));
    }
    else if (kind == 4)   // slope
        h += dot(d, p1.xy);
    outColour = vec4(h, 0.0, 0.0, 1.0);
}
