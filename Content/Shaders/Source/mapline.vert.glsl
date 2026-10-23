#version 450
// Map lines (MapLines): anti-aliased polylines expanded to quads on the CPU, already in NDC. Each vertex carries its
// signed distance from the line's centre in pixels, the line's half width, the distance along the strip and the
// casing's width (px); and the strip's style: the dash period (px, 0 solid), the dash's share of it (negative: round
// dots) and the casing's alpha (docs/map-ui/symbols.md Y2).
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec4 inEdge;    // x: signed distance from the centre line, px; y: half the width, px; z: along, px; w: casing, px
layout(location = 2) in vec4 inColor;
layout(location = 3) in vec4 inStyle;   // x: dash period, px (0 solid); y: the dash's share (negative: dots); z: casing alpha

layout(location = 0) out vec4 fragEdge;
layout(location = 1) out vec4 fragColor;
layout(location = 2) out vec4 fragStyle;

void main()
{
    gl_Position = vec4(inPos, 0.0, 1.0);
    fragEdge = inEdge;
    fragColor = inColor;
    fragStyle = inStyle;
}
