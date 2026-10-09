namespace Fleetwright.Shipgen;

/// <summary>A point in ship-local metres (Python's (x, y) tuple); written as [x, y].</summary>
public readonly record struct Pt(double X, double Y) : IPyValue
{
    public object? ToPy() => new List<object?> { X, Y };
    public static implicit operator Pt((double X, double Y) t) => new(t.X, t.Y);
}

/// <summary>AA mounts: tub radius, barrels, barrel length, barrel width and barrel spacing in metres, and the gun.</summary>
public sealed record AAMount(double R, long Barrels, double BarrelLen, double BarrelW, double Spacing, double CalibreMm,
    double CalibreLength);

/// <summary>Turret-local polygons: the body, extra parts and each barrel.</summary>
public sealed class TurretShapes
{
    public List<Pt> Body = [];
    public List<List<Pt>> Parts = [];
    public List<List<Pt>> Barrels = [];
}

/// <summary>geometry: the SAME functions produce the sprite outlines and the hitboxes. Coordinates are metres; turret
/// shapes are turret-local (pivot at 0, 0, barrels along +x).</summary>
public static class Geometry
{
    // ------------------------------------------------------------------ primitives

    /// <summary>Clamp corner radii exactly the way the SVG renderer does.</summary>
    public static (double Rf, double Rb) RrectClamped(double x0, double y0, double x1, double y1, double rf = 0.0,
        double rb = 0.0)
    {
        double h = (y1 - y0) / 2;
        double halfLen = (x1 - x0) / 2;
        return (Py.Min(rf, h, halfLen), Py.Min(rb, h, halfLen));
    }

