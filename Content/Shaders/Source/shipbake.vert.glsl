#version 450
// Shipgen bake: flat triangles in canvas pixels (x right, y down), mapped to the tile being drawn.
layout(location = 0) in vec2 pos;

layout(set = 1, binding = 0) uniform Xf
{
    vec4 xf;   // ndc = pos * xf.xy + xf.zw
};

void main()
{
    gl_Position = vec4(pos * xf.xy + xf.zw, 0.0, 1.0);
}
