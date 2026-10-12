#version 450
// Map lines (MapLines): anti-aliased polylines expanded to quads on the CPU, already in NDC. Each vertex carries its
// signed distance from the line's centre in pixels, and the line's half width.
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec2 inEdge;    // x: signed distance from the centre line, px; y: half the width, px
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec2 fragEdge;
layout(location = 1) out vec4 fragColor;

void main()
{
    gl_Position = vec4(inPos, 0.0, 1.0);
    fragEdge = inEdge;
    fragColor = inColor;
}
