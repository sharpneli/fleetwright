// The sea in the world view (docs/visuals/sea.md W3), included by globe.frag after shiplight.glsl: a pixel of open
// water shaded from the FFT cascades (SeaSim), after the Sea State Lab's render pass with the foam and the gust
// patches left out. Per pixel:
//  1. its position in the sea frame's plane, metres from the view's centre (the frame and the centre's offsets come
//     from the CPU in double, wrapped on the textures' and the hex lattice's periods, so floats stay small);
//  2. the choppy displacement undone by one fixed-point step (faded out beyond 1.5 m/px, where it's sub-pixel);
//  3. the slope moments of cascades 0-1 through hex-tile bombing (Mikkelsen 2022: three random offsets and small
//     turns, blended so the variance holds), cascade 2 plain and its ripple copy at 1/7.93 scale; each fetched with the
//     pixel's footprint as gradient, so the mips turn waves too small to see into slope variance (LEAN), topped up to
//     Cox and Munk's for the wind;
//  4. lit: the water body from the sun and the sky, the sky's reflection by Fresnel, and the sun's and moon's glints
//     from the anisotropic Gaussian slope distribution (Cox-Munk, Bruneton). Light from the ships' Lights block.
//  5. the wakes' slopes and variance added to the sea's (linear waves superpose), before the light; their foam (the
//     trails' lanes) over the water after it, lit by the same lights;
//  6. the muzzle flashes' lights (docs/visuals/muzzle-flashes.md F4): point lights, their glints through the same
//     slope distribution (so they break up as the sun's do), and on the foam.

layout(set = 2, binding = 1) uniform sampler2D seaM0;
layout(set = 2, binding = 2) uniform sampler2D seaM1;
layout(set = 2, binding = 3) uniform sampler2D seaM2;
layout(set = 2, binding = 4) uniform sampler2D seaD0;
layout(set = 2, binding = 5) uniform sampler2D seaD1;
layout(set = 2, binding = 6) uniform sampler2D seaD2;
// the wakes (docs/visuals/wakes.md), per pixel: slopes along the camera's right and up, their unresolved variance,
// foam (the ships' layer draws it before the pass: ShipBatchRenderer.DrawWake)
layout(set = 2, binding = 7) uniform sampler2D wakeTex;

layout(set = 3, binding = 1) uniform Sea
{
    vec4 seaOn;     // x: the world factor (0: no sea); y: metres per pixel; z: the displacement's fade; w: debug view
    vec4 seaTiles;  // the cascades' tiles, metres: 0, 1, 2 and the ripples (3)
    vec4 seaOff01;  // the view centre in the frame's plane, wrapped on cascade 0's tile (xy) and cascade 1's (zw)
    vec4 seaOff23;  // and on cascade 2's (xy) and the ripples' (zw)
    vec4 seaHexOff; // and on the hex lattices' hash periods, cascade 0 (xy) and 1 (zw)
    vec4 seaHexS;   // x, y: the hex lattices' spacing, metres; z: the top mip; w: Hs
    vec4 seaAxes;   // the camera's right (xy) and up (zw) in the frame's axes
    vec4 seaVarU;   // xy: the slope variance no cascade resolves (Cox-Munk's remainder); zw: the ripples' amplitude
    vec4 seaMss2;   // xy: cascade 2's mean-square slopes; z: the glints' scale (0 in time-lapse); w: the Earth's radius, m
    vec4 seaBody;   // rgb: the water body's reflectance, linear; w: how much of its light follows the facet (FacetLight)
    vec4 seaMoon;   // xyz: toward the moon, Earth-fixed; w: the slopes' exaggeration for the body's light (Relief)
    vec4 seaNight;  // rgb: the night's light on the water (SeaView.NightLight: tuned, not real), linear; w: its relief
    vec4 seaNightDir; // xyz: toward it, Earth-fixed
};

// The wakes' foam (wakeribbon.frag packs two layers into wake.w): aerated water in 0-0.4, which brightens the water a
// little toward a pale turquoise (the bubbles under the surface scatter the light back up, still water's colour);
// surface foam in 0.4-1, white but never wholly opaque (a lacy film over the water, not snow). Reflectances by eye.
const float FoamAlbedo = 0.65, FoamCover = 0.75, AeratedAlbedo = 0.14;
const vec3 AeratedTint = vec3(0.55, 0.85, 0.9);

float seaHash(ivec2 c)
{
    c = c & 255;
    uint n = uint(c.x) * 73856093u ^ uint(c.y) * 19349663u;
    n = (n ^ (n >> 13)) * 1274126177u;
    return float(n & 0xffffffu) / 16777215.0;
}

