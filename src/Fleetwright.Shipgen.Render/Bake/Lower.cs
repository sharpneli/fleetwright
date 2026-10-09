using System.Numerics;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>What a draw op does with its triangles (DrawList.Verts[First .. First + Count]).</summary>
public enum OpKind
{
    Fill,        // stencil the triangles (nonzero winding, or union), then cover them with Colour
    PushClip,    // the triangles become the clip: inside the current clip and inside them
    PopClip,     // back to the enclosing clip (the same triangles as the push)
    GroupEnd,    // an opacity group is done: clear its coverage flag over these triangles
}

/// <summary>One step of a lowered scene. Colour is premultiplied. Union: the triangles are a union of positively
/// wound pieces (a stroke), so coverage is set rather than counted. InGroup: inside an opacity group, which is drawn
/// topmost first, each pixel by the first op that reaches it.</summary>
public readonly record struct DrawOp(OpKind Kind, int First, int Count, Vector4 Colour = default, bool Union = false,
    bool InGroup = false);

/// <summary>A scene lowered to triangles in canvas pixels (x right, y down), ready for stencil-then-cover.</summary>
public sealed class DrawList(int width, int height)
{
    public readonly int Width = width, Height = height;
    public readonly List<Vector2> Verts = [];
    public readonly List<DrawOp> Ops = [];
}

/// <summary>Lowers a Scene (SVG's model) to a DrawList: transforms applied, styles inherited, arcs, curves, circles
/// and text flattened to polygons within a tolerance in pixels, strokes expanded (Stroker), fills fanned into
/// triangles for nonzero winding.</summary>
public static class Lower
{
    /// <summary>Flattening tolerance, px: the most a flattened edge strays from the true curve.</summary>
    public const double TolPx = 0.05;

    readonly record struct Affine(double A, double B, double C, double D, double E, double F)
    {
        // x' = A x + C y + E, y' = B x + D y + F (SVG's matrix(a b c d e f))
        public static readonly Affine Identity = new(1, 0, 0, 1, 0, 0);

        public Affine Then(Affine o) =>   // this, then o
            new(o.A * A + o.C * B, o.B * A + o.D * B, o.A * C + o.C * D, o.B * C + o.D * D, o.A * E + o.C * F + o.E,
                o.B * E + o.D * F + o.F);

        public Vector2 Apply(double x, double y) => new((float)(A * x + C * y + E), (float)(B * x + D * y + F));

        public double Scale => Math.Sqrt(Math.Abs(A * D - B * C));

        public static Affine Of(XOp x) => x switch
        {
            Translate t => new(1, 0, 0, 1, t.X, t.Y),
            Scale s => new(s.S, 0, 0, s.S, 0, 0),
            Rotate r => Rot(r),
            _ => throw new ArgumentException(x.GetType().Name),
        };

        static Affine Rot(Rotate r)
        {
            double a = r.Deg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            var rot = new Affine(c, s, -s, c, 0, 0);
            if (!r.About)
                return rot;
            return new Affine(1, 0, 0, 1, -r.Cx, -r.Cy).Then(rot).Then(new Affine(1, 0, 0, 1, r.Cx, r.Cy));
        }
    }

    /// <summary>The style in force: the node's own settings over its ancestors'.</summary>
    sealed record Paint(Rgb? Fill, bool FillNone, double FillOpacity, Rgb? Stroke, double StrokeWidth, string? Join,
        string? Cap, double[]? Dash, double DashOffset, double StrokeOpacity)
    {
        public static readonly Paint Default = new(null, false, 1, null, 1, null, null, null, 0, 1);

        public Paint With(Style s) => new(
            s.FillNone ? null : s.Fill ?? Fill, s.FillNone || (s.Fill == null && FillNone),
            s.FillOpacity ?? FillOpacity, s.Stroke ?? Stroke, s.StrokeWidth ?? StrokeWidth, s.LineJoin ?? Join,
            s.LineCap ?? Cap, s.Dash ?? Dash, s.DashOffset ?? DashOffset, s.StrokeOpacity ?? StrokeOpacity);
    }

    sealed class Ctx(DrawList dl)
    {
        public readonly DrawList Dl = dl;
        public int ClipDepth;
        public int GroupDepth;
    }

