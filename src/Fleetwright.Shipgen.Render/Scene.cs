using System.Globalization;
using System.Text;

namespace Fleetwright.Shipgen.Render;

/// <summary>An opaque sRGB colour, written #rrggbb.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>"#rrggbb" or "#rgb", optionally keyed ("#rrggbb/deck": <see cref="Keyed"/>).</summary>
    public static Rgb Parse(string c)
    {
        c = Plain(c);
        if (c.Length == 4 && c[0] == '#')
            return new(H(c[1..2] + c[1..2]), H(c[2..3] + c[2..3]), H(c[3..4] + c[3..4]));
        if (c.Length == 7 && c[0] == '#')
            return new(H(c[1..3]), H(c[3..5]), H(c[5..7]));
        throw new FormatException($"not a colour: {c}");
    }

    static byte H(string s) => byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public static Rgb Grey(int v) => new((byte)v, (byte)v, (byte)v);

    /// <summary>A colour string carrying the palette key it came from, "#rrggbb/key" (<see cref="Painter.C"/>), so a
    /// fill or stroke knows its material (docs/visuals/ship-lighting.md, L4) wherever the colour goes: shading it
    /// (<see cref="Painter.Shade"/>) keeps the key. Everything that reads the colour reads only the hex.</summary>
    public static string Keyed(string colour, string key) => Plain(colour) + "/" + key;

    /// <summary>The key a colour string carries, or null.</summary>
    public static string? KeyOf(string c)
    {
        int i = c.IndexOf('/');
        return i < 0 ? null : c[(i + 1)..];
    }

    /// <summary>The colour without its key.</summary>
    public static string Plain(string c)
    {
        int i = c.IndexOf('/');
        return i < 0 ? c : c[..i];
    }

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}

/// <summary>Paint settings on a node. Null means unset: inherited from the enclosing group, else the SVG default
/// (fill black, no stroke, opacities 1).</summary>
public sealed class Style
{
    public Rgb? Fill;
    public bool FillNone;
    public double? FillOpacity;
    public Rgb? Stroke;
    public double? StrokeWidth;
    public string? LineJoin;        // "round" (default miter)
    public string? LineCap;         // "round" (default butt)
    public double[]? Dash;
    public double? DashOffset;
    public double? StrokeOpacity;
    public double? Opacity;         // group opacity: the node drawn whole, then faded
    public Relief? Relief;          // what the node does to the relief layer (inherited); unset: leaves it alone
    public string? FillMat;         // the material key the fill's colour came with (Rgb.Keyed), set with the fill
    public string? StrokeMat;       // the stroke's
    public string? Mat;             // a material key set where the drawing knows better (inherited, over the colours'
                                    // keys): brass, glass, bare steel. Not part of the SVG
}

/// <summary>A relief profile (docs/visuals/ship-lighting.md, "The bake"): how a draw shapes the relief layer the
/// normals and ambient occlusion come from, in the node's own coordinates (metres), heights in metres (over the
/// waterline on the hull, over the turret's base on a turret). Paint on a surface carries none, so it never flattens
/// what's under it. Not part of the SVG.
/// <list type="bullet">
/// <item>Plateau: TopM everywhere.</item>
/// <item>Cylinder: its axis through (X0, Y0) and (X1, Y1), radius R, its top at TopM (the axis R below):
/// barrels, tubes, yards, booms.</item>
/// <item>Dome: an ellipsoid's cap centred at (X0, Y0), radii R and R2, rising H to TopM: cupolas, vent heads,
/// searchlights, boats, a drum turret's roof.</item>
/// <item>Slope: a plane, TopM at (X0, Y0) and rising X1 metres a metre along x, Y1 along y: face plates.</item>
/// </list></summary>
public readonly record struct Relief(ReliefKind Kind, double TopM, double X0 = 0, double Y0 = 0, double X1 = 0,
    double Y1 = 0, double R = 0, double R2 = 0, double H = 0)
{
    public static Relief Plateau(double topM) => new(ReliefKind.Plateau, topM);

    public static Relief Cylinder(double x0, double y0, double x1, double y1, double r, double topM) =>
        new(ReliefKind.Cylinder, topM, x0, y0, x1, y1, r);

    public static Relief Dome(double cx, double cy, double rx, double ry, double topM, double heightM) =>
        new(ReliefKind.Dome, topM, cx, cy, R: rx, R2: ry, H: heightM);

    public static Relief Slope(double x0, double y0, double topM, double riseX, double riseY) =>
        new(ReliefKind.Slope, topM, x0, y0, riseX, riseY);
}