mat2 seaRot(float a)
{
    float c = cos(a), s = sin(a);
    return mat2(c, s, -s, c);
}

// the three hex cells round q (a triangle lattice of spacing S), their sharpened weights, centres, offsets and turns
struct Hex { vec3 w; vec2 c0, c1, c2; vec2 o0, o1, o2; mat2 R0, R1, R2; };

Hex hexSetup(vec2 q, vec2 hexOff, float S, float maxRot, int salt)
{
    Hex h;
    vec2 st = (q + hexOff) / S;
    vec2 sk = vec2(st.x - 0.57735027 * st.y, 1.15470054 * st.y);
    ivec2 b = ivec2(floor(sk));
    vec3 t = vec3(fract(sk), 0.0);
    t.z = 1.0 - t.x - t.y;
    float s = step(0.0, -t.z), s2 = 2.0 * s - 1.0;
    vec3 w = vec3(-t.z * s2, s - t.y * s2, s - t.x * s2);
    int si = int(s);
    ivec2 v0 = b + ivec2(si, si), v1 = b + ivec2(si, 1 - si), v2 = b + ivec2(1 - si, si);
    w = w * w * w;
    h.w = w / (w.x + w.y + w.z);
    h.c0 = vec2(float(v0.x) + 0.5 * float(v0.y), float(v0.y) * 0.8660254) * S - hexOff;
    h.c1 = vec2(float(v1.x) + 0.5 * float(v1.y), float(v1.y) * 0.8660254) * S - hexOff;
    h.c2 = vec2(float(v2.x) + 0.5 * float(v2.y), float(v2.y) * 0.8660254) * S - hexOff;
    ivec2 k = ivec2(salt * 37, salt * 91);
    h.o0 = vec2(seaHash(v0 + k), seaHash(v0 + k + ivec2(97, 13)));
    h.o1 = vec2(seaHash(v1 + k), seaHash(v1 + k + ivec2(97, 13)));
    h.o2 = vec2(seaHash(v2 + k), seaHash(v2 + k + ivec2(97, 13)));
    h.R0 = seaRot((seaHash(v0 + k + ivec2(31, 177)) - 0.5) * 2.0 * maxRot);
    h.R1 = seaRot((seaHash(v1 + k + ivec2(31, 177)) - 0.5) * 2.0 * maxRot);
    h.R2 = seaRot((seaHash(v2 + k + ivec2(31, 177)) - 0.5) * 2.0 * maxRot);
    return h;
}

// moments through the hex cells: slopes turned back to the frame, the means variance-preserving, the squares linear
vec4 hexM(sampler2D T, Hex h, vec2 q, float L, vec2 g)
{
    vec4 a0 = textureGrad(T, h.R0 * (q - h.c0) / L + h.o0, h.R0 * g, h.R0 * g.yx);
    vec4 a1 = textureGrad(T, h.R1 * (q - h.c1) / L + h.o1, h.R1 * g, h.R1 * g.yx);
    vec4 a2 = textureGrad(T, h.R2 * (q - h.c2) / L + h.o2, h.R2 * g, h.R2 * g.yx);
    vec2 m = h.w.x * (transpose(h.R0) * a0.xy) + h.w.y * (transpose(h.R1) * a1.xy) + h.w.z * (transpose(h.R2) * a2.xy);
    vec2 cs0 = h.R0[0] * h.R0[0], cs1 = h.R1[0] * h.R1[0], cs2 = h.R2[0] * h.R2[0];
    vec2 mm = h.w.x * vec2(dot(cs0, a0.zw), dot(cs0.yx, a0.zw)) + h.w.y * vec2(dot(cs1, a1.zw), dot(cs1.yx, a1.zw))
            + h.w.z * vec2(dot(cs2, a2.zw), dot(cs2.yx, a2.zw));
    return vec4(m * inversesqrt(dot(h.w, h.w)), mm);
}

// the choppy displacement through the hex cells, turned back and variance-preserving
vec2 hexD(sampler2D T, Hex h, vec2 q, float L, vec2 g)
{
    vec2 a0 = textureGrad(T, h.R0 * (q - h.c0) / L + h.o0, h.R0 * g, h.R0 * g.yx).xy;
    vec2 a1 = textureGrad(T, h.R1 * (q - h.c1) / L + h.o1, h.R1 * g, h.R1 * g.yx).xy;
    vec2 a2 = textureGrad(T, h.R2 * (q - h.c2) / L + h.o2, h.R2 * g, h.R2 * g.yx).xy;
    vec2 D = h.w.x * (transpose(h.R0) * a0) + h.w.y * (transpose(h.R1) * a1) + h.w.z * (transpose(h.R2) * a2);
    return D * inversesqrt(dot(h.w, h.w));
}

