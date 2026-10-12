#version 450
layout(location = 0) in vec2 fragEdge;
layout(location = 1) in vec4 fragColor;
layout(location = 0) out vec4 outColor;

void main()
{
    float a = clamp(fragEdge.y + 0.5 - abs(fragEdge.x), 0.0, 1.0);
    outColor = vec4(fragColor.rgb, fragColor.a * a);
}
