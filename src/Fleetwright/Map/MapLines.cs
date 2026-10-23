using System.Numerics;
using System.Runtime.InteropServices;

namespace Fleetwright.Map;

/// <summary>One vertex of a map line: NDC position; signed distance from the line's centre, half width, distance along
/// the strip and the casing's width (px); colour (straight alpha); and the strip's style: the dash period (px, 0
/// solid), the dash's share of it (negative: round dots) and the casing's alpha.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LineVertex
{
    public Vector2 Pos;
    public Vector4 Edge;
    public Vector4 Color;
    public Vector4 Style;

    public const uint SizeInBytes = 56;
}

/// <summary>A stroke's pattern (docs/map-ui/symbols.md Y2).</summary>
public enum Dash : byte
{
    Solid,
    Dashed,
    Dotted,
}

/// <summary>Anti-aliased polylines in a target's pixels, expanded on the CPU into triangles with mitred joins (no
/// overlaps, so a faint line has no brighter dots at its joints). Points come in already projected, in double through
/// <see cref="MapView"/>, so a line on the globe stays put at any zoom. A point may carry its own colour and width, so a
/// strip can taper and fade (the battle's streaks); a strip may be dashed or dotted and cased in the ground's dark
/// (<see cref="BeginStrip"/>, Y2); <see cref="Disc"/> is a soft round glow. Curves are split by their
/// chord error on the screen (<see cref="Segments"/>, docs/map-ui/symbols.md Y1), and a strip fed through
/// <see cref="ClipPoint"/> keeps only what's on the screen. The arrays are kept between frames: building the same lines
/// again allocates nothing.</summary>
public sealed class MapLines
{
    LineVertex[] verts = new LineVertex[1 << 15];
    Vector2[] strip = new Vector2[1024];
    Vector4[] stripColor = new Vector4[1024];
    float[] stripHalf = new float[1024];
    float[] stripAlong = new float[1024];   // given, or NaN: measured from the point before
    int count, stripCount;
    double width = 1, height = 1;
    Vector4 color;
    float half;
    bool closed;
    float period, share, casing;   // the strip's style, target px (casing: its width)

    /// <summary>Target pixels a width's pixel: <see cref="SymbolLook.Px"/> for the map's symbols, 1 for the grid's lines.</summary>
    public float WidthScale = 1;

    /// <summary>Target pixels a chord error's reference pixel (<see cref="SymbolLook.Chord"/>).</summary>
    public float ChordScale = 1;

    public ReadOnlySpan<LineVertex> Vertices => verts.AsSpan(0, count);

    /// <summary>Starts a frame's lines for a target of this size.</summary>
    public void Begin(double targetWidth, double targetHeight)
    {
        (width, height) = (Math.Max(targetWidth, 1), Math.Max(targetHeight, 1));
        count = stripCount = 0;
        (closed, clipHave) = (false, false);
    }

    /// <summary>Starts a polyline of a colour and a width in pixels, solid, dashed or dotted (round dots as wide as the
    /// line), with or without a casing (<see cref="SymbolLook.Casing"/>: a dark rim either side that keeps it legible
    /// over smoke, land or another line).</summary>
    public void BeginStrip(Vector4 rgba, float widthPx, Dash dash = Dash.Solid, bool cased = false)
    {
        End();
        (color, half) = (rgba, widthPx / 2);
        clipHave = false;
        (period, share) = dash switch
        {
            Dash.Dashed => (Math.Max(9f, 6 * widthPx) * WidthScale, 0.6f),
            Dash.Dotted => (Math.Max(5f, 3 * widthPx) * WidthScale, -1f),
            _ => (0f, 0f),
        };
        casing = cased ? SymbolLook.Casing * WidthScale : 0;
    }

    /// <summary>The next point, in pixels (x right, y down).</summary>
    public void Point(double x, double y) => Point(x, y, color, half * 2);

    /// <summary>The next point with its own colour and width: the strip blends between its points.</summary>
    public void Point(double x, double y, Vector4 rgba, float widthPx) => PointAt(x, y, rgba, widthPx, float.NaN);

    /// <summary>A point with its distance along the strip given (px): a ring's dashes stay put on it as the screen's edge
    /// cuts it in different places.</summary>
    void PointAt(double x, double y, Vector4 rgba, float widthPx, float along)
    {
        if (stripCount == strip.Length)
        {
            Array.Resize(ref strip, strip.Length * 2);
            Array.Resize(ref stripColor, strip.Length);
            Array.Resize(ref stripHalf, strip.Length);
            Array.Resize(ref stripAlong, strip.Length);
        }
        stripColor[stripCount] = rgba;
        stripHalf[stripCount] = widthPx * WidthScale / 2;
        stripAlong[stripCount] = along;
        strip[stripCount++] = new Vector2((float)x, (float)y);
    }

