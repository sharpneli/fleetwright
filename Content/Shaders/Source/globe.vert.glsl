#version 450
// The globe: one triangle over the whole target, each pixel ray-cast against the sphere in globe.frag.glsl.
layout(location = 0) out vec2 fragNdc;

void main()
{
    vec2 p = vec2(float((gl_VertexIndex << 1) & 2), float(gl_VertexIndex & 2)) * 2.0 - 1.0;
    fragNdc = p;
    gl_Position = vec4(p, 0.0, 1.0);
}