    /// <summary>Rounded rectangle (front corners radius rf at +x, back corners rb at -x) as a polygon.</summary>
    public static List<Pt> RrectPolygon(double x0, double y0, double x1, double y1, double rf = 0.0, double rb = 0.0,
        int seg = 8)
    {
        (rf, rb) = RrectClamped(x0, y0, x1, y1, rf, rb);
        var pts = new List<Pt>();

        void Arc(double cx, double cy, double r, int a0, int a1)
        {
            if (r <= 1e-9)
            {
                pts.Add(new Pt(cx, cy));
                return;
            }
            for (int i = 0; i <= seg; i++)
            {
                double a = Py.Radians(a0 + (double)((a1 - a0) * i) / seg);
                pts.Add(new Pt(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
        }

        Arc(x1 - rf, y0 + rf, rf, -90, 0);
        Arc(x1 - rf, y1 - rf, rf, 0, 90);
        Arc(x0 + rb, y1 - rb, rb, 90, 180);
        Arc(x0 + rb, y0 + rb, rb, 180, 270);
        return pts;
    }

    /// <summary>A superstructure block's outline: its own polygon ("points") when it has one, else its rounded
    /// rectangle.</summary>
    public static List<Pt> BlockOutline(PyDict b)
    {
        if (b.B("points"))
            return [.. Pts(b["points"])];
        double y = b.F("y", 0.0);
        return RrectPolygon(b.F("x0"), y - b.F("w") / 2, b.F("x1"), y + b.F("w") / 2, b.F("rf", 0.0), b.F("rb", 0.0));
    }

    /// <summary>A polygon stored in a dict (a list of Pt, or Python-style [[x, y], ...]) as points.</summary>
    public static List<Pt> Pts(object? v) => v switch
    {
        List<Pt> l => l,
        System.Collections.IEnumerable e => e.Cast<object?>().Select(AsPt).ToList(),
        _ => throw new PyTypeError("not a polygon"),
    };

    public static Pt AsPt(object? p) => p switch
    {
        Pt q => q,
        List<object?> l => new Pt(Py.ToDouble(l[0]), Py.ToDouble(l[1])),
        object?[] t => new Pt(Py.ToDouble(t[0]), Py.ToDouble(t[1])),
        _ => throw new PyTypeError("not a point"),
    };

    /// <summary>Area and centroid x of a simple polygon.</summary>
    public static (double Area, double Cx) PolygonCentroid(IReadOnlyList<Pt> pts)
    {
        double a = 0.0, cx = 0.0;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % n];
            double c = x0 * y1 - x1 * y0;
            a += c;
            cx += (x0 + x1) * c;
        }
        return (Math.Abs(a) / 2, a != 0 ? cx / (3 * a) : Py.Sum(pts.Select(p => p.X)) / n);
    }

    /// <summary>Sutherland-Hodgman: the part of polygon pts inside the convex polygon clip (either winding).</summary>
    public static List<Pt> ClipConvex(IReadOnlyList<Pt> pts, IReadOnlyList<Pt> clip)
    {
        int m = clip.Count;
        double sgn = Py.Sum(Enumerable.Range(0, m).Select(i => clip[i].X * clip[(i + 1) % m].Y - clip[(i + 1) % m].X * clip[i].Y)) > 0
            ? 1.0 : -1.0;
        var out_ = new List<Pt>(pts);
        for (int ci = 0; ci < m; ci++)
        {
            if (out_.Count == 0)
                break;
            var (ax, ay) = clip[ci];
            var (bx, by) = clip[(ci + 1) % m];
            double Side(Pt p) => sgn * ((bx - ax) * (p.Y - ay) - (by - ay) * (p.X - ax));
            var src = out_;
            out_ = [];
            int n = src.Count;
            for (int i = 0; i < n; i++)
            {
                var p = src[i];
                var q = src[(i + 1) % n];
                double sp = Side(p), sq = Side(q);
                if (sp >= 0)
                    out_.Add(p);
                if ((sp >= 0) != (sq >= 0))
                {
                    double t = sp / (sp - sq);
                    out_.Add(new Pt(p.X + t * (q.X - p.X), p.Y + t * (q.Y - p.Y)));
                }
            }
        }
        return SimplifyPolygon(out_);
    }

    /// <summary>Drop repeated and collinear vertices.</summary>
    public static List<Pt> SimplifyPolygon(IReadOnlyList<Pt> pts, double tol = 1e-3)
    {
        var out_ = new List<Pt>();
        foreach (var p in pts)
            if (out_.Count == 0 || Math.Abs(p.X - out_[^1].X) > tol || Math.Abs(p.Y - out_[^1].Y) > tol)
                out_.Add(p);
        if (out_.Count > 1 && Math.Abs(out_[0].X - out_[^1].X) <= tol && Math.Abs(out_[0].Y - out_[^1].Y) <= tol)
            out_.RemoveAt(out_.Count - 1);
        bool changed = true;
        while (changed && out_.Count > 3)
        {
            changed = false;
            for (int i = 0; i < out_.Count; i++)
            {
                int n = out_.Count;
                var a = out_[(i - 1 + n) % n];
                var b = out_[i];
                var c = out_[(i + 1) % n];
                if (Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) <=
                    tol * Py.Max(1.0, Py.Hypot(c.X - a.X, c.Y - a.Y)))
                {
                    out_.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return out_;
    }

    /// <summary>A fire-control director facing ahead: its outline (the hitbox) and the parts it is drawn from.</summary>
    public static (List<Pt> Outline, List<Pt> Hood, List<Pt>? Tube, List<List<Pt>> Ends) DirectorParts(double x, double y,
        double l, double w, double rangefinderM)
    {
        if (rangefinderM == 0)
        {
            var tub = CirclePolygon(x, y, Py.Min(l, w) / 2, 16);
            return (tub, tub, null, []);
        }
        double x0 = x - l / 2, x1 = x + l / 2;
        double hh = Py.Min(w, l * 1.15) / 2;
        double cf = 0.35 * hh, cb = 0.1 * hh;
        double rx = x - 0.12 * l, tw = Py.Max(0.4, 0.14 * l);
        double el = Py.Min(0.9, 0.3 * l), ew = 1.7 * tw;
        double ye = w / 2 - ew;
        if (ye <= hh)
        {
            hh = w / 2;
            List<Pt> h0 =
            [
                new(x1, y - hh + cf), new(x1, y + hh - cf), new(x1 - cf, y + hh), new(x0 + cb, y + hh),
                new(x0, y + hh - cb), new(x0, y - hh + cb), new(x0 + cb, y - hh), new(x1 - cf, y - hh),
            ];
            return (h0, h0, null, []);
        }
        Pt[] half =
        [
            new(x1, hh - cf), new(x1 - cf, hh), new(rx + tw / 2, hh), new(rx + tw / 2, ye), new(rx + el / 2, ye),
            new(rx + el / 2, w / 2), new(rx - el / 2, w / 2), new(rx - el / 2, ye), new(rx - tw / 2, ye),
            new(rx - tw / 2, hh), new(x0 + cb, hh), new(x0, hh - cb),
        ];
        var outline = half.Select(p => new Pt(p.X, y + p.Y)).Concat(half.Reverse().Select(p => new Pt(p.X, y - p.Y))).ToList();
        List<Pt> hood =
        [
            new(x1, y - hh + cf), new(x1, y + hh - cf), new(x1 - cf, y + hh), new(x0 + cb, y + hh), new(x0, y + hh - cb),
            new(x0, y - hh + cb), new(x0 + cb, y - hh), new(x1 - cf, y - hh),
        ];
        List<Pt> tube = [new(rx - tw / 2, y - ye), new(rx + tw / 2, y - ye), new(rx + tw / 2, y + ye), new(rx - tw / 2, y + ye)];
        var ends = new List<List<Pt>>();
        foreach (int s in new[] { 1, -1 })
            ends.Add([new(rx - el / 2, y + s * ye), new(rx + el / 2, y + s * ye), new(rx + el / 2, y + s * w / 2),
                      new(rx - el / 2, y + s * w / 2)]);
        return (outline, hood, tube, ends);
    }

    public static List<Pt> CirclePolygon(double cx, double cy, double r, int seg = 32)
    {
        var pts = new List<Pt>(seg);
        for (int i = 0; i < seg; i++)
            pts.Add(new Pt(cx + r * Math.Cos(2 * Math.PI * i / seg), cy + r * Math.Sin(2 * Math.PI * i / seg)));
        return pts;
    }

    public static List<Pt> RotateTranslate(IEnumerable<Pt> pts, double deg, double tx, double ty)
    {
        double c = Math.Cos(Py.Radians(deg)), s = Math.Sin(Py.Radians(deg));
        return pts.Select(p => new Pt(tx + p.X * c - p.Y * s, ty + p.X * s + p.Y * c)).ToList();
    }

    public static bool PointInPolygon(double x, double y, IReadOnlyList<Pt> pts)
    {
        bool inside = false;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var (x1, y1) = pts[i];
            var (x2, y2) = pts[(i + 1) % n];
            if ((y1 > y) != (y2 > y))
                if (x < x1 + (y - y1) * (x2 - x1) / (y2 - y1))
                    inside = !inside;
        }
        return inside;
    }

    public static readonly IReadOnlyDictionary<string, AAMount> AA_CFG = new Dictionary<string, AAMount>(StringComparer.Ordinal)
    {
        ["quad40"] = new(2.0, 4, 2.8, 0.17, 0.42, 40.0, 56.0),
        ["twin40"] = new(1.5, 2, 2.6, 0.17, 0.5, 40.0, 56.0),
        ["single20"] = new(0.75, 1, 1.7, 0.12, 0.0, 20.0, 70.0),
    };

    /// <summary>The midship section's fullness for a block coefficient (Kerlen's fit), kept to a sane range.</summary>
    public static double MidshipCoefficient(double cb) => Py.Min(0.995, Py.Max(0.6, 1.006 - 0.0056 * Py.Pow(cb, -3.56)));

    /// <summary>The share of its box that a quadrant (y / b) ** p + s ** q = 1 fills.</summary>
    public static double SuperellipseFill(double p, double q) =>
        Py.Gamma(1 + 1 / p) * Py.Gamma(1 + 1 / q) / Py.Gamma(1 + 1 / p + 1 / q);

    // ------------------------------------------------------------------ turret types

    /// <summary>The gun of turret type t, as exported: calibre_mm and calibre_length, those it has.</summary>
    public static PyDict GunOf(PyDict t)
    {
        var o = new PyDict();
        foreach (var k in new[] { "calibre_mm", "calibre_length" })
            if (t.Has(k))
                o[k] = t[k];
        return o;
    }

    /// <summary>Size a turret from its guns: (type id, type dict).</summary>
    public static (string Id, PyDict T) MakeTurretType(object? calibreMm, object? calibreLength, object? barrels,
        string kind = "auto")
    {
        double calMm = Py.ToDouble(calibreMm), calLen = Py.ToDouble(calibreLength);
        long n = Py.ToLong(barrels);
        double cal = calMm / 1000.0;
        double spacing = n > 1 ? Py.Max(cal * 6.5, 0.9 + cal * 3.0) : 0.0;
        double width = (n - 1) * spacing + cal * 15.0 + 1.5;
        double r = width / 1.7;
        if (kind == "auto")
            kind = calMm >= 150 ? "bb" : calMm >= 76 ? "dp" : "open";
        string tid = $"t{Py.Str(barrels)}x{Py.RoundObj(calibreMm)}L{Py.RoundObj(calibreLength)}" +
                     (kind is "bb" or "dp" ? "" : "_" + kind);
        var t = PyDict.Of(("desc", $"{Py.Str(barrels)} x {Py.G(calMm)}mm/{Py.G(calLen)}"), ("shape", kind),
            ("r", Py.Round(r, 3)), ("barrels", barrels), ("barrel_len", Py.Round(cal * calLen, 3)),
            ("barrel_w", Py.Round(Py.Max(cal * 2.3, 0.18), 3)), ("spacing", Py.Round(spacing, 3)),
            ("calibre_mm", calibreMm), ("calibre_length", calibreLength));
        if (kind == "torp")
            t.Update(("centered", true), ("barbette", false));
        if (kind is "open" or "casemate")
            t["barbette"] = false;
        return (tid, t);
    }

    /// <summary>make_turret_type for a battery, carrying its rounds_per_gun when it gives one.</summary>
    public static (string Id, PyDict T) BatteryType(PyDict b, string kind = "auto")
    {
        var (tid, t) = MakeTurretType(b["calibre_mm"], b["calibre_length"], b["barrels"], kind);
        if (b.Has("rounds_per_gun"))
            t["rounds_per_gun"] = b["rounds_per_gun"];
        return (tid, t);
    }

    public static (string Id, PyDict T) MakeTorpedoType(object? tubes, bool fixed_ = false)
    {
        long n = Py.ToLong(tubes);
        string ts = Py.Str(tubes);
        if (fixed_)
            return ($"tube{ts}x533", PyDict.Of(("desc", $"{ts} x 533mm fixed torpedo tube{(n > 1 ? "s" : "")}"),
                ("shape", "tube"), ("r", 0.55), ("barrels", tubes), ("barrel_len", 7.2), ("barrel_w", 0.55),
                ("spacing", 0.75), ("centered", true), ("barbette", false), ("fixed_tube", true), ("calibre_mm", 533.0)));
        return ($"torp{ts}x533", PyDict.Of(("desc", $"{ts} x 533mm torpedo tubes"), ("shape", "torp"), ("r", 1.9),
            ("barrels", tubes), ("barrel_len", 7.6), ("barrel_w", 0.55), ("spacing", 0.66), ("centered", true),
            ("barbette", false), ("calibre_mm", 533.0)));
    }

    static readonly Dictionary<string, double> BarrelRoot = new() { ["bb"] = 0.5, ["dp"] = 0.3, ["open"] = -0.3, ["casemate"] = 0.0 };
    public const double CASEMATE_SHIELD = 0.55;
    static readonly Dictionary<string, double> BarrelShownK = new() { ["bb"] = 0.8, ["dp"] = 0.8, ["casemate"] = 0.5 };

    /// <summary>Does a mount of turret type t stand on a barbette? (t.get("barbette", True))</summary>
    public static bool HasBarbette(PyDict t) => Py.Truthy(t.Get("barbette", true));

    public static double BarrelShown(PyDict t) =>
        t.F("barrel_len") * (BarrelShownK.TryGetValue(t.S("shape", "bb")!, out var k) ? k : 1.0);

    /// <summary>Polygons (turret-local) for the turret body, extra parts and each barrel.</summary>
    public static TurretShapes TurretShapesOf(PyDict t)
    {
        double r = t.F("r");
        long n = t.I("barrels");
        double bl = t.F("barrel_len"), bw = t.F("barrel_w"), sp = t.F("spacing");
        string shape = t.S("shape", "bb")!;
        var out_ = new TurretShapes();

        List<List<Pt>> BarrelPolys(double x0)
        {
            var res = new List<List<Pt>>();
            for (long i = 0; i < n; i++)
            {
                double y = (i - (n - 1) / 2.0) * sp;
                double xe = x0 + BarrelShown(t);
                res.Add([new(x0, y - bw * 0.62), new(xe, y - bw / 2), new(xe, y + bw / 2), new(x0, y + bw * 0.62)]);
            }
            return res;
        }

        switch (shape)
        {
            case "bb":
                {
                    double R = 1.1 * r;
                    double half = 0.85 * r;
                    double cx = -0.55 * r + Math.Sqrt(R * R - half * half);
                    double a0 = Py.Degrees(Math.Atan2(half, -0.55 * r - cx));
                    var pts = new List<Pt> { new(0.85 * r, -0.5 * r), new(0.85 * r, 0.5 * r), new(0.4 * r, 0.85 * r) };
                    int seg = 16;
                    for (int i = 0; i <= seg; i++)
                    {
                        double a = Py.Radians(a0 + (360 - 2 * a0) * i / seg);
                        pts.Add(new Pt(cx + R * Math.Cos(a), R * Math.Sin(a)));
                    }
                    pts.Add(new Pt(0.4 * r, -0.85 * r));
                    out_.Body = pts;
                    out_.Parts = [[new(-0.66 * r, -1.02 * r), new(-0.48 * r, -1.02 * r), new(-0.48 * r, 1.02 * r), new(-0.66 * r, 1.02 * r)]];
                    out_.Barrels = BarrelPolys(BarrelRoot["bb"] * r);
                    break;
                }
            case "dp":
                out_.Body = RrectPolygon(-0.95 * r, -0.72 * r, 0.75 * r, 0.72 * r, 0.42 * r, 0.6 * r);
                out_.Barrels = BarrelPolys(BarrelRoot["dp"] * r);
                break;
            case "open":
                out_.Body = CirclePolygon(0, 0, r);
                out_.Barrels = BarrelPolys(BarrelRoot["open"] * r);
                break;
            case "casemate":
                out_.Body = CirclePolygon(0, 0, CASEMATE_SHIELD * r);
                out_.Barrels = BarrelPolys(BarrelRoot["casemate"] * r);
                break;
            case "torp":
                {
                    out_.Body = CirclePolygon(0, 0, r);
                    for (long i = 0; i < n; i++)
                    {
                        double y = (i - (n - 1) / 2.0) * sp;
                        out_.Barrels.Add(RrectPolygon(-bl / 2, y - bw / 2, bl / 2, y + bw / 2, bw / 2, bw / 2, seg: 4));
                    }
                    double ty = (n - 1) / 2.0 * sp + bw / 2;
                    out_.Parts =
                    [
                        [new(-bl / 2 - 0.2, -ty - 0.2), new(-bl / 2 + 0.7, -ty - 0.2), new(-bl / 2 + 0.7, ty + 0.2), new(-bl / 2 - 0.2, ty + 0.2)],
                        RrectPolygon(-r * 0.4, -ty - 0.9, r * 0.4, -ty + 0.1, 0.25, 0.25, seg: 3),
                    ];
                    break;
                }
            case "tube":
                {
                    double w = (n - 1) * sp + bw;
                    out_.Body = RrectPolygon(-bl / 2 + 0.6, -w / 2 - 0.15, bl / 2 - 1.2, w / 2 + 0.15, 0.1, 0.1, seg: 2);
                    for (long i = 0; i < n; i++)
                    {
                        double y = (i - (n - 1) / 2.0) * sp;
                        out_.Barrels.Add(RrectPolygon(-bl / 2, y - bw / 2, bl / 2, y + bw / 2, bw / 2, 0.05, seg: 4));
                    }
                    break;
                }
            default:
                throw new PyValueError(shape);
        }
        return out_;
    }

    /// <summary>Max distance of any part of the turret from its pivot.</summary>
    public static double TurretReach(PyDict t)
    {
        var sh = TurretShapesOf(t);
        var pts = sh.Body.Concat(sh.Parts.Concat(sh.Barrels).SelectMany(p => p));
        return Py.Max(pts.Select(p => Py.Hypot(p.X, p.Y)));
    }

    static readonly Dictionary<string, double> HeightK = new()
        { ["bb"] = 0.42, ["dp"] = 0.55, ["open"] = 0.9, ["torp"] = 0.5, ["tube"] = 1.6, ["casemate"] = 0.42 };

    /// <summary>Roof height of a turret above its base, metres.</summary>
    public static double TurretHeight(PyDict t) => HeightK[t.S("shape", "bb")!] * t.F("r");

    /// <summary>The waterplane coefficient a hull of block coefficient cb aims for.</summary>
    public static double Cwp(double cb) => 0.18 + 0.86 * cb;

    /// <summary>m between decks: the deck stack, raised stretches of hull and superstructure levels share it.</summary>
    public const double DECK_PITCH = 2.6;

    /// <summary>How much higher each superfiring tier stands than the one it fires over, for a turret th tall.</summary>
    public static double SuperfireStep(double th) => 2.0 + 0.2 * th;

    public static double PolygonArea(IReadOnlyList<Pt> pts)
    {
        int n = pts.Count;
        return Math.Abs(Py.Sum(Enumerable.Range(0, n).Select(i => pts[i].X * pts[(i + 1) % n].Y - pts[(i + 1) % n].X * pts[i].Y))) / 2;
    }

    /// <summary>(min y, max y) where the vertical line at x crosses the polygon, or null if it misses.</summary>
    public static (double Lo, double Hi)? PolygonYSpan(IReadOnlyList<Pt> pts, double x)
    {
        var ys = new List<double>();
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % n];
            if ((x0 <= x && x <= x1 || x1 <= x && x <= x0) && x0 != x1)
                ys.Add(y0 + (x - x0) * (y1 - y0) / (x1 - x0));
        }
        return ys.Count > 0 ? (Py.Min(ys), Py.Max(ys)) : null;
    }