    /// <summary>A soft round glow of radius r px: <paramref name="rgba"/> at the centre, half its alpha at
    /// <paramref name="core"/> of the radius, none at the rim. Ends any strip.</summary>
    public void Disc(double x, double y, double r, Vector4 rgba, float core = 0.45f)
    {
        End();
        int segs = Math.Clamp(Segments(r) / 2, 6, 48);   // a soft edge hides twice the chord error
        if (count + segs * 9 > verts.Length)
            Array.Resize(ref verts, Math.Max(verts.Length * 2, count + segs * 9));
        var c = new Vector2((float)x, (float)y);
        Vector4 mid = rgba with { W = rgba.W * 0.5f }, rim = rgba with { W = 0 };
        var prev = new Vector2((float)r, 0);
        for (int k = 1; k <= segs; k++)
        {
            double a = k * Math.PI * 2 / segs;
            var next = new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
            // centre to the core ring, then the core ring out to the rim
            Solid(c, rgba);
            Solid(c + prev * core, mid);
            Solid(c + next * core, mid);
            Solid(c + prev * core, mid);
            Solid(c + prev, rim);
            Solid(c + next, rim);
            Solid(c + prev * core, mid);
            Solid(c + next, rim);
            Solid(c + next * core, mid);
            prev = next;
        }
    }

    /// <summary>Ends the polyline (a break in it: the next points start a new one in the same style).</summary>
    public void End()
    {
        if (stripCount >= 2)
            Expand();
        stripCount = 0;
        closed = false;
    }

    /// <summary>Ends a polyline whose last point is its first: the seam is joined like any other corner (an unbroken
    /// ring; one the screen's edge broke ends as an open one).</summary>
    public void EndClosed()
    {
        closed = stripCount >= 4 && Vector2.DistanceSquared(strip[0], strip[stripCount - 1]) < 1e-4f;
        End();
    }

    /// <summary>How many pieces a whole circle of radius r target px needs for its chord error to stay under
    /// <see cref="SymbolLook.Chord"/>: about π√(r/2ε), so a ring ten times larger takes about three times the points.</summary>
    public int Segments(double radiusPx)
    {
        double e = SymbolLook.Chord * ChordScale;
        if (radiusPx <= e)
            return 8;
        return Math.Clamp((int)Math.Ceiling(Math.PI / Math.Acos(1 - e / radiusPx)), 8, 4096);
    }

    /// <summary>A ring of radius r px round (x, y), as smooth as <see cref="Segments"/> says; only its arc on the screen
    /// is built.</summary>
    public void Circle(double x, double y, double r, Vector4 rgba, float widthPx, Dash dash = Dash.Solid, bool cased = false)
    {
        if (!CircleShows(x, y, r))
            return;
        BeginRing(rgba, widthPx, r, dash, cased);
        int n = Segments(r);
        for (int i = 0; i <= n; i++)
        {
            double a = i * Math.PI * 2 / n;
            ClipPoint(x + Math.Cos(a) * r, y + Math.Sin(a) * r, a * r);
        }
        EndClosed();
    }

    /// <summary>Starts a ring of radius r px (its points given their distance along it, r times their angle, through
    /// <see cref="ClipPoint(double, double, double)"/>): as <see cref="BeginStrip"/>, its dashes fitted to it (a whole
    /// number of them, no short one at the seam).</summary>
    public void BeginRing(Vector4 rgba, float widthPx, double radiusPx, Dash dash = Dash.Solid, bool cased = false)
    {
        BeginStrip(rgba, widthPx, dash, cased);
        double around = 2 * Math.PI * radiusPx;
        if (period > 0)
            period = (float)(around / Math.Max(1, Math.Round(around / period)));
    }

    /// <summary>Whether any of a ring of radius r px round (x, y) is on the screen (and its margin): not when it's
    /// off it, nor when it's round the whole of it.</summary>
    public bool CircleShows(double x, double y, double r)
    {
        const double M = 64;
        if (x + r < -M || y + r < -M || x - r > width + M || y - r > height + M)
            return false;
        double fx = Math.Max(Math.Abs(x), Math.Abs(x - width)), fy = Math.Max(Math.Abs(y), Math.Abs(y - height));
        return r <= Math.Sqrt(fx * fx + fy * fy) + M;
    }

    // the screen clip's last point: whether there is one, whether it was on the screen, where, and how far along
    bool clipHave, clipPrevIn;
    double clipX, clipY, clipAlong;

    /// <summary>Whether the clipped strip has a point yet.</summary>
    public bool ClipHave => clipHave;