public enum ReliefKind : byte
{
    Plateau = 1,
    Cylinder = 2,
    Dome = 3,
    Slope = 4,
}

/// <summary>One transform step, applied left to right as in SVG's transform list.</summary>
public abstract record XOp;
public sealed record Translate(double X, double Y) : XOp;
public sealed record Rotate(double Deg, double Cx = 0.0, double Cy = 0.0, bool About = false) : XOp;
public sealed record Scale(double S) : XOp;

/// <summary>A node of the display list: a shape or a group, with its paint and transform.</summary>
public abstract class Node
{
    public readonly Style S = new();
    public List<XOp>? Transform;
}

/// <summary>Children drawn in order, optionally clipped to the union of the clip shapes (in the group's own
/// coordinates).</summary>
public sealed class Group : Node
{
    public readonly List<Node> Items = [];
    public List<Node>? Clip;

    public Group() { }
    public Group(IEnumerable<Node> items) => Items.AddRange(items);
}

public enum SegKind { M, L, A, C, Z }

/// <summary>A path segment. A: rx, ry, x-axis rotation, large-arc, sweep, end x, end y (as SVG); C: two control
/// points then the end point.</summary>
public readonly record struct Seg(SegKind Kind, double A = 0, double B = 0, double C = 0, double D = 0, double E = 0,
    double F = 0, double G = 0);

public sealed class PathData
{
    public readonly List<Seg> Segs = [];

    public PathData M(double x, double y) { Segs.Add(new(SegKind.M, x, y)); return this; }
    public PathData L(double x, double y) { Segs.Add(new(SegKind.L, x, y)); return this; }
    public PathData A(double rx, double ry, double rot, bool large, bool sweep, double x, double y)
    {
        Segs.Add(new(SegKind.A, rx, ry, rot, large ? 1 : 0, sweep ? 1 : 0, x, y));
        return this;
    }
    public PathData C(double x1, double y1, double x2, double y2, double x, double y)
    {
        Segs.Add(new(SegKind.C, x1, y1, x2, y2, x, y));
        return this;
    }
    public PathData Z() { Segs.Add(new(SegKind.Z)); return this; }

    /// <summary>A closed polygon (shipgen.poly).</summary>
    public static PathData Poly(IEnumerable<Pt> pts)
    {
        var d = new PathData();
        bool first = true;
        foreach (var p in pts)
        {
            if (first)
                d.M(p.X, p.Y);
            else
                d.L(p.X, p.Y);
            first = false;
        }
        return d.Z();
    }
}

public sealed class PathNode(PathData d) : Node { public readonly PathData D = d; }
public sealed class CircleNode(double cx, double cy, double r) : Node { public readonly double Cx = cx, Cy = cy, R = r; }
public sealed class EllipseNode(double cx, double cy, double rx, double ry) : Node
{
    public readonly double Cx = cx, Cy = cy, Rx = rx, Ry = ry;
}
public sealed class RectNode(double x, double y, double w, double h, double? rx = null) : Node
{
    public readonly double X = x, Y = y, W = w, H = h;
    public readonly double? Rx = rx;
}
public sealed class LineNode(double x1, double y1, double x2, double y2) : Node
{
    public readonly double X1 = x1, Y1 = y1, X2 = x2, Y2 = y2;
}

/// <summary>Bold DejaVu Sans text centred on (x, y) both ways (the hull numbers).</summary>
public sealed class TextNode(double x, double y, double size, string text) : Node
{
    public readonly double X = x, Y = y, Size = size;
    public readonly string Text = text;
}

/// <summary>A drawing: the view box in metres and the scale. The canvas is round(w * scale) x round(h * scale) px,
/// as cairosvg sized it from the SVG's width and height.</summary>
public sealed class Scene(double x, double y, double w, double h, double scale)
{
    public readonly double X = x, Y = y, W = w, H = h, Scale = scale;
    public readonly Group Root = new();

