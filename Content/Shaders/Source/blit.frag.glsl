#version 450

layout(location = 0) in vec2 fragUv;

layout(set = 2, binding = 0) uniform sampler2D sourceTex;

layout(location = 0) out vec4 outColor;

void main() {
    vec3 color = texture(sourceTex, fragUv).rgb;
    outColor = vec4(color, 1.0);
}
