#version 450
// The land's fill (LandView): fans of triangles in NDC, counted into a coverage target by winding.
layout(location = 0) in vec2 inPos;

void main()
{
    gl_Position = vec4(inPos, 0.0, 1.0);
}