    public int WidthPx => (int)(long)Math.Round(W * Scale);
    public int HeightPx => (int)(long)Math.Round(H * Scale);
}

/// <summary>Fluent paint setters, so the drawing code reads like the SVG it replaces.</summary>
public static class NodeStyle
{
    public static T Fill<T>(this T n, string col) where T : Node
    {
        if (col == "none")
            n.S.FillNone = true;
        else
        {
            n.S.Fill = Rgb.Parse(col);
            n.S.FillMat = Rgb.KeyOf(col);
        }
        return n;
    }

    public static T Fill<T>(this T n, Rgb col) where T : Node { n.S.Fill = col; return n; }
    public static T FillOp<T>(this T n, double v) where T : Node { n.S.FillOpacity = v; return n; }
    public static T Stroke<T>(this T n, string col, double w) where T : Node
    {
        n.S.Stroke = Rgb.Parse(col);
        n.S.StrokeMat = Rgb.KeyOf(col);
        n.S.StrokeWidth = w;
        return n;
    }
    public static T Stroke<T>(this T n, Rgb col, double w) where T : Node
    {
        n.S.Stroke = col;
        n.S.StrokeWidth = w;
        return n;
    }
    public static T StrokeOp<T>(this T n, double v) where T : Node { n.S.StrokeOpacity = v; return n; }
    public static T Join<T>(this T n, string j) where T : Node { n.S.LineJoin = j; return n; }
    public static T Cap<T>(this T n, string c) where T : Node { n.S.LineCap = c; return n; }
    public static T Dash<T>(this T n, params double[] d) where T : Node { n.S.Dash = d; return n; }
    public static T DashOff<T>(this T n, double v) where T : Node { n.S.DashOffset = v; return n; }
    public static T Opacity<T>(this T n, double v) where T : Node { n.S.Opacity = v; return n; }
    /// <summary>A plateau in the relief layer at topM metres (<see cref="Relief"/>).</summary>
    public static T Plateau<T>(this T n, double topM) where T : Node { n.S.Relief = Relief.Plateau(topM); return n; }
    /// <summary>Any relief profile (<see cref="Relief"/>).</summary>
    public static T Profile<T>(this T n, Relief r) where T : Node { n.S.Relief = r; return n; }
    /// <summary>The material by key (the look's materials table), over the colours' own (<see cref="Style.Mat"/>).</summary>
    public static T Mat<T>(this T n, string key) where T : Node { n.S.Mat = key; return n; }
    public static T Tr<T>(this T n, params XOp[] ops) where T : Node
    {
        n.Transform = [.. ops];
        return n;
    }
}

/// <summary>The display list as SVG, for looking at and for comparing with Python's SVGs (golden/svg). Numbers are
/// written as shipgen.f wrote them: 3 decimals, trailing zeros dropped.</summary>
public static class SvgWriter
{
    public static string F(double v) => v.ToString("F3").TrimEnd('0').TrimEnd('.');

    public static string Write(Scene sc)
    {
        var defs = new StringBuilder();
        var body = new StringBuilder();
        int clipN = 0;
        Node(sc.Root, body, defs, ref clipN, root: true);
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{sc.WidthPx}\" height=\"{sc.HeightPx}\" " +
               $"viewBox=\"{F(sc.X)} {F(sc.Y)} {F(sc.W)} {F(sc.H)}\" shape-rendering=\"geometricPrecision\">" +
               $"<defs>{defs}</defs>{body}</svg>";
    }

