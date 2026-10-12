#version 450
// +1 for a triangle facing one way, -1 the other, added up: the winding number of the rings around each pixel.
// Non-zero is land; holes wind the other way and cancel, and a fan's overlaps cancel too.
layout(location = 0) out vec4 outWinding;

void main()
{
    outWinding = vec4(gl_FrontFacing ? 1.0 : -1.0, 0.0, 0.0, 1.0);
}