    /// <summary>The next point of a strip clipped to the screen (and a margin): off it the strip breaks, keeping the point
    /// just past the edge; a piece with both ends off is drawn alone if it may cross the screen. A ring's points give
    /// their distance along it (<see cref="BeginRing"/>); NaN measures it from the point before.</summary>
    public void ClipPoint(double x, double y, double along = double.NaN)
    {
        const double M = 64;
        bool inside = x > -M && y > -M && x < width + M && y < height + M;
        float w = half * 2;
        if (inside)
        {
            if (clipHave && !clipPrevIn)
                PointAt(clipX, clipY, color, w, (float)clipAlong);
            PointAt(x, y, color, w, (float)along);
        }
        else if (clipHave && clipPrevIn)
        {
            PointAt(x, y, color, w, (float)along);
            End();
        }
        else if (clipHave && Math.Max(clipX, x) > -M && Math.Min(clipX, x) < width + M && Math.Max(clipY, y) > -M && Math.Min(clipY, y) < height + M)
        {
            PointAt(clipX, clipY, color, w, (float)clipAlong);   // a long piece across the screen, both ends off it
            PointAt(x, y, color, w, (float)along);
            End();
        }
        (clipHave, clipPrevIn, clipX, clipY, clipAlong) = (true, inside, x, y, along);
    }

    /// <summary>A break in a clipped strip (a point that didn't project): the next point starts afresh.</summary>
    public void ClipBreak()
    {
        End();
        clipHave = false;
    }

    void Expand()
    {
        int segs = stripCount - 1;
        if (count + segs * 6 > verts.Length)
            Array.Resize(ref verts, Math.Max(verts.Length * 2, count + segs * 6));
        Vector2 prevL = default, prevR = default;
        float prevReach = 0, prevHalf = 0, prevAlong = 0;
        Vector4 prevColor = default;
        for (int i = 0; i < stripCount; i++)
        {
            var p = strip[i];
            float h = stripHalf[i], reach = h + casing + 1;   // the casing, and a pixel of fade beyond it
            float along = stripAlong[i];
            if (float.IsNaN(along))
                along = i == 0 ? 0 : prevAlong + Vector2.Distance(strip[i - 1], p);
            var col = stripColor[i];
            // a closed ring's seam: its ends joined through each other
            var dIn = i > 0 ? Dir(strip[i - 1], p) : closed ? Dir(strip[stripCount - 2], p) : Dir(p, strip[i + 1]);
            var dOut = i < stripCount - 1 ? Dir(p, strip[i + 1]) : closed ? Dir(p, strip[1]) : dIn;
            var nIn = new Vector2(-dIn.Y, dIn.X);
            var n = new Vector2(-(dIn.Y + dOut.Y), dIn.X + dOut.X);
            float len = n.Length();
            n = len > 1e-6f ? n / len : nIn;
            float miter = MathF.Min(1 / MathF.Max(Vector2.Dot(n, nIn), 0.25f), 4);
            var l = p + n * (reach * miter);
            var r = p - n * (reach * miter);
            if (i > 0)
            {
                Add(prevL, prevReach, prevHalf, prevAlong, prevColor);
                Add(prevR, -prevReach, prevHalf, prevAlong, prevColor);
                Add(l, reach, h, along, col);
                Add(prevR, -prevReach, prevHalf, prevAlong, prevColor);
                Add(r, -reach, h, along, col);
                Add(l, reach, h, along, col);
            }
            (prevL, prevR, prevReach, prevHalf, prevColor, prevAlong) = (l, r, reach, h, col, along);
        }
    }

    static Vector2 Dir(Vector2 a, Vector2 b)
    {
        var d = b - a;
        float len = d.Length();
        return len > 1e-6f ? d / len : new Vector2(1, 0);
    }

    void Add(Vector2 px, float edge, float h, float along, Vector4 rgba) => verts[count++] = new LineVertex
    {
        Pos = new Vector2((float)(px.X / width * 2 - 1), (float)(1 - px.Y / height * 2)),
        Edge = new Vector4(edge, h, along, casing),
        Color = rgba,
        Style = new Vector4(period, share, SymbolLook.CasingAlpha, 0),
    };

    /// <summary>A vertex inside a fill: its edge far from the line's, so the shader's edge fade leaves it whole; no
    /// dashes, no casing.</summary>
    void Solid(Vector2 px, Vector4 rgba) => verts[count++] = new LineVertex
    {
        Pos = new Vector2((float)(px.X / width * 2 - 1), (float)(1 - px.Y / height * 2)),
        Edge = new Vector4(0, 1e4f, 0, 0),
        Color = rgba,
    };
}
