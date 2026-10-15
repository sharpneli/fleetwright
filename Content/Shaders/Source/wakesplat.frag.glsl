#version 450
// A wake's stamp (docs/visuals/wakes.md K3), drawn by shipmap.vert's quads into the wake target, which the sea's
// shading takes in (seashade.glsl): the ship's steady wave field as slopes along the camera's right and up (added, so
// two wakes superpose as linear waves do), and the slope variance the pixel can't resolve (added). Foam stays 0 here.
// The field comes from the two bakes round the ship's speed (WakeLibrary), the starboard half of each, η and |∇η|²
// in RG16F with mips. Each is read at the ship's point scaled about the stem by s = (its speed / the ship's)², which
// lines its crests up with the ship's (the wavelength goes as the speed squared), and its slopes times the bow waves'
// ratio and s. A turn bends the field astern of the pivot onto the arc the ship has come along.
layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragShip;
layout(location = 2) flat in vec4 fragMode;    // y: the stamp's opacity (the speed's fade, a sinking ship's)
layout(location = 3) flat in vec4 fragSun;     // x: the ship's stem; y: the path's curvature, 1/m, + to starboard; z: the pivot; w: bake a's weight
layout(location = 4) flat in vec4 fragMarch;   // bake a: X0, 1/(X1 - X0), 1/HalfY, the stem it was baked for
layout(location = 5) flat in vec4 fragHmap;    // bake b: the same
layout(location = 6) flat in vec4 fragTint;    // x, z: each bake's s; y, w: its slope gain (the bow waves' ratio times s)
layout(location = 7) flat in vec4 fragSea;     // the ship's +x (xy) and +y (zw) along the camera's right and up

layout(set = 2, binding = 0) uniform sampler2D wakeA;
layout(set = 2, binding = 1) uniform sampler2D wakeB;

layout(location = 0) out vec4 outWake;

// the point in a bake's texture: x about the stem, |y| (the bake is the starboard half)
vec2 bakeUv(vec2 p, vec4 g, float s)
{
    return vec2((g.w + (p.x - fragSun.x) * s - g.x) * g.y, abs(p.y) * s * g.z);
}

// a bake at uv: the mean slope along x and |y| (its own metres) from η's differences at the mip the footprint asks
// for, and the mean |∇η|² there (LEAN: their difference is the variance)
vec3 bake(sampler2D T, vec2 uv, vec2 du, vec2 dv, vec4 g)
{
    vec2 size = vec2(textureSize(T, 0));
    float lod = clamp(log2(max(max(length(du * size), length(dv * size)), 1e-6)), 0.0, 7.0);
    vec2 st = exp2(lod) / size;
    float ex = textureLod(T, uv + vec2(st.x, 0.0), lod).r - textureLod(T, uv - vec2(st.x, 0.0), lod).r;
    // across the track the field is mirrored: |v - step| reads the starboard texel for the port one
    float ey = textureLod(T, vec2(uv.x, uv.y + st.y), lod).r - textureLod(T, vec2(uv.x, abs(uv.y - st.y)), lod).r;
    float g2 = textureLod(T, uv, lod).g;
    vec2 slope = vec2(ex * g.y / (2.0 * st.x), ey * g.z / (2.0 * st.y));
    // outside the crop: nothing (the bake fades to nothing at its edges; clamping would smear the edge texels)
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(uv.y, 1.0);
    return vec3(slope, max(g2 - dot(slope, slope), 0.0)) * inside;
}

void main()
{
    // the turn: astern of the pivot the straight field sits on the arc y = -κx²/2
    vec2 p = fragShip;
    float back = min(p.x - fragSun.z, 0.0);
    p.y += 0.5 * fragSun.y * back * back;
    float side = p.y >= 0.0 ? 1.0 : -1.0;

    vec2 ua = bakeUv(p, fragMarch, fragTint.x), ub = bakeUv(p, fragHmap, fragTint.z);
    vec2 dua = dFdx(ua), dva = dFdy(ua), dub = dFdx(ub), dvb = dFdy(ub);
    vec3 a = bake(wakeA, ua, dua, dva, fragMarch);
    vec3 b = bake(wakeB, ub, dub, dvb, fragHmap);
    float wa = fragSun.w, wb = 1.0 - wa;
    vec2 slope = wa * fragTint.y * vec2(a.x, side * a.y) + wb * fragTint.w * vec2(b.x, side * b.y);
    float variance = wa * fragTint.y * fragTint.y * a.z + wb * fragTint.w * fragTint.w * b.z;

    // the ship's frame to the camera's right and up
    vec2 m = vec2(slope.x * fragSea.x + slope.y * fragSea.z, slope.x * fragSea.y + slope.y * fragSea.w);
    float op = fragMode.y;
    outWake = vec4(m * op, variance * op, 0.0);
}
