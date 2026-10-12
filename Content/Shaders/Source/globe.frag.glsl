#version 450
// The globe of water (GlobeRenderer): each pixel's ray against the sphere, in units of the Earth's radius with the
// camera at the origin. The one large quantity, |eye|² - 1, comes from doubles on the CPU, and the nearer root is
// taken without cancellation, so the hit holds from orbit down to a few metres over the sea. Nothing here places
// anything that must stay put (that's done in double on the CPU): it shades plain water and the land, day and night
// from the sun, and the limb with its glow. Where the land is comes from the winding count LandView's fill left in
// the coverage target, pixel for pixel.
layout(location = 0) in vec2 fragNdc;
layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform sampler2D coverage;   // the land's winding number per pixel

layout(set = 3, binding = 0) uniform Globe
{
    vec4 right;    // xyz: the camera's right, Earth-fixed axes; w: tan of half the horizontal field
    vec4 up;       // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;      // xyz: the camera's forward; w: the eye's distance from the centre, in radii
    vec4 eyeDir;   // xyz: unit vector from the centre to the eye; w: |eye|² - 1, in radii², exact
    vec4 sun;      // xyz: unit vector toward the sun; w: the sun's motion, 1 live, 0 time-lapse (SunLapse)
    vec4 depth;    // x: |eye|² - r², r the masking sphere's radius (2 km inside), radii²; y: near, z: far, radii;
                   // w: a pixel's angle at the centre, radians
};

const vec3 Space = vec3(0.006, 0.008, 0.016);
const vec3 DaySea = vec3(0.13, 0.31, 0.43);
const vec3 NightSea = vec3(0.012, 0.025, 0.05);
const vec3 Glow = vec3(0.30, 0.50, 0.85);
const vec3 DayLand = vec3(0.34, 0.36, 0.27);
const vec3 NightLand = vec3(0.022, 0.026, 0.022);
const vec3 Track = vec3(1.0, 0.78, 0.45);

// The time-lapse sun (SunLapse.DayAverage, the same sum): over one turn of the Earth at a latitude, the mean of the
// terminator's smoothstep(lo, hi, s) (x) and of that times max(s, 0) (y). Sweeping too fast to watch, the sun becomes
// this: a function of latitude and the season alone, so nothing moves faster than the year.
vec2 dayAverage(float sinLat, float sinDec, float lo, float hi)
{
    float a = sinLat * sinDec, b = sqrt(max(1.0 - sinLat * sinLat, 0.0)) * sqrt(max(1.0 - sinDec * sinDec, 0.0));
    vec2 sum = vec2(0.0);
    for (int i = 0; i < 32; i++)
    {
        float s = a + b * cos((float(i) + 0.5) * (6.2831853 / 32.0));
        float day = smoothstep(lo, hi, s);
        sum += vec2(day, day * max(s, 0.0));
    }
    return sum / 32.0;
}

void main()
{
    vec3 d = normalize(fwd.xyz + fragNdc.x * right.w * right.xyz + fragNdc.y * up.w * up.xyz);
    float D = fwd.w, c = eyeDir.w;
    float b = D * dot(eyeDir.xyz, d);   // the eye · the ray
    float disc = b * b - c;             // 1 - (the ray's closest approach to the centre)²
    float fw = max(fwidth(disc), 1e-12);   // derivatives only in uniform control flow, so here

    // outside the disc: space, and the air's glow over the limb on the sunlit side
    vec3 outside = Space;
    if (b < 0.0)
    {
        float closest = sqrt(max(1.0 - disc, 0.0));
        vec3 nearest = normalize(D * eyeDir.xyz - b * d);
        float lit = mix(dayAverage(nearest.z, sun.z, -0.35, 0.35).x, smoothstep(-0.35, 0.35, dot(nearest, sun.xyz)), sun.w);
        outside +=Glow * exp(-max(closest - 1.0, 0.0) * 60.0) * (0.15 + 0.85 * lit) * 0.6;
    }

    // on the disc: the sea, lit by the sun, with a soft terminator (about civil twilight) and a glint
    vec3 sea = outside;
    float cover = 0.0;
    if (b < 0.0 && disc > -4.0 * fw)
    {
        float t = c / (-b + sqrt(max(disc, 0.0)));
        vec3 n = normalize(D * eyeDir.xyz + t * d);
        float s = dot(n, sun.xyz);
        float day = smoothstep(-0.10, 0.12, s);
        float land = clamp(abs(texelFetch(coverage, ivec2(gl_FragCoord.xy), 0).r), 0.0, 1.0);
        vec3 col = mix(NightSea, DaySea * (0.6 + 0.4 * max(s, 0.0)), day);
        col += vec3(1.0, 0.95, 0.85) * pow(max(dot(reflect(d, n), sun.xyz), 0.0), 150.0) * 0.35 * day * sun.w;
        col = mix(col, mix(NightLand, DayLand * (0.55 + 0.45 * max(s, 0.0)), day), land);
        if (sun.w < 1.0)
        {
            // time-lapse: each colour's mean over the day (mix is linear in day), and the sun's track along its
            // parallel, like a long exposure; it fades as it grows wider than the view can hold
            vec2 avg = dayAverage(n.z, sun.z, -0.10, 0.12);
            vec3 sea0 = NightSea * (1.0 - avg.x) + DaySea * (0.6 * avg.x + 0.4 * avg.y);
            vec3 land0 = NightLand * (1.0 - avg.x) + DayLand * (0.55 * avg.x + 0.45 * avg.y);
            vec3 lapse = mix(sea0, land0, land);
            float off = (asin(clamp(n.z, -1.0, 1.0)) - asin(clamp(sun.z, -1.0, 1.0))) / 0.0035;   // 0.2° core
            float halo = off * (0.0035 / 0.035);                                                   // 2° halo
            float vis = 1.0 - smoothstep(60.0, 200.0, 0.035 / depth.w);
            lapse += Track * (0.35 * exp(-off * off) + 0.08 * exp(-halo * halo)) * vis;
            col = mix(lapse, col, sun.w);
            day = mix(avg.x, day, sun.w);
        }
        col += Glow * pow(1.0 - clamp(dot(n, -d), 0.0, 1.0), 4.0) * (0.1 + 0.4 * day);
        sea = col;
        cover = clamp(disc / fw + 0.5, 0.0, 1.0);
    }
    outColor = vec4(mix(outside, sea, cover), 1.0);

    // depth: the masking sphere's, as spheredepth.frag, so the baked coast behind the globe is hidden
    float cs = depth.x, discS = b * b - cs, zd = 1.0;
    if (b < 0.0 && discS >= 0.0)
    {
        float ts = cs / (-b + sqrt(discS)), zv = ts * dot(d, fwd.xyz);
        zd = depth.z * (zv - depth.y) / (zv * (depth.z - depth.y));
    }
    gl_FragDepth = clamp(zd, 0.0, 1.0);
}