    public static DrawList Run(Scene sc)
    {
        var dl = new DrawList(sc.WidthPx, sc.HeightPx);
        var m = new Affine(1, 0, 0, 1, -sc.X, -sc.Y).Then(new Affine(sc.Scale, 0, 0, sc.Scale, 0, 0));
        // cairosvg fits the view box to the canvas: scale by the rounded size over the view box
        m = m.Then(new Affine(sc.WidthPx / (sc.W * sc.Scale), 0, 0, sc.HeightPx / (sc.H * sc.Scale), 0, 0));
        Walk(sc.Root, m, Paint.Default, new Ctx(dl));
        return dl;
    }

    static Affine Local(Node n, Affine m)
    {
        if (n.Transform == null)
            return m;
        var l = Affine.Identity;
        foreach (var x in n.Transform)
            l = Affine.Of(x).Then(l);     // SVG applies the list right to left to the points
        return l.Then(m);
    }

    static void Walk(Node n, Affine parent, Paint inherited, Ctx cx)
    {
        var m = Local(n, parent);
        var paint = inherited.With(n.S);
        if (n is Group g)
        {
            var dl = cx.Dl;
            (int First, int Count) clip = default;
            if (g.Clip != null)
            {
                int first = dl.Verts.Count;
                foreach (var c in g.Clip)
                    FanFill(Contours(c, Local(c, m)), dl.Verts);
                clip = (first, dl.Verts.Count - first);
                if (++cx.ClipDepth > 7)
                    throw new NotSupportedException("clips nest deeper than 7");
                dl.Ops.Add(new DrawOp(OpKind.PushClip, clip.First, clip.Count));
            }
            if (g.S.Opacity is double op && op < 1)
                Group(g, m, paint, op, cx);
            else
                foreach (var c in g.Items)
                    Walk(c, m, paint, cx);
            if (g.Clip != null)
            {
                dl.Ops.Add(new DrawOp(OpKind.PopClip, clip.First, clip.Count));
                cx.ClipDepth--;
            }
            return;
        }
        Shape(n, m, paint, cx, 1.0);
    }

    /// <summary>An opacity group: its children are drawn topmost first, each pixel by the first that reaches it, at
    /// the group's opacity; for opaque children that is exactly the group composited as one. Only flat shapes may be
    /// in it (all the drawing puts there is dazzle panels).</summary>
    static void Group(Group g, Affine m, Paint paint, double opacity, Ctx cx)
    {
        if (cx.GroupDepth > 0)
            throw new NotSupportedException("nested opacity groups");
        cx.GroupDepth++;
        var dl = cx.Dl;
        int first = dl.Verts.Count;
        int ops0 = dl.Ops.Count;
        foreach (var c in g.Items)
        {
            if (c is Group)
                throw new NotSupportedException("a group inside an opacity group");
            Shape(c, Local(c, m), paint.With(c.S), cx, opacity);
        }
        dl.Ops.Reverse(ops0, dl.Ops.Count - ops0);
        for (int i = ops0; i < dl.Ops.Count; i++)
            dl.Ops[i] = dl.Ops[i] with { InGroup = true };
        dl.Ops.Add(new DrawOp(OpKind.GroupEnd, first, dl.Verts.Count - first));
        cx.GroupDepth--;
    }

    static Vector4 Premul(Rgb c, double a) =>
        new((float)(c.R / 255.0 * a), (float)(c.G / 255.0 * a), (float)(c.B / 255.0 * a), (float)a);

    /// <summary>A shape's fill, then its stroke. m already includes the shape's own transform.</summary>
    static void Shape(Node n, Affine m, Paint p, Ctx cx, double opacity)
    {
        var dl = cx.Dl;
        if (n is Group)
            throw new ArgumentException("not a shape");
        bool hasFill = !p.FillNone && n is not LineNode;
        if (hasFill)
        {
            var contours = Contours(n, m);
            double a = p.FillOpacity * opacity;
            if (contours.Count > 0 && a > 0)
            {
                int first = dl.Verts.Count;
                FanFill(contours, dl.Verts);
                if (dl.Verts.Count > first)
                    dl.Ops.Add(new DrawOp(OpKind.Fill, first, dl.Verts.Count - first, Premul(p.Fill ?? new Rgb(0, 0, 0), a)));
            }
        }
        if (p.Stroke is Rgb sc && p.StrokeWidth > 0 && n is not TextNode)
        {
            double a = p.StrokeOpacity * opacity;
            double k = m.Scale;
            var lines = Polylines(n, m);
            int first = dl.Verts.Count;
            var dash = p.Dash?.Select(d => d * k).ToArray();
            foreach (var (pts, closed) in lines)
                Stroker.Stroke(pts, closed, p.StrokeWidth * k, p.Join ?? "miter", p.Cap ?? "butt", dash, p.DashOffset * k,
                    TolPx, dl.Verts);
            if (dl.Verts.Count > first && a > 0)
                dl.Ops.Add(new DrawOp(OpKind.Fill, first, dl.Verts.Count - first, Premul(sc, a), Union: true));
        }
    }

