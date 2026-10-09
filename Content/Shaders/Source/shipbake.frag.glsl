#version 450
// Shipgen bake: one flat colour per draw (premultiplied for the colour layers; grey in .r for the height map).
layout(set = 3, binding = 0) uniform Paint
{
    vec4 colour;
};

layout(location = 0) out vec4 outColour;

void main()
{
    outColour = colour;
}