    static void Node(Node n, StringBuilder o, StringBuilder defs, ref int clipN, bool root = false)
    {
        switch (n)
        {
            case Group g:
                if (root && g.Clip == null && g.Transform == null)
                {
                    foreach (var c in g.Items)
                        Node(c, o, defs, ref clipN);
                    return;
                }
                o.Append("<g");
                if (g.Clip != null)
                {
                    string id = $"c{++clipN}";
                    defs.Append($"<clipPath id=\"{id}\">");
                    foreach (var c in g.Clip)
                        Node(c, defs, defs, ref clipN);
                    defs.Append("</clipPath>");
                    o.Append($" clip-path=\"url(#{id})\"");
                }
                Attrs(g, o);
                o.Append('>');
                foreach (var c in g.Items)
                    Node(c, o, defs, ref clipN);
                o.Append("</g>");
                return;
            case PathNode p:
                o.Append("<path d=\"").Append(D(p.D)).Append('"');
                break;
            case CircleNode c:
                o.Append($"<circle cx=\"{F(c.Cx)}\" cy=\"{F(c.Cy)}\" r=\"{F(c.R)}\"");
                break;
            case EllipseNode e:
                o.Append($"<ellipse cx=\"{F(e.Cx)}\" cy=\"{F(e.Cy)}\" rx=\"{F(e.Rx)}\" ry=\"{F(e.Ry)}\"");
                break;
            case RectNode r:
                o.Append($"<rect x=\"{F(r.X)}\" y=\"{F(r.Y)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\"");
                if (r.Rx is double rx)
                    o.Append($" rx=\"{F(rx)}\"");
                break;
            case LineNode l:
                o.Append($"<line x1=\"{F(l.X1)}\" y1=\"{F(l.Y1)}\" x2=\"{F(l.X2)}\" y2=\"{F(l.Y2)}\"");
                break;
            case TextNode t:
                o.Append($"<text x=\"{F(t.X)}\" y=\"{F(t.Y)}\" font-family=\"DejaVu Sans\" font-weight=\"bold\" " +
                         $"font-size=\"{F(t.Size)}\" text-anchor=\"middle\" dominant-baseline=\"central\"");
                Attrs(t, o);
                o.Append('>').Append(t.Text).Append("</text>");
                return;
            default:
                throw new ArgumentException(n.GetType().Name);
        }
        Attrs(n, o);
        o.Append("/>");
    }

    static void Attrs(Node n, StringBuilder o)
    {
        if (n.Transform != null)
            o.Append(" transform=\"").Append(string.Join(" ", n.Transform.Select(Xf))).Append('"');
        var s = n.S;
        if (s.FillNone)
            o.Append(" fill=\"none\"");
        else if (s.Fill is Rgb f)
            o.Append($" fill=\"{f}\"");
        if (s.FillOpacity is double fo)
            o.Append($" fill-opacity=\"{F(fo)}\"");
        if (s.Stroke is Rgb st)
            o.Append($" stroke=\"{st}\"");
        if (s.StrokeWidth is double sw)
            o.Append($" stroke-width=\"{F(sw)}\"");
        if (s.LineJoin != null)
            o.Append($" stroke-linejoin=\"{s.LineJoin}\"");
        if (s.LineCap != null)
            o.Append($" stroke-linecap=\"{s.LineCap}\"");
        if (s.Dash != null)
            o.Append(" stroke-dasharray=\"").Append(string.Join(" ", s.Dash.Select(F))).Append('"');
        if (s.DashOffset is double dof)
            o.Append($" stroke-dashoffset=\"{F(dof)}\"");
        if (s.StrokeOpacity is double so)
            o.Append($" stroke-opacity=\"{F(so)}\"");
        if (s.Opacity is double op)
            o.Append($" opacity=\"{F(op)}\"");
    }

    static string Xf(XOp x) => x switch
    {
        Translate t => $"translate({F(t.X)},{F(t.Y)})",
        Rotate r when r.About => $"rotate({F(r.Deg)} {F(r.Cx)} {F(r.Cy)})",
        Rotate r => $"rotate({F(r.Deg)})",
        Scale s => $"scale({F(s.S)})",
        _ => throw new ArgumentException(x.GetType().Name),
    };

    public static string D(PathData d)
    {
        var parts = new List<string>(d.Segs.Count);
        foreach (var s in d.Segs)
            parts.Add(s.Kind switch
            {
                SegKind.M => $"M{F(s.A)},{F(s.B)}",
                SegKind.L => $"L{F(s.A)},{F(s.B)}",
                SegKind.A => $"A{F(s.A)},{F(s.B)} {F(s.C)} {(int)s.D} {(int)s.E} {F(s.F)},{F(s.G)}",
                SegKind.C => $"C{F(s.A)},{F(s.B)} {F(s.C)},{F(s.D)} {F(s.E)},{F(s.F)}",
                _ => "Z",
            });
        return string.Join(" ", parts);
    }
}
