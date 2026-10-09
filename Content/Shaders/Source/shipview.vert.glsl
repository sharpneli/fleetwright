#version 450
// Ship viewer: one textured quad per draw, made from gl_VertexIndex (6 vertices). Each corner carries its texture
// uv and its ship-local position in metres (+x bow, +y starboard), for the shadow shaders.
layout(set = 1, binding = 0) uniform Quad
{
    vec4 o;   // corner uv (0, 0): .xy in NDC, .zw in ship metres
    vec4 u;   // the quad's edge along uv.x, the same units
    vec4 v;   // the quad's edge along uv.y
};

layout(location = 0) out vec2 fragUv;
layout(location = 1) out vec2 fragShip;

void main()
{
    vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));
    vec2 t = corners[gl_VertexIndex];
    gl_Position = vec4(o.xy + t.x * u.xy + t.y * v.xy, 0.0, 1.0);
    fragUv = t;
    fragShip = o.zw + t.x * u.zw + t.y * v.zw;
}
