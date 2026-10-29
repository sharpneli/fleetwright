#version 450
// Map symbols (docs/map-ui/symbols.md Y3): the shape as a signed distance in pixels, so every edge and corner is
// anti-aliased over a pixel at any size. Drawn back to front: the casing (the ground's dark, either side of the
// outline), the fill inside, the stroke on the outline. Shapes: 0 circle, 1 diamond, 2 square, 3 arrowhead (its tip
// at +y), 4 wreck (a ring and a cross: lines only, no fill), 5 quatrefoil (four lobes: APP-6's unknown, docs/battle/
// night.md Q9).
layout(location = 0) in vec2 fragLocal;
layout(location = 1) flat in vec4 fragShape;
layout(location = 2) flat in vec4 fragFill;
layout(location = 3) flat in vec4 fragStroke;
layout(location = 4) flat in vec4 fragExtra;
layout(location = 0) out vec4 outColor;

const vec3 CasingRgb = vec3(0.035, 0.05, 0.06);   // as mapline.frag's

// iq's isosceles triangle: tip at the origin, base at y = q.y
float triangle(vec2 p, vec2 q)
{
    p.x = abs(p.x);
    vec2 a = p - q * clamp(dot(p, q) / dot(q, q), 0.0, 1.0);
    vec2 b = p - q * vec2(clamp(p.x / q.x, 0.0, 1.0), 1.0);
    float s = -sign(q.y);
    vec2 d = min(vec2(dot(a, a), s * (p.x * q.y - p.y * q.x)), vec2(dot(b, b), s * (p.y - q.y)));
    return -sqrt(d.x) * sign(d.y);
}

float segment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a, ba = b - a;
    return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0));
}

// over, straight alpha in, premultiplied out
vec4 over(vec4 top, vec4 under) { return top + under * (1.0 - top.a); }

void main()
{
    int shape = int(fragShape.x + 0.5);
    float r = fragShape.y, sw = fragShape.z, casing = fragShape.w;
    vec2 p = fragLocal;
    float d, line;   // d: signed distance to the outline (inside negative); line: distance to the stroke's centre
    if (shape == 1)
        d = (abs(p.x) + abs(p.y) - r) * 0.70710678;
    else if (shape == 2)
    {
        vec2 q = abs(p) - vec2(r * 0.85);
        d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
    }
    else if (shape == 3)
        d = triangle(vec2(p.x, r - p.y), vec2(0.62 * r, 1.7 * r));
    else if (shape == 5)
    {
        vec2 q = abs(p);
        d = min(length(q - vec2(0.48 * r, 0.0)), length(q - vec2(0.0, 0.48 * r))) - 0.52 * r;
    }
    else
        d = length(p) - r;
    line = abs(d);
    bool filled = true;
    if (shape == 4)
    {
        float k = r * 0.62;
        line = min(line, min(segment(p, vec2(-k, -k), vec2(k, k)), segment(p, vec2(-k, k), vec2(k, -k))));
        filled = false;
    }
    float inside = filled ? clamp(0.5 - d, 0.0, 1.0) : 0.0;
    float stroke = clamp(sw + 0.5 - line, 0.0, 1.0);
    float cased = casing > 0.0 ? clamp(sw + casing + 0.5 - line, 0.0, 1.0) : 0.0;
    float strength = max(fragStroke.a, fragFill.a);
    vec4 c = vec4(CasingRgb, 1.0) * (cased * fragExtra.x * strength);
    c = over(vec4(fragFill.rgb, 1.0) * (fragFill.a * inside), c);
    c = over(vec4(fragStroke.rgb, 1.0) * (fragStroke.a * stroke), c);
    // the pipeline blends straight alpha
    outColor = c.a > 0.0 ? vec4(c.rgb / c.a, c.a) : vec4(0.0);
}
