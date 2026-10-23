#version 450
// Map symbols (MapSymbols, docs/map-ui/symbols.md Y3): one quad a symbol, built on the CPU in NDC; each corner carries
// its point in the symbol's own frame (px, +y the symbol's forward), the shape and its sizes, the fill and stroke
// colours and the casing's alpha.
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec2 inLocal;
layout(location = 2) in vec4 inShape;    // x: shape, y: radius px, z: stroke half width px, w: casing px
layout(location = 3) in vec4 inFill;
layout(location = 4) in vec4 inStroke;
layout(location = 5) in vec4 inExtra;    // x: casing alpha

layout(location = 0) out vec2 fragLocal;
layout(location = 1) flat out vec4 fragShape;
layout(location = 2) flat out vec4 fragFill;
layout(location = 3) flat out vec4 fragStroke;
layout(location = 4) flat out vec4 fragExtra;

void main()
{
    gl_Position = vec4(inPos, 0.0, 1.0);
    fragLocal = inLocal;
    fragShape = inShape;
    fragFill = inFill;
    fragStroke = inStroke;
    fragExtra = inExtra;
}
