using System.Numerics;
using System.Runtime.InteropServices;

namespace Fleetwright.Map;

/// <summary>A map symbol's shape (mapsymbol.frag).</summary>
public enum SymbolShape : byte
{
    Circle,
    Diamond,
    Square,
    /// <summary>An arrowhead, its tip toward the angle given.</summary>
    Arrow,
    /// <summary>A ring and a cross: what's lost (no fill).</summary>
    Wreck,
}

/// <summary>One corner of a map symbol's quad: NDC position, its point in the symbol's own frame (px), the shape and
/// its sizes (radius, stroke half width, casing; px), fill and stroke (straight alpha), the casing's alpha.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SymbolVertex
{
    public Vector2 Pos;
    public Vector2 Local;
    public Vector4 Shape;
    public Vector4 Fill;
    public Vector4 Stroke;
    public Vector4 Extra;

    public const uint SizeInBytes = 80;
}

/// <summary>The map's symbols (docs/map-ui/symbols.md Y3): rings, dots, diamonds, arrowheads and wrecks, each one quad
/// whose shape the fragment shader draws as a signed distance, so it is exact and anti-aliased at any size, filled,
/// stroked and cased (<see cref="SymbolLook.Casing"/>) in one draw after the overlay's lines. Positions and radii are
/// target pixels (radii from fixed sizes times <see cref="SymbolLook.Px"/>, as the caller has them); stroke widths are
/// reference pixels, scaled here. The array is kept between frames: no garbage.</summary>
public sealed class MapSymbols
{
    SymbolVertex[] verts = new SymbolVertex[6 * 512];
    int count;
    double width = 1, height = 1;

    /// <summary>Target pixels a reference pixel (<see cref="SymbolLook.Px"/>): the strokes' and casing's widths.</summary>
    public float Scale = 1;

    public ReadOnlySpan<SymbolVertex> Vertices => verts.AsSpan(0, count);

    /// <summary>Starts a frame's symbols for a target of this size.</summary>
    public void Begin(double targetWidth, double targetHeight, float scale)
    {
        (width, height, Scale) = (Math.Max(targetWidth, 1), Math.Max(targetHeight, 1), scale);
        count = 0;
    }

    /// <summary>A symbol at (x, y) px of radius r px: filled with <paramref name="fill"/> (alpha 0: hollow), outlined
    /// with <paramref name="stroke"/> at <paramref name="strokeWidth"/> reference px, turned to <paramref name="angleRad"/>
    /// (an arrowhead's tip: 0 along +x, screen y down), cased unless told not to. Off the screen: nothing.</summary>
    public void Add(SymbolShape shape, double x, double y, double r, Vector4 fill, Vector4 stroke, float strokeWidth,
        double angleRad = 0, bool cased = true)
    {
        float sw = strokeWidth * Scale / 2, cas = cased ? SymbolLook.Casing * Scale : 0;
        double ext = r + sw + cas + 1.5;
        if (x + ext < 0 || y + ext < 0 || x - ext > width || y - ext > height || fill.W <= 0 && stroke.W <= 0)
            return;
        if (count + 6 > verts.Length)
            Array.Resize(ref verts, verts.Length * 2);
        // the symbol's frame: +y its forward (the angle), +x to its right on the screen
        double c = Math.Cos(angleRad), s = Math.Sin(angleRad);
        var fwd = new Vector2((float)c, (float)s);
        var right = new Vector2((float)-s, (float)c);
        var shapeV = new Vector4((int)shape, (float)r, sw, cas);
        var extra = new Vector4(SymbolLook.CasingAlpha, 0, 0, 0);
        var centre = new Vector2((float)x, (float)y);
        float e = (float)ext;
        Corner(centre, right, fwd, -e, -e, shapeV, fill, stroke, extra);
        Corner(centre, right, fwd, e, -e, shapeV, fill, stroke, extra);
        Corner(centre, right, fwd, e, e, shapeV, fill, stroke, extra);
        Corner(centre, right, fwd, -e, -e, shapeV, fill, stroke, extra);
        Corner(centre, right, fwd, e, e, shapeV, fill, stroke, extra);
        Corner(centre, right, fwd, -e, e, shapeV, fill, stroke, extra);
    }

    /// <summary>A hollow ring: <see cref="Add"/> with no fill.</summary>
    public void Ring(double x, double y, double r, Vector4 stroke, float strokeWidth, bool cased = true) =>
        Add(SymbolShape.Circle, x, y, r, default, stroke, strokeWidth, 0, cased);

    void Corner(Vector2 centre, Vector2 right, Vector2 fwd, float lx, float ly, Vector4 shape, Vector4 fill, Vector4 stroke, Vector4 extra)
    {
        // the frame's +y is the symbol's forward; the screen's y runs down, so a local point lies at right·x + fwd·y
        var px = centre + right * lx + fwd * ly;
        verts[count++] = new SymbolVertex
        {
            Pos = new Vector2((float)(px.X / width * 2 - 1), (float)(1 - px.Y / height * 2)),
            Local = new Vector2(lx, ly),
            Shape = shape,
            Fill = fill,
            Stroke = stroke,
            Extra = extra,
        };
    }
}
