#version 450
// The baked coast (LandMesh): each vertex holds its point, the ring's points before and after it, and a side. All
// three are projected and the join is mitred in screen space, as MapLines does on the CPU; mapline.frag fades the
// edges. Depth-tested against the globe pass's sphere, so the far side's coast is hidden.
layout(location = 0) in vec3 inPrev;
layout(location = 1) in vec3 inCur;
layout(location = 2) in vec3 inNext;
layout(location = 3) in float inSide;

layout(set = 1, binding = 0) uniform View
{
    vec4 eye;
    vec4 right;
    vec4 up;
    vec4 fwd;
    vec4 depth;
    vec4 viewport;
    vec4 color;
};

layout(location = 0) out vec4 fragEdge;
layout(location = 1) out vec4 fragColor;
layout(location = 2) out vec4 fragStyle;   // mapline.frag's: solid, no casing

vec4 Project(vec3 p)
{
    vec3 r = p - eye.xyz;
    float z = dot(r, fwd.xyz);
    return vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
}

vec2 Px(vec4 c) { return c.xy / c.w * viewport.xy * 0.5; }   // pixels about the centre, y up

vec2 Dir(vec2 a, vec2 b)
{
    vec2 d = b - a;
    float l = length(d);
    return l > 1e-4 ? d / l : vec2(1.0, 0.0);
}

void main()
{
    vec4 c = Project(inCur);
    vec2 p = Px(c), dIn = Dir(Px(Project(inPrev)), p), dOut = Dir(p, Px(Project(inNext)));
    vec2 nIn = vec2(-dIn.y, dIn.x);
    vec2 n = vec2(-(dIn.y + dOut.y), dIn.x + dOut.x);
    float len = length(n);
    n = len > 1e-4 ? n / len : nIn;
    float miter = min(1.0 / max(dot(n, nIn), 0.25), 4.0);
    float reach = viewport.z + 1.0;   // a pixel of fade beyond the edge
    vec2 q = p + n * (reach * miter * inSide);
    gl_Position = vec4(q / (viewport.xy * 0.5) * c.w, c.z, c.w);
    fragEdge = vec4(inSide * reach, viewport.z, 0.0, 0.0);
    fragColor = color;
    fragStyle = vec4(0.0);
}
