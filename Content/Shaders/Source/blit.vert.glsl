#version 450

// Fullscreen triangle - no vertex input needed
// Generates positions and UVs procedurally

layout(location = 0) out vec2 fragUv;

void main() {
    // Generate fullscreen triangle vertices
    // Vertex 0: (-1, -1), Vertex 1: (3, -1), Vertex 2: (-1, 3)
    vec2 positions[3] = vec2[](
        vec2(-1.0, -1.0),
        vec2( 3.0, -1.0),
        vec2(-1.0,  3.0)
    );

    // SDL_GPU's NDC is y-up and its texture origin top-left, so NDC y = -1 (the bottom) samples v = 1
    vec2 uvs[3] = vec2[](
        vec2(0.0, 1.0),
        vec2(2.0, 1.0),
        vec2(0.0, -1.0)
    );

    gl_Position = vec4(positions[gl_VertexIndex], 0.0, 1.0);
    fragUv = uvs[gl_VertexIndex];
}
