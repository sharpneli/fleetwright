#version 450
// The sea's wave sim, dispatch 2 of 2 (docs/visuals/sea.md W2; the second half of the lab's FFT and its P_COMB, foam
// left out): one workgroup per column of one cascade (z), one thread per row. The column's inverse FFT in shared
// memory finishes the 2D transform of dispatch 1's rows; then each texel is combined into what the sea is shaded
// from:
// - moments (sx, sy, sx², sy²): the slope on the displaced (Lagrangian) surface, through the inverse Jacobian of the
//   choppy displacement, and its square, so the mips average both and the shader reads their variance (LEAN);
// - displacement (Dx, Dy, h, J): the choppy displacement scaled by λ, the height, and the Jacobian (foam, later).
layout(local_size_x = 128) in;
const int N = 128;
const int LogN = 7;

layout(set = 0, binding = 0, rgba32f) uniform readonly image2D A0;
layout(set = 0, binding = 1, rgba32f) uniform readonly image2D B0;
layout(set = 0, binding = 2, rgba32f) uniform readonly image2D A1;
layout(set = 0, binding = 3, rgba32f) uniform readonly image2D B1;
layout(set = 0, binding = 4, rgba32f) uniform readonly image2D A2;
layout(set = 0, binding = 5, rgba32f) uniform readonly image2D B2;
layout(set = 1, binding = 0, rgba16f) uniform writeonly image2D M0;
layout(set = 1, binding = 1, rgba16f) uniform writeonly image2D D0;
layout(set = 1, binding = 2, rgba16f) uniform writeonly image2D M1;
layout(set = 1, binding = 3, rgba16f) uniform writeonly image2D D1;
layout(set = 1, binding = 4, rgba16f) uniform writeonly image2D M2;
layout(set = 1, binding = 5, rgba16f) uniform writeonly image2D D2;

layout(set = 2, binding = 0) uniform Params
{
    vec4 misc;   // x: choppiness λ
};

shared vec2 buf[2][4][N];

vec2 cmul(vec2 a, vec2 b) { return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x); }

void main()
{
    int m = int(gl_LocalInvocationID.x), x = int(gl_WorkGroupID.x), c = int(gl_WorkGroupID.z);
    ivec2 ij = ivec2(x, m);
    vec4 a = c == 0 ? imageLoad(A0, ij) : c == 1 ? imageLoad(A1, ij) : imageLoad(A2, ij);
    vec4 b = c == 0 ? imageLoad(B0, ij) : c == 1 ? imageLoad(B1, ij) : imageLoad(B2, ij);
    buf[0][0][m] = a.xy;
    buf[0][1][m] = a.zw;
    buf[0][2][m] = b.xy;
    buf[0][3][m] = b.zw;
    barrier();

    int src = 0;
    for (int stage = 1; stage <= LogN; stage++)
    {
        int s = 1 << stage, hs = s >> 1;
        int ev = (m / s) * hs + (m % hs), od = ev + N / 2;
        float an = 6.28318530718 * float(m) / float(s);
        vec2 tw = vec2(cos(an), sin(an));
        for (int f = 0; f < 4; f++)
            buf[1 - src][f][m] = buf[src][f][ev] + cmul(tw, buf[src][f][od]);
        src = 1 - src;
        barrier();
    }
    vec4 A = vec4(buf[src][0][m], buf[src][1][m]), B = vec4(buf[src][2][m], buf[src][3][m]);

    float chop = misc.x;
    float h = A.x;
    vec2 D = A.yz * chop, s = vec2(A.w, B.x);
    float jxx = 1.0 + chop * B.y, jyy = 1.0 + chop * B.z, jxy = chop * B.w;
    float J = jxx * jyy - jxy * jxy;
    float Jc = max(J, 0.15);
    vec2 sl = vec2(jyy * s.x - jxy * s.y, -jxy * s.x + jxx * s.y) / Jc;
    sl = clamp(sl, -2.5, 2.5);
    vec4 M = vec4(sl, sl * sl), Dout = vec4(D, h, J);
    if (c == 0) { imageStore(M0, ij, M); imageStore(D0, ij, Dout); }
    else if (c == 1) { imageStore(M1, ij, M); imageStore(D1, ij, Dout); }
    else { imageStore(M2, ij, M); imageStore(D2, ij, Dout); }
}
