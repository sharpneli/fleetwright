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
/// <see cref="MapView"/>, so a line on the globe stays put at any zoom. A point may carry its own colour and width, so a
/// strip can taper and fade (the battle's streaks); <see cref="Disc"/> is a soft round glow. Curves are split by their
/// chord error on the screen (<see cref="Segments"/>, docs/map-ui/symbols.md Y1), and a strip fed through
/// <see cref="ClipPoint"/> keeps only what's on the screen. The arrays are kept between frames: building the same lines
/// again allocates nothing.</summary>
public sealed class MapLines
{
    LineVertex[] verts = new LineVertex[1 << 15];
    Vector2[] strip = new Vector2[1024];
    Vector4[] stripColor = new Vector4[1024];
    float[] stripHalf = new float[1024];
    int count, stripCount;
    double width = 1, height = 1;
    Vector4 color;
    float half;
    bool closed;

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

    /// <summary>Starts a polyline of a colour and a width in pixels.</summary>
    public void BeginStrip(Vector4 rgba, float widthPx)
    {
        End();
        (color, half) = (rgba, widthPx / 2);
        clipHave = false;
    }

    /// <summary>The next point, in pixels (x right, y down).</summary>
    public void Point(double x, double y) => Point(x, y, color, half * 2);

    /// <summary>The next point with its own colour and width: the strip blends between its points.</summary>
    public void Point(double x, double y, Vector4 rgba, float widthPx)
    {
        if (stripCount == strip.Length)
        {
            Array.Resize(ref strip, strip.Length * 2);
            Array.Resize(ref stripColor, strip.Length);
            Array.Resize(ref stripHalf, strip.Length);
        }
        stripColor[stripCount] = rgba;
        stripHalf[stripCount] = widthPx * WidthScale / 2;
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
    public void Circle(double x, double y, double r, Vector4 rgba, float widthPx)
    {
        if (!CircleShows(x, y, r))
            return;
        BeginStrip(rgba, widthPx);
        int n = Segments(r);
        for (int i = 0; i <= n; i++)
        {
            double a = i * Math.PI * 2 / n;
            ClipPoint(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
        }
        EndClosed();
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

    // the screen clip's last point: whether there is one, whether it was on the screen, and where
    bool clipHave, clipPrevIn;
    double clipX, clipY;

    /// <summary>Whether the clipped strip has a point yet.</summary>
    public bool ClipHave => clipHave;

    /// <summary>The next point of a strip clipped to the screen (and a margin): off it the strip breaks, keeping the point
    /// just past the edge; a piece with both ends off is drawn alone if it may cross the screen.</summary>
    public void ClipPoint(double x, double y)
    {
        const double M = 64;
        bool inside = x > -M && y > -M && x < width + M && y < height + M;
        if (inside)
        {
            if (clipHave && !clipPrevIn)
                Point(clipX, clipY);
            Point(x, y);
        }
        else if (clipHave && clipPrevIn)
        {
            Point(x, y);
            End();
        }
        else if (clipHave && Math.Max(clipX, x) > -M && Math.Min(clipX, x) < width + M && Math.Max(clipY, y) > -M && Math.Min(clipY, y) < height + M)
        {
            Point(clipX, clipY);   // a long piece across the screen, both ends off it
            Point(x, y);
            End();
        }
        (clipHave, clipPrevIn, clipX, clipY) = (true, inside, x, y);
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
        float prevReach = 0, prevHalf = 0;
        Vector4 prevColor = default;
        for (int i = 0; i < stripCount; i++)
        {
            var p = strip[i];
            float h = stripHalf[i], reach = h + 1;   // a pixel of fade beyond the line's edge
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
                Add(prevL, prevReach, prevHalf, prevColor);
                Add(prevR, -prevReach, prevHalf, prevColor);
                Add(l, reach, h, col);
                Add(prevR, -prevReach, prevHalf, prevColor);
                Add(r, -reach, h, col);
                Add(l, reach, h, col);
            }
            (prevL, prevR, prevReach, prevHalf, prevColor) = (l, r, reach, h, col);
        }
    }

    static Vector2 Dir(Vector2 a, Vector2 b)
    {
        var d = b - a;
        float len = d.Length();
        return len > 1e-6f ? d / len : new Vector2(1, 0);
    }

    void Add(Vector2 px, float edge, float h, Vector4 rgba) => verts[count++] = new LineVertex
    {
        Pos = new Vector2((float)(px.X / width * 2 - 1), (float)(1 - px.Y / height * 2)),
        Edge = new Vector2(edge, h),
        Color = rgba,
    };

    /// <summary>A vertex inside a fill: its edge far from the line's, so the shader's edge fade leaves it whole.</summary>
    void Solid(Vector2 px, Vector4 rgba) => Add(px, 0, 1e4f, rgba);
}
