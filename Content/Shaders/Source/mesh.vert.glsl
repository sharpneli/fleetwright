#version 450

// Vertex inputs (matching Vertex struct)
layout(location = 0) in vec3 inPosition;
layout(location = 1) in float inUvX;
layout(location = 2) in vec3 inNormal;
layout(location = 3) in float inUvY;
layout(location = 4) in vec4 inColor;

// Uniform buffer (SDL3 GPU uses uniform buffers, not push constants)
// Set 1 is for vertex uniform buffers in SDL3 GPU
layout(set = 1, binding = 0) uniform VertexUniforms {
    mat4 viewProj;
    mat4 modelMatrix;
    vec4 colorFactors;
} uniforms;

// Outputs to fragment shader
layout(location = 0) out vec3 fragNormal;
layout(location = 1) out vec3 fragColor;
layout(location = 2) out vec2 fragUv;

void main() {
    vec4 position = vec4(inPosition, 1.0);

    gl_Position = uniforms.viewProj * uniforms.modelMatrix * position;
    fragNormal = (uniforms.modelMatrix * vec4(inNormal, 0.0)).xyz;
    fragColor = inColor.xyz * uniforms.colorFactors.xyz;
    fragUv = vec2(inUvX, inUvY);
}