    /// <summary>Pie slice of radius R from bearing a0 to a1 (degrees clockwise from +x; a1 &gt; a0, ship-local).</summary>
    public static List<Pt> SectorPolygon(double cx, double cy, double R, double a0, double a1, double step = 10.0)
    {
        long n = Py.Max(2L, Py.Int(Math.Ceiling((a1 - a0) / step)) + 1);
        var pts = a1 - a0 < 360 ? new List<Pt> { new(cx, cy) } : new List<Pt>();
        for (long i = 0; i < n; i++)
        {
            double a = Py.Radians(a0 + (a1 - a0) * i / (n - 1));
            pts.Add(new Pt(cx + R * Math.Cos(a), cy + R * Math.Sin(a)));
        }
        return pts;
    }

    static double Orient(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    internal static bool SegmentsCross(Pt p, Pt q, Pt r, Pt s)
    {
        double d1 = Orient(r, s, p), d2 = Orient(r, s, q), d3 = Orient(p, q, r), d4 = Orient(p, q, s);
        return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
    }

    /// <summary>True if two simple polygons overlap (edges cross, or one lies inside the other).</summary>
    public static bool PolygonsIntersect(IReadOnlyList<Pt> a, IReadOnlyList<Pt> b)
    {
        double x0 = Py.Max(Py.Min(a.Select(p => p.X)), Py.Min(b.Select(p => p.X)));
        double x1 = Py.Min(Py.Max(a.Select(p => p.X)), Py.Max(b.Select(p => p.X)));
        double y0 = Py.Max(Py.Min(a.Select(p => p.Y)), Py.Min(b.Select(p => p.Y)));
        double y1 = Py.Min(Py.Max(a.Select(p => p.Y)), Py.Max(b.Select(p => p.Y)));
        if (x1 < x0 || y1 < y0)
            return false;

        List<(Pt S, Pt E)> Near(IReadOnlyList<Pt> p)
        {
            var out_ = new List<(Pt, Pt)>();
            var s = p[^1];
            foreach (var e in p)
            {
                if ((s.X <= x1 || e.X <= x1) && (s.X >= x0 || e.X >= x0) && (s.Y <= y1 || e.Y <= y1) && (s.Y >= y0 || e.Y >= y0))
                    out_.Add((s, e));
                s = e;
            }
            return out_;
        }

        var eb = Near(b);
        if (eb.Count > 0)
            foreach (var (p, q) in Near(a))
                foreach (var (r, s) in eb)
                    if (SegmentsCross(p, q, r, s))
                        return true;
        return PointInPolygon(a[0].X, a[0].Y, b) || PointInPolygon(b[0].X, b[0].Y, a);
    }

    // ------------------------------------------------------------------ firing arcs

    public static double Wrap180(double a) => Py.Mod(a + 180.0, 360.0) - 180.0;

    public static bool AngleAllowed(IEnumerable<(double Lo, double Hi)> arcs, double a)
    {
        a = Py.Mod(a, 360.0);
        return arcs.Any(r => r.Lo <= a && a <= r.Hi || r.Lo <= a + 360.0 && a + 360.0 <= r.Hi);
    }
}

/// <summary>A polygon tested many times (a layout footprint): polygons_intersect and point_in_polygon with the same
/// arithmetic on the same edges, looking only at the edges in the x cells or y slab they need. pts must not change.</summary>
public sealed class PreparedPolygon
{
    const int CELLS = 64;
    readonly IReadOnlyList<Pt> pts;
    readonly double bx0, by0, bx1, by1;
    readonly int k;
    readonly double dx, dy;
    readonly List<(int First, Pt S, Pt E)>[] xcells;
    readonly List<(Pt S, Pt E)>[] yslabs;

