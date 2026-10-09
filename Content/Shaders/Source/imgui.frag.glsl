#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUV;

layout(set = 2, binding = 0) uniform sampler2D sTexture;

layout(location = 0) out vec4 fColor;

void main() {
    fColor = vColor * texture(sTexture, vUV);
}