float seaFresnel(float c)
{
    c = clamp(c, 0.0, 1.0);
    return 0.02 + 0.98 * pow(1.0 - c, 5.0);
}

// the light a facet with normal n lets into the water from a light toward l: its cosine through 1 - Fresnel
float seaLetIn(vec3 n, vec3 l)
{
    float c = dot(n, l);
    return c > 0.0 ? c * (1.0 - seaFresnel(c)) : 0.0;
}

// Cox-Munk's glint for a light toward l: pi times the reflected radiance per unit of the light (the lights' units:
// their irradiance across the beam), v toward the eye, the mean slope m and its variance var, all in the pixel's
// frame (x right, y up the screen, z the local vertical)
float seaGlint(vec3 l, vec3 v, vec2 m, vec2 var)
{
    if (l.z <= 0.0)
        return 0.0;
    vec3 h = normalize(l + v);
    vec2 z = -h.xy / h.z - m;
    float pdf = exp(-0.5 * (z.x * z.x / var.x + z.y * z.y / var.y)) / (6.2831853 * sqrt(var.x * var.y));
    return 3.14159265 * seaFresnel(dot(v, h)) * pdf / (4.0 * max(v.z, 0.05) * pow(h.z, 4.0)) * smoothstep(0.0, 0.05, l.z);
}

