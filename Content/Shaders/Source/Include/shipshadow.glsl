// Ship shadows, shared by the map's and the viewer's sprite and shadow shaders (docs/visuals/ship-lighting.md, step
// S): the hull's shadow from its height map, and where a turret's roof shades a point. Grey x 0.25 m in the height
// map is the height above the waterline. The shadows' light (the stronger of sun and moon) comes as toLight, a unit
// vector toward it on the deck (ship-local), and tanEl, the tangent of its elevation. A ship settled, heeled or
// trimmed (sea: the sea's plane over its waterline in its own frame, z at the origin and the slopes along and across;
// w the tallest column over the waterline, m, when on, 0 when off) casts and takes its shadows by the heights over
// that plane: what's under it casts none, and the sea's surface over a drowned deck is what takes them (the light's
// direction stays the level one: close enough).

// The height map at ship-local metres q (hmap: uv = q * xy + zw), 0 off the map.
float shadowHeight(sampler2D heightTex, vec4 hmap, vec2 q, float lod)
{
    vec2 huv = q * hmap.xy + hmap.zw;
    if (any(lessThan(huv, vec2(0.0))) || any(greaterThan(huv, vec2(1.0))))
        return 0.0;
    return textureLod(heightTex, huv, lod).r * 63.75;
}

// The sea's plane over the waterline at ship-local q (0 when off).
float seaAt(vec4 sea, vec2 q)
{
    return sea.w > 0.5 ? sea.x + dot(sea.yz, q) : 0.0;
}

// How high a point h over the waterline (by the height map: 0 is off the hull, the sea) stands over the sea's plane at
// q: the sea's own surface, and whatever is under it, at 0.
float overSea(vec4 sea, vec2 q, float h)
{
    return h > 0.0 ? max(h - seaAt(sea, q), 0.0) : 0.0;
}

// The most steps shownAt's search takes.
const int ShownSteps = 96;

// Which point of a hull heeled and trimmed shows at ship-local q, and the height its view's ray meets it at (under the
// point's own on a wall: a deck edge seen from the side): a point h over the waterline shows h times the sea's slope
// toward the low end and side, so the points that could show at q lie along q - h * slope, and the one that shows is
// the highest. The ray is walked down from the tallest column (sea.w) in steps of about stepM (a screen pixel or a
// height map texel, whichever is larger) across the map, then the edge it meets is narrowed down. Several can solve it
// where the slope is steep (the deck and the sea beside it, a bridge and the deck under it): taking any but the highest
// shows the ship twice.
vec2 shownAt(sampler2D heightTex, vec4 hmap, vec2 q, vec4 sea, float stepM, out float hit)
{
    hit = 0.0;
    float shift = sea.w * length(sea.yz);
    if (sea.w <= 0.5 || shift < 1e-4)
    {
        hit = shadowHeight(heightTex, hmap, q, 0.0);
        return q;
    }
    vec2 texel = 1.0 / (abs(hmap.xy) * vec2(textureSize(heightTex, 0)));
    int n = clamp(int(ceil(shift / max(stepM, min(texel.x, texel.y)))), 1, ShownSteps);
    float dh = sea.w / float(n), lo = 0.0, hi = sea.w;   // lo meets the hull or the sea (h 0 always does), hi is clear
    for (int i = 1; i <= n; i++)
    {
        float h = sea.w - float(i) * dh;
        if (shadowHeight(heightTex, hmap, q - h * sea.yz, 0.0) >= h)
        {
            lo = h;
            break;
        }
        hi = h;
    }
    for (int i = 0; i < 6; i++)
    {
        float h = 0.5 * (lo + hi);
        if (shadowHeight(heightTex, hmap, q - h * sea.yz, 0.0) >= h)
            lo = h;
        else
            hi = h;
    }
    hit = lo;
    return q - lo * sea.yz;
}

vec2 shownAt(sampler2D heightTex, vec4 hmap, vec2 q, vec4 sea, float stepM)
{
    float hit;
    return shownAt(heightTex, hmap, q, sea, stepM, hit);
}

// How much the hull shades a point q standing hp over the waterline: marching from it toward the light, a column
// standing higher than the light's ray, both over the sea's plane. march: x step, m; y steps; z softness, m; w the
// height map's mip level. Only the steps where the ray crosses the height map are taken (off it, nothing stands).
float hullShadow(sampler2D heightTex, vec4 hmap, vec2 q, float hp, vec2 toLight, float tanEl, vec4 march, vec4 sea)
{
    float over = overSea(sea, q, hp);
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
        vec2 at = q + toLight * t;
        float occ = overSea(sea, at, shadowHeight(heightTex, hmap, at, march.w));
        shade = max(shade, clamp((occ - (over + t * tanEl)) / march.z, 0.0, 1.0));
    }
    return shade;
}

float hullShadow(sampler2D heightTex, vec4 hmap, vec2 q, float hp, vec2 toLight, float tanEl, vec4 march)
{
    return hullShadow(heightTex, hmap, q, hp, toLight, tanEl, march, vec4(0.0));
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