    public PreparedPolygon(IReadOnlyList<Pt> pts)
    {
        this.pts = pts;
        bx0 = Py.Min(pts.Select(p => p.X));
        by0 = Py.Min(pts.Select(p => p.Y));
        bx1 = Py.Max(pts.Select(p => p.X));
        by1 = Py.Max(pts.Select(p => p.Y));
        int n = pts.Count;
        k = Math.Max(1, Math.Min(CELLS, n / 4));
        dx = (bx1 - bx0) / k;
        if (dx == 0)
            dx = 1.0;
        dy = (by1 - by0) / k;
        if (dy == 0)
            dy = 1.0;
        xcells = new List<(int, Pt, Pt)>[k];
        yslabs = new List<(Pt, Pt)>[k];
        for (int i = 0; i < k; i++)
        {
            xcells[i] = [];
            yslabs[i] = [];
        }
        for (int i = 0; i < n; i++)
        {
            var s = pts[i];
            var e = pts[(i + 1) % n];
            int c0 = Xc(Py.Min(s.X, e.X)), c1 = Xc(Py.Max(s.X, e.X));
            for (int c = c0; c <= c1; c++)
                xcells[c].Add((c0, s, e));
            for (int c = Yc(Py.Min(s.Y, e.Y)); c <= Yc(Py.Max(s.Y, e.Y)); c++)
                yslabs[c].Add((s, e));
        }
    }