// The sea's colour (sRGB-coded) at a pixel: local its offset from the view's centre in metres along the camera's
// right and up; v toward the eye, lSun, lMoon and lNight toward the lights, in the pixel's frame; wake the wakes'
// texel (slopes in the pixel's frame, their variance, foam); pEye where it is, metres from the eye, and the pixel's
// frame's axes (rt, upv, nrm), Earth-fixed, for the flashes' lights.
vec3 seaShade(vec2 local, vec3 v, vec3 lSun, vec3 lMoon, vec3 lNight, vec4 wake, vec3 pEye, vec3 rt, vec3 upv, vec3 nrm)
{
    float mpp = seaOn.y;
    vec2 p = local.x * seaAxes.xy + local.y * seaAxes.zw;   // in the frame's axes
    float L0 = seaTiles.x, L1 = seaTiles.y, L2 = seaTiles.z, L3 = seaTiles.w;
    vec2 g0 = vec2(mpp / L0, 0.0), g1 = vec2(mpp / L1, 0.0), g2 = vec2(mpp / L2, 0.0), g3 = vec2(mpp / L3, 0.0);
    Hex H0 = hexSetup(p, seaHexOff.xy, seaHexS.x, 0.21, 1);
    Hex H1 = hexSetup(p, seaHexOff.zw, seaHexS.y, 0.35, 2);

    // 1) undo the choppy displacement: one fixed-point step, the hex cells taken at p and reused at q
    vec2 q = p;
    if (seaOn.z > 0.0)
    {
        vec2 D = textureGrad(seaD2, (p + seaOff23.xy) / L2, g2, g2.yx).xy + hexD(seaD0, H0, p, L0, g0) + hexD(seaD1, H1, p, L1, g1);
        q = p - D * seaOn.z;
    }

    // 2) the moments: mean slope and its variance, cascade by cascade
    vec4 a0 = hexM(seaM0, H0, q, L0, g0);
    vec4 a1 = hexM(seaM1, H1, q, L1, g1);
    float tl = seaHexS.z;
    vec4 a2 = mix(textureGrad(seaM2, (q + seaOff23.xy) / L2, g2, g2.yx), textureLod(seaM2, vec2(0.5), tl), smoothstep(48.0, 20.0, L2 / mpp));
    vec2 m = a0.xy + a1.xy + a2.xy;
    vec2 var = max(a0.zw - a0.xy * a0.xy, 0.0) + max(a1.zw - a1.xy * a1.xy, 0.0) + max(a2.zw - a2.xy * a2.xy, 0.0) + seaVarU.xy + 1e-4;
    if (seaVarU.z > 0.0)
    {
        // the ripples: cascade 2 again at 1/7.93 scale, taking half of what's unresolved; resolved, it moves from the
        // variance into the mean slope
        vec4 a3 = mix(textureGrad(seaM2, (q + seaOff23.zw) / L3, g3, g3.yx), textureLod(seaM2, vec2(0.5), tl), smoothstep(48.0, 20.0, L3 / mpp));
        vec2 k3 = seaVarU.zw;
        m += a3.xy * k3;
        var += (max(a3.zw - a3.xy * a3.xy, 0.0) - seaMss2.xy) * k3 * k3;
    }
    var = max(var, vec2(1e-4));
    // into the pixel's frame (the turn between the frame and the camera is the meridians' convergence, a few degrees
    // at most: the variance's axes are left as they are)
    m = vec2(dot(m, seaAxes.xy), dot(m, seaAxes.zw));
    // the wakes: their slopes on the sea's, their unresolved variance split evenly between the axes; a torpedo track's
    // slick takes some away (negative), never below a quarter of the sea's own
    m += wake.xy;
    var = max(var + vec2(0.5 * wake.z), 0.25 * var);

    if (seaOn.w > 0.5)
    {
        int view = int(seaOn.w + 0.5);
        vec3 nrm = normalize(vec3(-m, 1.0));
        if (view == 3)   // 3 the wakes alone: their slopes, red right and green up, blue their spread
            return vec3(0.5 + wake.xy * 2.5, sqrt(wake.z) * 3.0);
        if (view == 4)   // 4 the foam's coverage
            return vec3(wake.w);
        return view == 1 ? nrm * 0.5 + 0.5 : vec3(sqrt(var) * 3.0, 0.0);   // 1 the normal, 2 the slopes' spread
    }

    // 3) light: the body, the sky's reflection, the glints
    vec3 n = normalize(vec3(-m, 1.0));
    float F = seaFresnel(dot(v, n));
    vec3 r = reflect(-v, n);
    // the body's light: what the surface lets in (cosine and 1 - Fresnel), part by the facet it falls on, part by a
    // level sea (it diffuses under the surface). The facet's part is what shows the waves when the sun is too low
    // for its glint to reach a view from above. In time-lapse it fades with the glints (the sun would spin).
    float k = seaBody.w * seaMss2.z;
    vec3 nb = normalize(vec3(-m * seaMoon.w, 1.0));
    vec3 body = seaBody.rgb * (sunRad.rgb * mix(seaLetIn(vec3(0.0, 0.0, 1.0), lSun), seaLetIn(nb, lSun), k)
                             + moonRad.rgb * mix(seaLetIn(vec3(0.0, 0.0, 1.0), lMoon), seaLetIn(nb, lMoon), k) + skyAmb.rgb);
    // the night's light (tuned: a real night sea from above is black and still), all by the facet, with its own relief
    if (seaNight.r + seaNight.g + seaNight.b > 0.0)
    {
        vec3 nn = normalize(vec3(-m * seaNight.w, 1.0));
        body += seaBody.rgb * seaNight.rgb * mix(seaLetIn(vec3(0.0, 0.0, 1.0), lNight), seaLetIn(nn, lNight), seaMss2.z);
    }
    // the flashes: each a point light, so its direction is the pixel's own; on the glints and the foam alone (in the
    // water's body their orange came out a teal glow under the guns)
    vec3 fGlint = vec3(0.0), fLevel = vec3(0.0);
    int nf = flashCount();
    for (int i = 0; i < nf; i++)
    {
        vec3 lw;
        vec3 rad = flashAt(i, pEye, lw);
        vec3 l = vec3(dot(lw, rt), dot(lw, upv), dot(lw, nrm));
        fGlint += rad * seaGlint(l, v, m, var);
        fLevel += rad * max(l.z, 0.0);
    }
    vec3 c = (1.0 - F) * body + F * skyAt(r, vec3(0.0, 0.0, 1.0), 0.0);
    c += (sunRad.rgb * seaGlint(lSun, v, m, var) + moonRad.rgb * seaGlint(lMoon, v, m, var) + fGlint) * seaMss2.z;
    // the foam, lit as a level surface by the sun, the moon, the sky and the night's light: the aerated water under it
    // brightens the water, the surface foam lays a translucent white over it (hiding most of the glint)
    float foam = clamp(wake.w, 0.0, 1.0);
    if (foam > 0.0)
    {
        vec3 lit = sunRad.rgb * max(lSun.z, 0.0) + moonRad.rgb * max(lMoon.z, 0.0) + skyAmb.rgb + seaNight.rgb * max(lNight.z, 0.0) + fLevel;
        float aerated = min(foam / 0.4, 1.0), surface = max((foam - 0.4) / 0.6, 0.0);
        c += aerated * AeratedAlbedo * AeratedTint * lit;
        c = mix(c, FoamAlbedo * lit, surface * FoamCover);
    }
    return linearToSrgb(shoulder(c, look.z));
}