    /// <summary>Each contour fanned from its first point into a triangle list: drawn with increment/decrement
    /// stencil, the counts add up to the nonzero winding number of the whole set of contours.</summary>
    internal static void FanFill(List<List<Vector2>> contours, List<Vector2> verts)
    {
        foreach (var c in contours)
            for (int i = 1; i + 1 < c.Count; i++)
            {
                verts.Add(c[0]);
                verts.Add(c[i]);
                verts.Add(c[i + 1]);
            }
    }

    /// <summary>Segments for a circle of radius r px so the chord strays at most TolPx.</summary>
    internal static int Segs(double rPx, double sweep = 2 * Math.PI)
    {
        if (rPx <= TolPx)
            return 3;
        double step = 2 * Math.Acos(1 - TolPx / rPx);
        return Math.Clamp((int)Math.Ceiling(Math.Abs(sweep) / step), 3, 4096);
    }

    static List<Vector2> Ellipse(double cx, double cy, double rx, double ry, Affine m)
    {
        int n = Math.Max(8, Segs(Math.Max(rx, ry) * m.Scale));
        var c = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            double t = 2 * Math.PI * i / n;
            c.Add(m.Apply(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return c;
    }

    /// <summary>The shape's outline as closed contours, in pixels.</summary>
    static List<List<Vector2>> Contours(Node n, Affine m) => n switch
    {
        PathNode p => Polylines(p, m).Select(l => l.Pts).Where(c => c.Count >= 3).ToList(),
        CircleNode c when c.R > 0 => [Ellipse(c.Cx, c.Cy, c.R, c.R, m)],
        EllipseNode e when e.Rx > 0 && e.Ry > 0 => [Ellipse(e.Cx, e.Cy, e.Rx, e.Ry, m)],
        RectNode r when r.W > 0 && r.H > 0 => [RectOutline(r, m)],
        TextNode t => Glyphs.Outline(t, m.Apply, m.Scale),
        _ => [],
    };

    static List<Vector2> RectOutline(RectNode r, Affine m)
    {
        double rx = Math.Min(r.Rx ?? 0, r.W / 2), ry = Math.Min(r.Rx ?? 0, r.H / 2);
        if (rx <= 0 || ry <= 0)
            return [m.Apply(r.X, r.Y), m.Apply(r.X + r.W, r.Y), m.Apply(r.X + r.W, r.Y + r.H), m.Apply(r.X, r.Y + r.H)];
        var c = new List<Vector2>();
        int n = Math.Max(2, Segs(Math.Max(rx, ry) * m.Scale, Math.PI / 2));
        void Corner(double cx, double cy, double a0)
        {
            for (int i = 0; i <= n; i++)
            {
                double t = a0 + Math.PI / 2 * i / n;
                c.Add(m.Apply(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
            }
        }
        Corner(r.X + r.W - rx, r.Y + ry, -Math.PI / 2);
        Corner(r.X + r.W - rx, r.Y + r.H - ry, 0);
        Corner(r.X + rx, r.Y + r.H - ry, Math.PI / 2);
        Corner(r.X + rx, r.Y + ry, Math.PI);
        return c;
    }

    /// <summary>The shape as polylines (for strokes; a closed one for each closed subpath), in pixels.</summary>
    static List<(List<Vector2> Pts, bool Closed)> Polylines(Node n, Affine m)
    {
        switch (n)
        {
            case LineNode l:
                return [([m.Apply(l.X1, l.Y1), m.Apply(l.X2, l.Y2)], false)];
            case PathNode p:
                return FlattenPath(p.D, m);
            default:
                return Contours(n, m).Select(c => (c, true)).ToList();
        }
    }

    static List<(List<Vector2>, bool)> FlattenPath(PathData d, Affine m)
    {
        var out_ = new List<(List<Vector2>, bool)>();
        List<Vector2>? cur = null;
        double x = 0, y = 0, sx = 0, sy = 0;
        double k = m.Scale;
        void End(bool closed)
        {
            if (cur != null && cur.Count > 0)
                out_.Add((cur, closed));
            cur = null;
        }
        foreach (var s in d.Segs)
        {
            switch (s.Kind)
            {
                case SegKind.M:
                    End(false);
                    (x, y) = (sx, sy) = (s.A, s.B);
                    cur = [m.Apply(x, y)];
                    break;
                case SegKind.L:
                    cur ??= [m.Apply(x, y)];
                    (x, y) = (s.A, s.B);
                    cur.Add(m.Apply(x, y));
                    break;
                case SegKind.C:
                    {
                        cur ??= [m.Apply(x, y)];
                        double len = Math.Sqrt(Sq(s.A - x, s.B - y)) + Math.Sqrt(Sq(s.C - s.A, s.D - s.B)) + Math.Sqrt(Sq(s.E - s.C, s.F - s.D));
                        int n = Math.Clamp((int)Math.Ceiling(Math.Sqrt(len * k / TolPx) / 2), 2, 256);
                        for (int i = 1; i <= n; i++)
                        {
                            double t = (double)i / n, u = 1 - t;
                            double px = u * u * u * x + 3 * u * u * t * s.A + 3 * u * t * t * s.C + t * t * t * s.E;
                            double py = u * u * u * y + 3 * u * u * t * s.B + 3 * u * t * t * s.D + t * t * t * s.F;
                            cur.Add(m.Apply(px, py));
                        }
                        (x, y) = (s.E, s.F);
                        break;
                    }
                case SegKind.A:
                    cur ??= [m.Apply(x, y)];
                    Arc(cur, x, y, s.A, s.B, s.C, s.D != 0, s.E != 0, s.F, s.G, m);
                    (x, y) = (s.F, s.G);
                    break;
                case SegKind.Z:
                    End(true);
                    (x, y) = (sx, sy);
                    break;
            }
        }
        End(false);
        return out_;
    }

    static double Sq(double a, double b) => a * a + b * b;

    /// <summary>An SVG elliptical arc from (x1, y1) to (x2, y2), flattened (the endpoint-to-centre conversion of SVG
    /// 1.1 F.6.5, radii scaled up when too small).</summary>
    static void Arc(List<Vector2> o, double x1, double y1, double rx, double ry, double rotDeg, bool large, bool sweep,
        double x2, double y2, Affine m)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx == 0 || ry == 0 || (x1 == x2 && y1 == y2))
        {
            o.Add(m.Apply(x2, y2));
            return;
        }
        double phi = rotDeg * Math.PI / 180, cp = Math.Cos(phi), sp = Math.Sin(phi);
        double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2;
        double x1p = cp * dx + sp * dy, y1p = -sp * dx + cp * dy;
        double lam = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lam > 1)
        {
            rx *= Math.Sqrt(lam);
            ry *= Math.Sqrt(lam);
        }
        double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
        double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        double co = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
        double cxp = co * rx * y1p / ry, cyp = -co * ry * x1p / rx;
        double cx = cp * cxp - sp * cyp + (x1 + x2) / 2, cy = sp * cxp + cp * cyp + (y1 + y2) / 2;
        double Ang(double ux, double uy, double vx, double vy)
        {
            double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            return a;
        }
        double t1 = Ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        double dt = Ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && dt > 0)
            dt -= 2 * Math.PI;
        else if (sweep && dt < 0)
            dt += 2 * Math.PI;
        int n = Math.Max(2, Segs(Math.Max(rx, ry) * m.Scale, dt));
        for (int i = 1; i <= n; i++)
        {
            double t = t1 + dt * i / n;
            double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
            o.Add(i == n ? m.Apply(x2, y2) : m.Apply(cp * ex - sp * ey + cx, sp * ex + cp * ey + cy));
        }
    }
}
