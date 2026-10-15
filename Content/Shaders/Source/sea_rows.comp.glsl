#version 450
// The sea's wave sim, dispatch 1 of 2 (docs/visuals/sea.md W2; the lab's P_SPEC and the first half of its FFT): one
// workgroup per row of one cascade (z), one thread per column. Each thread evolves its bin of the initial spectrum to
// the sea's clock, h(k, t) = h0(k) e^(-iωt) + conj(h0(-k)) e^(iωt) (with the e^(+ik·x) inverse transform this
// travels along +k, downwind), and forms the eight real fields' spectra, packed two to a complex number (the
// height and Dx; Dy and the x slope; the y slope and Jxx; Jyy and Jxy: each real, so one inverse transform gives one
// as the real part and the other as the imaginary). The row's inverse FFT is then done in shared memory (radix 2,
// Stockham, so the output comes out in order), and the row written for dispatch 2.
layout(local_size_x = 128) in;
const int N = 128;
const int LogN = 7;

layout(set = 0, binding = 0, rgba32f) uniform readonly image2D H0a;
layout(set = 0, binding = 1, rgba32f) uniform readonly image2D H0b;
layout(set = 0, binding = 2, rgba32f) uniform readonly image2D H0c;
layout(set = 1, binding = 0, rgba32f) uniform writeonly image2D A0;
layout(set = 1, binding = 1, rgba32f) uniform writeonly image2D B0;
layout(set = 1, binding = 2, rgba32f) uniform writeonly image2D A1;
layout(set = 1, binding = 3, rgba32f) uniform writeonly image2D B1;
layout(set = 1, binding = 4, rgba32f) uniform writeonly image2D A2;
layout(set = 1, binding = 5, rgba32f) uniform writeonly image2D B2;

layout(set = 2, binding = 0) uniform Params
{
    vec4 tile;   // xyz: the cascades' tiles, metres; w: the sea's clock, seconds (within one period)
    vec4 misc;   // x: the frequency step the dispersion is quantised to, rad/s (the clock's period is 2π over it)
};

shared vec2 buf[2][4][N];

vec2 cmul(vec2 a, vec2 b) { return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x); }
vec2 times_i(vec2 a) { return vec2(-a.y, a.x); }

void main()
{
    int n = int(gl_LocalInvocationID.x), m = int(gl_WorkGroupID.y), c = int(gl_WorkGroupID.z);
    ivec2 ij = ivec2(n, m);
    vec4 h0 = c == 0 ? imageLoad(H0a, ij) : c == 1 ? imageLoad(H0b, ij) : imageLoad(H0c, ij);
    float L = c == 0 ? tile.x : c == 1 ? tile.y : tile.z;
    float kx = float(n < N / 2 ? n : n - N) * 6.28318530718 / L, ky = float(m < N / 2 ? m : m - N) * 6.28318530718 / L;
    float k = length(vec2(kx, ky));
    float w = sqrt(9.81 * k);
    w = floor(w / misc.x + 0.5) * misc.x;
    float ph = mod(w * tile.w, 6.28318530718);
    vec2 e = vec2(cos(ph), -sin(ph));
    vec2 h = cmul(h0.xy, e) + cmul(h0.zw, vec2(e.x, -e.y));
    float ik = k > 1e-6 ? 1.0 / k : 0.0;
    vec2 dx = times_i(h) * kx * ik, dy = times_i(h) * ky * ik, sx = times_i(h) * kx, sy = times_i(h) * ky;
    vec2 jxx = -h * kx * kx * ik, jyy = -h * ky * ky * ik, jxy = -h * kx * ky * ik;
    buf[0][0][n] = h + times_i(dx);
    buf[0][1][n] = dy + times_i(sx);
    buf[0][2][n] = sy + times_i(jxx);
    buf[0][3][n] = jyy + times_i(jxy);
    barrier();

    // the inverse FFT along the row: at stage size s, output i takes its even and odd inputs half a row apart
    int src = 0;
    for (int stage = 1; stage <= LogN; stage++)
    {
        int s = 1 << stage, hs = s >> 1;
        int ev = (n / s) * hs + (n % hs), od = ev + N / 2;
        float a = 6.28318530718 * float(n) / float(s);
        vec2 tw = vec2(cos(a), sin(a));
        for (int f = 0; f < 4; f++)
            buf[1 - src][f][n] = buf[src][f][ev] + cmul(tw, buf[src][f][od]);
        src = 1 - src;
        barrier();
    }
    vec4 A = vec4(buf[src][0][n], buf[src][1][n]), B = vec4(buf[src][2][n], buf[src][3][n]);
    if (c == 0) { imageStore(A0, ij, A); imageStore(B0, ij, B); }
    else if (c == 1) { imageStore(A1, ij, A); imageStore(B1, ij, B); }
    else { imageStore(A2, ij, A); imageStore(B2, ij, B); }
}
