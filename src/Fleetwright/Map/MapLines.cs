using System.Numerics;
using System.Runtime.InteropServices;

namespace Fleetwright.Map;

/// <summary>One vertex of a map line: NDC position, signed distance from the line's centre and half width (px), and
/// colour (straight alpha).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LineVertex
{
    public Vector2 Pos;
    public Vector2 Edge;
    public Vector4 Color;

    public const uint SizeInBytes = 32;
}

/// <summary>Anti-aliased polylines in a target's pixels, expanded on the CPU into triangles with mitred joins (no
/// overlaps, so a faint line has no brighter dots at its joints). Points come in already projected, in double through
/// <see cref="MapView"/>, so a line on the globe stays put at any zoom. The arrays are kept between frames: building
/// the same lines again allocates nothing.</summary>
public sealed class MapLines
{
    LineVertex[] verts = new LineVertex[1 << 15];
    Vector2[] strip = new Vector2[1024];
    int count, stripCount;
    double width = 1, height = 1;
    Vector4 color;
    float half;

    public ReadOnlySpan<LineVertex> Vertices => verts.AsSpan(0, count);

    /// <summary>Starts a frame's lines for a target of this size.</summary>
    public void Begin(double targetWidth, double targetHeight)
    {
        (width, height) = (Math.Max(targetWidth, 1), Math.Max(targetHeight, 1));
        count = stripCount = 0;
    }

    /// <summary>Starts a polyline of a colour and a width in pixels.</summary>
    public void BeginStrip(Vector4 rgba, float widthPx)
    {
        End();
        (color, half) = (rgba, widthPx / 2);
    }

    /// <summary>The next point, in pixels (x right, y down).</summary>
    public void Point(double x, double y)
    {
        if (stripCount == strip.Length)
            Array.Resize(ref strip, strip.Length * 2);
        strip[stripCount++] = new Vector2((float)x, (float)y);
    }

    /// <summary>Ends the polyline (a break in it: the next points start a new one in the same style).</summary>
    public void End()
    {
        if (stripCount >= 2)
            Expand();
        stripCount = 0;
    }

    void Expand()
    {
        int segs = stripCount - 1;
        if (count + segs * 6 > verts.Length)
            Array.Resize(ref verts, Math.Max(verts.Length * 2, count + segs * 6));
        float reach = half + 1;   // a pixel of fade beyond the line's edge
        Vector2 prevL = default, prevR = default;
        for (int i = 0; i < stripCount; i++)
        {
            var p = strip[i];
            var dIn = i > 0 ? Dir(strip[i - 1], p) : Dir(p, strip[i + 1]);
            var dOut = i < stripCount - 1 ? Dir(p, strip[i + 1]) : dIn;
            var nIn = new Vector2(-dIn.Y, dIn.X);
            var n = new Vector2(-(dIn.Y + dOut.Y), dIn.X + dOut.X);
            float len = n.Length();
            n = len > 1e-6f ? n / len : nIn;
            float miter = MathF.Min(1 / MathF.Max(Vector2.Dot(n, nIn), 0.25f), 4);
            var l = p + n * (reach * miter);
            var r = p - n * (reach * miter);
            if (i > 0)
            {
                Add(prevL, reach);
                Add(prevR, -reach);
                Add(l, reach);
                Add(prevR, -reach);
                Add(r, -reach);
                Add(l, reach);
            }
            (prevL, prevR) = (l, r);
        }
    }

    static Vector2 Dir(Vector2 a, Vector2 b)
    {
        var d = b - a;
        float len = d.Length();
        return len > 1e-6f ? d / len : new Vector2(1, 0);
    }

    void Add(Vector2 px, float edge) => verts[count++] = new LineVertex
    {
        Pos = new Vector2((float)(px.X / width * 2 - 1), (float)(1 - px.Y / height * 2)),
        Edge = new Vector2(edge, half),
        Color = color,
    };
}