    int Xc(double x) => (int)Math.Max(0, Math.Min(k - 1, Py.Floor((x - bx0) / dx)));
    int Yc(double y) => (int)Math.Max(0, Math.Min(k - 1, Py.Floor((y - by0) / dy)));

    /// <summary>point_in_polygon(x, y, pts).</summary>
    public bool Contains(double x, double y)
    {
        bool inside = false;
        foreach (var ((x1, y1), (x2, y2)) in yslabs[Yc(y)])
            if ((y1 > y) != (y2 > y))
                if (x < x1 + (y - y1) * (x2 - x1) / (y2 - y1))
                    inside = !inside;
        return inside;
    }

    /// <summary>polygons_intersect(pts, b).</summary>
    public bool Intersects(IReadOnlyList<Pt> b)
    {
        double x0 = Py.Max(bx0, Py.Min(b.Select(p => p.X))), x1 = Py.Min(bx1, Py.Max(b.Select(p => p.X)));
        double y0 = Py.Max(by0, Py.Min(b.Select(p => p.Y))), y1 = Py.Min(by1, Py.Max(b.Select(p => p.Y)));
        if (x1 < x0 || y1 < y0)
            return false;

        bool Reaches(Pt s, Pt e) =>
            (s.X <= x1 || e.X <= x1) && (s.X >= x0 || e.X >= x0) && (s.Y <= y1 || e.Y <= y1) && (s.Y >= y0 || e.Y >= y0);

        var eb = new List<(Pt, Pt)>();
        var sp = b[^1];
        foreach (var e in b)
        {
            if (Reaches(sp, e))
                eb.Add((sp, e));
            sp = e;
        }
        if (eb.Count > 0)
        {
            int cLo = Xc(x0);
            for (int c = cLo; c <= Xc(x1); c++)
                foreach (var (first, p, q) in xcells[c])
                    if (c == Math.Max(cLo, first) && Reaches(p, q))
                        foreach (var (r, s) in eb)
                            if (Geometry.SegmentsCross(p, q, r, s))
                                return true;
        }
        return Geometry.PointInPolygon(pts[0].X, pts[0].Y, b) || Contains(b[0].X, b[0].Y);
    }
}
