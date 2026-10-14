// Ship shadows, shared by the map's and the viewer's sprite and shadow shaders (docs/visuals/ship-lighting.md, step
// S): the hull's shadow from its height map, and where a turret's roof shades a point. Grey x 0.25 m in the height
// map is the height above the waterline. The shadows' light (the stronger of sun and moon) comes as toLight, a unit
// vector toward it on the deck (ship-local), and tanEl, the tangent of its elevation.

// The height map at ship-local metres q (hmap: uv = q * xy + zw), 0 off the map.
float shadowHeight(sampler2D heightTex, vec4 hmap, vec2 q, float lod)
{
    vec2 huv = q * hmap.xy + hmap.zw;
    if (any(lessThan(huv, vec2(0.0))) || any(greaterThan(huv, vec2(1.0))))
        return 0.0;
    return textureLod(heightTex, huv, lod).r * 63.75;
}

// How much the hull shades a point q standing hp over the waterline: marching from it toward the light, a column
// standing higher than the light's ray. march: x step, m; y steps; z softness, m; w the height map's mip level.
// Only the steps where the ray crosses the height map are taken (off it, nothing stands).
float hullShadow(sampler2D heightTex, vec4 hmap, vec2 q, float hp, vec2 toLight, float tanEl, vec4 march)
{
    // the ray in the map's uv: in [0, 1] for t between enter and leave
    vec2 p0 = q * hmap.xy + hmap.zw, d = toLight * hmap.xy;
    vec2 inv = 1.0 / (abs(d) + 1e-9) * sign(d + 1e-12);
    vec2 ta = -p0 * inv, tb = (1.0 - p0) * inv;
    float enter = max(max(min(ta.x, tb.x), min(ta.y, tb.y)), 0.0);
    float leave = min(max(ta.x, tb.x), max(ta.y, tb.y));
    float shade = 0.0;
    int n = min(int(march.y), int(floor(leave / march.x)));
    for (int i = max(1, int(ceil(enter / march.x))); i <= n && shade < 1.0; i++)
    {
        float t = float(i) * march.x;
        float occ = shadowHeight(heightTex, hmap, q + toLight * t, march.w);
        shade = max(shade, clamp((occ - (hp + t * tanEl)) / march.z, 0.0, 1.0));
    }
    return shade;
}

// A point in a turret's uv: frame xy its uv (0, 0) corner, zw its u edge, both ship metres (the v edge is u turned
// +90°, as the turrets' quads are made).
vec2 turretUv(vec2 q, vec4 frame)
{
    vec2 d = q - frame.xy, u = frame.zw;
    return vec2(dot(d, u), dot(d, vec2(-u.y, u.x))) / dot(u, u);
}

// Where a turret's roof shades a receiver at q standing h over the waterline: the ray from it toward the light reaches
// the roof's height (roof - h) / tanEl along; that point in the turret's uv. Only meaningful for h < roof.
vec2 turretShadowUv(vec2 q, float h, float roof, vec2 toLight, float tanEl, vec4 frame)
{
    return turretUv(q + toLight * ((roof - h) / tanEl), frame);
}

// A turret's shadow's alpha over what the hull's shadow (hs) already darkens there, so the two come out as the darker
// of them rather than their product: both by the shadow's opacity.
float shadowOver(float turret, float hs, float opacity)
{
    return opacity * (max(turret, hs) - hs) / max(1.0 - opacity * hs, 1e-3);
}
