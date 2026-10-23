#version 450
// Map lines: the stroke's edge faded over a pixel; dashes or round dots along it; a dark casing under it, as wide as
// the vertex says (docs/map-ui/symbols.md Y2). A fill's vertices carry an edge far away and no style.
layout(location = 0) in vec4 fragEdge;
layout(location = 1) in vec4 fragColor;
layout(location = 2) in vec4 fragStyle;
layout(location = 0) out vec4 outColor;

const vec3 CasingRgb = vec3(0.035, 0.05, 0.06);   // the ground's, darker (docs/map-ui/README.md "Design tokens")

void main()
{
    float d = abs(fragEdge.x), h = fragEdge.y, along = fragEdge.z, casing = fragEdge.w;
    float a = clamp(h + 0.5 - d, 0.0, 1.0);
    float c = clamp(h + casing + 0.5 - d, 0.0, 1.0);
    float period = fragStyle.x, share = fragStyle.y;
    if (period > 0.0)
    {
        float t = mod(along, period);
        if (share < 0.0)
        {
            // round dots, as wide as the line, at the middle of each period
            float r = length(vec2(t - period * 0.5, d));
            a = clamp(h + 0.5 - r, 0.0, 1.0);
            c = clamp(h + casing + 0.5 - r, 0.0, 1.0);
        }
        else
        {
            // inside a dash: its distance to the dash's nearer end; in a gap: minus the distance to the nearer dash
            float on = period * share;
            float e = t < on ? min(t, on - t) : -min(t - on, period - t);
            a *= clamp(e + 0.5, 0.0, 1.0);
            c *= clamp(e + casing + 0.5, 0.0, 1.0);
        }
    }
    float sa = fragColor.a * a, ca = casing > 0.0 ? fragColor.a * fragStyle.z * c : 0.0;
    float oa = sa + ca * (1.0 - sa);
    vec3 rgb = oa > 0.0 ? (fragColor.rgb * sa + CasingRgb * ca * (1.0 - sa)) / oa : fragColor.rgb;
    outColor = vec4(rgb, oa);
}
